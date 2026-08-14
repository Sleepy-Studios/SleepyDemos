using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>腹部卷扬的收纳、放出和运输状态。</summary>
    internal enum DroneWinchState
    {
        Stowed,
        Deploying,
        Deployed,
        Retracting,
        Carrying
    }

    /// <summary>抓取后载荷由地面转交给无人机的承载阶段。</summary>
    internal enum DronePayloadSupportState
    {
        None,
        GroundSupported,
        TakingLoad,
        AirborneSupported,
        Unloading
    }

    /// <summary>在固定物理步内管理无质量卷扬长度、质量分布和载荷承载比例。</summary>
    public sealed class DroneWinchController : MonoBehaviour, IDroneExternalMassProvider,
        IDroneSuspensionStateProvider, IDroneExternalMassSynchronizer
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneSuspensionRig suspensionRig;

        private readonly DronePayloadLoadTransferEstimator loadTransferEstimator = new();
        private float targetLength;

        /// 当前卷扬状态。
        internal DroneWinchState State { get; private set; } = DroneWinchState.Stowed;

        /// 当前放出长度，单位 m。
        internal float CurrentLengthMeters { get; private set; }

        /// 当前载荷由地面向飞控转交的承载阶段。
        internal DronePayloadSupportState PayloadSupportState => loadTransferEstimator.State;

        /// 当前载荷有效的外部向上支撑 Collider 数量。
        internal int PayloadSupportContactCount => payloadMount != null
            ? payloadMount.AttachedPayloadSupportContactCount
            : 0;

        /// 当前载荷外部竖直支持力，单位 N。
        internal float PayloadUpwardSupportForceNewtons => payloadMount != null
            ? payloadMount.AttachedPayloadUpwardSupportForceNewtons
            : 0f;

        /// 抓取软约束当前竖直力，仅用于诊断。
        internal float PayloadGripVerticalForceNewtons => payloadMount != null
            ? payloadMount.CurrentVerticalGripForceNewtons
            : 0f;

        /// 载荷质量当前进入飞控前馈的比例。
        internal float PayloadSupportedFraction => loadTransferEstimator.SupportedFraction;

        /// 单摆关节实时遥测。
        internal DroneSuspensionJointTelemetry JointTelemetry => suspensionRig != null
            ? suspensionRig.JointTelemetry
            : default;

        float IDroneExternalMassProvider.SupportedMassKilograms => SupportedMassKilograms;
        float IDroneExternalMassProvider.InstalledHardwareMassKilograms => InstalledHardwareMassKilograms;
        float IDroneExternalMassProvider.HardwareMassKilograms => HardwareMassKilograms;
        float IDroneExternalMassProvider.PayloadMassKilograms => PayloadMassKilograms;
        float IDroneExternalMassProvider.SupportedPayloadMassKilograms => SupportedPayloadMassKilograms;
        DroneSuspensionState IDroneSuspensionStateProvider.SuspensionState => SuspensionState;
        void IDroneExternalMassSynchronizer.SynchronizeExternalMass() => SynchronizeExternalMass();

        /// 始终属于整机的抓斗设备质量。
        internal float InstalledHardwareMassKilograms => flightController != null && flightController.Config != null
            ? flightController.Config.GrappleHardwareMassKilograms
            : suspensionRig != null
                ? suspensionRig.HardwareMassKilograms
                : 0f;

        /// 已从主 Rigidbody 拆分并由关节承载的设备质量。
        internal float HardwareMassKilograms => suspensionRig != null && suspensionRig.IsPhysicsActive
            ? suspensionRig.HardwareMassKilograms
            : 0f;

        internal float PayloadMassKilograms => suspensionRig != null && suspensionRig.IsPhysicsActive && payloadMount != null
            ? payloadMount.AttachedMassKilograms
            : 0f;

        /// 已经通过地面支持力变化实际转交给无人机的载荷质量。
        internal float SupportedPayloadMassKilograms => PayloadMassKilograms * PayloadSupportedFraction;

        internal float SupportedMassKilograms => HardwareMassKilograms + SupportedPayloadMassKilograms;

        internal DroneSuspensionState SuspensionState
        {
            get
            {
                if (flightController == null || flightController.Body == null || suspensionRig == null
                    || !suspensionRig.TryGetMassWeightedState(
                        payloadMount != null ? payloadMount.AttachedPayload : null,
                        PayloadSupportedFraction,
                        out var position,
                        out var velocity,
                        out var suspendedMass))
                {
                    return default;
                }

                var ownerBody = flightController.Body;
                var origin = ownerBody.transform.TransformPoint(
                    suspensionRig.GrappleBody != null
                        ? suspensionRig.GrappleBody.GetComponent<ConfigurableJoint>()?.connectedAnchor ?? Vector3.zero
                        : Vector3.zero);
                var offset = position - origin;
                var cableDirection = offset.sqrMagnitude > 0.000001f ? offset.normalized : Vector3.down;
                var swingAngle = Vector3.Angle(Physics.gravity, cableDirection);
                var relativeVelocity = velocity - ownerBody.GetPointVelocity(position);
                var swingRate = suspensionRig.CurrentCableLengthMeters > 0.001f
                    ? relativeVelocity.magnitude / suspensionRig.CurrentCableLengthMeters * Mathf.Rad2Deg
                    : 0f;
                return new DroneSuspensionState(
                    suspensionRig.IsCableTaut,
                    suspendedMass,
                    suspensionRig.CurrentCableLengthMeters,
                    cableDirection,
                    relativeVelocity,
                    swingAngle,
                    swingRate,
                    HardwareMassKilograms,
                    PayloadMassKilograms,
                    PayloadSupportedFraction,
                    position);
            }
        }

        private void Awake()
        {
            ResetStowed();
        }

        private void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
            StepPayloadMass(Time.fixedDeltaTime);
        }

        /// <summary>推进卷扬长度、单摆物理切换和停靠状态。</summary>
        internal void Step(float deltaTime)
        {
            if (flightController == null || flightController.Config == null
                || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            var config = flightController.Config;
            SynchronizeExternalMass();
            if (State == DroneWinchState.Retracting && payloadMount != null && payloadMount.HasPayload)
            {
                targetLength = config.WinchCarryLengthMeters;
            }

            CurrentLengthMeters = Mathf.MoveTowards(
                CurrentLengthMeters,
                targetLength,
                config.WinchSpeedMetersPerSecond * deltaTime);
            suspensionRig?.SetCableLength(CurrentLengthMeters);

            if (State == DroneWinchState.Deploying && suspensionRig != null && !suspensionRig.IsPhysicsActive)
            {
                var activationLength = Mathf.Min(
                    config.WinchDeployedLengthMeters,
                    Mathf.Max(config.WinchStowedLengthMeters + 0.06f, 0.14f));
                suspensionRig.SetDeploymentProgress(CalculateDeploymentProgress(config));
                if (CurrentLengthMeters >= activationLength)
                {
                    suspensionRig.SetPhysicsActive(true);
                    flightController.RefreshMassDistribution();
                }
            }

            if (suspensionRig != null && suspensionRig.IsPhysicsActive)
            {
                suspensionRig.ApplyPassiveDampingDrive(SupportedPayloadMassKilograms);
            }

            if (!Mathf.Approximately(CurrentLengthMeters, targetLength))
            {
                return;
            }

            if (Mathf.Approximately(targetLength, config.WinchDeployedLengthMeters))
            {
                if (suspensionRig != null && !suspensionRig.IsPhysicsActive)
                {
                    suspensionRig.SetPhysicsActive(true);
                    flightController.RefreshMassDistribution();
                }

                State = DroneWinchState.Deployed;
                return;
            }

            if (payloadMount != null && payloadMount.HasPayload)
            {
                State = DroneWinchState.Carrying;
                return;
            }

            if (suspensionRig == null
                || !suspensionRig.IsPhysicsActive
                || suspensionRig.CanDock(config.GrappleDockPositionToleranceMeters, config.GrappleDockSpeedToleranceMetersPerSecond))
            {
                suspensionRig?.SetPhysicsActive(false);
                suspensionRig?.SetDeploymentProgress(0f);
                State = DroneWinchState.Stowed;
                flightController.RefreshMassDistribution();
            }
        }

        /// <summary>根据地面真实支持力推进载荷承载比例。</summary>
        internal void StepPayloadMass(float deltaTime)
        {
            var hasAttachedPayload = suspensionRig != null
                                     && suspensionRig.IsPhysicsActive
                                     && payloadMount != null
                                     && payloadMount.HasPayload;
            if (!hasAttachedPayload)
            {
                loadTransferEstimator.Reset();
                return;
            }

            var payload = payloadMount.AttachedPayload;
            var attachedMass = payloadMount.AttachedMassKilograms;
            if (payload == null || !float.IsFinite(attachedMass) || attachedMass <= 0f)
            {
                loadTransferEstimator.Reset();
                return;
            }

            var blendSeconds = flightController != null && flightController.Config != null
                ? flightController.Config.ExternalMassBlendSeconds
                : 0.15f;
            loadTransferEstimator.Step(
                attachedMass,
                payload.IsSupportStateConfirmed,
                payload.IsGroundSupported,
                payload.LastUpwardSupportForceNewtons,
                blendSeconds,
                deltaTime);
        }

        /// 在放出与收回目标之间切换。
        internal void Toggle()
        {
            if (flightController == null || flightController.Config == null)
            {
                return;
            }

            var config = flightController.Config;
            if (State is DroneWinchState.Deployed or DroneWinchState.Deploying or DroneWinchState.Carrying)
            {
                targetLength = payloadMount != null && payloadMount.HasPayload
                    ? config.WinchCarryLengthMeters
                    : config.WinchStowedLengthMeters;
                State = DroneWinchState.Retracting;
            }
            else
            {
                targetLength = config.WinchDeployedLengthMeters;
                State = DroneWinchState.Deploying;
            }
        }

        /// 将卷扬立即恢复为空载收纳状态。
        internal void ResetStowed()
        {
            var stowedLength = flightController != null && flightController.Config != null
                ? flightController.Config.WinchStowedLengthMeters
                : 0.08f;
            targetLength = stowedLength;
            CurrentLengthMeters = stowedLength;
            State = DroneWinchState.Stowed;
            loadTransferEstimator.Reset();
            suspensionRig?.SetCableLength(stowedLength);
            suspensionRig?.SetPhysicsActive(false);
            suspensionRig?.SetDeploymentProgress(0f);
            flightController?.RefreshMassDistribution();
        }

        /// <summary>由 Prefab 装配或测试绑定卷扬依赖。</summary>
        internal void Configure(
            DroneFlightController controller,
            ConfigurableJoint joint,
            PayloadMount mount,
            DroneSuspensionRig rig = null)
        {
            flightController = controller;
            payloadMount = mount;
            suspensionRig = rig;
            ResetStowed();
        }

        private float CalculateDeploymentProgress(DroneFlightConfig config)
        {
            var range = config.WinchDeployedLengthMeters - config.WinchStowedLengthMeters;
            return range > 0.0001f
                ? Mathf.Clamp01((CurrentLengthMeters - config.WinchStowedLengthMeters) / range)
                : 0f;
        }

        private void SynchronizeExternalMass()
        {
            if (flightController == null || flightController.Config == null)
            {
                return;
            }

            suspensionRig?.SetTotalHardwareMass(flightController.Config.GrappleHardwareMassKilograms);
        }
    }
}
