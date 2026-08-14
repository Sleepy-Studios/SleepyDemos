using System.Collections.Generic;
using Hotfix.DroneFlight;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.DroneFlight
{
    public abstract class DroneEquipmentConfigEditorBase : UnityEditor.Editor
    {
        private const string LanguageKey = "SleepyDemos.DroneEquipmentConfigEditor.Chinese";
        private const string PageKey = "SleepyDemos.DroneEquipmentConfigEditor.Page";
        private bool chinese;
        private int page;

        protected abstract IReadOnlyList<string> BasicFields { get; }
        protected abstract string ChineseTitle { get; }
        protected abstract string EnglishTitle { get; }
        protected abstract bool Validate(out string diagnostic);

        private void OnEnable()
        {
            chinese = EditorPrefs.GetBool(LanguageKey, true);
            page = EditorPrefs.GetInt(PageKey, 0);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(chinese ? "配置语言" : "Inspector Language", GUILayout.Width(110f));
                if (GUILayout.Toggle(chinese, "中文", EditorStyles.toolbarButton) != chinese)
                {
                    chinese = true;
                    EditorPrefs.SetBool(LanguageKey, true);
                }
                if (GUILayout.Toggle(!chinese, "English", EditorStyles.toolbarButton) == chinese)
                {
                    chinese = false;
                    EditorPrefs.SetBool(LanguageKey, false);
                }
            }

            EditorGUILayout.LabelField(chinese ? ChineseTitle : EnglishTitle, EditorStyles.boldLabel);
            var next = GUILayout.Toolbar(page, chinese
                ? new[] { "普通设置", "高级设置" }
                : new[] { "Basic", "Advanced" });
            if (next != page)
            {
                page = next;
                EditorPrefs.SetInt(PageKey, page);
            }

            if (page == 0)
            {
                foreach (var field in BasicFields)
                {
                    DrawField(field);
                }
            }
            else
            {
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (!Validate(out var diagnostic))
            {
                EditorGUILayout.HelpBox(chinese ? diagnostic : "Invalid equipment configuration.", MessageType.Error);
            }
        }

        private void DrawField(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    [CustomEditor(typeof(DroneGrappleConfig))]
    public sealed class DroneGrappleConfigEditor : DroneEquipmentConfigEditorBase
    {
        private static readonly string[] Fields =
        {
            "hardwareMassKilograms", "stowedDistanceMeters", "deployedDistanceMeters",
            "travelSpeedMetersPerSecond", "openAngleDegrees", "closedAngleDegrees",
            "clawSpring", "clawDamper", "breakForceNewtons", "breakTorqueNewtonMeters"
        };
        protected override IReadOnlyList<string> BasicFields => Fields;
        protected override string ChineseTitle => "四爪抓斗配置";
        protected override string EnglishTitle => "Four-Claw Grapple Configuration";
        protected override bool Validate(out string diagnostic) => ((DroneGrappleConfig)target).TryValidate(out diagnostic);
    }

    [CustomEditor(typeof(DroneHarpoonConfig))]
    public sealed class DroneHarpoonConfigEditor : DroneEquipmentConfigEditorBase
    {
        private static readonly string[] Fields =
        {
            "hardwareMassKilograms", "projectileMassKilograms", "muzzleSpeedMetersPerSecond",
            "maximumFlightDistanceMeters", "gimbalYawLimitDegrees", "gimbalPitchUpLimitDegrees",
            "gimbalPitchDownLimitDegrees", "minimumRopeLengthMeters", "maximumRopeLengthMeters",
            "reelSpeedMetersPerSecond", "ropeBreakForceNewtons", "automaticRecoverySpeedMetersPerSecond"
        };
        protected override IReadOnlyList<string> BasicFields => Fields;
        protected override string ChineseTitle => "渔叉与柔性绳索配置";
        protected override string EnglishTitle => "Harpoon And Flexible Rope Configuration";
        protected override bool Validate(out string diagnostic) => ((DroneHarpoonConfig)target).TryValidate(out diagnostic);
    }
}
