using System.Collections.Generic;
using Hotfix.DroneFlight;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>DroneFlightConfig 的本机双语 Inspector，不改变配置资产内容。</summary>
    [CustomEditor(typeof(DroneFlightConfig))]
    public sealed class DroneFlightConfigEditor : UnityEditor.Editor
    {
        private const string LanguagePreferenceKey = "SleepyDemos.DroneFlight.ConfigInspector.Chinese";
        private const string PagePreferenceKey = "SleepyDemos.DroneFlight.ConfigInspector.Page";

        internal static readonly IReadOnlyDictionary<string, DroneInspectorLabel> SerializedFieldLabels =
            new Dictionary<string, DroneInspectorLabel>
        {
            ["powerConfigurationMode"] = new("动力配置模式", "Power Configuration Mode", "自动载重调校会派生机体质量、推力系数与电机响应时间；手动模式使用真实物理字段。", "Automatic tuning derives body mass, thrust coefficient and motor response time; Manual uses raw physics fields."),
            ["ratedPayloadKilograms"] = new("额定载重 (kg)", "Rated Payload (kg)", "无人机可以长期稳定运输的标准载荷。接近该重量时会明显减少剩余动力。", "Standard payload for continuous transport. Power reserve becomes limited near this mass."),
            ["bodyMassMultiplier"] = new("机体质量倍率", "Body Mass Multiplier", "自动机体质量相对于额定载重的倍率。", "Automatic body mass relative to rated payload."),
            ["maximumPayloadMultiplier"] = new("最大载荷倍率", "Maximum Payload Multiplier", "最大允许载荷相对于额定载重的倍率。超过后抓斗拒绝建立抓取约束。", "Maximum allowed payload relative to rated payload. Heavier objects are rejected."),
            ["ratedPayloadHoverCommand"] = new("满载动力占用", "Rated Hover Command", "挂载额定重量悬停时的电机指令比例。越高表示满载越吃力。", "Motor command used to hover at rated payload. Higher values mean less reserve."),
            ["motorResponsiveness"] = new("电机响应速度", "Motor Responsiveness", "数值越高，操作和刹停越灵敏，释放载荷后的转速残留时间越短。", "Higher values improve response and braking and shorten residual RPM after release."),
            ["bodyMassKilograms"] = new("机体质量 (kg)", "Body Mass (kg)", "无人机裸机 Rigidbody 质量。", "Bare airframe Rigidbody mass."),
            ["bodyLinearDamping"] = new("机体线性阻尼", "Body Linear Damping", "无人机主刚体线性阻尼。", "Main Rigidbody linear damping."),
            ["bodyAngularDamping"] = new("机体角阻尼", "Body Angular Damping", "无人机主刚体角阻尼。", "Main Rigidbody angular damping."),
            ["motorResponseTimeSeconds"] = new("电机响应时间 (s)", "Motor Response Time (s)", "电机一阶响应时间常数。", "First-order motor response time constant."),
            ["maximumRpm"] = new("最大转速 (rpm)", "Maximum RPM", "归一化满量程对应转速。", "RPM at normalized full command."),
            ["thrustCoefficient"] = new("推力系数", "Thrust Coefficient", "公式 T = k × rpm² 中的 k。", "Coefficient k in T = k × rpm²."),
            ["reactionTorqueCoefficient"] = new("反扭矩系数", "Reaction Torque Coefficient", "公式 Q = T × coefficient。", "Coefficient in Q = T × coefficient."),
            ["attitudeGain"] = new("姿态增益", "Attitude Gain", "姿态误差到目标角速度。", "Attitude error to target angular rate."),
            ["yawAttitudeGain"] = new("偏航姿态增益", "Yaw Attitude Gain", "偏航误差到目标角速度。", "Yaw error to target angular rate."),
            ["yawWeight"] = new("偏航权重", "Yaw Weight", "推力方向优先时保留的偏航控制权重。", "Yaw authority retained while prioritizing thrust direction."),
            ["rateFeedForward"] = new("角速度前馈", "Rate Feed Forward", "目标角加速度直接进入 Rate 控制的比例。", "Target angular-acceleration feed-forward scale."),
            ["stateDerivativeFilterHz"] = new("状态导数滤波 (Hz)", "State Derivative Filter (Hz)", "实际加速度和角加速度的低通截止频率。", "Acceleration derivative low-pass cutoff."),
            ["maximumRateRadiansPerSecond"] = new("最大角速度 (rad/s)", "Maximum Rate (rad/s)", "内环目标角速度限幅。", "Inner-loop target rate limit."),
            ["rollRate"] = new("横滚 Rate PID", "Roll Rate PID", "横滚角速度内环。", "Roll angular-rate inner loop."),
            ["pitchRate"] = new("俯仰 Rate PID", "Pitch Rate PID", "俯仰角速度内环。", "Pitch angular-rate inner loop."),
            ["yawRate"] = new("偏航 Rate PID", "Yaw Rate PID", "偏航角速度内环。", "Yaw angular-rate inner loop."),
            ["proportionalGain"] = new("比例增益 P", "Proportional Gain P", "当前 PID 轴的比例增益。", "Proportional gain of this PID axis."),
            ["integralGain"] = new("积分增益 I", "Integral Gain I", "当前 PID 轴的积分增益。", "Integral gain of this PID axis."),
            ["derivativeGain"] = new("微分增益 D", "Derivative Gain D", "当前 PID 轴的微分增益。", "Derivative gain of this PID axis."),
            ["outputLimit"] = new("输出限制", "Output Limit", "当前 PID 轴的输出绝对值上限。", "Absolute output limit of this PID axis."),
            ["integralLimit"] = new("积分限制", "Integral Limit", "当前 PID 轴的积分状态上限。", "Integral-state limit of this PID axis."),
            ["derivativeFilterHz"] = new("微分滤波 (Hz)", "Derivative Filter (Hz)", "当前 PID 轴微分低通截止频率。", "Derivative low-pass cutoff of this PID axis."),
            ["altitudeGain"] = new("高度增益", "Altitude Gain", "高度误差到目标垂直速度。", "Altitude error to target vertical speed."),
            ["verticalSpeedProportionalGain"] = new("垂直速度 P", "Vertical Speed P", "垂直速度比例增益。", "Vertical-speed proportional gain."),
            ["verticalSpeedIntegralGain"] = new("垂直速度 I", "Vertical Speed I", "垂直速度积分增益。", "Vertical-speed integral gain."),
            ["verticalSpeedDerivativeGain"] = new("垂直速度 D", "Vertical Speed D", "垂直速度微分增益。", "Vertical-speed derivative gain."),
            ["verticalSpeedOutputLimit"] = new("垂直输出限制", "Vertical Output Limit", "总推力归一化修正限幅。", "Normalized collective correction limit."),
            ["verticalSpeedIntegralLimit"] = new("垂直积分限制", "Vertical Integral Limit", "积分状态限幅。", "Integral state limit."),
            ["verticalSpeedDerivativeFilterHz"] = new("垂直 D 滤波 (Hz)", "Vertical D Filter (Hz)", "微分低通截止频率。", "Derivative low-pass cutoff."),
            ["horizontalPositionGain"] = new("水平位置增益", "Horizontal Position Gain", "位置误差到目标速度。", "Position error to desired velocity."),
            ["horizontalVelocityGain"] = new("水平速度增益", "Horizontal Velocity Gain", "速度误差到目标加速度。", "Velocity error to desired acceleration."),
            ["horizontalVelocityIntegralGain"] = new("水平速度积分 I", "Horizontal Velocity I", "消除持续水平速度误差。", "Removes persistent horizontal velocity error."),
            ["horizontalVelocityDerivativeGain"] = new("水平速度微分 D", "Horizontal Velocity D", "根据实际加速度抑制超调。", "Damps overshoot using measured acceleration."),
            ["horizontalVelocityOutputLimit"] = new("水平速度输出限制", "Horizontal Velocity Output Limit", "单轴目标加速度上限，单位 m/s²。", "Per-axis acceleration output limit in m/s²."),
            ["horizontalVelocityIntegralLimit"] = new("水平速度积分限制", "Horizontal Velocity Integral Limit", "水平速度积分状态上限。", "Horizontal velocity integral-state limit."),
            ["horizontalVelocityDerivativeFilterHz"] = new("水平速度 D 滤波 (Hz)", "Horizontal Velocity D Filter (Hz)", "水平速度微分低通截止频率。", "Horizontal velocity derivative cutoff."),
            ["cineProfile"] = new("平稳档 (Cine)", "Cine Profile", "低速、低倾角、柔和输入。", "Slow, low-tilt and smooth response."),
            ["normalProfile"] = new("普通档 (Normal)", "Normal Profile", "默认综合响应。", "Default balanced response."),
            ["sportProfile"] = new("运动档 (Sport)", "Sport Profile", "高速、高倾角和快速响应。", "Fast, high-tilt response."),
            ["maximumHorizontalSpeed"] = new("最大水平速度", "Maximum Horizontal Speed", "该档位水平速度上限。", "Horizontal speed limit for this profile."),
            ["maximumHorizontalAcceleration"] = new("最大水平加速度", "Maximum Horizontal Acceleration", "该档位水平加速度上限。", "Horizontal acceleration limit for this profile."),
            ["maximumTiltDegrees"] = new("最大倾角 (°)", "Maximum Tilt (°)", "该档位目标机体倾角上限。", "Target airframe tilt limit for this profile."),
            ["maximumVerticalSpeed"] = new("最大垂直速度", "Maximum Vertical Speed", "该档位升降速度上限。", "Vertical speed limit for this profile."),
            ["maximumYawSpeedDegrees"] = new("最大偏航速度 (°/s)", "Maximum Yaw Speed (°/s)", "该档位偏航速度上限。", "Yaw speed limit for this profile."),
            ["inputRiseRate"] = new("输入响应速率", "Input Rise Rate", "该档位输入从零到目标的响应速率。", "Input slew rate for this profile."),
            ["maximumHorizontalJerk"] = new("最大水平加加速度 (m/s³)", "Maximum Horizontal Jerk", "水平加速度每秒允许变化的上限。", "Horizontal acceleration change limit."),
            ["maximumVerticalAcceleration"] = new("最大垂直加速度 (m/s²)", "Maximum Vertical Acceleration", "升降速度控制的加速度上限。", "Vertical acceleration limit."),
            ["maximumVerticalJerk"] = new("最大垂直加加速度 (m/s³)", "Maximum Vertical Jerk", "垂直加速度每秒允许变化的上限。", "Vertical acceleration change limit."),
            ["maximumYawAccelerationDegrees"] = new("最大偏航角加速度 (°/s²)", "Maximum Yaw Acceleration", "偏航角速度每秒允许变化的上限。", "Yaw-rate acceleration limit."),
            ["automaticTakeoffHeightMeters"] = new("自动起飞高度 (m)", "Automatic Takeoff Height (m)", "按 T 后自动起飞的目标高度。", "Target height after automatic takeoff."),
            ["automaticLandingSpeedMetersPerSecond"] = new("自动降落速度 (m/s)", "Automatic Landing Speed (m/s)", "自动降落阶段的下降速度。", "Descent speed during automatic landing."),
            ["defaultResponseProfile"] = new("默认飞行档位", "Default Flight Profile", "进入场景时使用的档位。", "Profile selected when entering the scene."),
            ["landingGearTransitionSeconds"] = new("起落架过渡时间 (s)", "Gear Transition Time (s)", "完全收放需要的时间。", "Time for a full gear transition."),
            ["resetHoldSeconds"] = new("长按重载场景时间 (s)", "Scene Reload Hold Time (s)", "R 键达到此时间后卸载并重新加载当前 DroneFlight 场景。", "Hold R for this duration to unload and reload the current DroneFlight scene.")
        };

        private bool useChinese;
        private int selectedPage;

        private void OnEnable()
        {
            useChinese = EditorPrefs.GetBool(LanguagePreferenceKey, true);
            selectedPage = EditorPrefs.GetInt(PagePreferenceKey, 0);
        }

        /// 绘制飞控配置的互斥语言选择、普通/高级分页和当前语言诊断。
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            useChinese = DroneConfigInspectorUi.DrawLanguageToolbar(useChinese, LanguagePreferenceKey);

            EditorGUILayout.Space(4f);
            var pages = useChinese
                ? new[] { "普通设置", "高级设置" }
                : new[] { "Basic", "Advanced" };
            var nextPage = GUILayout.Toolbar(selectedPage, pages);
            if (nextPage != selectedPage)
            {
                selectedPage = nextPage;
                EditorPrefs.SetInt(PagePreferenceKey, selectedPage);
            }

            EditorGUILayout.Space(6f);
            if (selectedPage == 0)
            {
                DrawBasicSettings();
            }
            else
            {
                DrawAdvancedSettings();
            }

            serializedObject.ApplyModifiedProperties();
            var validation = ((DroneFlightConfig)target).Validate();
            if (!validation.IsValid)
            {
                EditorGUILayout.HelpBox(
                    useChinese ? validation.ChineseMessage : validation.EnglishMessage,
                    MessageType.Error);
            }
        }

        private void DrawBasicSettings()
        {
            DrawSection(useChinese ? "载重能力" : "Payload Capability");
            DrawNamed("ratedPayloadKilograms");
            DrawNamed("maximumPayloadMultiplier");
            DrawNamed("ratedPayloadHoverCommand");
            DrawNamed("motorResponsiveness");
            DrawAutomaticResults();

            DrawSection(useChinese ? "飞行档位" : "Flight Profiles");
            DrawNamed("cineProfile");
            DrawNamed("normalProfile");
            DrawNamed("sportProfile");

            DrawSection(useChinese ? "自动起降" : "Automatic Flight");
            DrawNamed("automaticTakeoffHeightMeters");
            DrawNamed("automaticLandingSpeedMetersPerSecond");
            DrawNamed("defaultResponseProfile");

            DrawSection(useChinese ? "起落架" : "Landing Gear");
            DrawNamed("landingGearTransitionSeconds");

            DrawSection(useChinese ? "输入与场景重载" : "Input And Scene Reload");
            DrawNamed("resetHoldSeconds");
        }

        private void DrawAdvancedSettings()
        {
            DrawNamed("powerConfigurationMode");
            var mode = (DronePowerConfigurationMode)serializedObject
                .FindProperty("powerConfigurationMode").enumValueIndex;
            if (mode == DronePowerConfigurationMode.ManualPhysics)
            {
                EditorGUILayout.HelpBox(
                    useChinese
                        ? "手动物理参数可能导致额定载重无法悬停、动力过强或 PID 饱和。"
                        : "Manual physics may prevent rated-payload hover, create excessive power, or saturate PID controllers.",
                    MessageType.Warning);
            }

            DrawSection(useChinese ? "自动载重计算" : "Automatic Payload Tuning");
            DrawNamed("ratedPayloadKilograms");
            DrawNamed("bodyMassMultiplier");
            DrawNamed("maximumPayloadMultiplier");
            DrawNamed("ratedPayloadHoverCommand");
            DrawNamed("motorResponsiveness");
            DrawNamed("maximumRpm");

            DrawSection(useChinese ? "机体物理" : "Airframe Physics");
            DrawNamedDisabled("bodyMassKilograms", mode == DronePowerConfigurationMode.AutomaticPayloadTuning);
            DrawNamed("bodyLinearDamping");
            DrawNamed("bodyAngularDamping");

            DrawSection(useChinese ? "电机" : "Motors");
            DrawNamedDisabled("motorResponseTimeSeconds", mode == DronePowerConfigurationMode.AutomaticPayloadTuning);
            DrawNamedDisabled("thrustCoefficient", mode == DronePowerConfigurationMode.AutomaticPayloadTuning);
            DrawNamed("reactionTorqueCoefficient");

            DrawSection(useChinese ? "姿态控制" : "Attitude Control");
            DrawNamed("attitudeGain");
            DrawNamed("yawAttitudeGain");
            DrawNamed("yawWeight");
            DrawNamed("maximumRateRadiansPerSecond");
            DrawNamed("rateFeedForward");
            DrawNamed("stateDerivativeFilterHz");
            DrawNamed("rollRate");
            DrawNamed("pitchRate");
            DrawNamed("yawRate");

            DrawSection(useChinese ? "高度控制" : "Altitude Control");
            foreach (var field in new[]
                     {
                         "altitudeGain",
                         "verticalSpeedProportionalGain", "verticalSpeedIntegralGain",
                         "verticalSpeedDerivativeGain", "verticalSpeedOutputLimit",
                         "verticalSpeedIntegralLimit", "verticalSpeedDerivativeFilterHz"
                     })
            {
                DrawNamed(field);
            }

            DrawSection(useChinese ? "水平位置保持" : "Horizontal Position Hold");
            foreach (var field in new[]
                     {
                         "horizontalPositionGain",
                         "horizontalVelocityGain", "horizontalVelocityIntegralGain",
                         "horizontalVelocityDerivativeGain", "horizontalVelocityOutputLimit",
                         "horizontalVelocityIntegralLimit", "horizontalVelocityDerivativeFilterHz"
                     })
            {
                DrawNamed(field);
            }

            DrawSection(useChinese ? "飞行档位" : "Flight Profiles");
            DrawNamed("cineProfile");
            DrawNamed("normalProfile");
            DrawNamed("sportProfile");

            DrawSection(useChinese ? "自动起飞与降落" : "Automatic Takeoff And Landing");
            DrawNamed("automaticTakeoffHeightMeters");
            DrawNamed("automaticLandingSpeedMetersPerSecond");
            DrawNamed("defaultResponseProfile");

            DrawSection(useChinese ? "起落架" : "Landing Gear");
            DrawNamed("landingGearTransitionSeconds");

            DrawSection(useChinese ? "输入与场景重载" : "Input And Scene Reload");
            DrawNamed("resetHoldSeconds");

            if (mode == DronePowerConfigurationMode.AutomaticPayloadTuning)
            {
                DrawAutomaticResults();
            }

        }

        private void DrawAutomaticResults()
        {
            serializedObject.ApplyModifiedProperties();
            var result = ((DroneFlightConfig)target).AutomaticTuning;
            DrawSection(useChinese ? "自动计算结果（只读）" : "Automatic Results (Read Only)");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(useChinese ? "自动机体质量 (kg)" : "Automatic Body Mass (kg)", result.BodyMassKilograms);
                EditorGUILayout.FloatField(useChinese ? "最大允许载荷 (kg)" : "Maximum Payload (kg)", result.MaximumPayloadKilograms);
                EditorGUILayout.FloatField(useChinese ? "额定工况总质量 (kg)" : "Rated Operating Mass (kg)", result.RatedOperatingMassKilograms);
                EditorGUILayout.FloatField(useChinese ? "自动推力系数" : "Automatic Thrust Coefficient", result.ThrustCoefficient);
                EditorGUILayout.FloatField(useChinese ? "额定满载剩余动力" : "Rated Power Reserve", result.RatedPowerReserve);
                EditorGUILayout.FloatField(useChinese ? "最大载荷理论悬停指令" : "Maximum Payload Hover Command", result.MaximumPayloadHoverCommand);
                EditorGUILayout.Toggle(useChinese ? "最大载荷理论可悬停" : "Can Hover At Maximum Payload", result.CanHoverAtMaximumPayload);
            }

            if (result.IsValid && !result.CanHoverAtMaximumPayload)
            {
                EditorGUILayout.HelpBox(
                    useChinese ? "最大允许载荷工况超过理论推力上限。" : "Maximum payload exceeds the theoretical thrust limit.",
                    MessageType.Warning);
            }

        }

        private void DrawNamed(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                DrawProperty(property);
            }
        }

        private void DrawNamedDisabled(string propertyName, bool disabled)
        {
            using (new EditorGUI.DisabledScope(disabled))
            {
                DrawNamed(propertyName);
            }
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawProperty(SerializedProperty property)
        {
            var content = ResolveContent(property);
            if (!property.hasVisibleChildren || property.propertyType == SerializedPropertyType.String)
            {
                EditorGUILayout.PropertyField(property, content, true);
                return;
            }

            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, content, true);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var child = property.Copy();
                var end = child.GetEndProperty();
                if (!child.NextVisible(true))
                {
                    return;
                }

                do
                {
                    if (SerializedProperty.EqualContents(child, end))
                    {
                        break;
                    }

                    DrawProperty(child.Copy());
                }
                while (child.NextVisible(false));
            }
        }

        private GUIContent ResolveContent(SerializedProperty property)
        {
            if (SerializedFieldLabels.TryGetValue(property.propertyPath, out var pathLabel)
                || SerializedFieldLabels.TryGetValue(property.name, out pathLabel))
            {
                return pathLabel.Content(useChinese);
            }

            return new GUIContent(property.displayName);
        }

    }
}
