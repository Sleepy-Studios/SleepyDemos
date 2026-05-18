using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public enum UIStatePropertyType
    {
        GameObjectActive,
        GraphicColor,
        CanvasGroupAlpha,
        CanvasGroupInteractable,
        CanvasGroupBlocksRaycasts,
        SelectableInteractable,
        TextContent,
        RectTransformAnchoredPosition,
        RectTransformSizeDelta,
        TransformLocalScale,
        TransformLocalEulerAngles
    }

    [Serializable]
    public sealed class UIStateProperty
    {
        public UIStatePropertyType propertyType;
        public GameObject gameObject;
        public Graphic graphic;
        public CanvasGroup canvasGroup;
        public Selectable selectable;
        public Text text;
        public RectTransform rectTransform;
        public Transform transform;
        public bool boolValue;
        public float floatValue;
        public string stringValue;
        public Color colorValue = Color.white;
        public Vector2 vector2Value;
        public Vector3 vector3Value = Vector3.one;
    }

    [Serializable]
    public sealed class UIStateInfo
    {
        public string stateName;
        public List<UIStateProperty> properties = new List<UIStateProperty>();
    }

    public sealed class UIState : MonoBehaviour
    {
        [SerializeField] private List<UIStateInfo> states = new List<UIStateInfo>
        {
            new UIStateInfo { stateName = "Normal" },
            new UIStateInfo { stateName = "Selected" }
        };

        [SerializeField] private string currentStateId = "Normal";

        private readonly Dictionary<string, UIStateInfo> stateMap = new Dictionary<string, UIStateInfo>();

        public string CurrentStateId => currentStateId;
        public IReadOnlyList<UIStateInfo> States => states;

        private void Awake()
        {
            RebuildStateMap();
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(currentStateId))
            {
                SetState(currentStateId);
            }
        }

        private void OnValidate()
        {
            if (states != null && states.Count > 0)
            {
                return;
            }

            states = new List<UIStateInfo>
            {
                new UIStateInfo { stateName = "Normal" },
                new UIStateInfo { stateName = "Selected" }
            };
        }

        public void SetState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            if (ApplyState(stateName))
            {
                currentStateId = stateName;
            }
        }

        public bool ApplyState(string stateName)
        {
            if (stateMap.Count == 0)
            {
                RebuildStateMap();
            }

            if (!stateMap.TryGetValue(stateName, out var state) || state.properties == null)
            {
                return false;
            }

            foreach (var property in state.properties)
            {
                ApplyProperty(property, stateName);
            }

            return true;
        }

        public UIStateInfo GetState(string stateName)
        {
            if (stateMap.Count == 0)
            {
                RebuildStateMap();
            }

            return stateMap.TryGetValue(stateName, out var state) ? state : null;
        }

        private void RebuildStateMap()
        {
            stateMap.Clear();
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null || string.IsNullOrEmpty(state.stateName))
                {
                    continue;
                }

                stateMap[state.stateName] = state;
            }
        }

        private void ApplyProperty(UIStateProperty property, string stateName)
        {
            if (property == null)
            {
                return;
            }

            switch (property.propertyType)
            {
                case UIStatePropertyType.GameObjectActive:
                    if (property.gameObject != null)
                    {
                        property.gameObject.SetActive(property.boolValue);
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.gameObject));
                    }
                    break;
                case UIStatePropertyType.GraphicColor:
                    if (property.graphic != null)
                    {
                        property.graphic.color = property.colorValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.graphic));
                    }
                    break;
                case UIStatePropertyType.CanvasGroupAlpha:
                    if (property.canvasGroup != null)
                    {
                        property.canvasGroup.alpha = property.floatValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.canvasGroup));
                    }
                    break;
                case UIStatePropertyType.CanvasGroupInteractable:
                    if (property.canvasGroup != null)
                    {
                        property.canvasGroup.interactable = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.canvasGroup));
                    }
                    break;
                case UIStatePropertyType.CanvasGroupBlocksRaycasts:
                    if (property.canvasGroup != null)
                    {
                        property.canvasGroup.blocksRaycasts = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.canvasGroup));
                    }
                    break;
                case UIStatePropertyType.SelectableInteractable:
                    if (property.selectable != null)
                    {
                        property.selectable.interactable = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.selectable));
                    }
                    break;
                case UIStatePropertyType.TextContent:
                    if (property.text != null)
                    {
                        property.text.text = property.stringValue ?? string.Empty;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.text));
                    }
                    break;
                case UIStatePropertyType.RectTransformAnchoredPosition:
                    if (property.rectTransform != null)
                    {
                        property.rectTransform.anchoredPosition = property.vector2Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.rectTransform));
                    }
                    break;
                case UIStatePropertyType.RectTransformSizeDelta:
                    if (property.rectTransform != null)
                    {
                        property.rectTransform.sizeDelta = property.vector2Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.rectTransform));
                    }
                    break;
                case UIStatePropertyType.TransformLocalScale:
                    if (property.transform != null)
                    {
                        property.transform.localScale = property.vector3Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.transform));
                    }
                    break;
                case UIStatePropertyType.TransformLocalEulerAngles:
                    if (property.transform != null)
                    {
                        property.transform.localEulerAngles = property.vector3Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType, nameof(property.transform));
                    }
                    break;
            }
        }

        private void WarnMissingTarget(string stateName, UIStatePropertyType propertyType, string fieldName)
        {
            Debug.LogWarning($"[UIState] {name}.{stateName} 状态项 {propertyType} 缺少目标引用: {fieldName}");
        }
    }
}
