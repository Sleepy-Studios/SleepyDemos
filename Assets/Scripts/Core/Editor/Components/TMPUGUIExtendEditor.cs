using Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// TMP 复合效果的条件式双语 Inspector。
    [CustomEditor(typeof(TMP_UGUI_Extend))]
    [CanEditMultipleObjects]
    public sealed class TMPUGUIExtendEditor : UnityEditor.Editor
    {
        private static bool useEnglish;

        private SerializedProperty gradientType;
        private SerializedProperty leftTopColor;
        private SerializedProperty leftBottomColor;
        private SerializedProperty rightTopColor;
        private SerializedProperty rightBottomColor;
        private SerializedProperty gradientOffsetX;
        private SerializedProperty gradientOffsetY;
        private SerializedProperty gradientAngleOffset;
        private SerializedProperty centerColor;
        private SerializedProperty leftEdgeColor;
        private SerializedProperty rightEdgeColor;
        private SerializedProperty gradientIntensity;
        private SerializedProperty centerOffset;
        private SerializedProperty gradientWidth;
        private SerializedProperty leftColor;
        private SerializedProperty middleColor;
        private SerializedProperty rightColor;
        private SerializedProperty middlePosition;
        private SerializedProperty threePointOffsetX;
        private SerializedProperty unityGradient;
        private SerializedProperty unityGradientAngle;
        private SerializedProperty enableOutline;
        private SerializedProperty outlineColor;
        private SerializedProperty outlineWidth;
        private SerializedProperty enableShadow;
        private SerializedProperty shadowColor;
        private SerializedProperty shadowOffsetX;
        private SerializedProperty shadowOffsetY;
        private SerializedProperty shadowDilate;
        private SerializedProperty shadowSoftness;
        private SerializedProperty enableSkew;
        private SerializedProperty skewAngleDegrees;
        private SerializedProperty skewPivotY;

        private GUIStyle languageButtonStyle;
        private GUIStyle sectionHeaderStyle;

        private void OnEnable()
        {
            gradientType = Find("gradientType");
            leftTopColor = Find("leftTopColor");
            leftBottomColor = Find("leftBottomColor");
            rightTopColor = Find("rightTopColor");
            rightBottomColor = Find("rightBottomColor");
            gradientOffsetX = Find("gradientOffsetX");
            gradientOffsetY = Find("gradientOffsetY");
            gradientAngleOffset = Find("gradientAngleOffset");
            centerColor = Find("centerColor");
            leftEdgeColor = Find("leftEdgeColor");
            rightEdgeColor = Find("rightEdgeColor");
            gradientIntensity = Find("gradientIntensity");
            centerOffset = Find("centerOffset");
            gradientWidth = Find("gradientWidth");
            leftColor = Find("leftColor");
            middleColor = Find("middleColor");
            rightColor = Find("rightColor");
            middlePosition = Find("middlePosition");
            threePointOffsetX = Find("threePointOffsetX");
            unityGradient = Find("unityGradient");
            unityGradientAngle = Find("unityGradientAngle");
            enableOutline = Find("enableOutline");
            outlineColor = Find("outlineColor");
            outlineWidth = Find("outlineWidth");
            enableShadow = Find("enableShadow");
            shadowColor = Find("shadowColor");
            shadowOffsetX = Find("shadowOffsetX");
            shadowOffsetY = Find("shadowOffsetY");
            shadowDilate = Find("shadowDilate");
            shadowSoftness = Find("shadowSoftness");
            enableSkew = Find("enableSkew");
            skewAngleDegrees = Find("skewAngleDegrees");
            skewPivotY = Find("skewPivotY");

            EditorApplication.delayCall += RefreshPreview;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RefreshPreview;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawLanguageToggle();
            EditorGUILayout.Space(5f);

            EditorGUI.BeginChangeCheck();
            DrawSectionHeader(L("渐变设置", "Gradient Settings"));
            DrawGradientSettings();
            EditorGUILayout.Space(10f);

            DrawSectionHeader(L("描边设置", "Outline Settings"));
            DrawOutlineSettings();
            EditorGUILayout.Space(10f);

            DrawSectionHeader(L("阴影设置", "Shadow Settings"));
            DrawShadowSettings();
            EditorGUILayout.Space(10f);

            DrawSectionHeader(L("倾斜设置", "Skew Settings"));
            DrawSkewSettings();

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                RefreshPreview();
            }
        }

        private void DrawLanguageToggle()
        {
            languageButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedWidth = 130f
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIContent content = useEnglish
                ? new GUIContent("切换到中文", "将当前 Inspector 字段名称和浮动提示切换为中文。")
                : new GUIContent("Switch to English", "Switch Inspector labels and tooltips to English.");
            if (GUILayout.Button(content, languageButtonStyle))
            {
                useEnglish = !useEnglish;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSectionHeader(string title)
        {
            sectionHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.75f, 0.75f, 0.75f)
                        : Color.black
                }
            };
            EditorGUILayout.LabelField(title, sectionHeaderStyle);
            EditorGUILayout.Space(2f);
        }

        private void DrawGradientSettings()
        {
            EditorGUILayout.PropertyField(
                gradientType,
                Content(
                    "渐变类型",
                    "Gradient Type",
                    "选择渐变算法；只显示当前算法使用的参数。",
                    "Select a gradient algorithm. Only its relevant parameters are shown."));

            if (gradientType.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    L("多选对象使用了不同渐变类型，统一类型后才显示具体参数。", "Selected objects use different gradient types. Set one type to edit its parameters."),
                    MessageType.Info);
                return;
            }

            TMP_UGUI_Extend.GradientType selectedType = (TMP_UGUI_Extend.GradientType)gradientType.enumValueIndex;
            switch (selectedType)
            {
                case TMP_UGUI_Extend.GradientType.FourCorner:
                    DrawFourCornerGradient();
                    break;
                case TMP_UGUI_Extend.GradientType.CenterToEdge:
                    DrawCenterToEdgeGradient();
                    break;
                case TMP_UGUI_Extend.GradientType.ThreePoint:
                    DrawThreePointGradient();
                    break;
                case TMP_UGUI_Extend.GradientType.UnityGradient:
                    DrawUnityGradient();
                    break;
            }
        }

        private void DrawFourCornerGradient()
        {
            EditorGUILayout.PropertyField(leftTopColor, Content("左上角颜色", "Left Top Color", "文本包围盒左上角的颜色。", "Color at the top-left of the text bounds."));
            EditorGUILayout.PropertyField(leftBottomColor, Content("左下角颜色", "Left Bottom Color", "文本包围盒左下角的颜色。", "Color at the bottom-left of the text bounds."));
            EditorGUILayout.PropertyField(rightTopColor, Content("右上角颜色", "Right Top Color", "文本包围盒右上角的颜色。", "Color at the top-right of the text bounds."));
            EditorGUILayout.PropertyField(rightBottomColor, Content("右下角颜色", "Right Bottom Color", "文本包围盒右下角的颜色。", "Color at the bottom-right of the text bounds."));
            EditorGUILayout.PropertyField(gradientOffsetX, Content("X 偏移", "Offset X", "水平移动渐变采样位置。", "Offsets gradient sampling horizontally."));
            EditorGUILayout.PropertyField(gradientOffsetY, Content("Y 偏移", "Offset Y", "垂直移动渐变采样位置。", "Offsets gradient sampling vertically."));
            EditorGUILayout.PropertyField(gradientAngleOffset, Content("角度偏移", "Angle Offset", "围绕归一化渐变坐标旋转采样方向。", "Rotates the normalized gradient sampling direction."));
        }

        private void DrawCenterToEdgeGradient()
        {
            EditorGUILayout.PropertyField(centerColor, Content("中心颜色", "Center Color", "文本水平中心处的颜色。", "Color at the horizontal center of the text."));
            EditorGUILayout.PropertyField(leftEdgeColor, Content("左边缘颜色", "Left Edge Color", "文本左边缘目标颜色。", "Target color at the left edge."));
            EditorGUILayout.PropertyField(rightEdgeColor, Content("右边缘颜色", "Right Edge Color", "文本右边缘目标颜色。", "Target color at the right edge."));
            EditorGUILayout.PropertyField(gradientIntensity, Content("渐变强度", "Gradient Intensity", "控制中心色向边缘色混合的最大比例。", "Controls the maximum blend from center to edge colors."));
            EditorGUILayout.PropertyField(centerOffset, Content("中心偏移", "Center Offset", "沿文本宽度移动渐变中心。", "Moves the gradient center along the text width."));
            EditorGUILayout.PropertyField(gradientWidth, Content("渐变宽度", "Gradient Width", "控制中心色向边缘色过渡的快慢。", "Controls how quickly the center transitions to edge colors."));
        }

        private void DrawThreePointGradient()
        {
            EditorGUILayout.PropertyField(leftColor, Content("左侧颜色", "Left Color", "渐变起点颜色。", "Gradient start color."));
            EditorGUILayout.PropertyField(middleColor, Content("中间颜色", "Middle Color", "渐变中间控制点颜色。", "Gradient middle control color."));
            EditorGUILayout.PropertyField(rightColor, Content("右侧颜色", "Right Color", "渐变终点颜色。", "Gradient end color."));
            EditorGUILayout.PropertyField(middlePosition, Content("中间位置", "Middle Position", "中间颜色在文本宽度中的归一化位置。", "Normalized position of the middle color across the text width."));
            EditorGUILayout.PropertyField(threePointOffsetX, Content("X 偏移", "Offset X", "水平移动三点渐变采样位置。", "Offsets three-point gradient sampling horizontally."));
        }

        private void DrawUnityGradient()
        {
            EditorGUILayout.PropertyField(unityGradient, Content("渐变编辑器", "Gradient Editor", "使用 Unity Gradient 配置颜色键和透明度键。", "Configure color and alpha keys with Unity Gradient."));
            EditorGUILayout.PropertyField(unityGradientAngle, Content("渐变角度", "Gradient Angle", "0° 左到右，90° 下到上，180° 右到左，270° 上到下。", "0° left-to-right, 90° bottom-to-top, 180° right-to-left, 270° top-to-bottom."));
            EditorGUILayout.HelpBox(
                L(
                    "0°：左 → 右    90°：下 → 上\n180°：右 → 左    270°：上 → 下",
                    "0°: Left → Right    90°: Bottom → Top\n180°: Right → Left    270°: Top → Bottom"),
                MessageType.Info);
        }

        private void DrawOutlineSettings()
        {
            EditorGUILayout.PropertyField(enableOutline, Content("启用描边", "Enable Outline", "使用当前 TMP SDF 材质的描边能力。", "Uses the outline capability of the current TMP SDF material."));
            if (enableOutline.hasMultipleDifferentValues || !enableOutline.boolValue) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(outlineColor, Content("描边颜色", "Outline Color", "描边的统一颜色。", "Uniform outline color."));
            EditorGUILayout.PropertyField(outlineWidth, Content("描边宽度", "Outline Width", "组件会把 0–3 的编辑值映射到 TMP SDF 描边范围。", "Maps the 0–3 authoring value to the TMP SDF outline range."));
            EditorGUI.indentLevel--;
        }

        private void DrawShadowSettings()
        {
            EditorGUILayout.PropertyField(enableShadow, Content("启用阴影", "Enable Shadow", "使用 TMP SDF Underlay 实现单层阴影。", "Uses TMP SDF Underlay for a single shadow layer."));
            if (enableShadow.hasMultipleDifferentValues || !enableShadow.boolValue) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(shadowColor, Content("阴影颜色", "Shadow Color", "阴影颜色和透明度。", "Shadow color and opacity."));
            EditorGUILayout.PropertyField(shadowOffsetX, Content("X 偏移", "Offset X", "阴影水平偏移。", "Horizontal shadow offset."));
            EditorGUILayout.PropertyField(shadowOffsetY, Content("Y 偏移", "Offset Y", "阴影垂直偏移。", "Vertical shadow offset."));
            EditorGUILayout.PropertyField(shadowDilate, Content("阴影扩张", "Shadow Dilate", "扩大或收缩阴影轮廓。", "Expands or contracts the shadow silhouette."));
            EditorGUILayout.PropertyField(shadowSoftness, Content("阴影柔和度", "Shadow Softness", "控制阴影边缘柔和程度。", "Controls shadow edge softness."));
            EditorGUI.indentLevel--;
        }

        private void DrawSkewSettings()
        {
            EditorGUILayout.PropertyField(enableSkew, Content("启用倾斜", "Enable Skew", "在 TMP 生成网格后按 Y 位置偏移顶点 X。", "Offsets vertex X by Y position after TMP generates its mesh."));
            if (enableSkew.hasMultipleDifferentValues || !enableSkew.boolValue) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(skewAngleDegrees, Content("倾斜角度", "Skew Angle", "正值向右倾斜，负值向左倾斜。", "Positive values skew right; negative values skew left."));
            EditorGUILayout.PropertyField(skewPivotY, Content("倾斜轴心 Y", "Pivot Y", "0 为文本底部，1 为文本顶部。", "0 is the text bottom and 1 is the text top."));
            EditorGUI.indentLevel--;
        }

        private SerializedProperty Find(string propertyName)
        {
            return serializedObject.FindProperty(propertyName);
        }

        private string L(string chinese, string english)
        {
            return useEnglish ? english : chinese;
        }

        private GUIContent Content(string chinese, string english, string chineseTooltip, string englishTooltip)
        {
            return new GUIContent(L(chinese, english), L(chineseTooltip, englishTooltip));
        }

        private void RefreshPreview()
        {
            if (this == null) return;
            foreach (Object selectedTarget in targets)
            {
                if (selectedTarget is TMP_UGUI_Extend effect && effect != null)
                {
                    effect.RefreshAllEffects();
                }
            }

            SceneView.RepaintAll();
        }
    }
}
