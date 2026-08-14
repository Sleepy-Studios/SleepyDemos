using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>物理控制分配结果；旋翼推力顺序固定为 FL/FR/RL/RR。</summary>
    internal readonly struct DroneAllocationResult
    {
        internal DroneAllocationResult(
            Vector4 rotorThrustNewtons,
            float realizedThrustNewtons,
            Vector3 realizedForceBodyNewtons,
            Vector3 realizedTorqueNewtonMeters,
            float residualThrustNewtons,
            Vector3 residualTorqueNewtonMeters,
            float rollPitchScale,
            float yawScale,
            DroneControlSaturation saturation)
        {
            RotorThrustNewtons = rotorThrustNewtons;
            RealizedThrustNewtons = realizedThrustNewtons;
            RealizedForceBodyNewtons = realizedForceBodyNewtons;
            RealizedTorqueNewtonMeters = realizedTorqueNewtonMeters;
            ResidualThrustNewtons = residualThrustNewtons;
            ResidualTorqueNewtonMeters = residualTorqueNewtonMeters;
            RollPitchScale = rollPitchScale;
            YawScale = yawScale;
            Saturation = saturation;
        }

        internal Vector4 RotorThrustNewtons { get; }
        internal float RealizedThrustNewtons { get; }
        internal Vector3 RealizedForceBodyNewtons { get; }
        internal Vector3 RealizedTorqueNewtonMeters { get; }
        internal float ResidualThrustNewtons { get; }
        internal Vector3 ResidualTorqueNewtonMeters { get; }
        internal float RollPitchScale { get; }
        internal float YawScale { get; }
        internal DroneControlSaturation Saturation { get; }
    }

    /// <summary>由实际 Rotor 几何将总推力和机体力矩分配为四个旋翼推力。</summary>
    internal sealed class QuadrotorControlAllocator
    {
        private readonly Matrix4x4 effectiveness;
        private readonly Matrix4x4 inverseEffectiveness;
        private readonly Vector3[] forceDirectionsLocal;
        private readonly float maximumRotorThrust;
        private readonly bool isValid;

        internal QuadrotorControlAllocator(
            Vector3[] rotorLocalPositions,
            Vector3[] rotorLocalForceDirections,
            DroneRotorDirection[] rotorDirections,
            float reactionTorqueCoefficient,
            float maximumRotorThrust)
        {
            this.maximumRotorThrust = Mathf.Max(0f, maximumRotorThrust);
            if (rotorLocalPositions == null || rotorLocalForceDirections == null || rotorDirections == null
                || rotorLocalPositions.Length != 4 || rotorLocalForceDirections.Length != 4
                || rotorDirections.Length != 4
                || reactionTorqueCoefficient <= 0f || this.maximumRotorThrust <= 0f)
            {
                return;
            }

            effectiveness = Matrix4x4.zero;
            forceDirectionsLocal = new Vector3[4];
            for (var index = 0; index < 4; index++)
            {
                var forceDirection = rotorLocalForceDirections[index].sqrMagnitude > 0.000001f
                    ? rotorLocalForceDirections[index].normalized
                    : Vector3.zero;
                if (!IsFinite(forceDirection) || forceDirection.y <= 0.0001f)
                {
                    return;
                }

                forceDirectionsLocal[index] = forceDirection;
                var momentPerNewton = Vector3.Cross(rotorLocalPositions[index], forceDirection)
                                      + forceDirection
                                      * reactionTorqueCoefficient
                                      * (float)rotorDirections[index];
                effectiveness[0, index] = forceDirection.y;
                effectiveness[1, index] = momentPerNewton.x;
                effectiveness[2, index] = momentPerNewton.y;
                effectiveness[3, index] = momentPerNewton.z;
            }

            var determinant = effectiveness.determinant;
            if (!float.IsFinite(determinant) || Mathf.Abs(determinant) < 0.000001f)
            {
                return;
            }

            inverseEffectiveness = effectiveness.inverse;
            isValid = true;
        }

        internal bool IsValid => isValid;

        internal DroneAllocationResult Allocate(float thrustNewtons, Vector3 localTorqueNewtonMeters)
        {
            if (!isValid || !float.IsFinite(thrustNewtons) || !IsFinite(localTorqueNewtonMeters))
            {
                return default;
            }

            thrustNewtons = Mathf.Max(0f, thrustNewtons);
            var baseForces = Solve(thrustNewtons, new Vector3(
                localTorqueNewtonMeters.x,
                0f,
                localTorqueNewtonMeters.z));
            var rollPitchScale = FitHighPriority(ref baseForces, thrustNewtons, localTorqueNewtonMeters);

            var yawForces = Solve(0f, new Vector3(0f, localTorqueNewtonMeters.y, 0f));
            var yawScale = CalculateAdditiveScale(baseForces, yawForces);
            var forces = Clamp(baseForces + yawForces * yawScale);
            var realized = effectiveness * forces;
            var realizedTorque = new Vector3(realized.y, realized.z, realized.w);
            var realizedForceBody = Vector3.zero;
            for (var index = 0; index < 4; index++)
            {
                realizedForceBody += forceDirectionsLocal[index] * forces[index];
            }

            var saturation = new DroneControlSaturation(
                ResolveDirection(thrustNewtons, realized.x),
                ResolveDirection(localTorqueNewtonMeters.x, realizedTorque.x),
                ResolveDirection(localTorqueNewtonMeters.y, realizedTorque.y),
                ResolveDirection(localTorqueNewtonMeters.z, realizedTorque.z));
            return new DroneAllocationResult(
                forces,
                realized.x,
                realizedForceBody,
                realizedTorque,
                thrustNewtons - realized.x,
                localTorqueNewtonMeters - realizedTorque,
                rollPitchScale,
                yawScale,
                saturation);
        }

        private float FitHighPriority(
            ref Vector4 forces,
            float desiredThrust,
            Vector3 desiredTorque)
        {
            ShiftCollectiveIntoRange(ref forces);
            if (IsWithinRange(forces))
            {
                return 1f;
            }

            var minimum = 0f;
            var maximum = 1f;
            var best = Solve(desiredThrust, Vector3.zero);
            for (var iteration = 0; iteration < 16; iteration++)
            {
                var candidateScale = (minimum + maximum) * 0.5f;
                var candidate = Solve(
                    desiredThrust,
                    new Vector3(desiredTorque.x, 0f, desiredTorque.z) * candidateScale);
                ShiftCollectiveIntoRange(ref candidate);
                if (IsWithinRange(candidate))
                {
                    best = candidate;
                    minimum = candidateScale;
                }
                else
                {
                    maximum = candidateScale;
                }
            }

            forces = Clamp(best);
            return minimum;
        }

        private void ShiftCollectiveIntoRange(ref Vector4 forces)
        {
            var minimum = Min(forces);
            var maximum = Max(forces);
            if (maximum > maximumRotorThrust)
            {
                forces -= Vector4.one * (maximum - maximumRotorThrust);
            }

            minimum = Min(forces);
            if (minimum < 0f)
            {
                forces += Vector4.one * -minimum;
            }
        }

        private float CalculateAdditiveScale(Vector4 baseline, Vector4 additive)
        {
            var scale = 1f;
            for (var index = 0; index < 4; index++)
            {
                var delta = additive[index];
                if (delta > 0.000001f)
                {
                    scale = Mathf.Min(scale, (maximumRotorThrust - baseline[index]) / delta);
                }
                else if (delta < -0.000001f)
                {
                    scale = Mathf.Min(scale, -baseline[index] / delta);
                }
            }

            return Mathf.Clamp01(scale);
        }

        private Vector4 Solve(float thrust, Vector3 torque)
        {
            return inverseEffectiveness * new Vector4(thrust, torque.x, torque.y, torque.z);
        }

        private Vector4 Clamp(Vector4 value)
        {
            for (var index = 0; index < 4; index++)
            {
                value[index] = Mathf.Clamp(value[index], 0f, maximumRotorThrust);
            }

            return value;
        }

        private bool IsWithinRange(Vector4 value)
        {
            return Min(value) >= -0.00001f && Max(value) <= maximumRotorThrust + 0.00001f;
        }

        private static DroneSaturationDirection ResolveDirection(float desired, float realized)
        {
            var residual = desired - realized;
            if (Mathf.Abs(residual) <= Mathf.Max(0.0001f, Mathf.Abs(desired) * 0.001f))
            {
                return DroneSaturationDirection.None;
            }

            return residual > 0f ? DroneSaturationDirection.Positive : DroneSaturationDirection.Negative;
        }

        private static float Min(Vector4 value)
        {
            return Mathf.Min(Mathf.Min(value.x, value.y), Mathf.Min(value.z, value.w));
        }

        private static float Max(Vector4 value)
        {
            return Mathf.Max(Mathf.Max(value.x, value.y), Mathf.Max(value.z, value.w));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
