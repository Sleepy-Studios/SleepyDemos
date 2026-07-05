using System.Collections.Generic;
using Core.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Core.Editor
{
    [CustomPropertyDrawer(typeof(UIStateProperty))]
    public sealed class UIStatePropertyDrawer : PropertyDrawer
    {
        private const string PropertyTypeField = "propertyType";
        private const string TargetField = "target";
        private const string BoolValueField = "boolValue";
        private const string FloatValueField = "floatValue";
        private const string StringValueField = "stringValue";
        private const string ColorValueField = "colorValue";
        private const string Vector2ValueField = "vector2Value";
        private const string Vector3ValueField = "vector3Value";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var target = property.FindPropertyRelative(TargetField).objectReferenceValue;
            var supportedTypes = GetSupportedTypes(target);
            if (target == null || supportedTypes.Count == 0)
            {
                return 2 * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            var propertyType = GetPropertyType(property);
            var lineCount = 2 + GetValueLineCount(propertyType);

            return lineCount * EditorGUIUtility.singleLineHeight
                   + (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProperty = property.FindPropertyRelative(TargetField);
            var propertyTypeProperty = property.FindPropertyRelative(PropertyTypeField);
            var currentType = GetPropertyType(property);

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            targetProperty.objectReferenceValue = EditorGUI.ObjectField(
                row,
                "目标",
                targetProperty.objectReferenceValue,
                typeof(Object),
                true);

            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var supportedTypes = GetSupportedTypes(targetProperty.objectReferenceValue);
            if (targetProperty.objectReferenceValue == null)
            {
                EditorGUI.HelpBox(row, "先拖入 GameObject 或组件。", MessageType.Info);
                EditorGUI.EndProperty();
                return;
            }

            if (supportedTypes.Count == 0)
            {
                EditorGUI.HelpBox(row, "目标需要是 GameObject 或 Component。", MessageType.Warning);
                EditorGUI.EndProperty();
                return;
            }

            IReadOnlyList<UIStatePropertyType> selectableTypes = supportedTypes;
            currentType = DrawTypePopup(row, currentType, selectableTypes);
            propertyTypeProperty.enumValueIndex = (int)currentType;

            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            DrawValueField(row, property, currentType);

            EditorGUI.EndProperty();
        }

        private static UIStatePropertyType DrawTypePopup(
            Rect row,
            UIStatePropertyType currentType,
            IReadOnlyList<UIStatePropertyType> selectableTypes)
        {
            var selectedIndex = 0;
            var labels = new GUIContent[selectableTypes.Count];
            for (int i = 0; i < selectableTypes.Count; i++)
            {
                labels[i] = new GUIContent(GetDisplayName(selectableTypes[i]));
                if (selectableTypes[i] == currentType)
                {
                    selectedIndex = i;
                }
            }

            selectedIndex = EditorGUI.Popup(row, new GUIContent("操作"), selectedIndex, labels);
            return selectableTypes[selectedIndex];
        }

        private static void DrawValueField(Rect row, SerializedProperty property, UIStatePropertyType propertyType)
        {
            switch (propertyType)
            {
                case UIStatePropertyType.GameObjectActive:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(BoolValueField), new GUIContent("激活"));
                    break;
                case UIStatePropertyType.GraphicColor:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(ColorValueField), new GUIContent("颜色"));
                    break;
                case UIStatePropertyType.CanvasGroupAlpha:
                    var floatProperty = property.FindPropertyRelative(FloatValueField);
                    floatProperty.floatValue = EditorGUI.Slider(row, "透明度", floatProperty.floatValue, 0f, 1f);
                    break;
                case UIStatePropertyType.CanvasGroupInteractable:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(BoolValueField), new GUIContent("可交互"));
                    break;
                case UIStatePropertyType.CanvasGroupBlocksRaycasts:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(BoolValueField), new GUIContent("阻挡射线"));
                    break;
                case UIStatePropertyType.SelectableInteractable:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(BoolValueField), new GUIContent("可交互"));
                    break;
                case UIStatePropertyType.TextContent:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(StringValueField), new GUIContent("文本"));
                    break;
                case UIStatePropertyType.RectTransformAnchoredPosition:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(Vector2ValueField), new GUIContent("锚点坐标"));
                    break;
                case UIStatePropertyType.RectTransformSizeDelta:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(Vector2ValueField), new GUIContent("尺寸"));
                    break;
                case UIStatePropertyType.TransformLocalScale:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(Vector3ValueField), new GUIContent("本地缩放"));
                    break;
                case UIStatePropertyType.TransformLocalEulerAngles:
                    EditorGUI.PropertyField(row, property.FindPropertyRelative(Vector3ValueField), new GUIContent("本地旋转"));
                    break;
            }
        }

        private static int GetValueLineCount(UIStatePropertyType propertyType)
        {
            return propertyType switch
            {
                UIStatePropertyType.GameObjectActive => 1,
                UIStatePropertyType.GraphicColor => 1,
                UIStatePropertyType.CanvasGroupAlpha => 1,
                UIStatePropertyType.CanvasGroupInteractable => 1,
                UIStatePropertyType.CanvasGroupBlocksRaycasts => 1,
                UIStatePropertyType.SelectableInteractable => 1,
                UIStatePropertyType.TextContent => 1,
                UIStatePropertyType.RectTransformAnchoredPosition => 1,
                UIStatePropertyType.RectTransformSizeDelta => 1,
                UIStatePropertyType.TransformLocalScale => 1,
                UIStatePropertyType.TransformLocalEulerAngles => 1,
                _ => 0
            };
        }

        private static UIStatePropertyType GetPropertyType(SerializedProperty property)
        {
            return (UIStatePropertyType)property.FindPropertyRelative(PropertyTypeField).enumValueIndex;
        }

        private static List<UIStatePropertyType> GetSupportedTypes(Object target)
        {
            var supportedTypes = new List<UIStatePropertyType>();
            if (target is GameObject)
            {
                supportedTypes.Add(UIStatePropertyType.GameObjectActive);
                return supportedTypes;
            }

            if (target is not Component component)
            {
                return supportedTypes;
            }

            supportedTypes.Add(UIStatePropertyType.GameObjectActive);

            if (component is Graphic)
            {
                supportedTypes.Add(UIStatePropertyType.GraphicColor);
            }

            if (component is CanvasGroup)
            {
                supportedTypes.Add(UIStatePropertyType.CanvasGroupAlpha);
                supportedTypes.Add(UIStatePropertyType.CanvasGroupInteractable);
                supportedTypes.Add(UIStatePropertyType.CanvasGroupBlocksRaycasts);
            }

            if (component is Selectable)
            {
                supportedTypes.Add(UIStatePropertyType.SelectableInteractable);
            }

            if (component is Text)
            {
                supportedTypes.Add(UIStatePropertyType.TextContent);
            }

            if (component is RectTransform)
            {
                supportedTypes.Add(UIStatePropertyType.RectTransformAnchoredPosition);
                supportedTypes.Add(UIStatePropertyType.RectTransformSizeDelta);
            }

            if (component is Transform)
            {
                supportedTypes.Add(UIStatePropertyType.TransformLocalScale);
                supportedTypes.Add(UIStatePropertyType.TransformLocalEulerAngles);
            }

            return supportedTypes;
        }

        private static string GetDisplayName(UIStatePropertyType propertyType)
        {
            return propertyType switch
            {
                UIStatePropertyType.GameObjectActive => "激活 GameObject",
                UIStatePropertyType.GraphicColor => "修改颜色",
                UIStatePropertyType.CanvasGroupAlpha => "修改透明度",
                UIStatePropertyType.CanvasGroupInteractable => "CanvasGroup 可交互",
                UIStatePropertyType.CanvasGroupBlocksRaycasts => "CanvasGroup 阻挡射线",
                UIStatePropertyType.SelectableInteractable => "Selectable 可交互",
                UIStatePropertyType.TextContent => "修改文本",
                UIStatePropertyType.RectTransformAnchoredPosition => "修改锚点坐标",
                UIStatePropertyType.RectTransformSizeDelta => "修改尺寸",
                UIStatePropertyType.TransformLocalScale => "修改缩放",
                UIStatePropertyType.TransformLocalEulerAngles => "修改旋转",
                _ => propertyType.ToString()
            };
        }
    }
}
