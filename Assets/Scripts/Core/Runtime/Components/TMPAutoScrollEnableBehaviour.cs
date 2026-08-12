using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// TMP 横向自动滚动配置。
    [System.Serializable]
    public struct TMPAutoScrollOptions
    {
        /// 开始移动前等待秒数。
        public float StartDelay;
        /// 到达末端后停留秒数。
        public float EndStayTime;
        /// 每秒移动的 UI 像素数。
        public float PixelsPerSecond;
        /// 位移动画缓动类型。
        public Ease Ease;
        /// 是否循环播放。
        public bool Loop;
        /// 显式显示宽度；小于等于零时读取 viewport 实际宽度。
        public float ViewportWidth;

        /// 默认滚动配置。
        public static TMPAutoScrollOptions Default => new TMPAutoScrollOptions
        {
            StartDelay = 1.5f,
            EndStayTime = 2f,
            PixelsPerSecond = 40f,
            Ease = Ease.Linear,
            Loop = true,
            ViewportWidth = 0f
        };
    }

    /// 为 TextMeshProUGUI 提供无需业务轮询的横向自动滚动。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TMPAutoScrollEnableBehaviour : MonoBehaviour
    {
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private TextMeshProUGUI textComponent;
        [SerializeField] private bool scrollEnabled = true;
        [SerializeField] private bool ensureRectMask = true;
        [SerializeField] private TMPAutoScrollOptions options = default;

        private Vector2 originAnchoredPosition;
        private Vector2 endAnchoredPosition;
        private TextWrappingModes originWrappingMode;
        private TextOverflowModes originOverflowMode;
        private HorizontalAlignmentOptions originAlignment;
        private UnityAction textVerticesDirtyCallback;
        private string observedText;
        private bool isInitialized;
        private bool isCallbackRegistered;
        private bool pendingStart;
        private bool isScrolling;
        private bool autoStart = true;
        private float elapsed;
        private float moveDuration;
        private float cycleDuration;

        /// 是否允许自动滚动。
        public bool ScrollEnabled
        {
            get => scrollEnabled;
            set => SetScrollEnabled(value);
        }

        private void Awake()
        {
            textVerticesDirtyCallback = OnTextVerticesDirty;
            if (options.PixelsPerSecond <= 0f) options = TMPAutoScrollOptions.Default;
            TryInitializeFromGameObject();
        }

        private void OnEnable()
        {
            TryInitializeFromGameObject();
            RegisterTextDirtyCallback();
            CacheObservedText();
            if (autoStart) StartScroll();
        }

        private void OnDisable()
        {
            UnregisterTextDirtyCallback();
            CancelPendingStart();
            StopScrollAndReset();
        }

        private void OnDestroy()
        {
            UnregisterTextDirtyCallback();
            CancelPendingStart();
        }

        private void Update()
        {
            if (!isScrolling) return;

            elapsed += Time.deltaTime;
            float startDelay = Mathf.Max(0f, options.StartDelay);
            if (elapsed < startDelay) return;

            float moveElapsed = elapsed - startDelay;
            if (moveElapsed <= moveDuration)
            {
                float progress = moveDuration > 0f ? Mathf.Clamp01(moveElapsed / moveDuration) : 1f;
                textComponent.rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    originAnchoredPosition,
                    endAnchoredPosition,
                    EvaluateEase(progress, options.Ease));
                return;
            }

            if (!options.Loop)
            {
                textComponent.rectTransform.anchoredPosition = endAnchoredPosition;
                isScrolling = false;
                return;
            }

            if (elapsed >= cycleDuration)
            {
                textComponent.rectTransform.anchoredPosition = originAnchoredPosition;
                elapsed = 0f;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (autoStart && isActiveAndEnabled) RequestStartBeforeRender();
        }

        /// <summary>初始化显示区域和目标文本。</summary>
        /// <param name="viewport">用于计算可见宽度并承载裁剪组件的节点。</param>
        /// <param name="targetText">需要横向滚动的 TMP 文本。</param>
        public void Initialize(RectTransform viewport, TextMeshProUGUI targetText)
        {
            UnregisterTextDirtyCallback();
            StopScrollAndReset();
            viewportRect = viewport;
            textComponent = targetText;
            isInitialized = viewportRect != null && textComponent != null;
            if (!isInitialized) return;

            if (options.PixelsPerSecond <= 0f) options = TMPAutoScrollOptions.Default;
            originAnchoredPosition = textComponent.rectTransform.anchoredPosition;
            CacheTextComponentFormat();
            EnsureViewportMask();
            observedText = textComponent.text ?? string.Empty;
            RegisterTextDirtyCallback();
            if (isActiveAndEnabled && autoStart) StartScroll();
        }

        /// <summary>整体替换滚动配置，并按当前文本重新判断。</summary>
        /// <param name="newOptions">新的滚动配置。</param>
        public void SetOptions(TMPAutoScrollOptions newOptions)
        {
            options = newOptions;
            if (autoStart) StartScroll();
        }

        /// <summary>允许或禁止滚动。</summary>
        /// <param name="enabled">是否允许超宽文本滚动。</param>
        public void SetScrollEnabled(bool enabled)
        {
            if (scrollEnabled == enabled) return;
            scrollEnabled = enabled;
            if (enabled && autoStart) StartScroll();
            else StopScrollAndReset();
        }

        /// <summary>设置文本并按需自动启动。</summary>
        /// <param name="text">显示文本。</param>
        /// <param name="shouldAutoStart">是否在可见后自动滚动。</param>
        public void SetText(string text, bool shouldAutoStart = true)
        {
            TryInitializeFromGameObject();
            if (!isInitialized) return;

            string next = text ?? string.Empty;
            autoStart = shouldAutoStart;
            if (textComponent.text != next) textComponent.text = next;
            observedText = next;
            if (autoStart) StartScroll();
            else StopScrollAndReset();
        }

        /// <summary>设置颜色和字号。</summary>
        /// <param name="color">字体颜色。</param>
        /// <param name="fontSize">字号；小于等于零时保持不变。</param>
        public void SetTextStyle(Color color, float fontSize)
        {
            TryInitializeFromGameObject();
            if (!isInitialized) return;
            textComponent.color = color;
            if (fontSize > 0f) textComponent.fontSize = fontSize;
            if (autoStart) StartScroll();
        }

        /// <summary>设置显示区域和文本区域高度。</summary>
        /// <param name="height">新高度。</param>
        public void SetViewportHeight(float height)
        {
            TryInitializeFromGameObject();
            if (!isInitialized) return;
            viewportRect.SetHeight(height);
            textComponent.rectTransform.SetHeight(height);
        }

        /// 按当前文本和布局重新判断并启动滚动。
        public void StartScroll()
        {
            TryInitializeFromGameObject();
            StopScrollAndReset();
            RequestStartBeforeRender();
        }

        /// 停止滚动并恢复初始位置与 TMP 格式。
        public void StopScrollAndReset()
        {
            CancelPendingStart();
            isScrolling = false;
            if (!isInitialized || textComponent == null) return;
            textComponent.rectTransform.anchoredPosition = originAnchoredPosition;
            textComponent.textWrappingMode = originWrappingMode;
            textComponent.overflowMode = originOverflowMode;
            textComponent.horizontalAlignment = originAlignment;
        }

        /// 缓存当前 TMP 格式，停止滚动时恢复。
        public void CacheTextComponentFormat()
        {
            if (textComponent == null) return;
            originWrappingMode = textComponent.textWrappingMode;
            originOverflowMode = textComponent.overflowMode;
            originAlignment = textComponent.horizontalAlignment;
        }

        private void TryInitializeFromGameObject()
        {
            if (isInitialized) return;
            viewportRect ??= transform as RectTransform;
            textComponent ??= GetComponentInChildren<TextMeshProUGUI>(true);
            if (viewportRect != null && textComponent != null) Initialize(viewportRect, textComponent);
        }

        private void EnsureViewportMask()
        {
            if (ensureRectMask && viewportRect != null)
            {
                viewportRect.gameObject.GetOrAddComponent<RectMask2D>();
            }
        }

        private void RequestStartBeforeRender()
        {
            if (!CanStart() || pendingStart) return;
            pendingStart = true;
            Canvas.willRenderCanvases += StartBeforeRender;
        }

        private void CancelPendingStart()
        {
            if (!pendingStart) return;
            pendingStart = false;
            Canvas.willRenderCanvases -= StartBeforeRender;
        }

        private void StartBeforeRender()
        {
            // Canvas 正在派发回调时移除订阅不会改变本次委托快照，DestroyImmediate 后仍可能进到这里。
            if (this == null)
            {
                return;
            }

            CancelPendingStart();
            if (!CanStart()) return;

            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            textComponent.overflowMode = TextOverflowModes.Overflow;
            textComponent.horizontalAlignment = HorizontalAlignmentOptions.Left;
            textComponent.ForceMeshUpdate();

            float viewportWidth = options.ViewportWidth > 0f ? options.ViewportWidth : viewportRect.rect.width;
            float textWidth = textComponent.GetPreferredValues(textComponent.text, Mathf.Infinity, Mathf.Infinity).x;
            if (viewportWidth <= 0f || textWidth <= viewportWidth || options.PixelsPerSecond <= 0f)
            {
                StopScrollAndReset();
                textComponent.ForceMeshUpdate();
                return;
            }

            float distance = textWidth - viewportWidth;
            moveDuration = distance / options.PixelsPerSecond;
            cycleDuration = Mathf.Max(0f, options.StartDelay) + moveDuration + Mathf.Max(0f, options.EndStayTime);
            endAnchoredPosition = originAnchoredPosition + Vector2.left * distance;
            elapsed = 0f;
            isScrolling = true;
        }

        private bool CanStart()
        {
            return scrollEnabled && isInitialized && isActiveAndEnabled && gameObject.activeInHierarchy
                   && viewportRect != null && viewportRect.gameObject.activeInHierarchy
                   && textComponent != null && textComponent.isActiveAndEnabled;
        }

        private void RegisterTextDirtyCallback()
        {
            if (!isActiveAndEnabled || isCallbackRegistered || textComponent == null) return;
            textVerticesDirtyCallback ??= OnTextVerticesDirty;
            textComponent.RegisterDirtyVerticesCallback(textVerticesDirtyCallback);
            isCallbackRegistered = true;
        }

        private void UnregisterTextDirtyCallback()
        {
            if (!isCallbackRegistered || textComponent == null) return;
            textComponent.UnregisterDirtyVerticesCallback(textVerticesDirtyCallback);
            isCallbackRegistered = false;
        }

        private void CacheObservedText()
        {
            observedText = textComponent == null ? string.Empty : textComponent.text ?? string.Empty;
        }

        private void OnTextVerticesDirty()
        {
            if (!isInitialized || textComponent == null) return;
            string current = textComponent.text ?? string.Empty;
            if (observedText == current) return;
            observedText = current;
            if (autoStart) StartScroll();
        }

        private static float EvaluateEase(float progress, Ease ease)
        {
            switch (ease)
            {
                case Ease.InQuad: return progress * progress;
                case Ease.OutQuad: return 1f - (1f - progress) * (1f - progress);
                case Ease.InOutQuad: return progress < 0.5f
                    ? 2f * progress * progress
                    : 1f - Mathf.Pow(-2f * progress + 2f, 2f) * 0.5f;
                case Ease.InCubic: return progress * progress * progress;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - progress, 3f);
                case Ease.InOutCubic: return progress < 0.5f
                    ? 4f * progress * progress * progress
                    : 1f - Mathf.Pow(-2f * progress + 2f, 3f) * 0.5f;
                case Ease.InSine: return 1f - Mathf.Cos(progress * Mathf.PI * 0.5f);
                case Ease.OutSine: return Mathf.Sin(progress * Mathf.PI * 0.5f);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * progress) - 1f) * 0.5f;
                default: return progress;
            }
        }
    }
}
