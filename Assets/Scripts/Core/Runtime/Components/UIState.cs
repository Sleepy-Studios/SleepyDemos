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
        public UnityEngine.Object target;
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
                    var gameObject = ResolveGameObject(property.target);
                    if (gameObject != null)
                    {
                        gameObject.SetActive(property.boolValue);
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.GraphicColor:
                    var graphic = ResolveComponent<Graphic>(property.target);
                    if (graphic != null)
                    {
                        graphic.color = property.colorValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.CanvasGroupAlpha:
                    var alphaGroup = ResolveComponent<CanvasGroup>(property.target);
                    if (alphaGroup != null)
                    {
                        alphaGroup.alpha = property.floatValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.CanvasGroupInteractable:
                    var interactableGroup = ResolveComponent<CanvasGroup>(property.target);
                    if (interactableGroup != null)
                    {
                        interactableGroup.interactable = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.CanvasGroupBlocksRaycasts:
                    var raycastGroup = ResolveComponent<CanvasGroup>(property.target);
                    if (raycastGroup != null)
                    {
                        raycastGroup.blocksRaycasts = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.SelectableInteractable:
                    var selectable = ResolveComponent<Selectable>(property.target);
                    if (selectable != null)
                    {
                        selectable.interactable = property.boolValue;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.TextContent:
                    var text = ResolveComponent<Text>(property.target);
                    if (text != null)
                    {
                        text.text = property.stringValue ?? string.Empty;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.RectTransformAnchoredPosition:
                    var anchoredRect = ResolveComponent<RectTransform>(property.target);
                    if (anchoredRect != null)
                    {
                        anchoredRect.anchoredPosition = property.vector2Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.RectTransformSizeDelta:
                    var sizeRect = ResolveComponent<RectTransform>(property.target);
                    if (sizeRect != null)
                    {
                        sizeRect.sizeDelta = property.vector2Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.TransformLocalScale:
                    var scaleTransform = ResolveTransform(property.target);
                    if (scaleTransform != null)
                    {
                        scaleTransform.localScale = property.vector3Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
                case UIStatePropertyType.TransformLocalEulerAngles:
                    var rotationTransform = ResolveTransform(property.target);
                    if (rotationTransform != null)
                    {
                        rotationTransform.localEulerAngles = property.vector3Value;
                    }
                    else
                    {
                        WarnMissingTarget(stateName, property.propertyType);
                    }
                    break;
            }
        }

        private static GameObject ResolveGameObject(UnityEngine.Object target)
        {
            if (target is GameObject gameObject)
            {
                return gameObject;
            }

            return target is Component component ? component.gameObject : null;
        }

        private static T ResolveComponent<T>(UnityEngine.Object target) where T : Component
        {
            if (target is T typedComponent)
            {
                return typedComponent;
            }

            if (target is GameObject gameObject)
            {
                return gameObject.GetComponent<T>();
            }

            return target is Component component ? component.GetComponent<T>() : null;
        }

        private static Transform ResolveTransform(UnityEngine.Object target)
        {
            if (target is Transform transform)
            {
                return transform;
            }

            if (target is GameObject gameObject)
            {
                return gameObject.transform;
            }

            return target is Component component ? component.transform : null;
        }

        private void WarnMissingTarget(string stateName, UIStatePropertyType propertyType)
        {
            Debug.LogWarning($"[UIState] {name}.{stateName} 状态项 {propertyType} 缺少可用目标: {nameof(UIStateProperty.target)}");
        }
    }
}
