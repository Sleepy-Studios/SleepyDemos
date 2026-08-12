using Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    [CustomEditor(typeof(PyramidLayoutGroup))]
    [CanEditMultipleObjects]
    public sealed class PyramidLayoutGroupEditor : UnityEditor.Editor
    {
        private SerializedProperty padding;
        private SerializedProperty horizontalAlignment;
        private SerializedProperty columns;
        private SerializedProperty cellSize;
        private SerializedProperty spacing;
        private SerializedProperty layoutMode;
        private SerializedProperty remainderPosition;
        private SerializedProperty reverseArrangement;

        private void OnEnable()
        {
            padding = serializedObject.FindProperty("m_Padding");
            horizontalAlignment = serializedObject.FindProperty("horizontalAlignment");
            columns = serializedObject.FindProperty("columns");
            cellSize = serializedObject.FindProperty("cellSize");
            spacing = serializedObject.FindProperty("spacing");
            layoutMode = serializedObject.FindProperty("layoutMode");
            remainderPosition = serializedObject.FindProperty("remainderPosition");
            reverseArrangement = serializedObject.FindProperty("reverseArrangement");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(padding);
            EditorGUILayout.PropertyField(horizontalAlignment, new GUIContent("Child Alignment"));
            EditorGUILayout.PropertyField(columns);
            EditorGUILayout.PropertyField(cellSize);
            EditorGUILayout.PropertyField(spacing);
            EditorGUILayout.PropertyField(layoutMode);
            EditorGUILayout.PropertyField(remainderPosition);
            EditorGUILayout.PropertyField(reverseArrangement);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
