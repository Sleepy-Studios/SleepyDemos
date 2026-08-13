using Core.Runtime;
using UnityEditor;

namespace Core.Editor
{
    [CustomEditor(typeof(ComponentItemIndex))]
    [CanEditMultipleObjects]
    public sealed class ComponentItemIndexEditor : UnityEditor.Editor
    {
        private SerializedProperty components;

        private void OnEnable()
        {
            components = serializedObject.FindProperty("components");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 绑定关系由 MvcBind 工具维护，Inspector 只负责展示，避免手动破坏下标契约。
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(components, true);
            }
        }
    }
}
