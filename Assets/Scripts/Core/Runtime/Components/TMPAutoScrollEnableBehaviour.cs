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
        /// 是否使用非缩放时间；默认 false。
        public bool UseUnscaledTime;
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

    /// TMP 横向自动滚动组件；业务可直接修改子节点 TMP。
    [DisallowMultipleComponent]
    public sealed class TMPAutoScrollEnableBehaviour : MonoBehaviour
    {
        private const int DefaultPendingStartCapacity = 64;
        private const float ViewportWidthChangeThreshold = 0.1f;

        private struct MaterialMeshCache
        {
            public Vector3[] OriginVertices;
            public Vector4[] OriginUv0;
            public Vector3[] Vertices;
            public Vector4[] Uv0;
        }

        private static readonly Canvas.WillRenderCanvases ProcessPendingStartScrollsCallback = ProcessPendingStartScrolls;
        private static TMPAutoScrollEnableBehaviour[] pendingStartBehaviours = new TMPAutoScrollEnableBehaviour[DefaultPendingStartCapacity];
        private static int pendingStartBehaviourCount;
        private static bool isPendingStartCallbackRegistered;

        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private TextMeshProUGUI textComponent;
        [SerializeField] private bool scrollEnabled = true;
        [Tooltip("开启后，滚动阈值和裁剪区域会扣除 LayoutGroup 的左右 Padding。Text 节点必须直接挂在 Viewport 下，中间不能再套其他节点。")]
        [SerializeField] private bool respectLayoutPadding;

        [SerializeField] private TMPAutoScrollOptions options = TMPAutoScrollOptions.Default;
        private LayoutGroup viewportLayoutGroup;
        private Vector2 originTextAnchoredPosition;
        private Vector2 scrollEndPosition;
        private TextWrappingModes originWrappingMode;
        private TextOverflowModes originOverflowMode;
        private HorizontalAlignmentOptions originHorizontalAlignment;
        // 复用 TMP 原始网格数据缓存，避免每次滚动都 Clone 顶点/UV 数组造成 GC。
        private Vector3[][] originMeshVertices;
        private Vector4[][] originMeshUv0;
        private readonly Vector3[] viewportWorldCorners = new Vector3[4];
        // 每个材质对应一组顶点/UV 引用，滚动中按 materialIndex 直接取，避免每个字符重复拆 TMP_MeshInfo。
        private MaterialMeshCache[] materialMeshCaches;
        private int cachedMaterialCount;
        private bool autoStart;
        private bool pendingStartScroll;
        private bool isScrolling;
        private bool hasCachedMeshData;
        private bool isInitialized;
        private float lastMeasuredViewportWidth = -1f;
        private float scrollElapsed;
        private float scrollMoveDuration;
        private float scrollCycleDuration;
        private string observedText;
        private UnityAction textVerticesDirtyCallback;
        private bool isTextDirtyCallbackRegistered;
        private bool textMeshRefreshRequired;
        private bool isRefreshingMesh;

        /// 非循环滚动到末尾并停留后触发；短文本测量后立即触发，停止或重置不会触发。
        public event System.Action ScrollCompleted;

        /// 正在等待布局或执行本轮滚动（包括首尾停留）。
        public bool IsReading => pendingStartScroll || isScrolling;

        /// 是否允许自动滚动。
        public bool ScrollEnabled
        {
            get => scrollEnabled;
            set => SetScrollEnabled(value);
        }

        private void Awake()
        {
            textVerticesDirtyCallback = OnTextVerticesDirty;
            TryInitializeFromGameObject();
        }

        private void OnEnable()
        {
            TryInitializeFromGameObject();
            RegisterTextDirtyCallback();
            CacheObservedText();
            if (autoStart)
            {
                StartScroll();
            }
        }

        private void OnDisable()
        {
            UnregisterTextDirtyCallback();
            StopScrollAndReset();
        }

        private void Update()
        {
            if (!isScrolling)
            {
                return;
            }

            UpdateScroll(options.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isInitialized || !autoStart)
            {
                return;
            }

            if (pendingStartScroll)
            {
                return;
            }

            float viewportWidth = GetViewportWidth();
            if (lastMeasuredViewportWidth >= 0f
                && Mathf.Abs(viewportWidth - lastMeasuredViewportWidth) <= ViewportWidthChangeThreshold)
            {
                return;
            }

            // 列表和 LayoutGroup 可能在首次裁剪后继续调整宽度，需要恢复网格并按最终宽度重新测量。
            StopScrollAndReset();
            RequestStartScrollBeforeRender();
        }

        private void OnDestroy()
        {
            UnregisterTextDirtyCallback();
            StopScrollAndReset();
            isInitialized = false;
        }

        /// <summary>
        /// 初始化滚动组件；普通 MonoBehaviour 可直接传入显示区域和目标文本。
        /// </summary>
        /// <param name="viewport">用于裁剪和计算可见宽度的 RectTransform。</param>
        /// <param name="targetText">需要横向滚动的 TextMeshProUGUI。</param>
        public void Initialize(RectTransform viewport, TextMeshProUGUI targetText)
        {
            UnregisterTextDirtyCallback();
            if (isInitialized)
            {
                StopScrollAndReset();
            }

            viewportRect = viewport;
            textComponent = targetText;
            if (viewportRect == null || textComponent == null)
            {
                isInitialized = false;
                return;
            }

            CacheViewportLayoutGroup();
            originTextAnchoredPosition = textComponent.rectTransform.anchoredPosition;
            autoStart = true;
            CacheTextComponentFormat();
            observedText = null;
            lastMeasuredViewportWidth = -1f;
            CacheObservedText();
            isInitialized = true;
            RegisterTextDirtyCallback();

            if (isActiveAndEnabled)
            {
                StartScroll();
            }
        }

        /// <summary>
        /// 设置滚动配置。
        /// </summary>
        /// <param name="newOptions">新的滚动配置，会整体替换当前配置。</param>
        public void SetOptions(TMPAutoScrollOptions newOptions)
        {
            TryInitializeFromGameObject();
            options = newOptions;
            if (autoStart) StartScroll();
        }

        /// <summary>
        /// 设置是否允许滚动；启用时会按最新布局重新测宽，禁用时恢复文本初始状态。
        /// </summary>
        /// <param name="enabled">是否允许文本在超出显示区域时自动滚动。</param>
        public void SetScrollEnabled(bool enabled)
        {
            scrollEnabled = enabled;
            if (!scrollEnabled)
            {
                StopScrollAndReset();
                return;
            }

            if (autoStart)
            {
                StartScroll();
            }
        }

        /// <summary>
        /// 设置显示文本。
        /// </summary>
        /// <param name="text">要显示的文本内容。</param>
        /// <param name="shouldAutoStart">是否自动启动滚动；当前已显示时立即启动，未显示时在激活后启动。</param>
        public void SetText(string text, bool shouldAutoStart = true)
        {
            if (!isInitialized)
            {
                TryInitializeFromGameObject();
            }

            if (!isInitialized)
            {
                return;
            }

            string nextText = text ?? string.Empty;
            // 列表复用或刷新时常会重复设置同一个文本；直接跳过，避免无效 Stop/Start 和 TMP 测宽。
            if (autoStart == shouldAutoStart && textComponent.text == nextText)
            {
                return;
            }

            StopScrollAndReset();
            observedText = nextText;
            textMeshRefreshRequired = true;
            textComponent.text = nextText;
            autoStart = shouldAutoStart;

            if (autoStart)
            {
                StartScroll();
            }
        }

        /// <summary>
        /// 设置文本颜色和字号，传入前需保证已设置文本。
        /// </summary>
        /// <param name="color">字体颜色。</param>
        /// <param name="fontSize">字号，小于等于 0 时保持当前字号不变。</param>
        public void SetTextStyle(Color color, float fontSize)
        {
            TryInitializeFromGameObject();
            if (!isInitialized)
            {
                return;
            }

            textComponent.color = color;
            SetTextFontSize(fontSize);
        }

        /// <summary>
        /// 设置文本字号，传入前需保证已设置文本。
        /// </summary>
        /// <param name="fontSize">字号，小于等于 0 时保持当前字号不变。</param>
        public void SetTextFontSize(float fontSize)
        {
            TryInitializeFromGameObject();
            if (!isInitialized)
            {
                return;
            }
            if (fontSize > 0f && !Mathf.Approximately(textComponent.fontSize, fontSize))
            {
                textComponent.fontSize = fontSize;
                StartScroll();
            }
        }

        /// <summary>
        /// 设置显示区域高度。
        /// </summary>
        /// <param name="height">显示区域和文本区域的新高度。</param>
        public void SetViewportHeight(float height)
        {
            TryInitializeFromGameObject();
            if (!isInitialized)
            {
                return;
            }

            viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            textComponent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        /// 按当前文本和配置重新计算，并在文本超出显示区域时启动横向滚动。
        public void StartScroll()
        {
            TryInitializeFromGameObject();
            StopScrollAndReset();
            RequestStartScrollBeforeRender();
        }

        /// 停止当前滚动，并恢复文本到初始位置。
        public void StopScrollAndReset()
        {
            bool wasRefreshing = isRefreshingMesh;
            isRefreshingMesh = true;
            try { ResetScrollInternal(); }
            finally { isRefreshingMesh = wasRefreshing; }
        }

        private void ResetScrollInternal()
        {
            CancelPendingStartScroll();
            isScrolling = false;

            if (!hasCachedMeshData || !isInitialized)
            {
                return;
            }

            ResetTextPosition();
            RestoreTextComponentFormat();
            hasCachedMeshData = false;
            cachedMaterialCount = 0;
            textComponent.ForceMeshUpdate();
            textMeshRefreshRequired = false;
        }

        /// 缓存当前 TMP 格式，后续停止滚动并重置时会恢复到该格式。
        public void CacheTextComponentFormat()
        {
            if (textComponent == null)
            {
                return;
            }

            originWrappingMode = textComponent.textWrappingMode;
            originOverflowMode = textComponent.overflowMode;
            originHorizontalAlignment = textComponent.horizontalAlignment;
        }

        private void TryInitializeFromGameObject()
        {
            if (isInitialized)
            {
                return;
            }

            if (viewportRect == null)
            {
                viewportRect = transform as RectTransform;
            }

            if (textComponent == null)
            {
                textComponent = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (viewportRect == null || textComponent == null)
            {
                return;
            }

            CacheViewportLayoutGroup();
            originTextAnchoredPosition = textComponent.rectTransform.anchoredPosition;
            autoStart = true;
            CacheTextComponentFormat();
            observedText = null;
            lastMeasuredViewportWidth = -1f;
            CacheObservedText();
            isInitialized = true;
            RegisterTextDirtyCallback();
        }

        private void RegisterTextDirtyCallback()
        {
            if (!isActiveAndEnabled || isTextDirtyCallbackRegistered || textComponent == null)
            {
                return;
            }

            textVerticesDirtyCallback ??= OnTextVerticesDirty;
            textComponent.RegisterDirtyVerticesCallback(textVerticesDirtyCallback);
            isTextDirtyCallbackRegistered = true;
        }

        private void UnregisterTextDirtyCallback()
        {
            if (!isTextDirtyCallbackRegistered || textComponent == null)
            {
                return;
            }

            textComponent.UnregisterDirtyVerticesCallback(textVerticesDirtyCallback);
            isTextDirtyCallbackRegistered = false;
        }

        private void CacheObservedText()
        {
            string currentText = textComponent == null ? string.Empty : textComponent.text ?? string.Empty;
            if (observedText != null && observedText != currentText)
            {
                textMeshRefreshRequired = true;
            }

            observedText = currentText;
        }

        private void OnTextVerticesDirty()
        {
            if (!isInitialized || textComponent == null || isRefreshingMesh)
            {
                return;
            }

            string currentText = textComponent.text ?? string.Empty;
            if (observedText == currentText)
            {
                if (pendingStartScroll)
                {
                    return;
                }

                // Canvas、字体或布局可能在文本内容不变时重建 TMP 网格，旧的手工裁剪缓存已不能继续使用。
                textMeshRefreshRequired = true;
                StopScrollAndReset();
                if (autoStart)
                {
                    RequestStartScrollBeforeRender();
                }
                return;
            }

            // 兼容业务直接给 TMP.text 赋值：仅在文本内容真正变化时复位并重新判断滚动。
            observedText = currentText;
            textMeshRefreshRequired = true;
            StopScrollAndReset();
            if (autoStart)
            {
                RequestStartScrollBeforeRender();
            }
        }

        private void RequestStartScrollBeforeRender()
        {
            // 启动前判断可见性，隐藏或被复用到非激活状态时，不注册渲染前回调。
            if (!CanStartScroll())
            {
                return;
            }

            // 首次裁剪必须等到 Canvas 渲染前，此时布局和 TMP 网格才接近本帧最终状态。
            // 所有实例共用一份静态队列，整帧只保留一次全局回调。
            pendingStartScroll = true;
            AddPendingStartScroll(this);
        }

        private void CancelPendingStartScroll()
        {
            if (!pendingStartScroll)
            {
                return;
            }

            pendingStartScroll = false;
            RemovePendingStartScroll(this);
        }

        private static void AddPendingStartScroll(TMPAutoScrollEnableBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            if (pendingStartBehaviourCount >= pendingStartBehaviours.Length)
            {
                TMPAutoScrollEnableBehaviour[] oldPendingBehaviours = pendingStartBehaviours;
                pendingStartBehaviours = new TMPAutoScrollEnableBehaviour[oldPendingBehaviours.Length << 1];
                System.Array.Copy(oldPendingBehaviours, pendingStartBehaviours, oldPendingBehaviours.Length);
            }

            pendingStartBehaviours[pendingStartBehaviourCount] = behaviour;
            pendingStartBehaviourCount++;

            if (isPendingStartCallbackRegistered)
            {
                return;
            }

            Canvas.willRenderCanvases += ProcessPendingStartScrollsCallback;
            isPendingStartCallbackRegistered = true;
        }

        private static void RemovePendingStartScroll(TMPAutoScrollEnableBehaviour behaviour)
        {
            if (behaviour == null || pendingStartBehaviourCount <= 0)
            {
                return;
            }

            for (int i = 0; i < pendingStartBehaviourCount; i++)
            {
                if (!ReferenceEquals(pendingStartBehaviours[i], behaviour))
                {
                    continue;
                }

                int lastIndex = pendingStartBehaviourCount - 1;
                pendingStartBehaviours[i] = pendingStartBehaviours[lastIndex];
                pendingStartBehaviours[lastIndex] = null;
                pendingStartBehaviourCount = lastIndex;
                break;
            }

            // 回调第一次注册后常驻，避免后续反复 += / -= 产生事件列表分配。
        }

        private static void ProcessPendingStartScrolls()
        {
            if (pendingStartBehaviourCount <= 0)
            {
                return;
            }

            int count = pendingStartBehaviourCount;
            while (count-- > 0 && pendingStartBehaviourCount > 0)
            {
                TMPAutoScrollEnableBehaviour behaviour = pendingStartBehaviours[--pendingStartBehaviourCount];
                pendingStartBehaviours[pendingStartBehaviourCount] = null;
                if (behaviour == null || !behaviour.pendingStartScroll) continue;
                behaviour.pendingStartScroll = false;
                behaviour.StartScrollInternal();
            }
        }

        private void StartScrollInternal()
        {
            if (this == null) return;
            isRefreshingMesh = true;
            try { MeasureAndStartScroll(); }
            finally { isRefreshingMesh = false; }
        }

        private void MeasureAndStartScroll()
        {
            // 渲染前再次判断，防止等待本帧回调期间节点被隐藏或复用。
            if (!CanStartScroll())
            {
                return;
            }

            if (respectLayoutPadding)
            {
                // 使用动态布局内边距时，延迟到渲染前缓存 LayoutGroup 调整后的最终位置。
                originTextAnchoredPosition = textComponent.rectTransform.anchoredPosition;
            }

            // 宽度判断：优先使用配置宽度，否则使用根节点当前宽度。
            float viewportWidth = GetViewportWidth();
            if (viewportWidth <= 0f)
            {
                return;
            }
            lastMeasuredViewportWidth = viewportWidth;
            if (options.PixelsPerSecond <= 0f)
            {
                return;
            }

            // 先用 TMP 自身的未换行 preferred 宽度判断；短文本仅在内容变化时刷新一次显示网格。
            float textWidth = GetTextPreferredWidth();
            if (textWidth <= viewportWidth)
            {
                ResetTextPosition();
                RestoreTextComponentFormat();
                if (textMeshRefreshRequired)
                {
                    // 直接赋值后短文本不会进入下方长文本的 ForceMeshUpdate，需在此生成一次可见网格。
                    textComponent.ForceMeshUpdate();
                    textMeshRefreshRequired = false;
                }

                if (!options.Loop)
                {
                    ScrollCompleted?.Invoke();
                }

                return;
            }

            PrepareTextComponent();
            textComponent.ForceMeshUpdate();
            textMeshRefreshRequired = false;
            textWidth = textComponent.preferredWidth;
            if (textWidth <= viewportWidth)
            {
                ResetTextPosition();
                RestoreTextComponentFormat();
                if (!options.Loop)
                {
                    ScrollCompleted?.Invoke();
                }

                return;
            }

            float moveDistance = textWidth - viewportWidth;
            float duration = moveDistance / options.PixelsPerSecond;
            if (duration <= 0f)
            {
                ResetTextPosition();
                RestoreTextComponentFormat();
                return;
            }

            CacheTextMeshData();
            ClipTextMesh();
            StartManualScroll(moveDistance, duration);
        }

        private void PrepareTextComponent()
        {
            textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            textComponent.overflowMode = TextOverflowModes.Overflow;
            textComponent.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }

        private float GetViewportWidth()
        {
            if (!respectLayoutPadding)
            {
                return options.ViewportWidth > 0f ? options.ViewportWidth : viewportRect.rect.width;
            }

            GetViewportHorizontalRange(out float minX, out float maxX);
            return maxX - minX;
        }

        private float GetTextPreferredWidth()
        {
            return textComponent.GetPreferredValues(textComponent.text, Mathf.Infinity, Mathf.Infinity).x;
        }

        private void StartManualScroll(float moveDistance, float duration)
        {
            scrollElapsed = 0f;
            scrollMoveDuration = duration;
            scrollCycleDuration = Mathf.Max(0f, options.StartDelay) + duration + Mathf.Max(0f, options.EndStayTime);
            scrollEndPosition = new Vector2(originTextAnchoredPosition.x - moveDistance, originTextAnchoredPosition.y);
            isScrolling = true;
        }

        private void UpdateScroll(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            scrollElapsed += deltaTime;
            float moveStartTime = Mathf.Max(0f, options.StartDelay);
            if (scrollElapsed < moveStartTime)
            {
                return;
            }

            float moveTime = scrollElapsed - moveStartTime;
            if (moveTime <= scrollMoveDuration)
            {
                float progress = scrollMoveDuration > 0f ? Mathf.Clamp01(moveTime / scrollMoveDuration) : 1f;
                float easedProgress = EvaluateEase(progress, options.Ease);
                textComponent.rectTransform.anchoredPosition = Vector2.LerpUnclamped(originTextAnchoredPosition, scrollEndPosition, easedProgress);
                ClipTextMesh();
                return;
            }

            if (!options.Loop)
            {
                textComponent.rectTransform.anchoredPosition = scrollEndPosition;
                ClipTextMesh();
                if (scrollElapsed < scrollCycleDuration)
                {
                    return;
                }

                isScrolling = false;
                ScrollCompleted?.Invoke();
                return;
            }

            if (scrollElapsed < scrollCycleDuration)
            {
                return;
            }

            ResetTextPosition();
            ClipTextMesh();
            scrollElapsed = 0f;
        }

        private float EvaluateEase(float progress, Ease ease)
        {
            switch (ease)
            {
                case Ease.InQuad:
                    return progress * progress;
                case Ease.OutQuad:
                    return 1f - (1f - progress) * (1f - progress);
                case Ease.InOutQuad:
                    return progress < 0.5f ? 2f * progress * progress : 1f - Mathf.Pow(-2f * progress + 2f, 2f) * 0.5f;
                case Ease.InCubic:
                    return progress * progress * progress;
                case Ease.OutCubic:
                    return 1f - Mathf.Pow(1f - progress, 3f);
                case Ease.InOutCubic:
                    return progress < 0.5f ? 4f * progress * progress * progress : 1f - Mathf.Pow(-2f * progress + 2f, 3f) * 0.5f;
                case Ease.InSine:
                    return 1f - Mathf.Cos(progress * Mathf.PI * 0.5f);
                case Ease.OutSine:
                    return Mathf.Sin(progress * Mathf.PI * 0.5f);
                case Ease.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * progress) - 1f) * 0.5f;
                default:
                    return progress;
            }
        }

        private bool CanStartScroll()
        {
            if (!scrollEnabled || !isInitialized || !isActiveAndEnabled)
            {
                return false;
            }

            if (viewportRect == null || !viewportRect.gameObject.activeInHierarchy)
            {
                return false;
            }

            RectTransform textRect = textComponent == null ? null : textComponent.rectTransform;
            if (textRect == null || !textRect.gameObject.activeInHierarchy)
            {
                return false;
            }

            return textComponent.isActiveAndEnabled;
        }

        private void ResetTextPosition()
        {
            if (!isInitialized || textComponent == null)
            {
                return;
            }

            textComponent.rectTransform.anchoredPosition = originTextAnchoredPosition;
        }

        private void RestoreTextComponentFormat()
        {
            if (!isInitialized || textComponent == null)
            {
                return;
            }

            textComponent.textWrappingMode = originWrappingMode;
            textComponent.overflowMode = originOverflowMode;
            textComponent.horizontalAlignment = originHorizontalAlignment;
        }

        private void CacheTextMeshData()
        {
            TMP_TextInfo textInfo = textComponent.textInfo;
            // TMP_TextInfo.meshInfo 是 TMP 维护的数组字段，这里只是取引用，不会产生 GC。
            TMP_MeshInfo[] meshInfo = textInfo.meshInfo;
            int materialCount = Mathf.Min(textInfo.materialCount, meshInfo.Length);
            EnsureMeshCacheCapacity(materialCount);

            for (int i = 0; i < materialCount; i++)
            {
                Vector3[] vertices = meshInfo[i].vertices;
                Vector4[] uvs0 = meshInfo[i].uvs0;
                if (vertices == null || uvs0 == null)
                {
                    materialMeshCaches[i] = default;
                    continue;
                }

                // 只在容量不足时扩容；容量够时复用旧数组，滚动重启不会产生新数组。
                originMeshVertices[i] = EnsureVector3ArrayCapacity(originMeshVertices[i], vertices.Length);
                originMeshUv0[i] = EnsureVector4ArrayCapacity(originMeshUv0[i], uvs0.Length);
                System.Array.Copy(vertices, originMeshVertices[i], vertices.Length);
                System.Array.Copy(uvs0, originMeshUv0[i], uvs0.Length);

                ref MaterialMeshCache materialCache = ref materialMeshCaches[i];
                materialCache.OriginVertices = originMeshVertices[i];
                materialCache.OriginUv0 = originMeshUv0[i];
                materialCache.Vertices = vertices;
                materialCache.Uv0 = uvs0;
            }

            cachedMaterialCount = materialCount;
            hasCachedMeshData = materialCount > 0;
        }

        private void EnsureMeshCacheCapacity(int materialCount)
        {
            if (originMeshVertices == null)
            {
                originMeshVertices = new Vector3[materialCount][];
            }
            else if (originMeshVertices.Length < materialCount)
            {
                Vector3[][] oldVertices = originMeshVertices;
                originMeshVertices = new Vector3[materialCount][];
                System.Array.Copy(oldVertices, originMeshVertices, oldVertices.Length);
            }

            if (originMeshUv0 == null)
            {
                originMeshUv0 = new Vector4[materialCount][];
            }
            else if (originMeshUv0.Length < materialCount)
            {
                Vector4[][] oldUv0 = originMeshUv0;
                originMeshUv0 = new Vector4[materialCount][];
                System.Array.Copy(oldUv0, originMeshUv0, oldUv0.Length);
            }

            if (materialMeshCaches == null)
            {
                materialMeshCaches = new MaterialMeshCache[materialCount];
            }
            else if (materialMeshCaches.Length < materialCount)
            {
                MaterialMeshCache[] oldMaterialMeshCaches = materialMeshCaches;
                materialMeshCaches = new MaterialMeshCache[materialCount];
                System.Array.Copy(oldMaterialMeshCaches, materialMeshCaches, oldMaterialMeshCaches.Length);
            }
        }

        private Vector3[] EnsureVector3ArrayCapacity(Vector3[] cache, int length)
        {
            if (cache == null || cache.Length < length)
            {
                return new Vector3[length];
            }

            return cache;
        }

        private Vector4[] EnsureVector4ArrayCapacity(Vector4[] cache, int length)
        {
            if (cache == null || cache.Length < length)
            {
                return new Vector4[length];
            }

            return cache;
        }

        private void ClipTextMesh()
        {
            if (!isInitialized || !hasCachedMeshData || materialMeshCaches == null || textComponent == null)
            {
                return;
            }

            TMP_TextInfo textInfo = textComponent.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0)
            {
                return;
            }

            GetViewportXInTextLocal(out float clipMinX, out float clipMaxX);
            // 每帧先恢复到未裁剪的原始网格，再按当前滚动位置重新裁剪，避免裁剪误差累积。
            RestoreOriginMeshData();

            // 宽度裁剪：只改 TMP 顶点，避免每个 item 拆 drawcall。
            // TMP_TextInfo.characterInfo 是 TMP 内部数组字段；取到局部变量只是缓存引用，不是分配。
            TMP_CharacterInfo[] characterInfoList = textInfo.characterInfo;
            int characterCount = textInfo.characterCount;
            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo characterInfo = characterInfoList[i];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                if (materialIndex < 0 || materialIndex >= cachedMaterialCount)
                {
                    continue;
                }

                ClipCharacterQuad(ref materialMeshCaches[materialIndex], characterInfo.vertexIndex, clipMinX, clipMaxX);
            }

            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Uv0);
        }

        private void RestoreOriginMeshData()
        {
            for (int i = 0; i < cachedMaterialCount; i++)
            {
                ref MaterialMeshCache materialCache = ref materialMeshCaches[i];
                Vector3[] vertices = materialCache.Vertices;
                Vector4[] uvs0 = materialCache.Uv0;
                Vector3[] originVertices = materialCache.OriginVertices;
                Vector4[] originUv0 = materialCache.OriginUv0;
                if (vertices == null || uvs0 == null || originVertices == null || originUv0 == null)
                {
                    continue;
                }

                int verticesLength = Mathf.Min(vertices.Length, originVertices.Length);
                int uvLength = Mathf.Min(uvs0.Length, originUv0.Length);
                System.Array.Copy(originVertices, vertices, verticesLength);
                System.Array.Copy(originUv0, uvs0, uvLength);
            }
        }

        private void GetViewportXInTextLocal(out float minX, out float maxX)
        {
            RectTransform textRect = textComponent.rectTransform;
            if (!respectLayoutPadding)
            {
                viewportRect.GetWorldCorners(viewportWorldCorners);
            }
            else
            {
                Rect viewportBounds = viewportRect.rect;
                GetViewportHorizontalRange(out float viewportMinX, out float viewportMaxX);
                viewportWorldCorners[0] = viewportRect.TransformPoint(new Vector3(viewportMinX, viewportBounds.yMin));
                viewportWorldCorners[1] = viewportRect.TransformPoint(new Vector3(viewportMinX, viewportBounds.yMax));
                viewportWorldCorners[2] = viewportRect.TransformPoint(new Vector3(viewportMaxX, viewportBounds.yMax));
                viewportWorldCorners[3] = viewportRect.TransformPoint(new Vector3(viewportMaxX, viewportBounds.yMin));
            }

            minX = float.MaxValue;
            maxX = float.MinValue;
            for (int i = 0; i < viewportWorldCorners.Length; i++)
            {
                float x = textRect.InverseTransformPoint(viewportWorldCorners[i]).x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
        }

        private void CacheViewportLayoutGroup()
        {
            viewportLayoutGroup = respectLayoutPadding
                && textComponent != null
                && textComponent.rectTransform.parent == viewportRect
                ? viewportRect.GetComponent<LayoutGroup>()
                : null;
        }

        private void GetViewportHorizontalRange(out float minX, out float maxX)
        {
            Rect viewportBounds = viewportRect.rect;
            minX = viewportBounds.xMin;
            maxX = viewportBounds.xMax;

            if (viewportLayoutGroup != null && viewportLayoutGroup.isActiveAndEnabled)
            {
                RectOffset padding = viewportLayoutGroup.padding;
                minX += padding.left;
                maxX -= padding.right;
            }

            maxX = Mathf.Max(minX, maxX);

            if (options.ViewportWidth <= 0f)
            {
                return;
            }

            float centerX = (minX + maxX) * 0.5f;
            float halfWidth = Mathf.Min(options.ViewportWidth, maxX - minX) * 0.5f;
            minX = centerX - halfWidth;
            maxX = centerX + halfWidth;
        }

        private void ClipCharacterQuad(ref MaterialMeshCache materialCache, int vertexIndex, float clipMinX, float clipMaxX)
        {
            // 下面只是取缓存中的数组引用，不会创建新数组；真正的数组只在容量不足时扩容。
            Vector3[] originVertices = materialCache.OriginVertices;
            Vector4[] originUv0 = materialCache.OriginUv0;
            Vector3[] vertices = materialCache.Vertices;
            Vector4[] uvs0 = materialCache.Uv0;
            if (originVertices == null || originUv0 == null || vertices == null || uvs0 == null ||
                vertexIndex + 3 >= vertices.Length || vertexIndex + 3 >= originVertices.Length)
            {
                return;
            }

            float x0 = originVertices[vertexIndex].x;
            float x1 = originVertices[vertexIndex + 1].x;
            float x2 = originVertices[vertexIndex + 2].x;
            float x3 = originVertices[vertexIndex + 3].x;
            float charMinX = Mathf.Min(Mathf.Min(x0, x1), Mathf.Min(x2, x3));
            float charMaxX = Mathf.Max(Mathf.Max(x0, x1), Mathf.Max(x2, x3));
            if (charMaxX <= clipMinX || charMinX >= clipMaxX || charMaxX <= charMinX)
            {
                // 完全在可视区域外时折叠四个顶点，让字符不可见但不改变字符布局。
                CollapseCharacterQuad(vertices, vertexIndex);
                return;
            }

            float clippedMinX = Mathf.Max(charMinX, clipMinX);
            float clippedMaxX = Mathf.Min(charMaxX, clipMaxX);
            if (clippedMaxX <= clippedMinX)
            {
                // 裁剪后没有可显示宽度，同样折叠顶点。
                CollapseCharacterQuad(vertices, vertexIndex);
                return;
            }

            float leftPercent = Mathf.InverseLerp(charMinX, charMaxX, clippedMinX);
            float rightPercent = Mathf.InverseLerp(charMinX, charMaxX, clippedMaxX);

            vertices[vertexIndex] = new Vector3(clippedMinX, originVertices[vertexIndex].y, originVertices[vertexIndex].z);
            vertices[vertexIndex + 1] = new Vector3(clippedMinX, originVertices[vertexIndex + 1].y, originVertices[vertexIndex + 1].z);
            vertices[vertexIndex + 2] = new Vector3(clippedMaxX, originVertices[vertexIndex + 2].y, originVertices[vertexIndex + 2].z);
            vertices[vertexIndex + 3] = new Vector3(clippedMaxX, originVertices[vertexIndex + 3].y, originVertices[vertexIndex + 3].z);

            // 顶点被水平截断后同步插值 UV，避免边缘字符被拉伸。
            uvs0[vertexIndex] = Vector4.Lerp(originUv0[vertexIndex], originUv0[vertexIndex + 3], leftPercent);
            uvs0[vertexIndex + 1] = Vector4.Lerp(originUv0[vertexIndex + 1], originUv0[vertexIndex + 2], leftPercent);
            uvs0[vertexIndex + 2] = Vector4.Lerp(originUv0[vertexIndex + 1], originUv0[vertexIndex + 2], rightPercent);
            uvs0[vertexIndex + 3] = Vector4.Lerp(originUv0[vertexIndex], originUv0[vertexIndex + 3], rightPercent);
        }

        private void CollapseCharacterQuad(Vector3[] vertices, int vertexIndex)
        {
            vertices[vertexIndex] = Vector3.zero;
            vertices[vertexIndex + 1] = Vector3.zero;
            vertices[vertexIndex + 2] = Vector3.zero;
            vertices[vertexIndex + 3] = Vector3.zero;
        }
    }
}
