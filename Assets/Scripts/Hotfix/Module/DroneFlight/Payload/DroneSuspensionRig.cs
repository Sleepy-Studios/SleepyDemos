using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>飞控只读的外部承载质量来源。</summary>
    internal interface IDroneExternalMassProvider
    {
        float SupportedMassKilograms { get; }
        float InstalledHardwareMassKilograms { get; }
        float HardwareMassKilograms { get; }
        float PayloadMassKilograms { get; }
        float SupportedPayloadMassKilograms { get; }
    }

    /// <summary>飞控读取的吊挂方向和相对运动来源。</summary>
    internal interface IDroneSuspensionStateProvider
    {
        DroneSuspensionState SuspensionState { get; }
    }

    /// <summary>在飞控读取质量前原子同步可热调的外部刚体质量。</summary>
    internal interface IDroneExternalMassSynchronizer
    {
        void SynchronizeExternalMass();
    }

    /// <summary>单摆抓斗关节的实时诊断。</summary>
    internal readonly struct DroneSuspensionJointTelemetry
    {
        internal DroneSuspensionJointTelemetry(
            float twistDegrees,
            float swingDegrees,
            float twistLimitDegrees,
            float swingLimitDegrees,
            float swingRateDegreesPerSecond,
            float passiveDampingTorqueNewtonMeters,
            bool isCableTaut)
        {
            TwistDegrees = twistDegrees;
            SwingDegrees = swingDegrees;
            TwistLimitDegrees = twistLimitDegrees;
            SwingLimitDegrees = swingLimitDegrees;
            SwingRateDegreesPerSecond = swingRateDegreesPerSecond;
            PassiveDampingTorqueNewtonMeters = passiveDampingTorqueNewtonMeters;
            IsCableTaut = isCableTaut;
        }

        internal float TwistDegrees { get; }
        internal float SwingDegrees { get; }
        internal float TwistLimitDegrees { get; }
        internal float SwingLimitDegrees { get; }
        internal float SwingRateDegreesPerSecond { get; }
        internal float PassiveDampingTorqueNewtonMeters { get; }
        internal bool IsCableTaut { get; }

        // 旧遥测别名只为避免现有 HUD/测试在迁移期间丢失字段。
        internal float TopTwistDegrees => TwistDegrees;
        internal float TopSwingDegrees => SwingDegrees;
        internal float BottomTwistDegrees => 0f;
        internal float BottomSwingDegrees => 0f;
        internal float TopTwistLimitDegrees => TwistLimitDegrees;
        internal float TopSwingLimitDegrees => SwingLimitDegrees;
        internal float BottomTwistLimitDegrees => 0f;
        internal float BottomSwingLimitDegrees => 0f;
    }

    /// <summary>管理单一动态抓斗在腹部停靠与无质量单摆物理之间的切换。</summary>
    public sealed class DroneSuspensionRig : MonoBehaviour
    {
        [SerializeField] private Rigidbody droneBody;
        [SerializeField] private Transform parkingRoot;
        [SerializeField] private Rigidbody grappleBody;
        [SerializeField] private Collider[] mechanismColliders = Array.Empty<Collider>();
        [SerializeField] private DroneFlightConfig config;
        [SerializeField] private ConfigurableJoint suspensionJoint;
        [SerializeField] private Transform cableVisual;

        private float currentCableLengthMeters = 0.08f;
        private float lastPassiveDampingTorque;
        private bool stateApplied;

        /// 吊挂物理当前是否启用。
        internal bool IsPhysicsActive { get; private set; }

        /// 单一抓斗动态刚体。
        internal Rigidbody GrappleBody => grappleBody;

        /// 设备固定质量，单位 kg。
        internal float HardwareMassKilograms => grappleBody != null && float.IsFinite(grappleBody.mass)
            ? Mathf.Max(0f, grappleBody.mass)
            : 0f;

        /// 当前无质量吊杆长度。
        internal float CurrentCableLengthMeters => currentCableLengthMeters;

        /// 单摆当前是否已接入物理解算。
        internal bool IsCableTaut => IsPhysicsActive
                                     && suspensionJoint != null
                                     && suspensionJoint.connectedBody == droneBody;

        /// 单摆抓斗关节的实时诊断。
        internal DroneSuspensionJointTelemetry JointTelemetry
        {
            get
            {
                var twist = CalculateTwistDegrees();
                var swing = CalculateSwingDegrees();
                var rate = CalculateSwingRateDegreesPerSecond();
                return new DroneSuspensionJointTelemetry(
                    twist,
                    swing,
                    config != null ? config.SuspensionTwistLimitDegrees : 25f,
                    config != null ? config.SuspensionSwingLimitDegrees : 45f,
                    rate,
                    lastPassiveDampingTorque,
                    IsCableTaut);
            }
        }

        private void Awake()
        {
            if (grappleBody == null)
            {
                grappleBody = GetComponentInChildren<Rigidbody>(true);
                if (grappleBody == droneBody)
                {
                    grappleBody = null;
                }
            }

            if (suspensionJoint == null && grappleBody != null)
            {
                suspensionJoint = grappleBody.GetComponent<ConfigurableJoint>();
            }

            ConfigureVisualSmoothing();
            ApplyJointConfiguration();
            IgnoreOwnerCollisions();
            SetPhysicsActive(false);
        }

        private void LateUpdate()
        {
            UpdateCableVisual();
        }

        /// <summary>返回抓斗与已承载载荷的质量加权运动状态。</summary>
        internal bool TryGetMassWeightedState(
            DronePayload payload,
            float supportedPayloadFraction,
            out Vector3 position,
            out Vector3 velocity,
            out float totalMass)
        {
            position = Vector3.zero;
            velocity = Vector3.zero;
            totalMass = 0f;
            if (!IsPhysicsActive || grappleBody == null)
            {
                return false;
            }

            AddBody(grappleBody, 1f, ref position, ref velocity, ref totalMass);
            if (payload != null && payload.Body != null)
            {
                AddBody(
                    payload.Body,
                    Mathf.Clamp01(supportedPayloadFraction),
                    ref position,
                    ref velocity,
                    ref totalMass);
            }

            if (totalMass <= 0f)
            {
                return false;
            }

            position /= totalMass;
            velocity /= totalMass;
            return true;
        }

        /// <summary>切换抓斗动态单摆与腹部停靠状态。</summary>
        internal void SetPhysicsActive(bool active)
        {
            if (grappleBody == null)
            {
                IsPhysicsActive = false;
                return;
            }

            if (stateApplied && IsPhysicsActive == active)
            {
                return;
            }

            stateApplied = true;
            IsPhysicsActive = active;
            if (suspensionJoint != null)
            {
                suspensionJoint.connectedBody = null;
            }

            if (active)
            {
                var inheritedLinearVelocity = droneBody != null
                    ? droneBody.GetPointVelocity(GetOwnerAnchorWorldPosition())
                    : Vector3.zero;
                var inheritedAngularVelocity = droneBody != null ? droneBody.angularVelocity : Vector3.zero;
                grappleBody.transform.SetParent(null, true);
                AlignGrappleToCurrentCable();
                grappleBody.isKinematic = false;
                grappleBody.useGravity = true;
                grappleBody.linearVelocity = inheritedLinearVelocity;
                grappleBody.angularVelocity = inheritedAngularVelocity;
                SetMechanismCollidersEnabled(true);
                Physics.SyncTransforms();
                if (suspensionJoint != null)
                {
                    suspensionJoint.connectedBody = droneBody;
                }
                grappleBody.WakeUp();
            }
            else
            {
                grappleBody.linearVelocity = Vector3.zero;
                grappleBody.angularVelocity = Vector3.zero;
                grappleBody.isKinematic = true;
                grappleBody.useGravity = false;
                grappleBody.transform.SetParent(parkingRoot != null ? parkingRoot : transform, true);
                grappleBody.transform.localPosition = Vector3.zero;
                grappleBody.transform.localRotation = Quaternion.identity;
                grappleBody.transform.localScale = Vector3.one;
                SetMechanismCollidersEnabled(false);
            }

            Physics.SyncTransforms();
            UpdateCableVisual();
        }

        /// <summary>在物理启用前插值显示抓斗由腹部移向拆分点。</summary>
        internal void SetDeploymentProgress(float normalizedProgress)
        {
            if (IsPhysicsActive || grappleBody == null || droneBody == null)
            {
                return;
            }

            var parked = parkingRoot != null ? parkingRoot.position : transform.position;
            var deployed = GetOwnerAnchorWorldPosition() - droneBody.transform.up * currentCableLengthMeters;
            grappleBody.position = Vector3.Lerp(parked, deployed, Mathf.Clamp01(normalizedProgress));
            grappleBody.rotation = Quaternion.Slerp(
                parkingRoot != null ? parkingRoot.rotation : droneBody.rotation,
                droneBody.rotation,
                Mathf.Clamp01(normalizedProgress));
            UpdateCableVisual();
        }

        /// <summary>在物理步内修改无质量吊杆长度。</summary>
        internal void SetCableLength(float lengthMeters)
        {
            if (!float.IsFinite(lengthMeters) || lengthMeters <= 0f)
            {
                return;
            }

            currentCableLengthMeters = lengthMeters;
            if (suspensionJoint != null)
            {
                suspensionJoint.anchor = Vector3.up * currentCableLengthMeters;
            }

            ApplyPassiveDampingDrive(0f);
            UpdateCableVisual();
        }

        /// <summary>更新抓斗设备总质量，不改变速度和位姿。</summary>
        internal void SetTotalHardwareMass(float totalMassKilograms)
        {
            if (grappleBody != null
                && float.IsFinite(totalMassKilograms)
                && totalMassKilograms > 0f
                && !Mathf.Approximately(grappleBody.mass, totalMassKilograms))
            {
                grappleBody.mass = totalMassKilograms;
                grappleBody.ResetInertiaTensor();
            }
        }

        /// <summary>根据抓斗与载荷质量更新连续被动摆动阻尼。</summary>
        internal void ApplyPassiveDampingDrive(float supportedPayloadMassKilograms)
        {
            if (suspensionJoint == null || config == null)
            {
                return;
            }

            var totalSuspendedMass = Mathf.Max(0.001f, HardwareMassKilograms + Mathf.Max(0f, supportedPayloadMassKilograms));
            var length = Mathf.Max(0.03f, currentCableLengthMeters);
            var naturalFrequency = Mathf.Sqrt(Mathf.Abs(Physics.gravity.y) / length);
            var dampingAcceleration = 2f * Mathf.Clamp01(config.SuspensionDampingRatio) * naturalFrequency;
            var angularInertia = Mathf.Max(0.0001f, totalSuspendedMass * length * length);
            var maximumDampingTorque = Mathf.Max(0.01f, config.SuspensionMaximumDampingTorque);
            var drive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = dampingAcceleration,
                maximumForce = maximumDampingTorque / angularInertia,
                useAcceleration = true
            };
            suspensionJoint.rotationDriveMode = RotationDriveMode.Slerp;
            suspensionJoint.slerpDrive = drive;
            suspensionJoint.targetAngularVelocity = Vector3.zero;
            var relativeAngularVelocity = CalculateRelativeAngularVelocity();
            var dampingTorque = Vector3.ClampMagnitude(
                -relativeAngularVelocity * angularInertia * dampingAcceleration,
                maximumDampingTorque);
            lastPassiveDampingTorque = dampingTorque.magnitude;
            if (IsPhysicsActive && grappleBody != null && !grappleBody.isKinematic)
            {
                grappleBody.AddTorque(dampingTorque, ForceMode.Force);
                if (droneBody != null && !droneBody.isKinematic)
                {
                    droneBody.AddTorque(-dampingTorque, ForceMode.Force);
                }
            }
        }

        /// <summary>判断抓斗是否已进入允许合并回机体的停靠窗口。</summary>
        internal bool CanDock(float positionToleranceMeters, float relativeSpeedToleranceMetersPerSecond)
        {
            if (grappleBody == null || droneBody == null)
            {
                return false;
            }

            var expected = GetOwnerAnchorWorldPosition() - droneBody.transform.up * currentCableLengthMeters;
            var positionError = Vector3.Distance(grappleBody.position, expected);
            var ownerVelocity = droneBody.GetPointVelocity(grappleBody.position);
            var relativeSpeed = (grappleBody.linearVelocity - ownerVelocity).magnitude;
            return positionError <= Mathf.Max(0.001f, positionToleranceMeters)
                   && relativeSpeed <= Mathf.Max(0.001f, relativeSpeedToleranceMetersPerSecond);
        }

        /// <summary>兼容旧夹具：从数组中选择最后一个刚体作为抓斗根。</summary>
        internal void Configure(Rigidbody owner, Transform park, Rigidbody[] bodies, Collider[] colliders)
        {
            var selectedBody = bodies != null && bodies.Length > 0 ? bodies[^1] : null;
            Configure(owner, park, selectedBody, colliders, null, selectedBody != null
                ? selectedBody.GetComponent<ConfigurableJoint>()
                : null, null);
        }

        /// <summary>兼容旧装配签名；底部关节不再参与单摆结构。</summary>
        internal void Configure(
            Rigidbody owner,
            Transform park,
            Rigidbody[] bodies,
            Collider[] colliders,
            DroneFlightConfig flightConfig,
            ConfigurableJoint topJoint,
            ConfigurableJoint bottomJoint)
        {
            var selectedBody = bodies != null && bodies.Length > 0 ? bodies[^1] : null;
            Configure(owner, park, selectedBody, colliders, flightConfig, topJoint ?? bottomJoint, null);
        }

        /// <summary>绑定单摆抓斗的机体、停靠点、刚体、碰撞体和唯一关节。</summary>
        internal void Configure(
            Rigidbody owner,
            Transform park,
            Rigidbody body,
            Collider[] colliders,
            DroneFlightConfig flightConfig,
            ConfigurableJoint joint,
            Transform visualCable)
        {
            droneBody = owner;
            parkingRoot = park;
            grappleBody = body;
            mechanismColliders = colliders ?? Array.Empty<Collider>();
            config = flightConfig;
            suspensionJoint = joint;
            cableVisual = visualCable;
            currentCableLengthMeters = flightConfig != null
                ? flightConfig.WinchStowedLengthMeters
                : 0.08f;
            ConfigureVisualSmoothing();
            ApplyJointConfiguration();
            IgnoreOwnerCollisions();
            stateApplied = false;
            SetPhysicsActive(false);
        }

        private void ApplyJointConfiguration()
        {
            if (suspensionJoint == null)
            {
                return;
            }

            var twist = config != null ? config.SuspensionTwistLimitDegrees : 25f;
            var swing = config != null ? config.SuspensionSwingLimitDegrees : 45f;
            suspensionJoint.autoConfigureConnectedAnchor = false;
            suspensionJoint.xMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.yMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.zMotion = ConfigurableJointMotion.Locked;
            suspensionJoint.axis = Vector3.up;
            suspensionJoint.secondaryAxis = Vector3.forward;
            suspensionJoint.angularXMotion = ConfigurableJointMotion.Limited;
            suspensionJoint.angularYMotion = ConfigurableJointMotion.Limited;
            suspensionJoint.angularZMotion = ConfigurableJointMotion.Limited;
            suspensionJoint.lowAngularXLimit = new SoftJointLimit { limit = -twist };
            suspensionJoint.highAngularXLimit = new SoftJointLimit { limit = twist };
            suspensionJoint.angularYLimit = new SoftJointLimit { limit = swing };
            suspensionJoint.angularZLimit = new SoftJointLimit { limit = swing };
            suspensionJoint.angularXLimitSpring = new SoftJointLimitSpring();
            suspensionJoint.angularYZLimitSpring = new SoftJointLimitSpring();
            suspensionJoint.breakForce = float.PositiveInfinity;
            suspensionJoint.breakTorque = float.PositiveInfinity;
            suspensionJoint.projectionMode = JointProjectionMode.None;
            suspensionJoint.enablePreprocessing = true;
            suspensionJoint.enableCollision = false;
            suspensionJoint.massScale = 1f;
            suspensionJoint.connectedMassScale = 1f;
            suspensionJoint.anchor = Vector3.up * currentCableLengthMeters;
            ApplyPassiveDampingDrive(0f);
        }

        private void AlignGrappleToCurrentCable()
        {
            if (grappleBody == null || droneBody == null)
            {
                return;
            }

            var targetRotation = droneBody.rotation;
            var localAnchor = suspensionJoint != null
                ? suspensionJoint.anchor
                : Vector3.up * currentCableLengthMeters;
            var targetPosition = GetOwnerAnchorWorldPosition() - targetRotation * localAnchor;
            grappleBody.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private Vector3 GetOwnerAnchorWorldPosition()
        {
            if (droneBody == null)
            {
                return transform.position;
            }

            var connectedAnchor = suspensionJoint != null
                ? suspensionJoint.connectedAnchor
                : new Vector3(0f, -0.12f, 0f);
            return droneBody.transform.TransformPoint(connectedAnchor);
        }

        private void UpdateCableVisual()
        {
            if (cableVisual == null || grappleBody == null || droneBody == null)
            {
                return;
            }

            var start = GetOwnerAnchorWorldPosition();
            var end = grappleBody.worldCenterOfMass;
            var direction = end - start;
            var length = direction.magnitude;
            cableVisual.gameObject.SetActive(length > 0.005f);
            cableVisual.position = (start + end) * 0.5f;
            cableVisual.rotation = length > 0.0001f
                ? Quaternion.FromToRotation(Vector3.up, direction)
                : Quaternion.identity;
            cableVisual.localScale = new Vector3(0.008f, length * 0.5f, 0.008f);
        }

        private float CalculateSwingDegrees()
        {
            if (!IsCableTaut || grappleBody == null)
            {
                return 0f;
            }

            var cable = grappleBody.worldCenterOfMass - GetOwnerAnchorWorldPosition();
            return cable.sqrMagnitude > 0.000001f
                ? Vector3.Angle(Physics.gravity, cable)
                : 0f;
        }

        private float CalculateTwistDegrees()
        {
            if (!IsCableTaut || grappleBody == null || droneBody == null)
            {
                return 0f;
            }

            var ownerForward = Vector3.ProjectOnPlane(droneBody.transform.forward, droneBody.transform.up);
            var grappleForward = Vector3.ProjectOnPlane(grappleBody.transform.forward, droneBody.transform.up);
            if (ownerForward.sqrMagnitude <= 0.000001f || grappleForward.sqrMagnitude <= 0.000001f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(ownerForward, grappleForward, droneBody.transform.up);
        }

        private float CalculateSwingRateDegreesPerSecond()
        {
            if (!IsCableTaut || currentCableLengthMeters <= 0.001f || grappleBody == null || droneBody == null)
            {
                return 0f;
            }

            var relativeVelocity = grappleBody.linearVelocity
                                   - droneBody.GetPointVelocity(grappleBody.worldCenterOfMass);
            return relativeVelocity.magnitude / currentCableLengthMeters * Mathf.Rad2Deg;
        }

        private Vector3 CalculateRelativeAngularVelocity()
        {
            return grappleBody != null && droneBody != null
                ? grappleBody.angularVelocity - droneBody.angularVelocity
                : Vector3.zero;
        }

        private void ConfigureVisualSmoothing()
        {
            if (grappleBody != null)
            {
                grappleBody.interpolation = RigidbodyInterpolation.Interpolate;
                grappleBody.solverIterations = Mathf.Max(grappleBody.solverIterations, 12);
                grappleBody.solverVelocityIterations = Mathf.Max(grappleBody.solverVelocityIterations, 8);
            }
        }

        private void IgnoreOwnerCollisions()
        {
            if (droneBody == null)
            {
                return;
            }

            var ownerColliders = droneBody.GetComponentsInChildren<Collider>(true);
            foreach (var mechanismCollider in mechanismColliders)
            {
                if (mechanismCollider == null)
                {
                    continue;
                }

                foreach (var ownerCollider in ownerColliders)
                {
                    if (ownerCollider != null && ownerCollider != mechanismCollider)
                    {
                        Physics.IgnoreCollision(mechanismCollider, ownerCollider, true);
                    }
                }
            }
        }

        private void SetMechanismCollidersEnabled(bool enabledValue)
        {
            foreach (var collider in mechanismColliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabledValue;
                }
            }
        }

        private static void AddBody(
            Rigidbody body,
            float massFraction,
            ref Vector3 weightedPosition,
            ref Vector3 weightedVelocity,
            ref float totalMass)
        {
            if (body == null || !float.IsFinite(body.mass) || body.mass <= 0f || massFraction <= 0f)
            {
                return;
            }

            var mass = body.mass * massFraction;
            totalMass += mass;
            weightedPosition += body.worldCenterOfMass * mass;
            weightedVelocity += body.linearVelocity * mass;
        }
    }
}
