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

        private readonly DroneRotor[] orderedRotors = new DroneRotor[DroneRotorActuatorRuntime.RotorCount];
        private readonly DroneMotorModel[] motors = new DroneMotorModel[DroneRotorActuatorRuntime.RotorCount];
        private readonly DroneMotorState[] motorStates = new DroneMotorState[DroneRotorActuatorRuntime.RotorCount];

        private Rigidbody body;
        private DronePidController rollRateController;
        private DronePidController pitchRateController;
        private DronePidController yawRateController;
        private DronePidController verticalSpeedController;
        private DronePidController horizontalVelocityXController;
        private DronePidController horizontalVelocityZController;
        private readonly DroneTrajectoryGenerator trajectoryGenerator = new();
        private QuadrotorControlAllocator controlAllocator;
        private DroneControlInput controlInput;
        private float targetYawDegrees;
        private float targetHeightMeters;
        private Vector2 targetHorizontalPosition;
        private bool hadHorizontalInput;
        private bool hadVerticalInput;
        private bool isGrounded;
        private float unsafeTiltDuration;
        private float hoverCommand;
        private IDroneExternalMassProvider externalMassProvider;
        private bool initialized;
        private Vector3 lastTargetLocalRate;
        private Vector3 lastActualLocalRate;
        private Vector3 lastDesiredWorldVelocity;
        private Vector3 lastDesiredWorldAcceleration;
        private Vector3 lastDesiredWorldForce;
        private Vector3 previousVelocity;
        private Vector3 previousLocalAngularVelocity;
        private Vector3 filteredAcceleration;
        private Vector3 filteredLocalAngularAcceleration;
        private Vector3 previousTargetLocalRate;
        private DroneAllocationResult lastAllocation;
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

        internal Vector3 LastDesiredWorldForce => lastDesiredWorldForce;

        internal DroneAllocationResult LastAllocation => lastAllocation;

        internal float CurrentHardwareMassKilograms => externalMassProvider?.HardwareMassKilograms ?? 0f;

        internal float CurrentPayloadMassKilograms => externalMassProvider?.PayloadMassKilograms ?? 0f;

        internal float CurrentSupportedMassKilograms => body != null
            ? body.mass + (externalMassProvider?.SupportedMassKilograms ?? 0f)
            : 0f;

        internal float CurrentHoverCommand => initialized ? CalculateHoverCommand() : 0f;

        internal float CurrentAverageMotorCommand => motorStates.Length > 0
            ? (motorStates[0].NormalizedOutput + motorStates[1].NormalizedOutput
               + motorStates[2].NormalizedOutput + motorStates[3].NormalizedOutput) / 4f
            : 0f;

        internal float CurrentPowerReserve => Mathf.Clamp01(1f - CurrentHoverCommand);

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
            body ??= GetComponent<Rigidbody>();
            RefreshMassDistribution();
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

            var state = CaptureState(deltaTime);
            var actualYawDegrees = state.Rotation.eulerAngles.y;
            var trajectory = trajectoryGenerator.Step(controlInput, actualYawDegrees, profile, deltaTime);
            targetYawDegrees = trajectory.YawDegrees;
            var desiredVelocity = BuildPositionAwareVelocity(trajectory, profile);

            var horizontalX = horizontalVelocityXController.StepWithMeasurement(
                desiredVelocity.x - state.Velocity.x,
                state.Acceleration.x,
                trajectory.WorldAcceleration.x,
                deltaTime);
            var horizontalZ = horizontalVelocityZController.StepWithMeasurement(
                desiredVelocity.z - state.Velocity.z,
                state.Acceleration.z,
                trajectory.WorldAcceleration.z,
                deltaTime);
            var vertical = verticalSpeedController.StepWithMeasurement(
                desiredVelocity.y - state.Velocity.y,
                state.Acceleration.y,
                trajectory.WorldAcceleration.y,
                deltaTime);
            var desiredAcceleration = new Vector3(horizontalX, vertical, horizontalZ);
            var horizontalAcceleration = Vector3.ClampMagnitude(
                new Vector3(desiredAcceleration.x, 0f, desiredAcceleration.z),
                profile.MaximumHorizontalAcceleration);
            desiredAcceleration = new Vector3(
                horizontalAcceleration.x,
                Mathf.Clamp(desiredAcceleration.y, -profile.MaximumVerticalAcceleration, profile.MaximumVerticalAcceleration),
                horizontalAcceleration.z);
            lastDesiredWorldVelocity = desiredVelocity;
            lastDesiredWorldAcceleration = desiredAcceleration;

            var supportedMass = Mathf.Max(0.001f, CurrentSupportedMassKilograms);
            var desiredForce = supportedMass * (desiredAcceleration - Physics.gravity);
            desiredForce = DronePhysicalControlMath.LimitForceByTilt(desiredForce, profile.MaximumTiltDegrees);
            lastDesiredWorldForce = desiredForce;
            var targetLocalRate = DronePhysicalControlMath.CalculateReducedAttitudeRate(
                state.Rotation,
                desiredForce.normalized,
                trajectory.YawDegrees,
                config.AttitudeGain,
                config.YawAttitudeGain,
                config.YawWeight,
                config.MaximumRateRadiansPerSecond);
            targetLocalRate.y = Mathf.Clamp(
                targetLocalRate.y + trajectory.YawRateRadians,
                -config.MaximumRateRadiansPerSecond,
                config.MaximumRateRadiansPerSecond);
            var targetAngularAcceleration = (targetLocalRate - previousTargetLocalRate) / deltaTime;
            previousTargetLocalRate = targetLocalRate;
            lastTargetLocalRate = targetLocalRate;
            lastActualLocalRate = state.LocalAngularVelocity;

            var pitchAcceleration = pitchRateController.StepWithMeasurement(
                targetLocalRate.x - state.LocalAngularVelocity.x,
                state.LocalAngularAcceleration.x,
                targetAngularAcceleration.x * config.RateFeedForward,
                deltaTime);
            var yawAcceleration = yawRateController.StepWithMeasurement(
                targetLocalRate.y - state.LocalAngularVelocity.y,
                state.LocalAngularAcceleration.y,
                targetAngularAcceleration.y * config.RateFeedForward,
                deltaTime);
            var rollAcceleration = rollRateController.StepWithMeasurement(
                targetLocalRate.z - state.LocalAngularVelocity.z,
                state.LocalAngularAcceleration.z,
                targetAngularAcceleration.z * config.RateFeedForward,
                deltaTime);
            var desiredLocalAngularAcceleration = new Vector3(
                pitchAcceleration,
                yawAcceleration,
                rollAcceleration);
            var desiredLocalTorque = DronePhysicalControlMath.CalculateLocalTorque(
                desiredLocalAngularAcceleration,
                state.LocalAngularVelocity,
                body.inertiaTensor,
                body.inertiaTensorRotation);
            lastAllocation = controlAllocator.Allocate(desiredForce.magnitude, desiredLocalTorque);
            pitchRateController.ApplyDirectionalSaturation(lastAllocation.Saturation.Pitch);
            yawRateController.ApplyDirectionalSaturation(lastAllocation.Saturation.Yaw);
            rollRateController.ApplyDirectionalSaturation(lastAllocation.Saturation.Roll);
            verticalSpeedController.ApplyDirectionalSaturation(lastAllocation.Saturation.Thrust);

            LastMotorOutput = BuildMotorOutput(lastAllocation);
            LastTotalThrustNewtons = DroneRotorActuatorRuntime.StepAndApply(
                body,
                orderedRotors,
                motors,
                motorStates,
                LastMotorOutput,
                deltaTime);
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
            horizontalVelocityXController.Reset();
            horizontalVelocityZController.Reset();
            targetYawDegrees = body.rotation.eulerAngles.y;
            targetHeightMeters = body.position.y;
            targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
            hadHorizontalInput = false;
            hadVerticalInput = false;
            LastMotorOutput = default;
            LastTotalThrustNewtons = 0f;
            lastTargetLocalRate = Vector3.zero;
            lastActualLocalRate = Vector3.zero;
            lastDesiredWorldVelocity = Vector3.zero;
            lastDesiredWorldAcceleration = Vector3.zero;
            lastDesiredWorldForce = Vector3.zero;
            lastAllocation = default;
            filteredAcceleration = Vector3.zero;
            filteredLocalAngularAcceleration = Vector3.zero;
            previousVelocity = body.linearVelocity;
            previousLocalAngularVelocity = transform.InverseTransformDirection(body.angularVelocity);
            previousTargetLocalRate = Vector3.zero;
            trajectoryGenerator.Reset(body.linearVelocity, targetYawDegrees);
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
            externalMassProvider = externalMassProviderSource as IDroneExternalMassProvider;
            ApplyBodySettings();
            if (!DroneRotorActuatorRuntime.TryOrder(
                    GetComponentsInChildren<DroneRotor>(true),
                    orderedRotors,
                    out var rotorError))
            {
                Debug.LogError($"[DroneFlight] {rotorError}", this);
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
            horizontalVelocityXController = new DronePidController(config.CreateHorizontalVelocitySettings());
            horizontalVelocityZController = new DronePidController(config.CreateHorizontalVelocitySettings());

            if (!TryCreateControlAllocator())
            {
                Debug.LogError("[DroneFlight] Rotor 几何无法建立物理控制分配矩阵。", this);
                return false;
            }

            hoverCommand = CalculateHoverCommand();
            if (!float.IsFinite(hoverCommand) || hoverCommand >= 1f)
            {
                Debug.LogError("[DroneFlight] 最大总推力不足以抵消机体重量，已停止飞行施力。", this);
                return false;
            }

            targetYawDegrees = body.rotation.eulerAngles.y;
            targetHeightMeters = body.position.y;
            targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
            previousVelocity = body.linearVelocity;
            previousLocalAngularVelocity = transform.InverseTransformDirection(body.angularVelocity);
            trajectoryGenerator.Reset(body.linearVelocity, targetYawDegrees);
            return true;
        }

        private DroneStateSnapshot CaptureState(float deltaTime)
        {
            var velocity = body.linearVelocity;
            var localAngularVelocity = transform.InverseTransformDirection(body.angularVelocity);
            var rawAcceleration = (velocity - previousVelocity) / deltaTime;
            var rawLocalAngularAcceleration = (localAngularVelocity - previousLocalAngularVelocity) / deltaTime;
            var filter = 1f - Mathf.Exp(
                -2f * Mathf.PI * Mathf.Clamp(config.StateDerivativeFilterHz, 0.1f, 10f) * deltaTime);
            filteredAcceleration = Vector3.Lerp(filteredAcceleration, rawAcceleration, filter);
            filteredLocalAngularAcceleration = Vector3.Lerp(
                filteredLocalAngularAcceleration,
                rawLocalAngularAcceleration,
                filter);
            previousVelocity = velocity;
            previousLocalAngularVelocity = localAngularVelocity;
            return new DroneStateSnapshot(
                body.position,
                velocity,
                filteredAcceleration,
                body.rotation,
                localAngularVelocity,
                filteredLocalAngularAcceleration);
        }

        private Vector3 BuildPositionAwareVelocity(
            DroneTrajectorySetpoint trajectory,
            DroneResponseProfileParameters profile)
        {
            var horizontalInputActive = controlInput.Forward * controlInput.Forward
                                        + controlInput.Right * controlInput.Right > 0.0001f;
            var shapedHorizontalMoving = new Vector2(
                trajectory.WorldVelocity.x,
                trajectory.WorldVelocity.z).sqrMagnitude > 0.0025f;
            Vector2 horizontalVelocity;
            if (horizontalInputActive || shapedHorizontalMoving)
            {
                targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
                hadHorizontalInput = true;
                horizontalVelocity = new Vector2(
                    trajectory.WorldVelocity.x,
                    trajectory.WorldVelocity.z);
            }
            else
            {
                if (hadHorizontalInput)
                {
                    targetHorizontalPosition = new Vector2(body.position.x, body.position.z);
                    hadHorizontalInput = false;
                }

                var positionError = targetHorizontalPosition - new Vector2(body.position.x, body.position.z);
                horizontalVelocity = Vector2.ClampMagnitude(
                    positionError * config.HorizontalPositionGain,
                    profile.MaximumHorizontalSpeed);
            }

            float verticalVelocity;
            var automaticAltitude = OperationState is DroneFlightOperationState.TakingOff
                or DroneFlightOperationState.Landing;
            if (!automaticAltitude
                && (Mathf.Abs(controlInput.Lift) > 0.01f || Mathf.Abs(trajectory.WorldVelocity.y) > 0.05f))
            {
                targetHeightMeters = body.position.y;
                hadVerticalInput = true;
                verticalVelocity = trajectory.WorldVelocity.y;
            }
            else
            {
                if (hadVerticalInput)
                {
                    targetHeightMeters = body.position.y;
                    hadVerticalInput = false;
                }

                verticalVelocity = Mathf.Clamp(
                    (targetHeightMeters - body.position.y) * config.AltitudeGain,
                    -profile.MaximumVerticalSpeed,
                    profile.MaximumVerticalSpeed);
            }

            return new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.y);
        }

        private QuadrotorMotorOutput BuildMotorOutput(DroneAllocationResult allocation)
        {
            var thrust = allocation.RotorThrustNewtons;
            return new QuadrotorMotorOutput(
                motors[0].CommandForThrust(thrust.x),
                motors[1].CommandForThrust(thrust.y),
                motors[2].CommandForThrust(thrust.z),
                motors[3].CommandForThrust(thrust.w),
                allocation.RollPitchScale,
                allocation.Saturation.IsSaturated);
        }

        private bool TryCreateControlAllocator()
        {
            var positions = new Vector3[4];
            var forceDirections = new Vector3[4];
            var directions = new DroneRotorDirection[4];
            for (var index = 0; index < orderedRotors.Length; index++)
            {
                if (orderedRotors[index] == null)
                {
                    return false;
                }

                positions[index] = transform.InverseTransformPoint(orderedRotors[index].ForceTransform.position)
                                   - body.centerOfMass;
                forceDirections[index] = transform.InverseTransformDirection(
                    orderedRotors[index].ForceDirection);
                directions[index] = orderedRotors[index].Direction;
            }

            var maximumRotorThrust = config.ThrustCoefficient * config.MaximumRpm * config.MaximumRpm;
            controlAllocator = new QuadrotorControlAllocator(
                positions,
                forceDirections,
                directions,
                config.ReactionTorqueCoefficient,
                maximumRotorThrust);
            return controlAllocator.IsValid;
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

            (externalMassProviderSource as IDroneExternalMassSynchronizer)?.SynchronizeExternalMass();
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

            rollRateController?.UpdateSettings(config.CreateRollRateSettings(), preserveOutput: true);
            pitchRateController?.UpdateSettings(config.CreatePitchRateSettings(), preserveOutput: true);
            yawRateController?.UpdateSettings(config.CreateYawRateSettings(), preserveOutput: true);
            verticalSpeedController?.UpdateSettings(config.CreateVerticalSpeedSettings(), preserveOutput: true);
            horizontalVelocityXController?.UpdateSettings(
                config.CreateHorizontalVelocitySettings(),
                preserveOutput: true);
            horizontalVelocityZController?.UpdateSettings(
                config.CreateHorizontalVelocitySettings(),
                preserveOutput: true);
            TryCreateControlAllocator();

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
            body.linearDamping = config.BodyLinearDamping;
            body.angularDamping = config.BodyAngularDamping;
            RefreshMassDistribution();
        }

        /// <summary>从机体扣除已由子刚体求解的整机内含质量，保持空载总质量不变。</summary>
        internal void RefreshMassDistribution()
        {
            if (body == null || config == null)
            {
                return;
            }

            var integratedDynamicMass = externalMassProvider != null
                ? Mathf.Max(0f, externalMassProvider.IntegratedDynamicMassKilograms)
                : 0f;
            var targetMass = config.BodyMassKilograms - integratedDynamicMass;
            if (!float.IsFinite(targetMass) || targetMass <= 0f)
            {
                return;
            }

            if (!Mathf.Approximately(body.mass, targetMass))
            {
                body.mass = targetMass;
                body.ResetInertiaTensor();
            }
        }

        private int CalculateRuntimeTuningSignature()
        {
            return JsonUtility.ToJson(config).GetHashCode();
        }

        private void OnDestroy()
        {
            if (config != null && config != sourceConfig)
            {
                Destroy(config);
            }
        }

        internal bool TryGetRotorDebugVector(int index, out Vector3 origin, out Vector3 thrustForce)
        {
            return DroneRotorActuatorRuntime.TryGetDebugVector(
                orderedRotors,
                motorStates,
                index,
                out origin,
                out thrustForce);
        }

        internal Vector3 CurrentTotalThrustVector
        {
            get
            {
                return DroneRotorActuatorRuntime.SumThrust(orderedRotors, motorStates);
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
                Gizmos.DrawLine(origin, origin + rotor.ForceDirection * motorStates[index].ThrustNewtons * 0.08f);
            }
        }
    }
}
