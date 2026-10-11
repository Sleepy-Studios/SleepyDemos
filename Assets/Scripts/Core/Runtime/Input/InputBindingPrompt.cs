using System;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Core.Runtime.Inputs
{
    /// Inspector 选择动作与图标目录；设备/绑定变化时更新，业务可绑定会话动作副本。
    public sealed class InputBindingPrompt : MonoBehaviour
    {
        /// 正式 Prefab 提供键帽槽，支持组合键而不在运行时生成 UI。
        [Serializable]
        public struct GlyphSlot
        {
            public GameObject root;
            public Image icon;
            public TMP_Text keyText;
            public TMP_Text separator;
        }

        [SerializeField] private InputActionReference action;
        [SerializeField] private InputGlyphCatalog catalog;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string caption;
        [SerializeField] private string captionKey;
        [SerializeField] private GlyphSlot[] glyphSlots = Array.Empty<GlyphSlot>();
        [SerializeField] private string uiAction;
        [SerializeField, Tooltip("触控操作按钮只显示动作名，设备按键由独立提示区提供。")]
        private bool captionOnly;
        private InputAction runtimeAction;
        private bool bindingsVisible = true;
        /// 已绑定会话动作优先于 Inspector 的源资产引用。
        public InputAction Action => runtimeAction ?? action?.action;

        /// <summary>按显示生命周期配置公共文案键，不订阅语言变更。</summary>
        /// <param name="key">正式文案键；为空保留原作者文案。</param>
        public void SetCaptionKey(string key)
        {
            captionKey = key;
            Refresh();
        }

        /// <summary>辅助偏好收起物理按键提示，仍保留按钮必需的动作说明。</summary>
        /// <param name="visible">是否显示实际物理绑定，触控按钮继续仅显示文案。</param>
        public void SetBindingsVisible(bool visible)
        {
            bindingsVisible = visible;
            Refresh();
        }

        /// <summary>制作公共提示 Prefab 的显式配置入口，不添加运行时模板兜底。</summary>
        /// <param name="sourceAction">源动作引用，显示前 BindAsset 绑定实际会话。</param>
        /// <param name="glyphCatalog">已加载的正式图标目录。</param>
        /// <param name="captionLabel">独立的动作文案文本。</param>
        /// <param name="slots">制作的键帽组，包含 Image、键名和分隔符。</param>
        /// <param name="key">公共文案键。</param>
        /// <param name="onlyCaption">触控命令按钮只显示动作名。</param>
        /// <param name="menuAction">Submit 或 Cancel 时绑定当前 UI Module 的固定动作。</param>
        public void Configure(InputActionReference sourceAction, InputGlyphCatalog glyphCatalog, TMP_Text captionLabel,
            GlyphSlot[] slots, string key, bool onlyCaption = false, string menuAction = null)
        {
            action = sourceAction;
            catalog = glyphCatalog;
            label = captionLabel;
            glyphSlots = slots == null ? Array.Empty<GlyphSlot>() : (GlyphSlot[])slots.Clone();
            captionKey = key;
            captionOnly = onlyCaption;
            uiAction = menuAction;
            Refresh();
        }

        /// <summary>将提示绑定到业务会话副本，而不是未生效的源资产。</summary>
        /// <param name="value">实际动作。</param>
        public void Bind(InputAction value)
        {
            runtimeAction = value;
            Refresh();
        }

        /// <summary>按源动作GUID绑定实际会话副本，避免页面重复解析提示动作。</summary>
        /// <param name="asset">本次输入会话的动作资产；为空时恢复源引用。</param>
        public void BindAsset(InputActionAsset asset) => Bind(asset != null && action != null ? asset.FindAction(action.action.id) : null);
        private void OnEnable()
        {
            InputDeviceState.Initialize();
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (uiAction == "Submit")
            {
                runtimeAction = module?.submit?.action;
            }
            if (uiAction == "Cancel")
            {
                runtimeAction = module?.cancel?.action;
            }
            InputDeviceState.Changed += Refresh;
            InputSystem.onActionChange += OnActionChange;
            Refresh();
        }
        private void OnDisable()
        {
            InputDeviceState.Changed -= Refresh;
            InputSystem.onActionChange -= OnActionChange;
        }
        private void OnActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                Refresh();
            }
        }
        /// 按当前实际动作刷新提示；语言只在显示或重新配置时读取。
        public void Refresh()
        {
            string currentCaption = caption;
            if (!string.IsNullOrEmpty(captionKey) && LanguageService.IsInitialized)
            {
                if (label != null)
                {
                    LanguageService.SetText(label, captionKey);
                }
                currentCaption = LanguageService.Get(captionKey);
            }
            var parts = InputBindingDisplay.GetParts(Action, out var separator);
            if (glyphSlots.Length > 0)
            {
                for (int i = 0; i < glyphSlots.Length; i++)
                {
                    var slot = glyphSlots[i];
                    bool visible = bindingsVisible && !captionOnly && parts.Count <= glyphSlots.Length && i < parts.Count;
                    if (slot.root != null)
                    {
                        slot.root.SetActive(visible);
                    }
                    if (!visible)
                    {
                        continue;
                    }
                    var part = parts[i];
                    var glyph = catalog != null ? catalog.Get(InputDeviceState.PromptKind, InputDeviceState.PromptStyle, part.ControlPath) : null;
                    if (slot.icon != null)
                    {
                        slot.icon.sprite = glyph != null ? glyph : catalog != null ? catalog.KeyboardBlank : null;
                        slot.icon.gameObject.SetActive(slot.icon.sprite != null);
                    }
                    if (slot.keyText != null)
                    {
                        slot.keyText.text = glyph == null ? part.Text : string.Empty;
                        slot.keyText.gameObject.SetActive(glyph == null);
                    }
                    if (slot.separator != null)
                    {
                        slot.separator.text = i == 0 ? string.Empty : separator;
                    }
                }
                if (label != null)
                {
                    // 槽数量不足时完整显示实际绑定，不截掉组合键。
                    label.text = bindingsVisible && !captionOnly && parts.Count > glyphSlots.Length
                        ? InputBindingDisplay.Get(Action, out _) + " " + currentCaption : currentCaption;
                }
                return;
            }

            string path = null;
            string key = captionOnly || !bindingsVisible ? string.Empty : InputBindingDisplay.Get(Action, out path);
            var sprite = bindingsVisible && !captionOnly && catalog != null ? catalog.Get(InputDeviceState.PromptKind, InputDeviceState.PromptStyle, path) : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.gameObject.SetActive(sprite != null);
            }
            if (label != null)
            {
                label.text = sprite != null || string.IsNullOrEmpty(key) ? currentCaption : key + " " + currentCaption;
            }
        }
    }
}
