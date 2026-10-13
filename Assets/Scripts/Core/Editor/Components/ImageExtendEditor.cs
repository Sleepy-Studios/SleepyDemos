using Core.Runtime;
using UnityEditor;

namespace Core.Editor
{
    /// 按实际启用的图片效果展开配置，并提示不兼容组合。
    [CustomEditor(typeof(ImageExtend)), CanEditMultipleObjects]
    public sealed class ImageExtendEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
            EditorGUILayout.LabelField("图片变换", EditorStyles.boldLabel);
            Draw("flipHorizontal", "flipVertical", "rotationAngle");
            Group("enableGradient", "direction", "offset", "gradient");
            Group("enableRoundedCorners", "useUnifiedRadius", "unifiedRadius", "separateRadius");
            Group("enableOutline", "outlineWidth", "outlineColor");
            Group("enableShadow", "shadowColor", "shadowOffset", "shadowSoftness");
            var outline = serializedObject.FindProperty("enableOutline");
            var shadow = serializedObject.FindProperty("enableShadow");
            if (outline.boolValue || outline.hasMultipleDifferentValues || shadow.boolValue || shadow.hasMultipleDifferentValues)
            {
                Draw("useSpriteAlpha");
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
                string issue = ((ImageExtend)selected).GetSupportIssue();
                if (issue != null)
                {
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
                }
            }
            EditorGUILayout.HelpBox("纯透明度变化优先控制 CanvasGroup.alpha。阴影和描边不扩大点击区域；普通控件优先使用九宫格资源。", MessageType.Info);
        }

        private void Draw(params string[] names)
        {
            foreach (string name in names)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
            }
        }

        private void Group(string enabled, params string[] fields)
        {
            var property = serializedObject.FindProperty(enabled);
            EditorGUILayout.PropertyField(property);
            if (property.boolValue || property.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                Draw(fields);
                EditorGUI.indentLevel--;
            }
        }
    }
}
