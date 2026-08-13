using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 将纯控制算法桥接到 Rigidbody 四个真实旋翼施力点。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DroneFlightController : MonoBehaviour
    {
        [SerializeField] private DroneFlightConfig config;
        [SerializeField] private bool armOnStart;
        [SerializeField] private DroneResponseProfile responseProfile = DroneResponseProfile.Normal;
        [SerializeField] private MonoBehaviour externalMassProviderSource;

        private readonly DroneRotor[] orderedRotors = new DroneRotor[4];
        private readonly DroneMotorModel[] motors = new DroneMotorModel[4];
        private readonly DroneMotorState[] motorStates = new DroneMotorState[4];

        private Rigidbody body;
        private DronePidController rollRateController;
        private DronePidController pitchRateController;
        private DronePidController yawRateController;
        private DronePidController verticalSpeedController;
        private DroneControlInput controlInput;
        private float targetYawDegrees;
        private float targetHeightMeters;
        private Vector2 targetHorizontalPosition;
        private bool hadHorizontalInput;
        private bool isGrounded;
        private float unsafeTiltDuration;
        private float hoverCommand;
        private IDroneExternalMassProvider externalMassProvider;
        private bool initialized;
        private Vector3 lastTargetLocalRate;
        private Vector3 lastActualLocalRate;
        private Vector3 lastDesiredWorldVelocity;
        private Vector3 lastDesiredWorldAcceleration;
        private int runtimeTuningSignature;
        private DroneFlightConfig sourceConfig;
        private int sourceConfigSignature;

        /// <summary>电机是否已解锁。</summary>
        internal bool IsArmed { get; private set; }

        /// <summary>最近一次混控输出。</summary>
        internal QuadrotorMotorOutput LastMotorOutput { get; private set; }

        /// <summary>最近一次固定步四个旋翼的总推力，单位 N。</summary>
        internal float LastTotalThrustNewtons { get; private set; }

        /// <summary>当前高度目标，单位 m。</summary>
        internal float TargetHeightMeters => targetHeightMeters;

        /// <summary>当前响应档位。</summary>
        internal DroneResponseProfile ResponseProfile => responseProfile;

        /// <summary>当前飞控运行状态。</summary>
        internal DroneFlightOperationState OperationState { get; private set; }

        /// <summary>最近一次进入飞控的设备无关输入帧。</summary>
        internal DroneControlInput CurrentControlInput => controlInput;

        /// <summary>当前机体刚体，只供遥测和玩法查询，不允许外部覆盖飞控状态。</summary>
        internal Rigidbody Body => body;

        /// 当前飞行配置；只允许同模块运行时组件读取。
        internal DroneFlightConfig Config => config;

        /// <summary>最近一个固定步的目标机体角速度，单位 rad/s。</summary>
        internal Vector3 LastTargetLocalRate => lastTargetLocalRate;

        /// <summary>最近一个固定步的实际机体角速度，单位 rad/s。</summary>
        internal Vector3 LastActualLocalRate => lastActualLocalRate;

        internal Vector3 LastDesiredWorldVelocity => lastDesiredWorldVelocity;

        internal Vector3 LastDesiredWorldAcceleration => lastDesiredWorldAcceleration;

        internal DronePidTelemetry RollRateTelemetry => rollRateController?.Telemetry ?? default;

        internal DronePidTelemetry PitchRateTelemetry => pitchRateController?.Telemetry ?? default;

        internal DronePidTelemetry YawRateTelemetry => yawRateController?.Telemetry ?? default;

        internal DronePidTelemetry VerticalSpeedTelemetry => verticalSpeedController?.Telemetry ?? default;

        internal float CurrentHardwareMassKilograms => externalMassProvider?.HardwareMassKilograms ?? 0f;

        internal float CurrentPayloadMassKilograms => externalMassProvider?.PayloadMassKilograms ?? 0f;

        internal float CurrentSupportedPayloadMassKilograms => externalMassProvider?.SupportedPayloadMassKilograms ?? 0f;

        internal float CurrentSupportedMassKilograms => body != null
            ? body.mass + (externalMassProvider?.SupportedMassKilograms ?? 0f)
            : 0f;

        internal float CurrentHoverCommand => initialized ? CalculateHoverCommand() : 0f;

        internal float CurrentAverageMotorCommand => motorStates.Length > 0
            ? (motorStates[0].NormalizedOutput + motorStates[1].NormalizedOutput
               + motorStates[2].NormalizedOutput + motorStates[3].NormalizedOutput) / 4f
            : 0f;

        internal float CurrentPowerReserve => Mathf.Clamp01(1f - CurrentHoverCommand);

        internal DronePayloadOperatingZone CurrentPayloadZone => CurrentPayloadMassKilograms > config.MaximumPayloadMassKilograms
            ? DronePayloadOperatingZone.OverloadRejected
            : CurrentPayloadMassKilograms > config.RatedPayloadKilograms
                ? DronePayloadOperatingZone.AboveRated
                : DronePayloadOperatingZone.Rated;

        /// <summary>当前档位的键盘输入上升率。</summary>
        internal float InputRiseRate => initialized ? config.GetProfile(responseProfile).InputRiseRate : 0f;

        /// <summary>
        /// 供显式运行时装配和确定性测试在 Awake 前注入配置。
        /// </summary>
        /// <param name="flightConfig">飞行配置实例。</param>
        /// <param name="shouldArmOnStart">启动时是否立即解锁。</param>
        internal void Configure(DroneFlightConfig flightConfig, bool shouldArmOnStart)
        {
            config = flightConfig;
            armOnStart = shouldArmOnStart;
        }

        /// <summary>绑定只读外部承载质量来源。</summary>
        internal void ConfigureExternalMassProvider(MonoBehaviour provider)
        {
            externalMassProviderSource = provider;
            externalMassProvider = provider as IDroneExternalMassProvider;
        }

        private void Awake()
        {
            initialized = TryInitialize();
            if (!initialized)
            {
                enabled = false;
                return;
            }

            SetArmed(armOnStart);
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            SynchronizeRuntimeConfig();
            RefreshRuntimeTuning();
            if (!IsArmed)
            {
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            var profile = config.GetProfile(responseProfile);
            UpdateOperationState(deltaTime);
            if (!IsArmed)
            {
                return;
            }

            var actualYawDegrees = body.rotation.eulerAngles.y;
            var maximumYawLeadDegrees = Mathf.Clamp(
                config.MaximumRateRadiansPerSecond / Mathf.Max(0.01f, config.AttitudeGain) * Mathf.Rad2Deg,
                10f,
                60f);
            targetYawDegrees = DroneAttitudeMath.AdvanceBoundedYawTarget(
                targetYawDegrees,
                actualYawDegrees,
                controlInput.Yaw * profile.MaximumYawSpeedDegrees * deltaTime,
                maximumYawLeadDegrees);
            CalculateHorizontalAttitudeTargets(out var targetPitch, out var targetRoll);
            // 平移倾角始终在真实机头坐标系内求解；偏航误差单独生成 Y 轴目标角速度。
            // 若把尚未追上的 targetYaw 与 Pitch/Roll 合成同一个 Quaternion，持续偏航后的大角度误差
            // 会把水平速度修正耦合到 Roll/Pitch，表现为高速前飞时左右交替摇摆。
            var tiltAttitude = Quaternion.Euler(targetPitch, actualYawDegrees, targetRoll);
            var tiltTargetRate = DroneAttitudeMath.CalculateTargetRate(
                body.rotation,
                tiltAttitude,
                config.AttitudeGain,
                config.MaximumRateRadiansPerSecond);
            var yawTargetRate = Mathf.Clamp(
                Mathf.DeltaAngle(actualYawDegrees, targetYawDegrees)
                * Mathf.Deg2Rad
                * config.AttitudeGain,
                -config.MaximumRateRadiansPerSecond,
                config.MaximumRateRadiansPerSecond);
            var targetLocalRate = new Vector3(tiltTargetRate.x, yawTargetRate, tiltTargetRate.z);
            var actualLocalRate = transform.InverseTransformDirection(body.angularVelocity);
            lastTargetLocalRate = targetLocalRate;
            lastActualLocalRate = actualLocalRate;

            // 大疆式升降输入改变高度目标；松杆后继续保持当前目标高度。
            targetHeightMeters += controlInput.Lift
                * profile.MaximumVerticalSpeed
                * deltaTime;
            var targetVerticalSpeed = Mathf.Clamp(
                (targetHeightMeters - body.position.y) * config.AltitudeGain,
                -profile.MaximumVerticalSpeed,
                profile.MaximumVerticalSpeed);
            var verticalSpeedCorrection = verticalSpeedController.Step(
                targetVerticalSpeed - body.linearVelocity.y,
                deltaTime);

            // 玩家正 Roll 表示向右滚，对应 Unity 局部 Z 轴负方向。
            var rollOutput = rollRateController.Step(-targetLocalRate.z + actualLocalRate.z, deltaTime);
            var pitchOutput = pitchRateController.Step(targetLocalRate.x - actualLocalRate.x, deltaTime);
            var yawOutput = yawRateController.Step(targetLocalRate.y - actualLocalRate.y, deltaTime);
            // 推力与 RPM² 成正比；倾斜时要让总推力按 1/cos 增加，因此电机命令按 1/sqrt(cos) 补偿。
            var verticalThrustRatio = Mathf.Max(0.5f, Vector3.Dot(transform.up, Vector3.up));
            var tiltCompensatedHoverCommand = CalculateHoverCommand() / Mathf.Sqrt(verticalThrustRatio);
            var collective = Mathf.Clamp01(tiltCompensatedHoverCommand + verticalSpeedCorrection);
            LastMotorOutput = QuadrotorMixer.Mix(collective, rollOutput, pitchOutput, yawOutput);
            rollRateController.ApplyActuatorSaturation(LastMotorOutput.IsSaturated);
            pitchRateController.ApplyActuatorSaturation(LastMotorOutput.IsSaturated);
            yawRateController.ApplyActuatorSaturation(LastMotorOutput.IsSaturated);
            verticalSpeedController.ApplyActuatorSaturation(LastMotorOutput.IsSaturated);

            StepAndApplyMotors(LastMotorOutput, deltaTime);
        }

        /// <summary>
        /// 更新与设备无关的飞行输入。
        /// </summary>
        /// <param name="input">已经归一化并完成非法数值收口的输入帧。</param>
        internal void SetControlInput(DroneControlInput input)
        {
            controlInput = input;
        }

        /// <summary>
        /// 设置高度保持目标。
        /// </summary>
        /// <param name="heightMeters">世界 Y 高度，单位 m。</param>
        internal void SetTargetHeight(float heightMeters)
        {
            if (!float.IsFinite(heightMeters))
            {
                Debug.LogError("[DroneFlight] 高度目标不是有限值，已忽略。", this);
                return;
            }

            targetHeightMeters = heightMeters;
        }

        /// <summary>
        /// 平滑切换响应档位，不重置当前位置、高度或 Yaw 目标。
        /// </summary>
        /// <param name="profile">目标响应档位。</param>
        internal void SetResponseProfile(DroneResponseProfile profile)
        {
            responseProfile = profile;
        }

        /// <summary>解锁并开始自动上升到配置高度。</summary>
        internal void BeginAutomaticTakeoff()
        {
            if (!initialized || OperationState == DroneFlightOperationState.Fault)
            {
                return;
            }

            SetArmed(true);
            targetHeightMeters = Mathf.Max(config.AutomaticTakeoffHeightMeters, body.position.y + 0.5f);
            OperationState = DroneFlightOperationState.TakingOff;
        }

        /// <summary>开始限速下降，接地稳定后自动锁定电机。</summary>
        internal void BeginAutomaticLanding()
        {
            if (!initialized || !IsArmed)
            {
                return;
            }

            OperationState = DroneFlightOperationState.Landing;
        }

        /// <summary>
        /// 设置电机解锁状态；锁定会立即停止所有旋翼施力并清空控制历史。
        /// </summary>
        /// <param name="armed">是否允许旋翼产生物理推力。</param>
        internal void SetArmed(bool armed)
        {
            if (!initialized)
            {
                IsArmed = false;
                return;
            }

            IsArmed = armed;
            OperationState = armed
                ? DroneFlightOperationState.ArmedIdle
                : DroneFlightOperationState.Disarmed;
            controlInput = default;
            rollRateController.Reset();
            pitchRateController.Reset();
            yawRateController.Reset();
            verticalSpeedController.Reset();
            targetYawDegrees = body.rotation.eulerAngles.y;
            targetHeightMeters = body.position.y;
            targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
            hadHorizontalInput = false;
            LastMotorOutput = default;
            LastTotalThrustNewtons = 0f;
            lastTargetLocalRate = Vector3.zero;
            lastActualLocalRate = Vector3.zero;
            lastDesiredWorldVelocity = Vector3.zero;
            lastDesiredWorldAcceleration = Vector3.zero;
            for (var index = 0; index < motors.Length; index++)
            {
                motors[index].Reset();
                motorStates[index] = default;
                orderedRotors[index]?.ResetVisual();
            }
        }

        /// 将故障和所有控制历史恢复到锁定待机状态。
        internal void ResetFlightState()
        {
            if (!initialized || body == null)
            {
                return;
            }

            unsafeTiltDuration = 0f;
            isGrounded = true;
            SetArmed(false);
            OperationState = DroneFlightOperationState.Disarmed;
        }

        private bool TryInitialize()
        {
            if (config == null)
            {
                Debug.LogError("[DroneFlight] 缺少 DroneFlightConfig，已停止飞行施力。", this);
                return false;
            }

            if (!config.TryValidate(out var diagnostic))
            {
                Debug.LogError($"[DroneFlight] 配置无效：{diagnostic} 已停止飞行施力。", this);
                return false;
            }

            sourceConfig = config;
            config = Instantiate(sourceConfig);
            config.name = $"{sourceConfig.name} (Runtime)";
            sourceConfigSignature = CalculateSourceConfigSignature();

            body = GetComponent<Rigidbody>();
            ApplyBodySettings();
            externalMassProvider = externalMassProviderSource as IDroneExternalMassProvider;
            if (externalMassProvider == null)
            {
                var winch = GetComponent<DroneWinchController>();
                externalMassProvider = winch;
                externalMassProviderSource = winch;
            }
            if (!TryOrderRotors(GetComponentsInChildren<DroneRotor>(true)))
            {
                return false;
            }

            var motorSettings = new DroneMotorSettings(
                config.MotorResponseTimeSeconds,
                config.MaximumRpm,
                config.ThrustCoefficient,
                config.ReactionTorqueCoefficient);
            for (var index = 0; index < motors.Length; index++)
            {
                motors[index] = new DroneMotorModel(motorSettings);
            }
            runtimeTuningSignature = CalculateRuntimeTuningSignature();
            responseProfile = config.DefaultResponseProfile;

            rollRateController = new DronePidController(config.CreateRollRateSettings());
            pitchRateController = new DronePidController(config.CreatePitchRateSettings());
            yawRateController = new DronePidController(config.CreateYawRateSettings());
            verticalSpeedController = new DronePidController(config.CreateVerticalSpeedSettings());

            hoverCommand = CalculateHoverCommand();
            if (!float.IsFinite(hoverCommand) || hoverCommand >= 1f)
            {
                Debug.LogError("[DroneFlight] 最大总推力不足以抵消机体重量，已停止飞行施力。", this);
                return false;
            }

            targetYawDegrees = body.rotation.eulerAngles.y;
            targetHeightMeters = body.position.y;
            targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
            return true;
        }

        private float CalculateHoverCommand()
        {
            if (body == null || config == null)
            {
                return 0f;
            }

            var externalMass = externalMassProvider != null
                ? Mathf.Max(0f, externalMassProvider.SupportedMassKilograms)
                : 0f;
            var supportedMass = body.mass + externalMass;
            return DronePayloadTuningCalculator.CalculateHoverCommand(
                supportedMass,
                Mathf.Abs(Physics.gravity.y),
                config.MaximumRpm,
                config.ThrustCoefficient);
        }

        private void CalculateHorizontalAttitudeTargets(out float targetPitch, out float targetRoll)
        {
            var horizontalInput = new Vector2(controlInput.Right, controlInput.Forward);
            var profile = config.GetProfile(responseProfile);
            Vector3 desiredWorldVelocity;
            if (horizontalInput.sqrMagnitude > 0.0001f)
            {
                desiredWorldVelocity = DroneAttitudeMath.CalculateHeadingRelativeWorldVelocity(
                    horizontalInput,
                    body.rotation.eulerAngles.y,
                    profile.MaximumHorizontalSpeed);
                targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
                hadHorizontalInput = true;
            }
            else
            {
                if (hadHorizontalInput)
                {
                    targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
                    hadHorizontalInput = false;
                }

                var positionError = targetHorizontalPosition - new Vector2(body.position.x, body.position.z);
                var desiredPlanarVelocity = Vector2.ClampMagnitude(
                    positionError * config.HorizontalPositionGain,
                    profile.MaximumHorizontalSpeed);
                desiredWorldVelocity = new Vector3(desiredPlanarVelocity.x, 0f, desiredPlanarVelocity.y);
            }

            var currentWorldVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            var desiredWorldAcceleration = Vector3.ClampMagnitude(
                (desiredWorldVelocity - currentWorldVelocity) * config.HorizontalVelocityGain,
                profile.MaximumHorizontalAcceleration);
            lastDesiredWorldVelocity = desiredWorldVelocity;
            lastDesiredWorldAcceleration = desiredWorldAcceleration;
            var yawLocalAcceleration = Quaternion.Inverse(Quaternion.Euler(0f, body.rotation.eulerAngles.y, 0f))
                * desiredWorldAcceleration;

            targetPitch = Mathf.Clamp(
                Mathf.Atan2(yawLocalAcceleration.z, -Physics.gravity.y) * Mathf.Rad2Deg,
                -profile.MaximumTiltDegrees,
                profile.MaximumTiltDegrees);
            targetRoll = Mathf.Clamp(
                -Mathf.Atan2(yawLocalAcceleration.x, -Physics.gravity.y) * Mathf.Rad2Deg,
                -profile.MaximumTiltDegrees,
                profile.MaximumTiltDegrees);
        }

        private void UpdateOperationState(float deltaTime)
        {
            var upAlignment = Vector3.Dot(transform.up, Vector3.up);
            if (upAlignment < 0.2f)
            {
                unsafeTiltDuration += deltaTime;
                if (unsafeTiltDuration >= 0.5f)
                {
                    SetArmed(false);
                    OperationState = DroneFlightOperationState.Fault;
                    return;
                }
            }
            else
            {
                unsafeTiltDuration = 0f;
            }

            if (OperationState == DroneFlightOperationState.TakingOff
                && Mathf.Abs(body.position.y - targetHeightMeters) <= 0.1f
                && Mathf.Abs(body.linearVelocity.y) <= 0.2f)
            {
                OperationState = DroneFlightOperationState.Flying;
            }
            else if (OperationState == DroneFlightOperationState.Landing)
            {
                targetHeightMeters -= Mathf.Max(0.1f, config.AutomaticLandingSpeedMetersPerSecond) * deltaTime;
                if (isGrounded && Mathf.Abs(body.linearVelocity.y) <= 0.2f)
                {
                    SetArmed(false);
                }
            }
            else if (OperationState == DroneFlightOperationState.ArmedIdle && !isGrounded)
            {
                OperationState = DroneFlightOperationState.Flying;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            isGrounded = false;
            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    isGrounded = true;
                    break;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            isGrounded = false;
        }

        private void RefreshRuntimeTuning()
        {
            if (config == null || body == null || !config.TryValidate(out _))
            {
                return;
            }

            var signature = CalculateRuntimeTuningSignature();
            if (signature == runtimeTuningSignature)
            {
                return;
            }

            ApplyBodySettings();
            var settings = new DroneMotorSettings(
                config.MotorResponseTimeSeconds,
                config.MaximumRpm,
                config.ThrustCoefficient,
                config.ReactionTorqueCoefficient);
            foreach (var motor in motors)
            {
                motor?.UpdateSettings(settings, preserveCurrentRpm: true);
            }

            runtimeTuningSignature = signature;
        }

        private void SynchronizeRuntimeConfig()
        {
            if (sourceConfig == null || config == null)
            {
                return;
            }

            var signature = CalculateSourceConfigSignature();
            if (signature == sourceConfigSignature)
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceConfig), config);
            sourceConfigSignature = signature;
        }

        private int CalculateSourceConfigSignature()
        {
            return sourceConfig != null ? JsonUtility.ToJson(sourceConfig).GetHashCode() : 0;
        }

        private void ApplyBodySettings()
        {
            body.mass = config.BodyMassKilograms;
            body.linearDamping = config.BodyLinearDamping;
            body.angularDamping = config.BodyAngularDamping;
        }

        private int CalculateRuntimeTuningSignature()
        {
            var hash = new HashCode();
            hash.Add(config.PowerConfigurationMode);
            hash.Add(config.BodyMassKilograms);
            hash.Add(config.BodyLinearDamping);
            hash.Add(config.BodyAngularDamping);
            hash.Add(config.MotorResponseTimeSeconds);
            hash.Add(config.MaximumRpm);
            hash.Add(config.ThrustCoefficient);
            hash.Add(config.ReactionTorqueCoefficient);
            return hash.ToHashCode();
        }

        private void OnDestroy()
        {
            if (config != null && config != sourceConfig)
            {
                Destroy(config);
            }
        }

        private bool TryOrderRotors(DroneRotor[] rotors)
        {
            if (rotors.Length != orderedRotors.Length)
            {
                Debug.LogError($"[DroneFlight] 需要 4 个 DroneRotor，当前找到 {rotors.Length} 个。", this);
                return false;
            }

            var assigned = new bool[orderedRotors.Length];
            foreach (var rotor in rotors)
            {
                var index = (int)rotor.Position;
                if (index < 0 || index >= orderedRotors.Length || assigned[index])
                {
                    Debug.LogError($"[DroneFlight] Rotor 位置重复或越界：{rotor.Position}。", rotor);
                    return false;
                }

                assigned[index] = true;
                orderedRotors[index] = rotor;
            }

            return true;
        }

        private void StepAndApplyMotors(QuadrotorMotorOutput output, float deltaTime)
        {
            var commands = new[] { output.FrontLeft, output.FrontRight, output.RearLeft, output.RearRight };
            LastTotalThrustNewtons = 0f;
            for (var index = 0; index < motors.Length; index++)
            {
                var state = motors[index].Step(commands[index], deltaTime);
                motorStates[index] = state;
                LastTotalThrustNewtons += state.ThrustNewtons;
                var rotor = orderedRotors[index];
                body.AddForceAtPosition(
                    rotor.ForceTransform.up * state.ThrustNewtons,
                    rotor.ForceTransform.position,
                    ForceMode.Force);
                body.AddTorque(
                    rotor.ForceTransform.up
                    * state.ReactionTorqueNewtonMeters
                    * (float)rotor.Direction,
                    ForceMode.Force);

                rotor.SetVisualRpm(state.Rpm);
            }
        }

        internal bool TryGetRotorDebugVector(int index, out Vector3 origin, out Vector3 thrustForce)
        {
            if (index < 0 || index >= orderedRotors.Length || orderedRotors[index] == null)
            {
                origin = Vector3.zero;
                thrustForce = Vector3.zero;
                return false;
            }

            var forceTransform = orderedRotors[index].ForceTransform;
            origin = forceTransform.position;
            thrustForce = forceTransform.up * motorStates[index].ThrustNewtons;
            return true;
        }

        internal Vector3 CurrentTotalThrustVector
        {
            get
            {
                var total = Vector3.zero;
                for (var index = 0; index < orderedRotors.Length; index++)
                {
                    if (orderedRotors[index] != null)
                    {
                        total += orderedRotors[index].ForceTransform.up * motorStates[index].ThrustNewtons;
                    }
                }

                return total;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (var index = 0; index < orderedRotors.Length; index++)
            {
                var rotor = orderedRotors[index];
                if (rotor == null)
                {
                    continue;
                }

                var origin = rotor.ForceTransform.position;
                Gizmos.DrawLine(origin, origin + rotor.ForceTransform.up * motorStates[index].ThrustNewtons * 0.08f);
            }
        }
    }
}
