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
        [SerializeField] private float automaticTakeoffHeightMeters = 1.5f;
        [SerializeField] private float landingDescentSpeedMetersPerSecond = 0.5f;

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
        private bool initialized;
        private Vector3 lastTargetLocalRate;
        private Vector3 lastActualLocalRate;

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

        /// <summary>最近一个固定步的目标机体角速度，单位 rad/s。</summary>
        internal Vector3 LastTargetLocalRate => lastTargetLocalRate;

        /// <summary>最近一个固定步的实际机体角速度，单位 rad/s。</summary>
        internal Vector3 LastActualLocalRate => lastActualLocalRate;

        internal DronePidTelemetry RollRateTelemetry => rollRateController?.Telemetry ?? default;

        internal DronePidTelemetry PitchRateTelemetry => pitchRateController?.Telemetry ?? default;

        internal DronePidTelemetry YawRateTelemetry => yawRateController?.Telemetry ?? default;

        internal DronePidTelemetry VerticalSpeedTelemetry => verticalSpeedController?.Telemetry ?? default;

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
            if (!initialized || !IsArmed)
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

            targetYawDegrees += controlInput.Yaw * profile.MaximumYawSpeedDegrees * deltaTime;
            CalculateHorizontalAttitudeTargets(out var targetPitch, out var targetRoll);
            var targetAttitude = Quaternion.Euler(targetPitch, targetYawDegrees, targetRoll);
            var targetLocalRate = DroneAttitudeMath.CalculateTargetRate(
                body.rotation,
                targetAttitude,
                config.AttitudeGain,
                config.MaximumRateRadiansPerSecond);
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
            var tiltCompensatedHoverCommand = hoverCommand / Mathf.Sqrt(verticalThrustRatio);
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
            targetHeightMeters = Mathf.Max(automaticTakeoffHeightMeters, body.position.y + 0.5f);
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
            for (var index = 0; index < motors.Length; index++)
            {
                motors[index].Reset();
                motorStates[index] = default;
            }
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

            body = GetComponent<Rigidbody>();
            body.mass = config.BodyMassKilograms;
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

            rollRateController = new DronePidController(config.CreateRollRateSettings());
            pitchRateController = new DronePidController(config.CreatePitchRateSettings());
            yawRateController = new DronePidController(config.CreateYawRateSettings());
            verticalSpeedController = new DronePidController(config.CreateVerticalSpeedSettings());

            var hoverRpm = Mathf.Sqrt(body.mass * -Physics.gravity.y / (4f * config.ThrustCoefficient));
            hoverCommand = hoverRpm / config.MaximumRpm;
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

        private void CalculateHorizontalAttitudeTargets(out float targetPitch, out float targetRoll)
        {
            var horizontalInput = new Vector2(controlInput.Right, controlInput.Forward);
            var profile = config.GetProfile(responseProfile);
            Vector3 desiredWorldVelocity;
            if (horizontalInput.sqrMagnitude > 0.0001f)
            {
                var yawRotation = Quaternion.Euler(0f, targetYawDegrees, 0f);
                desiredWorldVelocity = yawRotation
                    * new Vector3(horizontalInput.x, 0f, horizontalInput.y)
                    * profile.MaximumHorizontalSpeed;
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
            var yawLocalAcceleration = Quaternion.Inverse(Quaternion.Euler(0f, targetYawDegrees, 0f))
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
                targetHeightMeters -= Mathf.Max(0.1f, landingDescentSpeedMetersPerSecond) * deltaTime;
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

                if (rotor.VisualPropeller != null)
                {
                    var degrees = state.Rpm * 6f * deltaTime * (float)rotor.Direction;
                    rotor.VisualPropeller.Rotate(0f, degrees, 0f, Space.Self);
                }
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
