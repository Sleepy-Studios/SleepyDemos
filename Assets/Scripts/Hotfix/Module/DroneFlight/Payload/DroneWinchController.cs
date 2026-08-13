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

    /// <summary>通过移动物理吊链的连接锚点实现卷扬收放。</summary>
    public sealed class DroneWinchController : MonoBehaviour, IDroneExternalMassProvider
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private ConfigurableJoint suspensionJoint;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneSuspensionRig suspensionRig;

        [SerializeField] private Vector3 baseConnectedAnchor;
        [SerializeField] private bool hasConfiguredBaseAnchor;
        private float targetLength;

        /// 当前卷扬状态。
        internal DroneWinchState State { get; private set; } = DroneWinchState.Stowed;

        /// 当前放出长度，单位 m。
        internal float CurrentLengthMeters { get; private set; }

        /// 收纳时为零，部署末段平滑建立设备前馈，物理启用后报告真实设备与载荷质量。
        float IDroneExternalMassProvider.SupportedMassKilograms => SupportedMassKilograms;
        float IDroneExternalMassProvider.HardwareMassKilograms => HardwareMassKilograms;
        float IDroneExternalMassProvider.PayloadMassKilograms => PayloadMassKilograms;
        float IDroneExternalMassProvider.SupportedPayloadMassKilograms => SupportedPayloadMassKilograms;

        internal float HardwareMassKilograms
        {
            get
            {
                if (suspensionRig == null)
                {
                    return 0f;
                }

                if (suspensionRig.IsPhysicsActive)
                {
                    return suspensionRig.HardwareMassKilograms;
                }

                if (State != DroneWinchState.Deploying || flightController == null || flightController.Config == null)
                {
                    return 0f;
                }

                // 当前部署动画在完成前使用运动学刚体，若等到最后一帧才报告全部质量，
                // 电机一阶响应会在物理载荷突然接入后产生可见掉高。末段逐渐建立前馈，
                // 等价于真实卷扬绳索逐渐拉紧，不改变部署完成后的真实设备质量。
                var deployment = CalculateDeploymentProgress(flightController.Config);
                var tensionBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, deployment));
                return suspensionRig.HardwareMassKilograms * tensionBlend;
            }
        }

        internal float PayloadMassKilograms => suspensionRig != null && suspensionRig.IsPhysicsActive && payloadMount != null
            ? payloadMount.AttachedMassKilograms
            : 0f;

        /// 已经通过吊挂张力实际传递给无人机的载荷质量。
        internal float SupportedPayloadMassKilograms => suspensionRig != null
                                                         && suspensionRig.IsPhysicsActive
                                                         && payloadMount != null
            ? payloadMount.SupportedMassKilograms
            : 0f;

        internal float SupportedMassKilograms => HardwareMassKilograms + SupportedPayloadMassKilograms;

        private void Awake()
        {
            if (!hasConfiguredBaseAnchor && suspensionJoint != null)
            {
                baseConnectedAnchor = suspensionJoint.connectedAnchor;
                hasConfiguredBaseAnchor = true;
            }
            ResetStowed();
        }

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>推进卷扬长度、可见部署动画和完成状态。</summary>
        internal void Step(float deltaTime)
        {
            if (flightController == null || flightController.Config == null
                || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                if (State is DroneWinchState.Deploying or DroneWinchState.Retracting)
                {
                    Debug.LogError("[DroneFlight] 卷扬缺少飞控配置，已回到收纳状态。", this);
                    ResetStowed();
                }
                return;
            }

            var config = flightController.Config;
            suspensionRig?.SetTotalHardwareMass(config.GrappleHardwareMassKilograms);
            if (State == DroneWinchState.Retracting && payloadMount != null && payloadMount.HasPayload)
            {
                targetLength = config.WinchCarryLengthMeters;
            }

            CurrentLengthMeters = Mathf.MoveTowards(
                CurrentLengthMeters,
                targetLength,
                config.WinchSpeedMetersPerSecond * deltaTime);
            if (suspensionJoint != null)
            {
                suspensionJoint.connectedAnchor = baseConnectedAnchor + Vector3.down * CurrentLengthMeters;
            }

            if ((State is DroneWinchState.Deploying or DroneWinchState.Retracting)
                && (payloadMount == null || !payloadMount.HasPayload))
            {
                suspensionRig?.SetDeploymentProgress(CalculateDeploymentProgress(config));
            }

            if (!Mathf.Approximately(CurrentLengthMeters, targetLength))
            {
                return;
            }

            if (Mathf.Approximately(targetLength, config.WinchDeployedLengthMeters))
            {
                suspensionRig?.SetDeploymentProgress(1f);
                suspensionRig?.SetPhysicsActive(true);
                State = DroneWinchState.Deployed;
            }
            else if (payloadMount != null && payloadMount.HasPayload)
            {
                State = DroneWinchState.Carrying;
            }
            else
            {
                suspensionRig?.SetDeploymentProgress(0f);
                suspensionRig?.SetPhysicsActive(false);
                State = DroneWinchState.Stowed;
            }
        }

        /// 在放出与收回目标之间切换。
        internal void Toggle()
        {
            if (flightController == null)
            {
                return;
            }

            var config = flightController.Config;
            if (State is DroneWinchState.Deployed or DroneWinchState.Deploying)
            {
                targetLength = payloadMount != null && payloadMount.HasPayload
                    ? config.WinchCarryLengthMeters
                    : config.WinchStowedLengthMeters;
                State = DroneWinchState.Retracting;
                if (payloadMount == null || !payloadMount.HasPayload)
                {
                    suspensionRig?.SetPhysicsActive(false);
                    suspensionRig?.SetDeploymentProgress(CalculateDeploymentProgress(config));
                }
            }
            else
            {
                suspensionRig?.SetPhysicsActive(false);
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
            suspensionRig?.SetPhysicsActive(false);
            suspensionRig?.SetDeploymentProgress(0f);
            if (suspensionJoint != null)
            {
                suspensionJoint.connectedAnchor = baseConnectedAnchor + Vector3.down * CurrentLengthMeters;
            }
        }

        /// <summary>
        /// 由 Prefab 装配或测试绑定卷扬依赖。
        /// </summary>
        /// <param name="controller">提供卷扬参数的飞控。</param>
        /// <param name="joint">第一节吊链连接无人机的物理 Joint。</param>
        /// <param name="mount">用于判断是否携带载荷。</param>
        internal void Configure(
            DroneFlightController controller,
            ConfigurableJoint joint,
            PayloadMount mount,
            DroneSuspensionRig rig = null)
        {
            flightController = controller;
            suspensionJoint = joint;
            payloadMount = mount;
            suspensionRig = rig;
            baseConnectedAnchor = joint != null ? joint.connectedAnchor : Vector3.zero;
            hasConfiguredBaseAnchor = joint != null;
            ResetStowed();
        }

        private float CalculateDeploymentProgress(DroneFlightConfig config)
        {
            var range = config.WinchDeployedLengthMeters - config.WinchStowedLengthMeters;
            return range > 0.0001f
                ? Mathf.Clamp01((CurrentLengthMeters - config.WinchStowedLengthMeters) / range)
                : 0f;
        }
    }
}
