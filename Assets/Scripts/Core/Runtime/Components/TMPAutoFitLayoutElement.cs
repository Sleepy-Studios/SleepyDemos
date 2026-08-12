using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 按文本内容更新 LayoutElement 或 ContentSizeFitter，并在超限时启用换行和自动字号。
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TMPAutoFitLayoutElement : MonoBehaviour, ITextPreprocessor
    {
        private static readonly FieldInfo FontSizeBaseField = typeof(TMP_Text).GetField(
            "m_fontSizeBase",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private enum DriverMode
        {
            Auto,
            ContentSizeFitter,
            LayoutElement
        }

        [Header("尺寸约束")]
        [SerializeField, Min(0f)] private float maxWidth = 300f;
        [SerializeField, Min(0f)] private float maxHeight = 100f;

        [Header("布局驱动")]
        [SerializeField] private DriverMode driverMode = DriverMode.Auto;
        [SerializeField] private RectTransform layoutRoot;

        [Header("超高处理")]
        [SerializeField] private bool enableAutoSizingWhenHeightExceeded = true;
        [SerializeField] private bool wrapBeforeAutoSizing;
        [SerializeField] private TextOverflowModes heightOverflowMode = TextOverflowModes.Overflow;

        private TextMeshProUGUI textMeshPro;
        private LayoutElement layoutElement;
        private ContentSizeFitter contentSizeFitter;
        private ITextPreprocessor previousTextPreprocessor;
        private int lockedWrapIndex = -1;
        private bool isRefreshing;

        private void Awake() => CacheComponents();

        private void OnEnable()
        {
            CacheComponents();
            if (!ReferenceEquals(textMeshPro.textPreprocessor, this))
            {
                previousTextPreprocessor = textMeshPro.textPreprocessor;
                textMeshPro.textPreprocessor = this;
            }

            textMeshPro.RegisterDirtyLayoutCallback(RefreshLayout);
            RefreshLayout();
        }

        private void OnDisable()
        {
            if (textMeshPro == null) return;

            textMeshPro.UnregisterDirtyLayoutCallback(RefreshLayout);
            if (ReferenceEquals(textMeshPro.textPreprocessor, this))
            {
                textMeshPro.textPreprocessor = previousTextPreprocessor;
                previousTextPreprocessor = null;
                lockedWrapIndex = -1;
                RefreshTextMesh();
            }
        }

        private void OnTransformParentChanged() => RefreshLayout();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            CacheComponents();
            RefreshLayout();
        }
#endif

        /// 根据 TMP 当前文本刷新布局尺寸。
        public void RefreshLayout()
        {
            if (isRefreshing || maxWidth <= 0f || maxHeight <= 0f) return;

            CacheComponents();
            if (textMeshPro == null) return;

            isRefreshing = true;
            try
            {
                int previousLockedWrapIndex = lockedWrapIndex;
                lockedWrapIndex = -1;
                bool originalAutoSizing = textMeshPro.enableAutoSizing;
                float designFontSize = GetDesignFontSize();

                textMeshPro.enableAutoSizing = true;
                textMeshPro.fontSize = designFontSize;
                textMeshPro.enableAutoSizing = false;
                textMeshPro.textWrappingMode = TextWrappingModes.NoWrap;
                textMeshPro.overflowMode = TextOverflowModes.Overflow;

                Vector2 singleLineSize = textMeshPro.GetPreferredValues(
                    textMeshPro.text,
                    Mathf.Infinity,
                    Mathf.Infinity);
                bool widthLimited = singleLineSize.x > maxWidth;
                bool heightLimited = false;
                float preferredWidth = singleLineSize.x;
                float preferredHeight = singleLineSize.y;
                Vector2 wrappedSize = Vector2.zero;

                if (widthLimited)
                {
                    textMeshPro.textWrappingMode = TextWrappingModes.Normal;
                    wrappedSize = textMeshPro.GetPreferredValues(textMeshPro.text, maxWidth, Mathf.Infinity);
                    preferredWidth = maxWidth;
                    preferredHeight = Mathf.Min(wrappedSize.y, maxHeight);
                    heightLimited = wrappedSize.y > maxHeight;
                    if (heightLimited) textMeshPro.overflowMode = heightOverflowMode;
                }

                if (enableAutoSizingWhenHeightExceeded && wrapBeforeAutoSizing && heightLimited)
                {
                    lockedWrapIndex = GetFirstWrappedLineStartIndex(wrappedSize.y);
                }

                textMeshPro.havePropertiesChanged = true;
                ApplyLayoutDriver(widthLimited, heightLimited, preferredWidth, preferredHeight);

                RectTransform targetRoot = layoutRoot != null ? layoutRoot : transform.parent as RectTransform;
                if (targetRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(targetRoot);

                bool autoSizing = enableAutoSizingWhenHeightExceeded || originalAutoSizing;
                if (enableAutoSizingWhenHeightExceeded && wrapBeforeAutoSizing) autoSizing = heightLimited;
                textMeshPro.enableAutoSizing = autoSizing;
                if (autoSizing || previousLockedWrapIndex != lockedWrapIndex) textMeshPro.ForceMeshUpdate();
                if (targetRoot != null) LayoutRebuilder.MarkLayoutForRebuild(targetRoot);
            }
            finally
            {
                isRefreshing = false;
            }
        }

        /// <summary>
        /// 保留已有文本预处理结果，并按设计字号的第一个换行位置锁定最终换行。
        /// </summary>
        /// <param name="value">业务设置给 TMP 的原始文本。</param>
        /// <returns>用于 TMP 排版的文本。</returns>
        public string PreprocessText(string value)
        {
            string processed = previousTextPreprocessor != null
                ? previousTextPreprocessor.PreprocessText(value)
                : value;
            if (!wrapBeforeAutoSizing || lockedWrapIndex <= 0 || string.IsNullOrEmpty(processed)
                || lockedWrapIndex >= processed.Length)
            {
                return processed;
            }

            int breakStart = lockedWrapIndex;
            int nextLineStart = lockedWrapIndex;
            while (breakStart > 0 && char.IsWhiteSpace(processed[breakStart - 1])) breakStart--;
            while (nextLineStart < processed.Length && char.IsWhiteSpace(processed[nextLineStart])) nextLineStart++;
            return processed.Remove(breakStart, nextLineStart - breakStart).Insert(breakStart, "\n");
        }

        /// <summary>更新最大宽度，并按需立即刷新。</summary>
        /// <param name="value">新的最大宽度。</param>
        /// <param name="refresh">是否立即刷新布局。</param>
        public void SetMaxWidth(float value, bool refresh = true)
        {
            maxWidth = value;
            if (refresh) RefreshLayout();
        }

        /// <summary>更新最大高度，并按需立即刷新。</summary>
        /// <param name="value">新的最大高度。</param>
        /// <param name="refresh">是否立即刷新布局。</param>
        public void SetMaxHeight(float value, bool refresh = true)
        {
            maxHeight = value;
            if (refresh) RefreshLayout();
        }

        private void ApplyLayoutDriver(bool widthLimited, bool heightLimited, float width, float height)
        {
            if (UsesLayoutElement())
            {
                if (contentSizeFitter != null) contentSizeFitter.enabled = false;
                layoutElement ??= gameObject.GetOrAddComponent<LayoutElement>();
                layoutElement.enabled = true;
                layoutElement.preferredWidth = width;
                layoutElement.preferredHeight = height;
                layoutElement.flexibleWidth = 0f;
                layoutElement.flexibleHeight = 0f;
                return;
            }

            if (layoutElement != null) layoutElement.enabled = false;
            contentSizeFitter ??= gameObject.GetOrAddComponent<ContentSizeFitter>();
            contentSizeFitter.enabled = true;
            contentSizeFitter.horizontalFit = widthLimited
                ? ContentSizeFitter.FitMode.Unconstrained
                : ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = heightLimited
                ? ContentSizeFitter.FitMode.Unconstrained
                : ContentSizeFitter.FitMode.PreferredSize;
            if (widthLimited) textMeshPro.rectTransform.SetWidth(width);
            if (heightLimited) textMeshPro.rectTransform.SetHeight(height);
        }

        private bool UsesLayoutElement()
        {
            if (driverMode == DriverMode.ContentSizeFitter) return false;
            if (driverMode == DriverMode.LayoutElement) return true;
            LayoutGroup parentLayout = transform.parent == null ? null : transform.parent.GetComponent<LayoutGroup>();
            return parentLayout != null && parentLayout.isActiveAndEnabled;
        }

        private void CacheComponents()
        {
            textMeshPro ??= GetComponent<TextMeshProUGUI>();
            layoutElement ??= GetComponent<LayoutElement>();
            contentSizeFitter ??= GetComponent<ContentSizeFitter>();
        }

        private float GetDesignFontSize()
        {
            return FontSizeBaseField?.GetValue(textMeshPro) is float fontSizeBase
                ? fontSizeBase
                : textMeshPro.fontSize;
        }

        private int GetFirstWrappedLineStartIndex(float wrappedHeight)
        {
            string processed = previousTextPreprocessor != null
                ? previousTextPreprocessor.PreprocessText(textMeshPro.text)
                : textMeshPro.text;
            if (string.IsNullOrEmpty(processed) || processed.IndexOf('\n') >= 0 || processed.IndexOf('\r') >= 0)
            {
                return -1;
            }

            RectTransform rectTransform = textMeshPro.rectTransform;
            Vector2 originalSize = rectTransform.rect.size;
            rectTransform.SetWidth(maxWidth);
            rectTransform.SetHeight(Mathf.Max(maxHeight, wrappedHeight));
            TMP_TextInfo textInfo = textMeshPro.GetTextInfo(textMeshPro.text);
            rectTransform.SetSize(originalSize);
            if (textInfo.lineCount <= 1) return -1;

            int firstCharacterIndex = textInfo.lineInfo[1].firstCharacterIndex;
            if (firstCharacterIndex < 0 || firstCharacterIndex >= textInfo.characterCount) return -1;
            int stringIndex = textInfo.characterInfo[firstCharacterIndex].index;
            return stringIndex > 0 && stringIndex < processed.Length ? stringIndex : -1;
        }

        private void RefreshTextMesh()
        {
            textMeshPro.havePropertiesChanged = true;
            textMeshPro.SetVerticesDirty();
            textMeshPro.SetLayoutDirty();
        }
    }
}
