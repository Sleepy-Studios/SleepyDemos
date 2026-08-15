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
            "hardwareMassKilograms", "stowedDistanceMeters", "deployedDistanceMeters",
            "travelSpeedMetersPerSecond", "openAngleDegrees", "closedAngleDegrees",
            "clawSpring", "clawDamper", "breakForceNewtons", "breakTorqueNewtonMeters"
        };

        private static readonly string[] All =
        {
            "hardwareMassKilograms", "stowedDistanceMeters", "deployedDistanceMeters",
            "travelSpeedMetersPerSecond", "dockPositionToleranceMeters",
            "dockSpeedToleranceMetersPerSecond", "twistLimitDegrees", "swingLimitDegrees",
            "dampingRatio", "maximumDampingTorqueNewtonMeters", "openAngleDegrees",
            "closedAngleDegrees", "clawSpring", "clawDamper",
            "stableContactSteps", "enclosureRadiusMeters", "enclosureHalfHeightMeters",
            "linearFreedomMeters", "constraintSpring", "constraintDamper", "breakForceNewtons",
            "breakTorqueNewtonMeters", "supportedLoadSmoothingSeconds"
        };

        private static readonly IReadOnlyDictionary<string, DroneInspectorLabel> FieldLabels =
            new Dictionary<string, DroneInspectorLabel>
            {
                ["hardwareMassKilograms"] = L("设备总质量 (kg)", "Hardware Mass (kg)", "抓斗底座与四爪的总质量。", "Combined mass of the grapple base and four claws."),
                ["stowedDistanceMeters"] = L("收纳距离 (m)", "Stowed Distance (m)", "抓斗收纳时距腹部挂点的距离。", "Distance from the belly mount while stowed."),
                ["deployedDistanceMeters"] = L("放下距离 (m)", "Deployed Distance (m)", "抓斗完全放下时距腹部挂点的距离。", "Distance from the belly mount when fully deployed."),
                ["travelSpeedMetersPerSecond"] = L("短行程速度 (m/s)", "Travel Speed (m/s)", "收纳与放下的物理推进速度。", "Physical travel speed between stowed and deployed positions."),
                ["dockPositionToleranceMeters"] = L("停靠位置容差 (m)", "Dock Position Tolerance (m)", "判定短行程到位的位置误差。", "Position tolerance used to complete docking."),
                ["dockSpeedToleranceMetersPerSecond"] = L("停靠速度容差 (m/s)", "Dock Speed Tolerance (m/s)", "判定短行程稳定到位的相对速度。", "Relative-speed tolerance used to complete docking."),
                ["twistLimitDegrees"] = L("扭转限位 (°)", "Twist Limit (°)", "放下后绕吊索轴允许的扭转角。", "Allowed twist around the suspension axis while deployed."),
                ["swingLimitDegrees"] = L("摆动限位 (°)", "Swing Limit (°)", "放下后允许的被动摆角。", "Allowed passive swing angle while deployed."),
                ["dampingRatio"] = L("悬挂阻尼比", "Suspension Damping Ratio", "悬挂 Drive 的无量纲阻尼比例。", "Dimensionless damping ratio of the suspension drive."),
                ["maximumDampingTorqueNewtonMeters"] = L("最大阻尼扭矩 (N·m)", "Maximum Damping Torque (N·m)", "被动摆动衰减允许的最大扭矩。", "Maximum torque used for passive swing damping."),
                ["openAngleDegrees"] = L("张开角度 (°)", "Open Angle (°)", "四爪张开时的 Hinge 目标角。", "Hinge target angle while the claws are open."),
                ["closedAngleDegrees"] = L("闭合角度 (°)", "Closed Angle (°)", "四爪闭合时的 Hinge 目标角。", "Hinge target angle while the claws are closed."),
                ["clawSpring"] = L("爪驱动弹簧", "Claw Spring", "四个爪 HingeJoint 的驱动弹簧。", "Drive spring applied to all four claw hinges."),
                ["clawDamper"] = L("爪驱动阻尼", "Claw Damper", "四个爪 HingeJoint 的驱动阻尼。", "Drive damper applied to all four claw hinges."),
                ["stableContactSteps"] = L("稳定接触物理步", "Stable Contact Steps", "建立辅助约束前要求连续满足接触门禁的 FixedUpdate 次数。", "Consecutive FixedUpdate contacts required before assisted attachment."),
                ["enclosureRadiusMeters"] = L("包围半径 (m)", "Enclosure Radius (m)", "载荷质心必须进入的水平包围半径。", "Horizontal enclosure radius required for payload attachment."),
                ["enclosureHalfHeightMeters"] = L("包围半高 (m)", "Enclosure Half Height (m)", "载荷质心必须进入的竖直半范围。", "Vertical half range required for payload attachment."),
                ["linearFreedomMeters"] = L("辅助约束线性自由度 (m)", "Constraint Linear Freedom (m)", "辅助抓取约束允许的微小线性活动。", "Small linear freedom allowed by the assisted grip constraint."),
                ["constraintSpring"] = L("辅助约束弹簧", "Constraint Spring", "辅助抓取约束的线性弹簧。", "Linear spring of the assisted grip constraint."),
                ["constraintDamper"] = L("辅助约束阻尼", "Constraint Damper", "辅助抓取约束的线性阻尼。", "Linear damper of the assisted grip constraint."),
                ["breakForceNewtons"] = L("断裂力 (N)", "Break Force (N)", "辅助抓取约束的断裂力。", "Break force of the assisted grip constraint."),
                ["breakTorqueNewtonMeters"] = L("断裂扭矩 (N·m)", "Break Torque (N·m)", "辅助抓取约束的断裂扭矩。", "Break torque of the assisted grip constraint."),
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
                "hardwareMassKilograms" or "stowedDistanceMeters" or "deployedDistanceMeters"
                    or "travelSpeedMetersPerSecond" or "dockPositionToleranceMeters"
                    or "dockSpeedToleranceMetersPerSecond" =>
                    L("质量与行程", "Mass And Travel", "抓斗质量、收纳与放下行程。", "Grapple mass, stow and deploy travel."),
                "twistLimitDegrees" or "swingLimitDegrees" or "dampingRatio"
                    or "maximumDampingTorqueNewtonMeters" =>
                    L("悬挂", "Suspension", "吊索摆动、扭转与阻尼。", "Suspension swing, twist and damping."),
                "openAngleDegrees" or "closedAngleDegrees" or "clawSpring" or "clawDamper"
                    or "stableContactSteps" or "enclosureRadiusMeters"
                    or "enclosureHalfHeightMeters" =>
                    L("四爪机构", "Claws", "四爪驱动与接触门禁。", "Claw drive and contact gate."),
                _ => L("辅助抓取", "Assisted Grip", "辅助约束和承载反馈。", "Assisted constraint and supported-load feedback.")
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
            "hardwareMassKilograms", "projectileMassKilograms", "muzzleSpeedMetersPerSecond",
            "maximumFlightDistanceMeters", "gimbalYawLimitDegrees", "gimbalPitchUpLimitDegrees",
            "gimbalPitchDownLimitDegrees", "minimumRopeLengthMeters", "maximumRopeLengthMeters",
            "reelSpeedMetersPerSecond", "ropeBreakForceNewtons", "automaticRecoverySpeedMetersPerSecond"
        };

        private static readonly string[] All =
        {
            "hardwareMassKilograms", "projectileMassKilograms", "muzzleSpeedMetersPerSecond",
            "maximumFlightDistanceMeters", "gimbalYawLimitDegrees", "gimbalPitchUpLimitDegrees",
            "gimbalPitchDownLimitDegrees", "allowedAimErrorDegrees", "minimumRopeLengthMeters",
            "maximumRopeLengthMeters", "reelSpeedMetersPerSecond", "ropeSpringNewtonsPerMeter",
            "ropeDamperNewtonSecondsPerMeter", "maximumTensionNewtons", "ropeBreakForceNewtons",
            "automaticRecoverySpeedMetersPerSecond", "dockPositionToleranceMeters",
            "dockSpeedToleranceMetersPerSecond", "hittableLayers", "ignoredLayers"
        };

        private static readonly IReadOnlyDictionary<string, DroneInspectorLabel> FieldLabels =
            new Dictionary<string, DroneInspectorLabel>
            {
                ["hardwareMassKilograms"] = L("设备总质量 (kg)", "Hardware Mass (kg)", "发射器与停靠弹体的总质量。", "Combined mass of the launcher and docked projectile."),
                ["projectileMassKilograms"] = L("弹体质量 (kg)", "Projectile Mass (kg)", "参与真实冲量和飞行的弹体质量。", "Projectile mass used by launch impulse and flight physics."),
                ["muzzleSpeedMetersPerSecond"] = L("出膛速度 (m/s)", "Muzzle Speed (m/s)", "弹体离开发射口的初速度。", "Projectile velocity when leaving the muzzle."),
                ["maximumFlightDistanceMeters"] = L("最大飞行距离 (m)", "Maximum Flight Distance (m)", "未命中时进入悬挂状态的最大距离。", "Maximum distance before an unhit projectile becomes suspended."),
                ["gimbalYawLimitDegrees"] = L("云台偏航限位 (°)", "Gimbal Yaw Limit (°)", "发射器左右瞄准范围。", "Left/right aiming range of the launcher."),
                ["gimbalPitchUpLimitDegrees"] = L("云台上仰限位 (°)", "Gimbal Pitch-Up Limit (°)", "发射器最大上仰角。", "Maximum upward launcher pitch."),
                ["gimbalPitchDownLimitDegrees"] = L("云台下俯限位 (°)", "Gimbal Pitch-Down Limit (°)", "发射器最大下俯角。", "Maximum downward launcher pitch."),
                ["allowedAimErrorDegrees"] = L("允许瞄准误差 (°)", "Allowed Aim Error (°)", "发射方向与屏幕射线的最大夹角。", "Maximum angle between launcher direction and screen-center ray."),
                ["minimumRopeLengthMeters"] = L("最短目标绳长 (m)", "Minimum Rope Length (m)", "卷线操作允许的最短目标长度。", "Minimum target length allowed by reel input."),
                ["maximumRopeLengthMeters"] = L("最长目标绳长 (m)", "Maximum Rope Length (m)", "放线操作允许的最长目标长度。", "Maximum target length allowed by reel input."),
                ["reelSpeedMetersPerSecond"] = L("收放线速度 (m/s)", "Reel Speed (m/s)", "J/K 改变目标绳长的速度。", "Rate at which J/K changes target rope length."),
                ["ropeSpringNewtonsPerMeter"] = L("绳索弹簧 (N/m)", "Rope Spring (N/m)", "绳索拉紧后的弹簧系数。", "Spring coefficient while the rope is taut."),
                ["ropeDamperNewtonSecondsPerMeter"] = L("绳索阻尼 (N·s/m)", "Rope Damper (N·s/m)", "绳索拉紧方向的相对速度阻尼。", "Relative-velocity damping along a taut rope."),
                ["maximumTensionNewtons"] = L("最大计算张力 (N)", "Maximum Tension (N)", "单步绳索物理允许施加的张力上限。", "Maximum rope tension applied during one physics step."),
                ["ropeBreakForceNewtons"] = L("绳索断裂力 (N)", "Rope Break Force (N)", "超过该张力时进入断裂/回收流程。", "Tension threshold that starts break and recovery."),
                ["automaticRecoverySpeedMetersPerSecond"] = L("自动回收速度 (m/s)", "Automatic Recovery Speed (m/s)", "解除或未命中后的弹体回收速度。", "Projectile recovery speed after release or miss."),
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
                "hardwareMassKilograms" or "projectileMassKilograms" or "muzzleSpeedMetersPerSecond"
                    or "maximumFlightDistanceMeters" or "gimbalYawLimitDegrees"
                    or "gimbalPitchUpLimitDegrees" or "gimbalPitchDownLimitDegrees"
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
