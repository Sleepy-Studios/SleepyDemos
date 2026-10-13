using System;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
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

        // 回退键帽临时接管宽度；切回真实图标或重新配置时恢复每个槽的作者值。
        private sealed class GlyphLayout
        {
            public readonly Image image;
            public readonly HorizontalLayoutGroup layout;
            public readonly HorizontalLayoutGroup slotLayout;
            public readonly LayoutElement element;
            public readonly Image.Type imageType;
            public readonly float pixelsPerUnitMultiplier;
            private readonly bool fillCenter;
            private readonly bool preserveAspect;
            private readonly float minWidth;
            private readonly float preferredWidth;
            private readonly float flexibleHeight;
            private readonly bool slotForceExpandHeight;
            private readonly int paddingLeft;
            private readonly int paddingRight;

            public GlyphLayout(Image value, GameObject root)
            {
                image = value;
                layout = value.GetComponent<HorizontalLayoutGroup>();
                slotLayout = root != null && value.transform.parent == root.transform
                    ? root.GetComponent<HorizontalLayoutGroup>() : null;
                element = value.GetComponent<LayoutElement>();
                imageType = value.type;
                pixelsPerUnitMultiplier = value.pixelsPerUnitMultiplier;
                fillCenter = value.fillCenter;
                preserveAspect = value.preserveAspect;
                minWidth = element != null ? element.minWidth : -1f;
                preferredWidth = element != null ? element.preferredWidth : -1f;
                flexibleHeight = element != null ? element.flexibleHeight : -1f;
                slotForceExpandHeight = slotLayout != null && slotLayout.childForceExpandHeight;
                paddingLeft = layout != null ? layout.padding.left : 0;
                paddingRight = layout != null ? layout.padding.right : 0;
            }

            public void Restore()
            {
                if (image == null)
                {
                    return;
                }
                image.type = imageType;
                image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
                image.fillCenter = fillCenter;
                image.preserveAspect = preserveAspect;
                if (element != null)
                {
                    element.minWidth = minWidth;
                    element.preferredWidth = preferredWidth;
                    element.flexibleHeight = flexibleHeight;
                }
                if (slotLayout != null)
                {
                    slotLayout.childForceExpandHeight = slotForceExpandHeight;
                }
                if (layout != null)
                {
                    layout.padding.left = paddingLeft;
                    layout.padding.right = paddingRight;
                    LayoutRebuilder.MarkLayoutForRebuild(image.rectTransform);
                }
            }
        }

        [SerializeField] private InputActionReference action;
        [SerializeField] private InputGlyphCatalog catalog;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string caption;
        [SerializeField] private string captionKey;
        [SerializeField] private string compositePart;
        [SerializeField] private GlyphSlot[] glyphSlots = Array.Empty<GlyphSlot>();
        [SerializeField] private string uiAction;
        [SerializeField, Tooltip("文案在前的提示；纯文本回退也保持相同阅读顺序。")]
        private bool captionFirst;
        [SerializeField, Tooltip("触控操作按钮只显示动作名，设备按键由独立提示区提供。")]
        private bool captionOnly;
        private InputAction runtimeAction;
        private bool bindingsVisible = true;
        private InputDeviceKind? displayDevice;
        private int? displayBindingIndex;
        private object[] captionArguments = Array.Empty<object>();
        private GlyphLayout[] glyphLayouts;
        private GamepadStyle? displayGamepadStyle;
        private LayoutElement promptWidthElement;
        private int promptLayoutPriority;
        /// 已绑定会话动作优先于 Inspector 的源资产引用。
        public InputAction Action => runtimeAction ?? action?.action;

        /// <summary>按显示生命周期配置公共文案键，不订阅语言变更。</summary>
        /// <param name="key">正式文案键；为空保留原作者文案。</param>
        /// <param name="args">完整句式的动态参数；复制数组，不订阅语言变化。</param>
        public void SetCaptionKey(string key, params object[] args)
        {
            captionKey = key;
            captionArguments = args == null || args.Length == 0 ? Array.Empty<object>() : (object[])args.Clone();
            Refresh();
        }

        /// <summary>保留作者的专属动作说明，键位仍由公共组件解析。</summary>
        /// <param name="value">完整动作说明，不包含手写物理键名。</param>
        public void SetCaption(string value)
        {
            caption = value;
            captionKey = null;
            captionArguments = Array.Empty<object>();
            Refresh();
        }

        /// <summary>操作说明页显式选择预览设备，不改变真实设备、触控布局或输入。</summary>
        /// <param name="kind">为空跟随最近实际操作设备。</param>
        public void SetDisplayDevice(InputDeviceKind? kind)
        {
            displayDevice = kind;
            Refresh();
        }

        /// <summary>候选页局部预览图标风格；为空使用已经应用的公共偏好。</summary>
        /// <param name="style">候选风格，不改变真实设备或生效设置。</param>
        public void SetDisplayGamepadStyle(GamepadStyle? style)
        {
            displayGamepadStyle = style;
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
        /// <param name="menuAction">Navigate、Submit 或 Cancel 时绑定当前 UI Module 的固定动作。</param>
        /// <param name="part">可选复合子绑定名。</param>
        /// <param name="authorCaption">可选的 Demo 专属完整动作说明。</param>
        public void Configure(InputActionReference sourceAction, InputGlyphCatalog glyphCatalog, TMP_Text captionLabel,
            GlyphSlot[] slots, string key, bool onlyCaption = false, string menuAction = null,
            string part = null, string authorCaption = null)
        {
            RestoreGlyphLayouts();
            glyphLayouts = null;
            action = sourceAction;
            catalog = glyphCatalog;
            label = captionLabel;
            glyphSlots = slots == null ? Array.Empty<GlyphSlot>() : (GlyphSlot[])slots.Clone();
            if (glyphSlots.Length > 0)
            {
                icon = null;
            }
            captionKey = key;
            captionArguments = Array.Empty<object>();
            captionOnly = onlyCaption;
            uiAction = menuAction;
            compositePart = part;
            if (authorCaption != null)
            {
                caption = authorCaption;
            }
            Refresh();
        }

        /// <summary>将提示绑定到业务会话副本，而不是未生效的源资产。</summary>
        /// <param name="value">实际动作。</param>
        public void Bind(InputAction value)
        {
            displayBindingIndex = null;
            runtimeAction = value;
            Refresh();
        }

        /// <summary>显示设置行所选择的精确绑定，不把同动作的其他别名当成当前绑定。</summary>
        /// <param name="value">当前会话动作。</param>
        /// <param name="bindingIndex">单绑定或复合子绑定在该动作内的索引。</param>
        public void BindBinding(InputAction value, int bindingIndex)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (bindingIndex < 0 || bindingIndex >= value.bindings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            }
            if (value.bindings[bindingIndex].isComposite)
            {
                throw new ArgumentException("精确提示只能选择单绑定或复合子绑定。", nameof(bindingIndex));
            }
            compositePart = null;
            displayBindingIndex = bindingIndex;
            runtimeAction = value;
            Refresh();
        }

        /// <summary>绑定复合动作的一项，用于独立操作行；不克隆动作或重新建立输入。</summary>
        /// <param name="value">当前会话动作。</param>
        /// <param name="part">复合子绑定名；为空恢复完整组合。</param>
        public void BindPart(InputAction value, string part)
        {
            compositePart = part;
            Bind(value);
        }

        /// <summary>按源动作GUID绑定实际会话副本，避免页面重复解析提示动作。</summary>
        /// <param name="asset">本次输入会话的动作资产；为空时恢复源引用。</param>
        public void BindAsset(InputActionAsset asset) => Bind(uiAction is "Navigate" or "Submit" or "Cancel"
            ? ResolveMenuAction() : asset != null && action != null ? asset.FindAction(action.action.id) : null);

        private InputAction ResolveMenuAction()
        {
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            return uiAction switch
            {
                "Navigate" => module?.move?.action,
                "Submit" => module?.submit?.action,
                "Cancel" => module?.cancel?.action,
                _ => null
            };
        }
        private void OnEnable()
        {
            InputDeviceState.Initialize();
            if (uiAction is "Navigate" or "Submit" or "Cancel")
            {
                runtimeAction = ResolveMenuAction();
            }
            InputDeviceState.Changed += Refresh;
            GameSettingsService.Changed += OnSettingsChanged;
            InputSystem.onActionChange += OnActionChange;
            Refresh();
        }
        private void OnDisable()
        {
            InputDeviceState.Changed -= Refresh;
            GameSettingsService.Changed -= OnSettingsChanged;
            InputSystem.onActionChange -= OnActionChange;
            RestoreGlyphLayouts();
            glyphLayouts = null;
        }
        private void OnActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                Refresh();
            }
        }

        private void OnSettingsChanged(GameSettingsSnapshot value) => Refresh();

        private void OnTransformParentChanged()
        {
            UpdateWidthLayout();
        }

        private void UpdateWidthLayout()
        {
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter == null || GetComponent<Selectable>() != null)
            {
                return;
            }
            var parentLayout = transform.parent != null ? transform.parent.GetComponent<LayoutGroup>() : null;
            var element = GetComponent<LayoutElement>();
            bool parentControlsWidth = parentLayout != null && parentLayout.isActiveAndEnabled
                && (element == null || !element.ignoreLayout)
                && (parentLayout is not HorizontalOrVerticalLayoutGroup row || row.childControlWidth);
            // 父布局接管宽度时由本组件的 LayoutGroup 提供 preferredWidth，避免两处驱动同一尺寸。
            fitter.horizontalFit = parentControlsWidth ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
        }

        /// 按当前实际动作刷新提示；语言只在显示或重新配置时读取。
        public void Refresh()
        {
            RestoreGlyphLayouts();
            UpdateWidthLayout();
            string currentCaption = caption;
            if (!string.IsNullOrEmpty(captionKey) && LanguageService.IsInitialized)
            {
                if (label != null)
                {
                    LanguageService.SetText(label, captionKey, args: captionArguments);
                }
                currentCaption = captionArguments.Length > 0 ? LanguageService.Format(captionKey, captionArguments) : LanguageService.Get(captionKey);
            }
            var kind = displayDevice ?? InputDeviceState.PromptKind;
            var style = displayGamepadStyle ?? GameSettingsService.CurrentGamepadStyle;
            var parts = InputBindingDisplay.GetParts(Action, out var separator, displayDevice, compositePart, displayBindingIndex);
            if (glyphSlots.Length > 0)
            {
                bool showGlyphs = bindingsVisible && !captionOnly && parts.Count > 0 && parts.Count <= glyphSlots.Length;
                if (showGlyphs)
                {
                    foreach (var part in parts)
                    {
                        if (FindGlyph(kind, style, part.ControlPath, part.Text) == null && (catalog == null || catalog.KeyboardBlank == null))
                        {
                            showGlyphs = false;
                            break;
                        }
                    }
                }
                // 无图标和空白键帽或槽不足时改用完整文本，避免截断组合键。
                if (glyphSlots[0].root != null && glyphSlots[0].root.transform.parent != null
                    && glyphSlots[0].root.transform.parent != transform)
                {
                    glyphSlots[0].root.transform.parent.gameObject.SetActive(showGlyphs);
                }
                for (int i = 0; i < glyphSlots.Length; i++)
                {
                    var slot = glyphSlots[i];
                    bool visible = showGlyphs && i < parts.Count;
                    if (slot.root != null)
                    {
                        slot.root.SetActive(visible);
                    }
                    if (!visible)
                    {
                        continue;
                    }
                    var part = parts[i];
                    var glyph = FindGlyph(kind, style, part.ControlPath, part.Text);
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
                    UpdateGlyphLayout(i, slot, glyph == null);
                    if (slot.separator != null)
                    {
                        slot.separator.text = i == 0 ? string.Empty : separator;
                        slot.separator.gameObject.SetActive(i > 0);
                    }
                }
                if (label != null)
                {
                    label.text = bindingsVisible && !captionOnly && !showGlyphs
                        ? FormatBindingCaption(InputBindingDisplay.Get(Action, out _, displayDevice, compositePart, displayBindingIndex), currentCaption)
                        : currentCaption;
                }
                return;
            }

            string path = null;
            string key = captionOnly || !bindingsVisible ? string.Empty : InputBindingDisplay.Get(Action, out path, displayDevice, compositePart, displayBindingIndex);
            var sprite = bindingsVisible && !captionOnly ? FindGlyph(kind, style, path, key) : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.gameObject.SetActive(sprite != null);
            }
            if (label != null)
            {
                label.text = sprite != null || string.IsNullOrEmpty(key) ? currentCaption : FormatBindingCaption(key, currentCaption);
            }
        }

        private void RestoreGlyphLayouts()
        {
            if (promptWidthElement != null)
            {
                promptWidthElement.layoutPriority = promptLayoutPriority;
                promptWidthElement = null;
            }
            if (glyphLayouts == null)
            {
                return;
            }
            foreach (var layout in glyphLayouts)
            {
                layout?.Restore();
            }
        }

        private void UpdateGlyphLayout(int index, GlyphSlot slot, bool blank)
        {
            if (slot.icon == null)
            {
                return;
            }
            glyphLayouts ??= new GlyphLayout[glyphSlots.Length];
            var authored = glyphLayouts[index] ??= new GlyphLayout(slot.icon, slot.root);
            if (!blank)
            {
                slot.icon.type = Image.Type.Simple;
                slot.icon.preserveAspect = true;
                return;
            }
            var sprite = slot.icon.sprite;
            if (sprite == null || sprite.border == Vector4.zero)
            {
                return;
            }
            slot.icon.type = Image.Type.Sliced;
            slot.icon.fillCenter = false;
            slot.icon.preserveAspect = false;
            // Icon 的内部键名布局也会贡献 flexibleHeight；空帽只使用作者的固定槽高。
            if (authored.element != null)
            {
                authored.element.flexibleHeight = 0f;
            }
            if (authored.slotLayout != null)
            {
                authored.slotLayout.childForceExpandHeight = false;
            }
            // 原方形图标风格保持整张空帽的显示高度；已制作的九宫格风格保留原边线粗细。
            float height = authored.element != null && authored.element.preferredHeight > 0f
                ? Mathf.Max(authored.element.minHeight, authored.element.preferredHeight)
                : authored.element != null && authored.element.minHeight > 0f
                    ? authored.element.minHeight : slot.icon.rectTransform.rect.height;
            if (authored.imageType != Image.Type.Sliced && height > 0f)
            {
                slot.icon.pixelsPerUnitMultiplier = sprite.rect.height / height / slot.icon.pixelsPerUnit;
            }
            if (slot.keyText == null || authored.layout == null || authored.element == null)
            {
                return;
            }
            float pixelsPerUnit = slot.icon.pixelsPerUnit * slot.icon.pixelsPerUnitMultiplier;
            authored.layout.padding.left = Mathf.Max(authored.layout.padding.left, Mathf.CeilToInt(sprite.border.x / pixelsPerUnit));
            authored.layout.padding.right = Mathf.Max(authored.layout.padding.right, Mathf.CeilToInt(sprite.border.z / pixelsPerUnit));
            // 原生布局按最新TMP字形计算宽度，不能用刷新时的旧测量覆盖其preferredWidth。
            authored.element.minWidth = Mathf.Max(authored.element.minWidth, authored.element.preferredWidth);
            authored.element.preferredWidth = -1f;
            AllowPromptContentWidth();
            LayoutRebuilder.MarkLayoutForRebuild(slot.icon.rectTransform);
        }

        private void AllowPromptContentWidth()
        {
            if (promptWidthElement != null)
            {
                return;
            }
            var element = GetComponent<LayoutElement>();
            var layout = GetComponent<LayoutGroup>();
            if (element == null || layout == null)
            {
                return;
            }
            promptWidthElement = element;
            promptLayoutPriority = element.layoutPriority;
            // 固定作者宽度仍是下限，原生内容宽度可以参与取max，避免长键帽被父层压缩。
            element.layoutPriority = Mathf.Min(element.layoutPriority, layout.layoutPriority);
        }

        private string FormatBindingCaption(string binding, string currentCaption)
        {
            if (string.IsNullOrEmpty(binding))
            {
                return currentCaption;
            }
            if (string.IsNullOrEmpty(currentCaption))
            {
                return binding;
            }
            return captionFirst ? currentCaption + " " + binding : binding + " " + currentCaption;
        }

        private Sprite FindGlyph(InputDeviceKind kind, GamepadStyle style, string path, string text)
        {
            // 字母键按当前键盘布局的实际键名选图，不能把 QWERTZ 的显示键名画成物理位置字母。
            if (kind == InputDeviceKind.KeyboardMouse && path?.Length == 1 && char.IsLetterOrDigit(path[0])
                && text?.Length == 1)
            {
                path = text.ToLowerInvariant();
            }
            return catalog != null ? catalog.Get(kind, style, path) : null;
        }
    }
}
