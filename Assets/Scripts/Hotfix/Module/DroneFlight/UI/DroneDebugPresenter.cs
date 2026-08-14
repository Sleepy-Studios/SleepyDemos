using TMPro;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>F3 面板的中文确定性文本格式化器。</summary>
    internal static class DroneDebugFormatter
    {
        internal static string Format(
            QuadrotorMotorOutput motors,
            DronePidTelemetry roll,
            DronePidTelemetry pitch,
            DronePidTelemetry yaw,
            float totalThrust,
            float fixedDeltaTime,
            DroneLandingGearState gear,
            DroneWinchState winch,
            DroneGrappleState grapple,
            int contacts,
            DronePowerConfigurationMode powerMode = DronePowerConfigurationMode.AutomaticPayloadTuning,
            float ratedPayload = 0f,
            float currentPayload = 0f,
            float maximumPayload = 0f,
            float bodyMass = 0f,
            float hardwareMass = 0f,
            float supportedPayloadMass = 0f,
            float supportedMass = 0f,
            float ratedHoverCommand = 0f,
            float theoreticalHoverCommand = 0f,
            float averageMotorCommand = 0f,
            float powerReserve = 0f,
            DronePayloadOperatingZone payloadZone = DronePayloadOperatingZone.Rated,
            Vector3 targetVelocity = default,
            Vector3 actualVelocity = default,
            DronePidTelemetry velocityX = default,
            DronePidTelemetry velocityY = default,
            DronePidTelemetry velocityZ = default,
            Vector3 targetAcceleration = default,
            Vector3 targetLocalRate = default,
            Vector3 actualLocalRate = default,
            Vector3 targetLocalTorque = default,
            Vector3 realizedLocalTorque = default,
            DroneControlSaturation saturation = default,
            float yawScale = 1f,
            Vector3 antiSwingCorrection = default,
            Vector3 targetWorldForce = default,
            float residualThrust = 0f,
            Vector3 residualTorque = default,
            DronePayloadSupportState payloadSupportState = DronePayloadSupportState.None,
            int payloadSupportContacts = 0,
            DroneSuspensionJointTelemetry suspensionJoints = default,
            float installedHardwareMass = 0f,
            float payloadSupportForce = 0f,
            float payloadGripForce = 0f,
            float payloadSupportedFraction = 0f,
            float gripTakeupProgress = 0f,
            Vector3 gripContactCenter = default,
            float suspensionLengthMeters = 0f)
        {
            var payloadRatio = ratedPayload > 0f ? currentPayload / ratedPayload : 0f;
            return $"电机 左前(FL) {motors.FrontLeft:F3}  右前(FR) {motors.FrontRight:F3}\n"
                   + $"电机 左后(RL) {motors.RearLeft:F3}  右后(RR) {motors.RearRight:F3}\n"
                   + FormatAxis("横滚(Roll)", roll) + "\n"
                   + FormatAxis("俯仰(Pitch)", pitch) + "\n"
                   + FormatAxis("偏航(Yaw)", yaw) + "\n"
                   + $"速度目标/实际 X {targetVelocity.x:F2}/{actualVelocity.x:F2}  Y {targetVelocity.y:F2}/{actualVelocity.y:F2}  Z {targetVelocity.z:F2}/{actualVelocity.z:F2} m/s\n"
                   + FormatAxis("速度X", velocityX) + "\n"
                   + FormatAxis("速度Y", velocityY) + "\n"
                   + FormatAxis("速度Z", velocityZ) + "\n"
                   + $"目标加速度 {FormatVector(targetAcceleration)} m/s²\n"
                   + $"目标推力 {targetWorldForce.magnitude:F2} N  方向 {FormatVector(targetWorldForce.sqrMagnitude > 0.000001f ? targetWorldForce.normalized : Vector3.zero)}\n"
                   + $"角速度目标/实际 {FormatVector(targetLocalRate)} / {FormatVector(actualLocalRate)} rad/s\n"
                   + $"力矩目标/可实现 {FormatVector(targetLocalTorque)} / {FormatVector(realizedLocalTorque)} N·m\n"
                   + $"分配残差 推力 {residualThrust:F2} N  力矩 {FormatVector(residualTorque)} N·m\n"
                   + $"饱和 推力{FormatSaturation(saturation.Thrust)} 俯仰{FormatSaturation(saturation.Pitch)} 偏航{FormatSaturation(saturation.Yaw)} 横滚{FormatSaturation(saturation.Roll)}  偏航保留 {yawScale:P0}\n"
                   + $"防摆修正 {FormatVector(antiSwingCorrection)} m/s²  主动防摆 {(antiSwingCorrection.sqrMagnitude > 0.000001f ? "介入" : "待机")}\n"
                   + $"姿态缩放 {motors.AttitudeScale:F3}  总推力 {totalThrust:F2} N\n"
                   + $"物理步长 {fixedDeltaTime:F3} s\n"
                   + $"动力模式 {FormatPowerMode(powerMode)}  额定载重 {ratedPayload:F2} kg\n"
                   + $"当前真实载荷 {currentPayload:F2} kg  飞控承载载荷 {supportedPayloadMass:F2} kg  载荷占额定 {payloadRatio:P0}\n"
                   + $"载荷支撑 {FormatPayloadSupport(payloadSupportState)}  外部有效支撑 {payloadSupportContacts}  承载比例 {payloadSupportedFraction:P0}\n"
                   + $"地面支持力 {payloadSupportForce:F1} N  抓取竖直力 {payloadGripForce:F1} N  软约束接入 {gripTakeupProgress:P0}\n"
                   + $"最大允许载荷 {maximumPayload:F2} kg\n"
                   + $"整机恒定总质量 {bodyMass + hardwareMass:F2} kg  主刚体质量 {bodyMass:F2} kg  机载抓斗设备 {installedHardwareMass:F2} kg\n"
                   + $"当前悬挂设备 {hardwareMass:F2} kg  当前受支持总质量 {supportedMass:F2} kg\n"
                   + $"满载动力占用 {ratedHoverCommand:P0}  理论悬停指令 {theoreticalHoverCommand:P0}\n"
                   + $"实际平均电机指令 {averageMotorCommand:P0}  当前动力余量 {powerReserve:P0}  载重区域 {FormatPayloadZone(payloadZone)}\n"
                   + $"起落架 {FormatGear(gear)}  卷扬 {FormatWinch(winch)}\n"
                   + $"单摆吊索 {(suspensionJoints.IsCableTaut ? "绷紧" : "收纳")}  长度 {suspensionLengthMeters:F2} m  扭转 {suspensionJoints.TwistDegrees:F1}/±{suspensionJoints.TwistLimitDegrees:F0}°  摆角 {suspensionJoints.SwingDegrees:F1}/{suspensionJoints.SwingLimitDegrees:F0}°\n"
                   + $"摆速 {suspensionJoints.SwingRateDegreesPerSecond:F1}°/s  被动阻尼扭矩 {suspensionJoints.PassiveDampingTorqueNewtonMeters:F2} N·m\n"
                   + $"六爪 {FormatGrapple(grapple)}  有效接触 {contacts}/3  接触质心 {FormatVector(gripContactCenter)}";
        }

        private static string FormatPowerMode(DronePowerConfigurationMode mode)
        {
            return mode == DronePowerConfigurationMode.ManualPhysics ? "手动物理参数" : "自动载重调校";
        }

        private static string FormatPayloadZone(DronePayloadOperatingZone zone)
        {
            return zone switch
            {
                DronePayloadOperatingZone.AboveRated => "超额区",
                DronePayloadOperatingZone.OverloadRejected => "超载拒绝区",
                _ => "额定区"
            };
        }

        private static string FormatPayloadSupport(DronePayloadSupportState state)
        {
            return state switch
            {
                DronePayloadSupportState.GroundSupported => "地面支撑",
                DronePayloadSupportState.TakingLoad => "离地接管",
                DronePayloadSupportState.AirborneSupported => "空中承载",
                DronePayloadSupportState.Unloading => "落地卸载",
                _ => "未抓取"
            };
        }

        private static string FormatAxis(string name, DronePidTelemetry telemetry)
        {
            return $"{name} 误差 {telemetry.Error:F3}  比例P {telemetry.Proportional:F3}  "
                   + $"积分I {telemetry.Integral:F3}  微分D {telemetry.Derivative:F3}  前馈 {telemetry.FeedForward:F3}";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        private static string FormatSaturation(DroneSaturationDirection direction)
        {
            return direction switch
            {
                DroneSaturationDirection.Positive => "+",
                DroneSaturationDirection.Negative => "-",
                DroneSaturationDirection.Both => "±",
                _ => "无"
            };
        }

        private static string FormatGear(DroneLandingGearState state)
        {
            return state switch
            {
                DroneLandingGearState.Deploying => "放下中",
                DroneLandingGearState.Retracted => "已收起",
                DroneLandingGearState.Retracting => "收起中",
                _ => "已放下"
            };
        }

        private static string FormatWinch(DroneWinchState state)
        {
            return state switch
            {
                DroneWinchState.Deploying => "放出中",
                DroneWinchState.Deployed => "已放出",
                DroneWinchState.Retracting => "收回中",
                DroneWinchState.Carrying => "运输高度",
                _ => "已收纳"
            };
        }

        private static string FormatGrapple(DroneGrappleState state)
        {
            return state switch
            {
                DroneGrappleState.Closing => "闭合中",
                DroneGrappleState.Contacting => "接触中",
                DroneGrappleState.AssistedGrip => "抓取中",
                DroneGrappleState.Releasing => "释放中",
                DroneGrappleState.Broken => "已脱落",
                _ => "已张开"
            };
        }
    }

    /// <summary>在 Tip 层右下角刷新飞控诊断，不承担开关和导航生命周期。</summary>
    public sealed class DroneDebugPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text debugText;
        [SerializeField] private float refreshIntervalSeconds = 0.1f;

        private DroneFlightController flightController;
        private DroneLandingGearController landingGear;
        private DroneWinchController winch;
        private DroneMechanicalHook grapple;
        private PayloadMount payloadMount;
        private DroneCameraRig cameraRig;
        private float nextRefreshTime;

        private void Update()
        {
            if (flightController == null || debugText == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
            var motors = flightController.LastMotorOutput;
            var roll = flightController.RollRateTelemetry;
            var pitch = flightController.PitchRateTelemetry;
            var yaw = flightController.YawRateTelemetry;
            debugText.text = DroneDebugFormatter.Format(
                motors,
                roll,
                pitch,
                yaw,
                flightController.LastTotalThrustNewtons,
                Time.fixedDeltaTime,
                landingGear != null ? landingGear.State : DroneLandingGearState.Deployed,
                winch != null ? winch.State : DroneWinchState.Stowed,
                grapple != null ? grapple.State : DroneGrappleState.Open,
                grapple != null ? grapple.CurrentContactCount : 0,
                flightController.Config.PowerConfigurationMode,
                flightController.Config.RatedPayloadKilograms,
                flightController.CurrentPayloadMassKilograms,
                flightController.Config.MaximumPayloadMassKilograms,
                flightController.Body.mass,
                flightController.CurrentHardwareMassKilograms,
                flightController.CurrentSupportedPayloadMassKilograms,
                flightController.CurrentSupportedMassKilograms,
                flightController.Config.RatedPayloadHoverCommand,
                flightController.CurrentHoverCommand,
                flightController.CurrentAverageMotorCommand,
                flightController.CurrentPowerReserve,
                flightController.CurrentPayloadZone,
                flightController.LastDesiredWorldVelocity,
                flightController.Body.linearVelocity,
                flightController.HorizontalVelocityXTelemetry,
                flightController.VerticalSpeedTelemetry,
                flightController.HorizontalVelocityZTelemetry,
                flightController.LastDesiredWorldAcceleration,
                flightController.LastTargetLocalRate,
                flightController.LastActualLocalRate,
                flightController.LastDesiredLocalTorque,
                flightController.LastRealizedLocalTorque,
                flightController.LastAllocation.Saturation,
                flightController.LastAllocation.YawScale,
                flightController.LastAntiSwingCorrection,
                flightController.LastDesiredWorldForce,
                flightController.LastAllocation.ResidualThrustNewtons,
                flightController.LastAllocation.ResidualTorqueNewtonMeters,
                winch != null ? winch.PayloadSupportState : DronePayloadSupportState.None,
                winch != null ? winch.PayloadSupportContactCount : 0,
                winch != null ? winch.JointTelemetry : default,
                flightController.CurrentInstalledHardwareMassKilograms,
                winch != null ? winch.PayloadUpwardSupportForceNewtons : 0f,
                winch != null ? winch.PayloadGripVerticalForceNewtons : 0f,
                winch != null ? winch.PayloadSupportedFraction : 0f,
                gripTakeupProgress: payloadMount != null ? payloadMount.TakeupProgress : 0f,
                gripContactCenter: payloadMount != null ? payloadMount.GripWorldContactCenter : Vector3.zero,
                suspensionLengthMeters: winch != null ? winch.CurrentLengthMeters : 0f);
        }

        internal void BindContext(DroneFlightSceneContext context)
        {
            flightController = context != null ? context.FlightController : null;
            landingGear = context != null ? context.LandingGear : null;
            winch = context != null ? context.Winch : null;
            grapple = context != null ? context.Grapple : null;
            payloadMount = context != null ? context.PayloadMount : null;
            cameraRig = context != null ? context.CameraRig : null;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || flightController == null || flightController.Body == null)
            {
                return;
            }

            var outputCamera = cameraRig != null ? cameraRig.OutputCamera : Camera.main;
            if (outputCamera == null)
            {
                return;
            }

            var rotorNames = new[] { "左前 (FL)", "右前 (FR)", "左后 (RL)", "右后 (RR)" };
            for (var index = 0; index < 4; index++)
            {
                if (flightController.TryGetRotorDebugVector(index, out var origin, out var thrust))
                {
                    DrawWorldVector(outputCamera, origin, thrust * 0.08f, Color.cyan,
                        $"{rotorNames[index]} {thrust.magnitude:F1} N");
                }
            }

            var center = flightController.Body.worldCenterOfMass;
            var totalThrust = flightController.CurrentTotalThrustVector;
            var gravityForce = Physics.gravity * flightController.CurrentSupportedMassKilograms;
            DrawWorldVector(outputCamera, center, totalThrust * 0.06f, Color.yellow,
                $"总升力 {totalThrust.magnitude:F1} N");
            DrawWorldVector(outputCamera, center, gravityForce * 0.06f, new Color(1f, 0.25f, 0.2f),
                $"重力 {gravityForce.magnitude:F1} N");
            DrawWorldVector(outputCamera, center, flightController.Body.linearVelocity * 0.35f, Color.green,
                $"实际速度 {flightController.Body.linearVelocity.magnitude:F1} m/s");
            DrawWorldVector(outputCamera, center, flightController.LastDesiredWorldVelocity * 0.35f,
                new Color(0.2f, 0.65f, 1f), $"目标速度 {flightController.LastDesiredWorldVelocity.magnitude:F1} m/s");
            DrawWorldVector(outputCamera, center, flightController.LastDesiredWorldAcceleration * 0.25f,
                new Color(1f, 0.35f, 1f), $"目标加速度 {flightController.LastDesiredWorldAcceleration.magnitude:F1} m/s²");
            DrawWorldVector(outputCamera, center, flightController.LastDesiredWorldForce * 0.06f,
                new Color(1f, 0.55f, 0.1f), $"目标推力 {flightController.LastDesiredWorldForce.magnitude:F1} N");
            var realizedForce = flightController.transform.TransformDirection(
                flightController.LastAllocation.RealizedForceBodyNewtons);
            DrawWorldVector(outputCamera, center, realizedForce * 0.06f,
                new Color(0.25f, 1f, 0.75f), $"可实现合力 {realizedForce.magnitude:F1} N");
            var targetTorqueWorld = flightController.transform.TransformDirection(flightController.LastDesiredLocalTorque);
            DrawWorldVector(outputCamera, center, targetTorqueWorld * 0.8f,
                new Color(1f, 0.2f, 0.65f), $"目标力矩 {flightController.LastDesiredLocalTorque.magnitude:F2} N·m");

            var legendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            GUI.Label(
                new Rect(12f, Screen.height - 112f, 760f, 100f),
                "F3 动力矢量：青=单旋翼升力  黄=实际总升力  红=重力\n绿=实际速度  蓝=目标速度  紫=目标加速度  橙=目标推力  青绿=可实现合力  粉=目标力矩",
                legendStyle);
        }

        private static void DrawWorldVector(
            Camera camera,
            Vector3 worldOrigin,
            Vector3 worldVector,
            Color color,
            string label)
        {
            if (worldVector.sqrMagnitude < 0.000001f)
            {
                return;
            }

            var startWorld = camera.WorldToScreenPoint(worldOrigin);
            var endWorld = camera.WorldToScreenPoint(worldOrigin + worldVector);
            if (startWorld.z <= 0f || endWorld.z <= 0f)
            {
                return;
            }

            var start = new Vector2(startWorld.x, Screen.height - startWorld.y);
            var end = new Vector2(endWorld.x, Screen.height - endWorld.y);
            DrawScreenLine(start, end, color, 3f);
            var direction = (end - start).normalized;
            if (direction.sqrMagnitude > 0f)
            {
                var perpendicular = new Vector2(-direction.y, direction.x);
                DrawScreenLine(end, end - direction * 10f + perpendicular * 5f, color, 3f);
                DrawScreenLine(end, end - direction * 10f - perpendicular * 5f, color, 3f);
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(end.x + 5f, end.y - 10f, 220f, 22f), label, style);
        }

        private static void DrawScreenLine(Vector2 start, Vector2 end, Color color, float width)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
#endif
    }
}
