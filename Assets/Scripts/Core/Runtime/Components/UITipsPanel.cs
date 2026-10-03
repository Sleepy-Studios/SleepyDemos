using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public enum CommonTipsType { Warning, Success, Notice }
    /// Tips 文本与背景适配；说明使用纵向滚动，消息使用单行自动滚动。
    [DisallowMultipleComponent]
    public sealed class UITipsPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform body;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private TMPAutoFitLayoutElement titleFit;
        [SerializeField] private TMPAutoFitLayoutElement contentFit;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LayoutElement viewportLayout;
        [SerializeField] private VerticalLayoutGroup bodyLayout;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite noticeIcon;
        [SerializeField] private float iconSpace = 48;
        [SerializeField] private CanvasGroup messageDetails;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image messageBackground;
        [SerializeField] private Outline messageOutline;
        [SerializeField] private TMPAutoScrollEnableBehaviour messageScroll;
        private float maximumWidth = 600;
        private int baseLeftPadding = -1;
        /// 主体矩形，箭头独立于其布局。
        public RectTransform Body => body;
        /// 当前正文可滚动距离。
        public float ScrollDistance => scroll == null ? 0 : Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
        internal Button CloseButton => closeButton;
        internal TMPAutoScrollEnableBehaviour MessageScroll => messageScroll;

        /// <summary>更新内容；下一次 RefreshLayout 重新测量并从顶部显示。</summary>
        /// <param name="content">正文，null 视为空。</param>
        /// <param name="title">可选标题，空白隐藏。</param>
        /// <param name="maxWidth">主体最大宽度，Canvas 单位，默认 600。</param>
        /// <param name="sprite">可选状态图标，null 隐藏。</param>
        public void SetContent(string content, string title = null, float maxWidth = 600, Sprite sprite = null)
        {
            contentText.text = content ?? string.Empty;
            titleText.text = title ?? string.Empty;
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
            maximumWidth = Mathf.Max(1, maxWidth);
            if (icon != null) { icon.sprite = sprite; icon.gameObject.SetActive(sprite != null); }
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
            }
        }

        /// <summary>更新三种状态的消息，图标来自正式 Prefab 的直接引用。</summary>
        /// <param name="content">提示正文。</param>
        /// <param name="type">红色警告、绿色成功或橙色提醒。</param>
        /// <param name="maxWidth">最大主体宽度，Canvas 单位。</param>
        public void SetMessage(string content, CommonTipsType type, float maxWidth = 1300)
        {
            Sprite sprite = type switch
            {
                CommonTipsType.Warning => warningIcon,
                CommonTipsType.Success => successIcon,
                CommonTipsType.Notice => noticeIcon,
                _ => throw new System.ArgumentOutOfRangeException(nameof(type))
            };
            content = (content ?? string.Empty).Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
            SetContent(content, null, maxWidth, sprite);
            var options = TMPAutoScrollOptions.Default;
            options.Loop = false;
            options.UseUnscaledTime = true;
            messageScroll.SetOptions(options);
            // 复用单色符号，着色使用统一语义色；警告三角本身已带红色与白色叹号。
            if (icon != null) icon.color = type == CommonTipsType.Warning ? Color.white
                : type == CommonTipsType.Success ? ColorUtil.Colors.Success : ColorUtil.Colors.Notice;
            if (messageBackground != null)
            {
                Color accent = type == CommonTipsType.Warning ? ColorUtil.Colors.Warning
                    : type == CommonTipsType.Success ? ColorUtil.Colors.Success : ColorUtil.Colors.Notice;
                messageBackground.color = Color.Lerp(new Color(0.035f, 0.045f, 0.055f), accent, .13f);
                if (messageOutline != null) messageOutline.effectColor = Color.Lerp(messageBackground.color, accent, .5f);
            }
        }

        // 堆叠后层只保留背景，隐藏内容不改变用于展开的测量尺寸。
        internal void SetMessagePresentation(float opacity, bool interactive)
        {
            if (messageDetails != null)
            {
                messageDetails.alpha = opacity;
                messageDetails.blocksRaycasts = interactive;
                messageDetails.interactable = interactive;
            }
            if (closeButton != null) closeButton.interactable = interactive;
            // 普通说明保留纵向阅读；消息滚轮由外层列表处理。
            if (scroll != null) scroll.enabled = interactive && scroll.vertical;
        }

        /// <summary>按可用区域刷新背景与滚动高度，返回最终主体尺寸。</summary>
        /// <param name="availableSize">扣除安全边距后的最大尺寸。</param>
        public Vector2 RefreshLayout(Vector2 availableSize)
        {
            float iconWidth = icon != null && icon.gameObject.activeSelf ? iconSpace : 0;
            var padding = bodyLayout.padding;
            if (baseLeftPadding < 0) baseLeftPadding = padding.left;
            padding.left = baseLeftPadding + Mathf.CeilToInt(iconWidth);
            float inset = padding.horizontal;
            float widthLimit = Mathf.Max(1, Mathf.Min(maximumWidth, availableSize.x));
            float textLimit = Mathf.Max(1, widthLimit - inset);
            float contentWidth = contentText.GetPreferredValues(contentText.text, Mathf.Infinity, Mathf.Infinity).x;
            float titleWidth = titleText.gameObject.activeSelf ? titleText.GetPreferredValues(titleText.text, Mathf.Infinity, Mathf.Infinity).x : 0;
            float textWidth = Mathf.Min(textLimit, Mathf.Max(1, Mathf.Max(contentWidth, titleWidth)));
            float width = Mathf.Min(widthLimit, textWidth + inset);
            body.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (messageScroll != null)
            {
                // 消息文字不参与纵向 Content 布局，保持单行字号和稳定裁剪区域。
                var viewport = (RectTransform)messageScroll.transform;
                float lineHeight = Mathf.Max(contentText.fontSize, contentText.GetPreferredValues(contentText.text, Mathf.Infinity, Mathf.Infinity).y);
                float height = Mathf.Min(availableSize.y, padding.vertical + Mathf.Max(lineHeight, iconWidth > 0 ? icon.rectTransform.rect.height : 0));
                viewportLayout.preferredWidth = textWidth;
                viewportLayout.preferredHeight = Mathf.Max(1, height - padding.vertical);
                viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
                viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportLayout.preferredHeight);
                body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyLayout.transform as RectTransform);
                return body.rect.size;
            }
            titleFit.SetMaxWidth(textWidth, false);
            contentFit.SetMaxWidth(textWidth, false);
            // 正文使用独立 Content 布局，不把滚动高度上限交给 TMP 缩字。
            titleFit.RefreshLayout();
            contentFit.RefreshLayout();
            scroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float contentHeight = contentText.GetPreferredValues(contentText.text, textWidth, Mathf.Infinity).y;
            float titleHeight = titleText.gameObject.activeSelf ? titleText.GetPreferredValues(titleText.text, textWidth, Mathf.Infinity).y + bodyLayout.spacing : 0;
            float maxHeight = Mathf.Max(1, availableSize.y);
            float viewHeight = Mathf.Max(1, Mathf.Min(contentHeight, maxHeight - padding.vertical - titleHeight));
            // 标题极长时也受安全高度限制；正文优先保留至少一行可见空间。
            if (titleText.gameObject.activeSelf)
            {
                var element = titleText.GetComponent<LayoutElement>();
                element.preferredHeight = Mathf.Min(Mathf.Max(1, titleHeight - bodyLayout.spacing), Mathf.Max(1, maxHeight - padding.vertical - contentText.fontSize - bodyLayout.spacing));
                titleText.overflowMode = TextOverflowModes.Ellipsis;
                titleHeight = element.preferredHeight + bodyLayout.spacing;
                viewHeight = Mathf.Max(1, Mathf.Min(contentHeight, maxHeight - padding.vertical - titleHeight));
            }
            if (iconWidth > 0) viewHeight = Mathf.Max(viewHeight, Mathf.Min(icon.rectTransform.rect.height, Mathf.Max(1, maxHeight - padding.vertical - titleHeight)));
            viewportLayout.preferredHeight = viewHeight;
            viewportLayout.preferredWidth = textWidth;
            contentText.GetComponent<LayoutElement>().preferredWidth = textWidth;
            contentText.GetComponent<LayoutElement>().preferredHeight = contentHeight;
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(maxHeight, padding.vertical + titleHeight + viewHeight));
            // 消息卡片的内容组可与背景分离；重建实际布局根，不能停在没有布局控制器的背景节点。
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyLayout.transform as RectTransform);
            scroll.vertical = contentHeight > viewHeight + .1f;
            if (!scroll.vertical) scroll.verticalNormalizedPosition = 1;
            return body.rect.size;
        }
    }
}
