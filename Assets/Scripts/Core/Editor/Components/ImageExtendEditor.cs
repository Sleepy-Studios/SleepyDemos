using Core.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Editor
{
    /// 按实际启用的图片效果展开配置，并提示不兼容组合。
    [CustomEditor(typeof(ImageExtend)), CanEditMultipleObjects]
    public sealed class ImageExtendEditor : UnityEditor.Editor
    {
        private const string LanguagePreference = "SleepyDemos.ImageExtend.Inspector.English";
        private bool english;

        public override void OnInspectorGUI()
        {
            english = EditorPrefs.GetBool(LanguagePreference, false);
            DrawLanguage();
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                Draw("m_Script", "脚本", "Script", "图片扩展组件脚本。", "Image extension component script.");
            }
            EditorGUILayout.LabelField(Text("图片变换", "Image Transform"), EditorStyles.boldLabel);
            Draw("flipHorizontal", "水平翻转", "Flip Horizontal", "翻转图片网格，不改变 RectTransform 或点击矩形。", "Flip the image mesh without changing the RectTransform or hit rectangle.");
            Draw("flipVertical", "垂直翻转", "Flip Vertical", "翻转图片网格，不改变 RectTransform 或点击矩形。", "Flip the image mesh without changing the RectTransform or hit rectangle.");
            Draw("rotationAngle", "旋转角度", "Rotation Angle", "绕 Rect 中心旋转图片网格，单位为度；点击矩形不旋转。", "Rotate the image mesh around the Rect center in degrees; the hit rectangle does not rotate.");
            if (Group("enableGradient", "渐变", "Gradient", "将渐变颜色与 Image 染色相乘。", "Multiply gradient colors by the Image tint."))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDirection();
                    Draw("offset", "采样偏移", "Sampling Offset", "渐变采样偏移，范围 -1 到 1。", "Gradient sampling offset from -1 to 1.");
                    Draw("gradient", "渐变颜色", "Gradient Colors", "设置渐变的颜色与透明度。", "Set gradient colors and alpha.");
                }
            }
            DrawRoundedCorners();
            if (Group("enableOutline", "描边", "Outline", "细描边不扩大点击区域；普通边框优先使用九宫格资源。", "A thin outline does not expand the hit area; prefer sliced sprites for ordinary borders."))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    Draw("outlineWidth", "描边宽度", "Outline Width", "范围 0 到 8 个 UI 本地单位。", "From 0 to 8 UI local units.");
                    Draw("outlineColor", "描边颜色", "Outline Color", "独立于 Image RGB 染色，透明度仍跟随 Image 与 CanvasGroup。", "Independent of the Image RGB tint; alpha still follows the Image and CanvasGroup.");
                }
            }
            if (Group("enableShadow", "阴影", "Shadow", "阴影不扩大点击区域；普通控件优先使用九宫格资源。", "A shadow does not expand the hit area; prefer sliced sprites for ordinary controls."))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    Draw("shadowColor", "阴影颜色", "Shadow Color", "阴影透明度仍跟随 Image 与 CanvasGroup。", "Shadow alpha still follows the Image and CanvasGroup.");
                    Draw("shadowOffset", "阴影偏移", "Shadow Offset", "图片局部坐标中的偏移，单位为 UI 本地单位。", "Offset in image local coordinates, measured in UI local units.");
                    Draw("shadowSoftness", "阴影柔化", "Shadow Softness", "范围 0 到 16 个 UI 本地单位。", "From 0 to 16 UI local units.");
                }
            }
            if (IsEnabled("enableOutline") || IsEnabled("enableShadow"))
            {
                Draw("useSpriteAlpha", "使用透明轮廓", "Use Sprite Alpha", "开启时按 Sprite 透明轮廓计算描边与阴影，关闭时按面板矩形计算。", "Use the Sprite alpha contour for outlines and shadows; otherwise use the panel rectangle.");
            }
            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var selected in targets)
                {
                    ((ImageExtend)selected).Refresh();
                }
            }
            foreach (var selected in targets)
            {
                string issue = ((ImageExtend)selected).GetSupportIssue(english);
                if (issue != null)
                {
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
                }
            }
            EditorGUILayout.HelpBox(Text("纯透明度变化优先控制 CanvasGroup.alpha。阴影和描边不扩大点击区域；普通控件优先使用九宫格资源。", "For opacity changes, prefer CanvasGroup.alpha. Shadows and outlines do not expand the hit area; prefer sliced sprites for ordinary controls."), MessageType.Info);
            DrawMaterialDiagnostics();
        }

        private void DrawLanguage()
        {
            EditorGUI.BeginChangeCheck();
            int language;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(Content("语言", "Language", "记住 ImageExtend Inspector 的显示语言；多个 Inspector 共用此偏好。", "Remember the ImageExtend Inspector language; multiple Inspectors share this preference."));
                language = GUILayout.Toolbar(english ? 1 : 0, new[]
                {
                    new GUIContent("中文", Text("使用简体中文显示 ImageExtend Inspector。", "Display the ImageExtend Inspector in Simplified Chinese.")),
                    new GUIContent("English", Text("使用英文显示 ImageExtend Inspector。", "Display the ImageExtend Inspector in English."))
                });
            }
            if (EditorGUI.EndChangeCheck())
            {
                english = language == 1;
                EditorPrefs.SetBool(LanguagePreference, english);
            }
        }

        private void DrawRoundedCorners()
        {
            if (!Group("enableRoundedCorners", "圆角", "Rounded Corners", "裁切图片圆角，不改变点击矩形。", "Clip rounded image corners without changing the hit rectangle."))
            {
                return;
            }
            using (new EditorGUI.IndentLevelScope())
            {
                Draw("useUnifiedRadius", "统一四角半径", "Unified Radius", "开启时四角共用一个半径；关闭时分别设置四角。", "Use one radius for all corners, or disable to set each corner separately.");
                var unified = serializedObject.FindProperty("useUnifiedRadius");
                if (unified.boolValue || unified.hasMultipleDifferentValues)
                {
                    Draw("unifiedRadius", "圆角半径", "Corner Radius", "非负的 UI 本地单位；绘制时按图片尺寸限制有效半径。", "Non-negative UI local units; the effective radius is limited by the image size.");
                }
                if (!unified.boolValue || unified.hasMultipleDifferentValues)
                {
                    var radius = serializedObject.FindProperty("separateRadius");
                    Draw(radius.FindPropertyRelative("x"), "左上半径", "Top Left Radius", "左上角半径，非负的 UI 本地单位。", "Top left radius in non-negative UI local units.");
                    Draw(radius.FindPropertyRelative("y"), "右上半径", "Top Right Radius", "右上角半径，非负的 UI 本地单位。", "Top right radius in non-negative UI local units.");
                    Draw(radius.FindPropertyRelative("z"), "右下半径", "Bottom Right Radius", "右下角半径，非负的 UI 本地单位。", "Bottom right radius in non-negative UI local units.");
                    Draw(radius.FindPropertyRelative("w"), "左下半径", "Bottom Left Radius", "左下角半径，非负的 UI 本地单位。", "Bottom left radius in non-negative UI local units.");
                }
                Draw("edgeFeather", "边缘羽化", "Edge Feather", "以圆角轮廓为中心，内外各半的总透明过渡宽度，单位为 UI 本地单位；0 仅使用自动抗锯齿。这不是整图模糊或阴影柔化。", "Total alpha transition width centered on rounded contours, half inward and half outward, in UI local units; 0 uses automatic antialiasing only. This is not whole-image blur or shadow softness.");
            }
        }

        private void DrawDirection()
        {
            var property = serializedObject.FindProperty("direction");
            var label = Content("渐变方向", "Gradient Direction", "在变换后的图片局部坐标中采样：垂直为下到上，水平为左到右。", "Sample in transformed image local coordinates: bottom to top vertically, or left to right horizontally.");
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, label, property);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int direction = EditorGUI.Popup(rect, label, property.enumValueIndex, new[] { new GUIContent(Text("垂直", "Vertical")), new GUIContent(Text("水平", "Horizontal")) });
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = direction;
            }
            EditorGUI.showMixedValue = previousMixedValue;
            EditorGUI.EndProperty();
        }

        private void DrawMaterialDiagnostics()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Text("当前渲染材质（只读）", "Current Render Material (Read Only)"), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (var selected in targets)
                {
                    var image = ((ImageExtend)selected).GetComponent<Image>();
                    var renderer = image != null ? image.canvasRenderer : null;
                    Material material = renderer != null && renderer.materialCount > 0 ? renderer.GetMaterial() : null;
                    if (targets.Length > 1)
                    {
                        EditorGUILayout.LabelField(selected.name, EditorStyles.boldLabel);
                    }
                    EditorGUILayout.TextField(Content("材质名称", "Material Name", "CanvasRenderer 当前已提交的最终材质名称。", "Name of the final material currently submitted to the CanvasRenderer."), material != null ? material.name : Text("尚未提交", "Not Submitted"));
                    EditorGUILayout.TextField(Content("实例 ID", "Instance ID", "仅用于本次 Unity 会话内比较材质实例。", "Compare material instances within the current Unity session only."), material != null ? material.GetInstanceID().ToString() : "—");
                }
            }
            EditorGUILayout.HelpBox(Text("显示 CanvasRenderer 最近一次提交的材质，Canvas 重建前可能仍是旧值。相同实例 ID 只表示共用材质，不保证合批；纹理、裁切、层级和 Canvas 也会影响批次。", "Shows the material last submitted to the CanvasRenderer; values may be stale until the Canvas rebuilds. Matching instance IDs mean shared material, but do not guarantee batching; textures, clipping, hierarchy and Canvas boundaries also affect batches."), MessageType.Info);
        }

        private bool Group(string enabled, string chinese, string englishLabel, string chineseTooltip, string englishTooltip)
        {
            EditorGUILayout.Space();
            Draw(enabled, chinese, englishLabel, chineseTooltip, englishTooltip);
            return IsEnabled(enabled);
        }

        private bool IsEnabled(string name)
        {
            var property = serializedObject.FindProperty(name);
            return property.boolValue || property.hasMultipleDifferentValues;
        }

        private void Draw(string name, string chinese, string englishLabel, string chineseTooltip, string englishTooltip)
        {
            Draw(serializedObject.FindProperty(name), chinese, englishLabel, chineseTooltip, englishTooltip);
        }

        private void Draw(SerializedProperty property, string chinese, string englishLabel, string chineseTooltip, string englishTooltip)
        {
            EditorGUILayout.PropertyField(property, Content(chinese, englishLabel, chineseTooltip, englishTooltip), true);
        }

        private GUIContent Content(string chinese, string englishLabel, string chineseTooltip, string englishTooltip) => new(Text(chinese, englishLabel), Text(chineseTooltip, englishTooltip));

        private string Text(string chinese, string englishText) => english ? englishText : chinese;
    }
}
