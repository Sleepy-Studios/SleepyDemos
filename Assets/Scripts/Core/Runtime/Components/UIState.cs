using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

    [DisallowMultipleComponent]
    public sealed class UIState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private List<UIStateInfo> states = new List<UIStateInfo>
        {
            new UIStateInfo { stateName = "Normal" },
            new UIStateInfo { stateName = "Hover" },
            new UIStateInfo { stateName = "Focused" },
            new UIStateInfo { stateName = "Pressed" },
            new UIStateInfo { stateName = "Disabled" }
        };

        [SerializeField] private string currentStateId = "Normal";

        private readonly Dictionary<string, UIStateInfo> stateMap = new Dictionary<string, UIStateInfo>();
        private Selectable selectable;
        private bool hovered;
        private bool pressed;
        private int pressedPointerId;
        private float submitUntil;
        private string appliedBusiness;
        private string appliedInteraction;

        /// 当前业务状态；Selected、On、Off 不被自动交互覆盖。
        public string CurrentStateId => currentStateId;
        /// 同对象 Selectable 的当前交互状态；没有 Selectable 时为 Normal。
        public string InteractionState => appliedInteraction;
        public IReadOnlyList<UIStateInfo> States => states;

        /// <summary>动态创建的 UI 显式配置状态；保存的 Prefab 继续使用 Inspector 配置。</summary>
        /// <param name="values">状态定义，每项只修改其拥有的表现属性。</param>
        public void ConfigureStates(IEnumerable<UIStateInfo> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            states = new List<UIStateInfo>(values);
            RebuildStateMap();
            Refresh(force: true);
        }

        private void Awake()
        {
            RebuildStateMap();
            selectable = GetComponent<Selectable>();
        }

        private void OnEnable()
        {
            selectable = GetComponent<Selectable>();
            if (selectable != null)
            {
                InputDeviceState.Initialize();
                InputDeviceState.Changed += OnDeviceChanged;
                Refresh(force: true);
            }
        }

        private void Start()
        {
            // 纯业务状态沿用原首次初始化时点；其激活属性可能控制自身，不能在 OnEnable 重入。
            if (selectable == null) Refresh(force: true);
        }

        private void OnDisable()
        {
            InputDeviceState.Changed -= OnDeviceChanged;
            hovered = pressed = false;
            submitUntil = 0;
            if (selectable != null)
            {
                ApplyCombinedState("Normal", force: true);
            }
        }

        // 原生 interactable / CanvasGroup 没有统一变更事件，只在状态变化时写表现。
        private void LateUpdate()
        {
            if (selectable != null) Refresh();
        }

        private void OnValidate()
        {
            stateMap.Clear();
            if (states != null && states.Count > 0)
            {
                return;
            }

            states = new List<UIStateInfo>
            {
                new UIStateInfo { stateName = "Normal" },
                new UIStateInfo { stateName = "Hover" },
                new UIStateInfo { stateName = "Focused" },
                new UIStateInfo { stateName = "Pressed" },
                new UIStateInfo { stateName = "Disabled" }
            };
        }

        /// <summary>应用业务状态，并保留当前有效的原生交互反馈。</summary>
        /// <param name="stateName">制作状态名；Normal 清除 Selected、On 等业务增量。</param>
        public void SetState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            var state = GetState(stateName);
            if (state != null && state.properties != null)
            {
                currentStateId = stateName;
                Refresh(force: true);
            }
        }

        private void OnDeviceChanged() => Refresh();

        private void Refresh(bool force = false)
        {
            if (selectable is Toggle toggle)
            {
                var state = GetState(toggle.isOn ? "On" : "Off");
                if (state != null)
                {
                    currentStateId = state.stateName;
                }
            }
            string interaction = "Normal";
            if (selectable != null && isActiveAndEnabled)
            {
                bool focus = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
                bool touch = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
                bool keyboardFocus = InputDeviceState.ActiveKind == InputDeviceKind.Gamepad || InputDeviceState.ActiveDevice is Keyboard;
                interaction = !selectable.IsInteractable() ? "Disabled" : pressed ? "Pressed" : touch ? "Normal"
                    : Time.unscaledTime < submitUntil ? "Pressed"
                    : focus && keyboardFocus ? "Focused" : hovered && InputDeviceState.ActiveKind == InputDeviceKind.KeyboardMouse ? "Hover" : "Normal";
            }
            ApplyCombinedState(interaction, force);
        }

        private void ApplyCombinedState(string interaction, bool force = false)
        {
            if (!force && appliedBusiness == currentStateId && appliedInteraction == interaction) return;
            appliedBusiness = currentStateId;
            appliedInteraction = interaction;
            if (selectable == null)
            {
                ApplyState(currentStateId);
                return;
            }
            // 一个制作表按确定顺序应用：基底恢复退出属性，业务保留选择，交互覆盖本次反馈。
            ApplyState("Normal");
            if (currentStateId != "Normal") ApplyState(currentStateId);
            if (interaction != "Normal") ApplyState(interaction);
        }

        private static void RecordPointer(PointerEventData value)
        {
            if (value is ExtendedPointerEventData extended) InputDeviceState.Notify(extended.device);
            else if (value.pointerId >= 0) InputDeviceState.NotifyTouch();
            else InputDeviceState.Notify(Mouse.current);
        }

        /// <summary>悬停只反映 EventSystem 命中，不凭静止鼠标切换设备。</summary>
        /// <param name="value">原生指针事件。</param>
        public void OnPointerEnter(PointerEventData value) { hovered = true; Refresh(); }
        /// <summary>指针离开后恢复仍有效的业务态与焦点。</summary>
        /// <param name="value">原生指针事件。</param>
        public void OnPointerExit(PointerEventData value) { hovered = false; Refresh(); }
        /// <summary>仅左键可进入按下表现，不交付业务点击。</summary>
        /// <param name="value">原生指针事件。</param>
        public void OnPointerDown(PointerEventData value)
        {
            if (selectable == null || value.button != PointerEventData.InputButton.Left || pressed) return;
            RecordPointer(value);
            pressed = selectable.IsInteractable();
            pressedPointerId = value.pointerId;
            Refresh();
        }
        /// <summary>释放按下表现，恢复当前业务态。</summary>
        /// <param name="value">原生指针事件。</param>
        public void OnPointerUp(PointerEventData value)
        {
            if (!pressed || value.pointerId != pressedPointerId)
            {
                return;
            }
            pressed = false;
            Refresh();
        }
        /// <summary>原生焦点改变只刷新表现。</summary>
        /// <param name="value">EventSystem 选择事件。</param>
        public void OnSelect(BaseEventData value) => Refresh();
        /// <summary>原生焦点离开只刷新表现。</summary>
        /// <param name="value">EventSystem 取消选择事件。</param>
        public void OnDeselect(BaseEventData value) => Refresh();
        /// <summary>确认键使用未缩放时间短暂反馈，不替 Button 再提交一次。</summary>
        /// <param name="value">EventSystem 确认事件。</param>
        public void OnSubmit(BaseEventData value)
        {
            if (selectable != null && selectable.IsInteractable())
                submitUntil = Time.unscaledTime + Mathf.Max(0.08f, selectable.colors.fadeDuration);
            Refresh();
        }

        /// <summary>直接应用指定状态的属性，不改变持有的业务状态。</summary>
        /// <param name="stateName">制作状态名。</param>
        /// <returns>存在可应用的状态时返回 true。</returns>
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

        /// <summary>查询制作状态定义。</summary>
        /// <param name="stateName">制作状态名。</param>
        /// <returns>对应状态；未配置时返回 null。</returns>
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
