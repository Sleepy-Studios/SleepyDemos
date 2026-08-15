using System;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// X 架四旋翼的归一化电机输出。
    /// </summary>
    internal readonly struct QuadrotorMotorOutput
    {
        internal QuadrotorMotorOutput(
            float frontLeft,
            float frontRight,
            float rearLeft,
            float rearRight,
            float attitudeScale,
            bool isSaturated)
        {
            FrontLeft = frontLeft;
            FrontRight = frontRight;
            RearLeft = rearLeft;
            RearRight = rearRight;
            AttitudeScale = attitudeScale;
            IsSaturated = isSaturated;
        }

        internal float FrontLeft { get; }

        internal float FrontRight { get; }

        internal float RearLeft { get; }

        internal float RearRight { get; }

        internal float AttitudeScale { get; }

        internal bool IsSaturated { get; }
    }

    /// <summary>
    /// 将总推力和三个姿态轴混控为四电机命令，并优先平移总推力以保留姿态权威。
    /// </summary>
    internal static class QuadrotorMixer
    {
        internal static QuadrotorMotorOutput Mix(float collective, float roll, float pitch, float yaw)
        {
            var hadInvalidInput = false;
            collective = Sanitize(collective, 0f, 1f, ref hadInvalidInput);
            roll = Sanitize(roll, -1f, 1f, ref hadInvalidInput);
            pitch = Sanitize(pitch, -1f, 1f, ref hadInvalidInput);
            yaw = Sanitize(yaw, -1f, 1f, ref hadInvalidInput);

            // FL/RR 为逆时针组；正滚转抬左侧，正俯仰抬后侧。
            var frontLeftAttitude = roll - pitch + yaw;
            var frontRightAttitude = -roll - pitch - yaw;
            var rearLeftAttitude = roll + pitch - yaw;
            var rearRightAttitude = -roll + pitch + yaw;

            var minimumAttitude = Math.Min(
                Math.Min(frontLeftAttitude, frontRightAttitude),
                Math.Min(rearLeftAttitude, rearRightAttitude));
            var maximumAttitude = Math.Max(
                Math.Max(frontLeftAttitude, frontRightAttitude),
                Math.Max(rearLeftAttitude, rearRightAttitude));
            var attitudeRange = maximumAttitude - minimumAttitude;
            var attitudeScale = attitudeRange > 1f ? 1f / attitudeRange : 1f;

            var frontLeft = collective + frontLeftAttitude * attitudeScale;
            var frontRight = collective + frontRightAttitude * attitudeScale;
            var rearLeft = collective + rearLeftAttitude * attitudeScale;
            var rearRight = collective + rearRightAttitude * attitudeScale;

            var maximumOutput = Math.Max(Math.Max(frontLeft, frontRight), Math.Max(rearLeft, rearRight));
            var minimumOutput = Math.Min(Math.Min(frontLeft, frontRight), Math.Min(rearLeft, rearRight));
            var collectiveShift = 0f;
            if (maximumOutput > 1f)
            {
                collectiveShift -= maximumOutput - 1f;
            }
            else if (minimumOutput < 0f)
            {
                collectiveShift -= minimumOutput;
            }

            frontLeft = Clamp01(frontLeft + collectiveShift);
            frontRight = Clamp01(frontRight + collectiveShift);
            rearLeft = Clamp01(rearLeft + collectiveShift);
            rearRight = Clamp01(rearRight + collectiveShift);

            var isSaturated = hadInvalidInput || attitudeScale < 1f || Math.Abs(collectiveShift) > 0.000001f;
            return new QuadrotorMotorOutput(
                frontLeft,
                frontRight,
                rearLeft,
                rearRight,
                attitudeScale,
                isSaturated);
        }

        private static float Sanitize(float value, float minimum, float maximum, ref bool hadInvalidInput)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                hadInvalidInput = true;
                return 0f;
            }

            var clamped = Math.Max(minimum, Math.Min(maximum, value));
            hadInvalidInput |= Math.Abs(clamped - value) > 0.000001f;
            return clamped;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
