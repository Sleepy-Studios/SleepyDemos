using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 有限时长的通知堆叠；模板、关闭按钮和滚动区均由正式 Prefab 提供。
    [DisallowMultipleComponent]
    public sealed class UITipsStack : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const int CollapsedCount = 3;
        private const float CollapsedStep = 10;
        private const float ExpandedGap = 12;
        [SerializeField] private UITipsPanel itemTemplate;
        [SerializeField] private RectTransform region;
        [SerializeField] private ScrollRect stackScroll;
        [SerializeField, Min(1)] private float maximumWidth = 600;
        private readonly List<Message> messages = new List<Message>();
        private Rect safeRect;
        private int nextId;
        private bool hovered;

        private sealed class Message
        {
            internal int Id;
            internal UITipsPanel Panel;
            internal CanvasGroup Group;
            internal Vector2 Size;
            internal float Elapsed;
            internal float Duration;
            internal bool ReadComplete;
            internal Action OnReadComplete;
            internal CancellationToken Cancellation;
        }

        /// 尚未关闭的消息数量，不包含隐藏模板。
        public int Count => messages.Count;
        /// 当前是否展开全部有效消息。
        public bool Expanded { get; private set; }
        /// 最后一条消息被关闭或到期时触发；Clear 不触发。
        public event Action Emptied;

        /// <summary>添加独立消息；新消息位于最前，悬停期间暂停消失计时，文字继续滚动。</summary>
        /// <param name="content">支持 TMP 富文本的正文。</param>
        /// <param name="type">三种提示状态。</param>
        /// <param name="duration">正文滚动结束后的停留秒数，必须有限且大于零。</param>
        /// <param name="cancellationToken">仅取消本条消息，不影响其他消息。</param>
        /// <returns>本组件内稳定的消息标识；预先取消时返回 0。</returns>
        public int Add(string content, CommonTipsType type, float duration, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(typeof(CommonTipsType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (cancellationToken.IsCancellationRequested) return 0;
            region.gameObject.SetActive(true);
            var panel = Instantiate(itemTemplate, stackScroll.content);
            panel.name = "Message";
            panel.gameObject.SetActive(true);
            panel.SetMessage(content, type, maximumWidth);
            var message = new Message
            {
                Id = ++nextId, Panel = panel, Group = panel.GetComponent<CanvasGroup>(),
                Duration = duration, Cancellation = cancellationToken
            };
            message.OnReadComplete = () => { message.ReadComplete = true; message.Elapsed = 0; };
            panel.MessageScroll.ScrollCompleted += message.OnReadComplete;
            messages.Insert(0, message);
            panel.CloseButton.onClick.AddListener(() => Dismiss(message.Id));
            panel.Body.anchoredPosition = new Vector2(0, -12);
            message.Group.alpha = 0;
            RefreshSizes();
            stackScroll.StopMovement();
            stackScroll.content.anchoredPosition = Vector2.zero;
            Arrange(0, messages.Count == 1);
            return message.Id;
        }

        /// <summary>仅关闭指定消息；已关闭的标识不产生副作用。</summary>
        /// <param name="id">Add 返回的消息标识。</param>
        public void Dismiss(int id)
        {
            int index = messages.FindIndex(message => message.Id == id);
            if (index < 0) return;
            RemoveAt(index);
            if (messages.Count == 0) Emptied?.Invoke();
        }

        /// 清空全部消息和阅读状态，不触发自动关闭导航。
        public void Clear()
        {
            for (int i = messages.Count - 1; i >= 0; i--) RemoveAt(i);
            hovered = Expanded = false;
            region.gameObject.SetActive(false);
            stackScroll.StopMovement();
            stackScroll.content.anchoredPosition = Vector2.zero;
        }

        /// <summary>通知区域及消息间隙共享悬停状态，避免移动到间隙时收起。</summary>
        /// <param name="eventData">所属 EventSystem 的指针进入事件。</param>
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        /// <summary>同帧清空并重新显示后，指针移动也能恢复悬停，不依赖再次进入父节点。</summary>
        /// <param name="eventData">所属 EventSystem 的指针移动事件。</param>
        public void OnPointerMove(PointerEventData eventData) => hovered = true;
        /// <summary>离开整个通知区域后恢复剩余计时。</summary>
        /// <param name="eventData">所属 EventSystem 的指针离开事件。</param>
        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        private void OnDisable() => Clear();

        private void Update()
        {
            if (messages.Count == 0) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Expanded = hovered || (selected != null && selected.transform.IsChildOf(region));
            if (TooltipPlacementUtil.GetSafeRect(transform as RectTransform) != safeRect) RefreshSizes();
            float delta = Time.unscaledDeltaTime;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                if (message.Cancellation.IsCancellationRequested) { Dismiss(message.Id); continue; }
                if (message.Panel.MessageScroll.IsReading)
                {
                    message.ReadComplete = false;
                    message.Elapsed = 0;
                    continue;
                }
                if (Expanded || !message.ReadComplete) continue;
                message.Elapsed += delta;
                if (message.Elapsed >= message.Duration) Dismiss(message.Id);
            }
            if (messages.Count > 0) Arrange(delta, false);
        }

        private void RefreshSizes()
        {
            safeRect = TooltipPlacementUtil.GetSafeRect(transform as RectTransform);
            foreach (var message in messages) message.Size = message.Panel.RefreshLayout(safeRect.size);
        }

        private void Arrange(float delta, bool immediate)
        {
            Vector2 frontSize = messages[0].Size;
            float width = frontSize.x;
            float totalHeight = 0;
            foreach (var message in messages)
            {
                width = Mathf.Max(width, message.Size.x);
                totalHeight += message.Size.y + ExpandedGap;
            }
            totalHeight -= ExpandedGap;
            float collapsedHeight = frontSize.y + (Mathf.Min(CollapsedCount, messages.Count) - 1) * CollapsedStep;
            Vector2 regionSize = new Vector2(Expanded ? width : frontSize.x,
                Mathf.Min(safeRect.height, Expanded ? totalHeight : collapsedHeight));
            float blend = immediate ? 1 : 1 - Mathf.Exp(-18 * delta);
            region.position = transform.TransformPoint(new Vector2(safeRect.center.x, safeRect.yMax));
            region.sizeDelta = Vector2.Min(Approach(region.sizeDelta, regionSize, blend), safeRect.size);
            stackScroll.content.sizeDelta = new Vector2(0, Expanded ? totalHeight : collapsedHeight);
            stackScroll.vertical = Expanded && totalHeight > regionSize.y + 1;
            if (!Expanded)
            {
                stackScroll.StopMovement();
                stackScroll.content.anchoredPosition = Vector2.zero;
            }
            float offset = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var rect = message.Panel.Body;
                bool visible = Expanded || i < CollapsedCount;
                bool readable = Expanded || i == 0;
                float scale = Expanded ? 1 : 1 - .04f * Mathf.Min(i, CollapsedCount - 1);
                rect.anchoredPosition = Approach(rect.anchoredPosition, new Vector2(0, -(Expanded ? offset : i * CollapsedStep)), blend);
                rect.sizeDelta = Approach(rect.sizeDelta, Expanded || i == 0 ? message.Size : frontSize, blend);
                rect.localScale = Vector3.one * Mathf.MoveTowards(rect.localScale.x, scale, delta * 3);
                message.Group.alpha = Mathf.MoveTowards(message.Group.alpha, visible ? 1 : 0, delta * 8);
                message.Group.blocksRaycasts = message.Group.interactable = readable;
                message.Panel.SetMessagePresentation(readable ? 1 : 0, readable);
                offset += message.Size.y + ExpandedGap;
            }
        }

        private static Vector2 Approach(Vector2 value, Vector2 target, float blend)
            => (value - target).sqrMagnitude < .01f ? target : Vector2.Lerp(value, target, blend);

        private void RemoveAt(int index)
        {
            var message = messages[index];
            messages.RemoveAt(index);
            message.Panel.MessageScroll.ScrollCompleted -= message.OnReadComplete;
            message.Panel.CloseButton.onClick.RemoveAllListeners();
            message.Panel.gameObject.SetActive(false);
            Destroy(message.Panel.gameObject);
            if (messages.Count == 0)
            {
                hovered = Expanded = false;
                region.gameObject.SetActive(false);
            }
        }
    }
}
