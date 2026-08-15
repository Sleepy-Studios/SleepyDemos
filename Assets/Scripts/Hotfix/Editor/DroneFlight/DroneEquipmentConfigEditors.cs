using System.Collections.Generic;
using Hotfix.DroneFlight;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>抓斗与渔叉配置共享的双语、双页 Inspector 基类。</summary>
    public abstract class DroneEquipmentConfigEditorBase : UnityEditor.Editor
    {
        private const string LanguageKey = "SleepyDemos.DroneEquipmentConfigEditor.Chinese";
        private const string PageKey = "SleepyDemos.DroneEquipmentConfigEditor.Page";
        private bool chinese;
        private int page;

        private protected abstract IReadOnlyList<string> BasicFields { get; }
        private protected abstract IReadOnlyList<string> AllFields { get; }
        private protected abstract IReadOnlyDictionary<string, DroneInspectorLabel> Labels { get; }
        private protected abstract string ChineseTitle { get; }
        private protected abstract string EnglishTitle { get; }
        private protected abstract DroneInspectorLabel ResolveSection(string fieldName);
        private protected abstract DroneConfigValidationResult Validate();

        private void OnEnable()
        {
            chinese = EditorPrefs.GetBool(LanguageKey, true);
            page = Mathf.Clamp(EditorPrefs.GetInt(PageKey, 0), 0, 1);
        }

        /// 绘制装备配置的互斥语言选择、普通/高级分页和当前语言诊断。
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            chinese = DroneConfigInspectorUi.DrawLanguageToolbar(chinese, LanguageKey);
            EditorGUILayout.LabelField(chinese ? ChineseTitle : EnglishTitle, EditorStyles.boldLabel);
            var next = GUILayout.Toolbar(page, chinese
                ? new[] { "普通设置", "高级设置" }
                : new[] { "Basic", "Advanced" });
            if (next != page)
            {
                page = next;
                EditorPrefs.SetInt(PageKey, page);
            }

            string currentSection = null;
            foreach (var field in page == 0 ? BasicFields : AllFields)
            {
                var section = ResolveSection(field);
                var sectionKey = section.English;
                if (sectionKey != currentSection)
                {
                    EditorGUILayout.Space(5f);
                    EditorGUILayout.LabelField(chinese ? section.Chinese : section.English, EditorStyles.boldLabel);
                    currentSection = sectionKey;
                }
                DrawField(field);
            }

            serializedObject.ApplyModifiedProperties();
            var validation = Validate();
            if (!validation.IsValid)
            {
                EditorGUILayout.HelpBox(
                    chinese ? validation.ChineseMessage : validation.EnglishMessage,
                    MessageType.Error);
            }
        }

        private void DrawField(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
            {
                EditorGUILayout.HelpBox(
                    chinese ? $"配置缺少字段：{name}" : $"Configuration field is missing: {name}",
                    MessageType.Error);
                return;
            }

            var content = Labels.TryGetValue(name, out var label)
                ? label.Content(chinese)
                : new GUIContent(property.displayName);
            EditorGUILayout.PropertyField(property, content, true);
        }
    }

    [CustomEditor(typeof(DroneGrappleConfig))]
    public sealed class DroneGrappleConfigEditor : DroneEquipmentConfigEditorBase
    {
        private static readonly string[] Basic =
        {
            "armLengthMeters", "maximumLiftTravelMeters", "liftSpeedMetersPerSecond", "swingLimitDegrees",
            "openAngleDegrees", "closedAngleDegrees",
            "clawSpring", "clawDamper", "breakForceNewtons", "breakTorqueNewtonMeters"
        };

        private static readonly string[] All =
        {
            "armLengthMeters", "maximumLiftTravelMeters", "liftSpeedMetersPerSecond", "swingLimitDegrees",
            "dampingRatio", "maximumDampingTorqueNewtonMeters", "openAngleDegrees",
            "closedAngleDegrees", "clawSpring", "clawDamper",
            "enclosureRadiusMeters", "enclosureHalfHeightMeters", "breakForceNewtons",
            "breakTorqueNewtonMeters", "supportedLoadSmoothingSeconds"
        };

        private static readonly IReadOnlyDictionary<string, DroneInspectorLabel> FieldLabels =
            new Dictionary<string, DroneInspectorLabel>
            {
                ["armLengthMeters"] = L("固定吊臂长度 (m)", "Fixed Arm Length (m)", "与抓斗底座刚性连接的单根吊臂长度。", "Length of the single arm rigidly attached to the grapple base."),
                ["maximumLiftTravelMeters"] = L("最大升降行程 (m)", "Maximum Lift Travel (m)", "K 下放允许的最大万向节吊点行程。", "Maximum universal-joint anchor travel allowed by K."),
                ["liftSpeedMetersPerSecond"] = L("升降速度 (m/s)", "Lift Speed (m/s)", "J 上收、K 下放吊点的速度。", "Anchor speed used by J to retract and K to lower."),
                ["swingLimitDegrees"] = L("万向节双轴摆角 (°)", "Universal Joint Swing (°)", "前后、左右两个被动摆动方向的共同限位。", "Shared limit for passive fore-aft and left-right swing."),
                ["dampingRatio"] = L("悬挂阻尼比", "Suspension Damping Ratio", "悬挂 Drive 的无量纲阻尼比例。", "Dimensionless damping ratio of the suspension drive."),
                ["maximumDampingTorqueNewtonMeters"] = L("最大阻尼扭矩 (N·m)", "Maximum Damping Torque (N·m)", "被动摆动衰减允许的最大扭矩。", "Maximum torque used for passive swing damping."),
                ["openAngleDegrees"] = L("张开角度 (°)", "Open Angle (°)", "四爪张开时的 Hinge 目标角。", "Hinge target angle while the claws are open."),
                ["closedAngleDegrees"] = L("闭合角度 (°)", "Closed Angle (°)", "四爪闭合时的 Hinge 目标角。", "Hinge target angle while the claws are closed."),
                ["clawSpring"] = L("爪驱动弹簧", "Claw Spring", "四个爪 HingeJoint 的驱动弹簧。", "Drive spring applied to all four claw hinges."),
                ["clawDamper"] = L("爪驱动阻尼", "Claw Damper", "四个爪 HingeJoint 的驱动阻尼。", "Drive damper applied to all four claw hinges."),
                ["enclosureRadiusMeters"] = L("捕获水平半径 (m)", "Capture Radius (m)", "闭爪时允许吸附 DronePayload 质心的水平半径。", "Horizontal radius that accepts a DronePayload center while closing."),
                ["enclosureHalfHeightMeters"] = L("捕获半高 (m)", "Capture Half Height (m)", "底座下方捕获体积的半高；总体积从底座向下延伸两倍此值。", "Half-height of the capture volume extending downward from the base."),
                ["breakForceNewtons"] = L("断裂力 (N)", "Break Force (N)", "刚性抓取连接的断裂力。", "Break force of the rigid grip connection."),
                ["breakTorqueNewtonMeters"] = L("断裂扭矩 (N·m)", "Break Torque (N·m)", "刚性抓取连接的断裂扭矩。", "Break torque of the rigid grip connection."),
                ["supportedLoadSmoothingSeconds"] = L("承载质量平滑时间 (s)", "Supported Load Smoothing (s)", "真实竖直拉力换算为飞控承载质量的平滑时间。", "Smoothing time from vertical constraint force to supported flight mass.")
            };

        internal static IReadOnlyList<string> AllSerializedFields => All;
        internal static IReadOnlyList<string> BasicSerializedFields => Basic;
        internal static IReadOnlyDictionary<string, DroneInspectorLabel> SerializedFieldLabels => FieldLabels;

        private protected override IReadOnlyList<string> BasicFields => Basic;
        private protected override IReadOnlyList<string> AllFields => All;
        private protected override IReadOnlyDictionary<string, DroneInspectorLabel> Labels => FieldLabels;
        private protected override string ChineseTitle => "四爪抓斗配置";
        private protected override string EnglishTitle => "Four-Claw Grapple Configuration";
        private protected override DroneInspectorLabel ResolveSection(string fieldName) => GetSectionLabel(fieldName);

        internal static DroneInspectorLabel GetSectionLabel(string fieldName)
        {
            return fieldName switch
            {
                "armLengthMeters" or "maximumLiftTravelMeters" or "liftSpeedMetersPerSecond" =>
                    L("吊臂与升降", "Arm And Lift", "固定吊臂与万向节升降行程。", "Fixed arm and universal-joint lift travel."),
                "swingLimitDegrees" or "dampingRatio"
                    or "maximumDampingTorqueNewtonMeters" =>
                    L("万向节", "Universal Joint", "双轴摆动、轴向锁定与被动阻尼。", "Dual-axis swing, axial lock and passive damping."),
                "openAngleDegrees" or "closedAngleDegrees" or "clawSpring" or "clawDamper"
                    or "enclosureRadiusMeters"
                    or "enclosureHalfHeightMeters" =>
                    L("四爪机构", "Claws", "四爪驱动与闭合捕获范围。", "Claw drive and closing capture volume."),
                _ => L("刚性抓取", "Rigid Grip", "临时 FixedJoint 和承载反馈。", "Temporary FixedJoint and supported-load feedback.")
            };
        }
        private protected override DroneConfigValidationResult Validate() => ((DroneGrappleConfig)target).Validate();

        private static DroneInspectorLabel L(string cn, string en, string cnTip, string enTip) =>
            new(cn, en, cnTip, enTip);
    }

    [CustomEditor(typeof(DroneHarpoonConfig))]
    public sealed class DroneHarpoonConfigEditor : DroneEquipmentConfigEditorBase
    {
        private static readonly string[] Basic =
        {
            "projectileMassKilograms", "launchImpulseNewtonSeconds",
            "maximumFlightDistanceMeters", "maximumAimRadiusMeters", "maximumAimConeDegrees",
            "minimumRopeLengthMeters", "maximumRopeLengthMeters",
            "reelSpeedMetersPerSecond", "ropeBreakForceNewtons", "automaticRecoverySpeedMetersPerSecond",
            "recoveryResponseSeconds", "maximumRecoveryAccelerationMetersPerSecondSquared"
        };

        private static readonly string[] All =
        {
            "projectileMassKilograms", "launchImpulseNewtonSeconds",
            "maximumFlightDistanceMeters", "maximumAimRadiusMeters", "maximumAimConeDegrees",
            "allowedAimErrorDegrees", "minimumRopeLengthMeters",
            "maximumRopeLengthMeters", "reelSpeedMetersPerSecond", "ropeSpringNewtonsPerMeter",
            "ropeDamperNewtonSecondsPerMeter", "maximumTensionNewtons", "ropeBreakForceNewtons",
            "automaticRecoverySpeedMetersPerSecond", "recoveryResponseSeconds",
            "maximumRecoveryAccelerationMetersPerSecondSquared", "dockPositionToleranceMeters",
            "dockSpeedToleranceMetersPerSecond", "hittableLayers", "ignoredLayers"
        };

        private static readonly IReadOnlyDictionary<string, DroneInspectorLabel> FieldLabels =
            new Dictionary<string, DroneInspectorLabel>
            {
                ["projectileMassKilograms"] = L("弹体质量 (kg)", "Projectile Mass (kg)", "参与真实冲量和飞行的弹体质量。", "Projectile mass used by launch impulse and flight physics."),
                ["launchImpulseNewtonSeconds"] = L("弹体发射冲量 (N·s)", "Launch Impulse (N·s)", "同时施加给弹体与机体的等量反向冲量；默认 0.12 N·s。", "Equal and opposite impulse applied to projectile and drone; default 0.12 N·s."),
                ["maximumFlightDistanceMeters"] = L("最大飞行距离 (m)", "Maximum Flight Distance (m)", "未命中时进入悬挂状态的最大距离。", "Maximum distance before an unhit projectile becomes suspended."),
                ["maximumAimRadiusMeters"] = L("瞄准水平半径 (m)", "Aim Radius (m)", "准星相对机体正下方允许的最大水平距离。", "Maximum horizontal cursor distance from directly below the drone."),
                ["maximumAimConeDegrees"] = L("向下圆锥半角 (°)", "Downward Cone Half-Angle (°)", "发射方向相对机体局部 -Y 的最大夹角。", "Maximum angle from the drone local -Y launch axis."),
                ["allowedAimErrorDegrees"] = L("允许瞄准误差 (°)", "Allowed Aim Error (°)", "发射方向与屏幕射线的最大夹角。", "Maximum angle between launcher direction and screen-center ray."),
                ["minimumRopeLengthMeters"] = L("最短目标绳长 (m)", "Minimum Rope Length (m)", "卷线操作允许的最短目标长度。", "Minimum target length allowed by reel input."),
                ["maximumRopeLengthMeters"] = L("最长目标绳长 (m)", "Maximum Rope Length (m)", "放线操作允许的最长目标长度。", "Maximum target length allowed by reel input."),
                ["reelSpeedMetersPerSecond"] = L("收放线速度 (m/s)", "Reel Speed (m/s)", "J/K 改变目标绳长的速度。", "Rate at which J/K changes target rope length."),
                ["ropeSpringNewtonsPerMeter"] = L("绳索弹簧 (N/m)", "Rope Spring (N/m)", "绳索拉紧后的弹簧系数。", "Spring coefficient while the rope is taut."),
                ["ropeDamperNewtonSecondsPerMeter"] = L("绳索阻尼 (N·s/m)", "Rope Damper (N·s/m)", "绳索拉紧方向的相对速度阻尼。", "Relative-velocity damping along a taut rope."),
                ["maximumTensionNewtons"] = L("最大计算张力 (N)", "Maximum Tension (N)", "单步绳索物理允许施加的张力上限。", "Maximum rope tension applied during one physics step."),
                ["ropeBreakForceNewtons"] = L("绳索断裂力 (N)", "Rope Break Force (N)", "超过该张力时进入断裂/回收流程。", "Tension threshold that starts break and recovery."),
                ["automaticRecoverySpeedMetersPerSecond"] = L("自动回收速度 (m/s)", "Automatic Recovery Speed (m/s)", "解除或未命中后的弹体回收速度。", "Projectile recovery speed after release or miss."),
                ["recoveryResponseSeconds"] = L("回收响应时间 (s)", "Recovery Response (s)", "弹体相对 Muzzle 速度收敛到回收速度的时间。", "Time for projectile velocity relative to the muzzle to converge."),
                ["maximumRecoveryAccelerationMetersPerSecondSquared"] = L("最大回收加速度 (m/s²)", "Maximum Recovery Acceleration (m/s²)", "限制 PD 回收力，避免弹体甩锤。", "Caps PD recovery force to prevent slinging."),
                ["dockPositionToleranceMeters"] = L("停靠位置容差 (m)", "Dock Position Tolerance (m)", "弹体重新锁回发射器的位置阈值。", "Position threshold for docking the projectile."),
                ["dockSpeedToleranceMetersPerSecond"] = L("停靠速度容差 (m/s)", "Dock Speed Tolerance (m/s)", "弹体重新锁回发射器的相对速度阈值。", "Relative-speed threshold for docking the projectile."),
                ["hittableLayers"] = L("可命中层", "Hittable Layers", "允许渔叉建立命中的物理层。", "Physics layers that allow harpoon attachment."),
                ["ignoredLayers"] = L("忽略层", "Ignored Layers", "即使包含在可命中层中也始终忽略的物理层。", "Physics layers always ignored even when included above.")
            };

        internal static IReadOnlyList<string> AllSerializedFields => All;
        internal static IReadOnlyList<string> BasicSerializedFields => Basic;
        internal static IReadOnlyDictionary<string, DroneInspectorLabel> SerializedFieldLabels => FieldLabels;

        private protected override IReadOnlyList<string> BasicFields => Basic;
        private protected override IReadOnlyList<string> AllFields => All;
        private protected override IReadOnlyDictionary<string, DroneInspectorLabel> Labels => FieldLabels;
        private protected override string ChineseTitle => "渔叉与柔性绳索配置";
        private protected override string EnglishTitle => "Harpoon And Flexible Rope Configuration";
        private protected override DroneInspectorLabel ResolveSection(string fieldName) => GetSectionLabel(fieldName);

        internal static DroneInspectorLabel GetSectionLabel(string fieldName)
        {
            return fieldName switch
            {
                "projectileMassKilograms" or "launchImpulseNewtonSeconds"
                    or "maximumFlightDistanceMeters" or "maximumAimRadiusMeters"
                    or "maximumAimConeDegrees"
                    or "allowedAimErrorDegrees" =>
                    L("发射器", "Launcher", "发射器、弹体和云台瞄准。", "Launcher, projectile and gimbal aiming."),
                "hittableLayers" or "ignoredLayers" =>
                    L("命中规则", "Hit Rules", "可命中与始终忽略的物理层。", "Hittable and always-ignored physics layers."),
                _ => L("柔性绳索", "Flexible Rope", "绳长、收放线、张力和回收。", "Rope length, reel, tension and recovery.")
            };
        }
        private protected override DroneConfigValidationResult Validate() => ((DroneHarpoonConfig)target).Validate();

        private static DroneInspectorLabel L(string cn, string en, string cnTip, string enTip) =>
            new(cn, en, cnTip, enTip);
    }
}
