using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix
{
    /// 保存于业务 Prefab 的可选触发器；不复制设备识别和菜单输入底座。
    [DisallowMultipleComponent]
    public sealed class SimpleTipsTrigger : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public enum TriggerMode { Click, Hover }
        [SerializeField] private TriggerMode mode;
        [SerializeField] private RectTransform target;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 8)] private string content;
        [SerializeField] private TooltipDirection direction = TooltipDirection.Up;
        [SerializeField, Min(0)] private float gap = 12;
        [SerializeField, Min(1)] private float maxWidth = 600;
        private CancellationTokenSource showing;
        private int ownedVersion;
        private bool pointerInside;
        private bool selected;

        /// <summary>更新提示文本，下次触发使用新内容。</summary>
        /// <param name="value">正文。</param>
        /// <param name="heading">可选标题。</param>
        public void SetContent(string value, string heading = null) { content = value; title = heading; }

        /// 显示本组件的 Tips。
        public void Show()
        {
            CancelPending();
            var source = new CancellationTokenSource(); showing = source;
            var operation = SingleUIManager.Instance.ShowSimpleTipsAsync(target != null ? target : transform as RectTransform, content, title,
                new SimpleTipsOptions(direction, gap, maxWidth, mode == TriggerMode.Click), source.Token);
            // ShowSimpleTipsAsync 在排队前分配代次；即便加载尚未完成也能只取消自己的请求。
            ownedVersion = SingleUIManager.Instance.CurrentSimpleVersion;
            ObserveShowAsync(operation).Forget();
        }

        /// 仅关闭本组件拥有的提示，不影响后来触发的其他 Tips。
        public void Hide()
        {
            CancelPending();
            if (ownedVersion != 0 && SingleUIManager.Instance.IsCurrentSimple(ownedVersion)) SingleUIManager.Instance.ObserveAsync(SingleUIManager.Instance.HideSimpleTipsAsync()).Forget();
            ownedVersion = 0;
        }

        /// <summary>点击模式显示提示。</summary>
        /// <param name="eventData">仅处理主按钮，触屏点击映射为主按钮。</param>
        public void OnPointerClick(PointerEventData eventData) { if (mode == TriggerMode.Click && eventData.button == PointerEventData.InputButton.Left) Show(); }
        /// <summary>Click 模式响应公共菜单的确认，不主动点击业务按钮。</summary>
        /// <param name="eventData">公共 EventSystem 的 Submit 事件。</param>
        public void OnSubmit(BaseEventData eventData) { if (mode == TriggerMode.Click) Show(); }
        /// <summary>悬停模式显示提示。</summary>
        /// <param name="eventData">EventSystem 的进入事件。</param>
        public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; if (mode == TriggerMode.Hover) Show(); }
        /// <summary>离开且没有焦点时关闭悬停提示。</summary>
        /// <param name="eventData">EventSystem 的离开事件。</param>
        public void OnPointerExit(PointerEventData eventData) { pointerInside = false; if (mode == TriggerMode.Hover && !selected) Hide(); }
        /// <summary>手柄或键盘聚焦时显示悬停提示。</summary>
        /// <param name="eventData">公共菜单导航产生的选中事件。</param>
        public void OnSelect(BaseEventData eventData) { selected = true; if (mode == TriggerMode.Hover) Show(); }
        /// <summary>失焦且指针已离开时关闭。</summary>
        /// <param name="eventData">公共菜单导航产生的失焦事件。</param>
        public void OnDeselect(BaseEventData eventData) { selected = false; if (mode == TriggerMode.Hover && !pointerInside) Hide(); }
        private void OnDisable() { pointerInside = selected = false; Hide(); }
        private void CancelPending() { var old = showing; showing = null; old?.Cancel(); old?.Dispose(); }
        private async UniTask ObserveShowAsync(UniTask<UIOperationResult> operation)
        {
            try { await SingleUIManager.Instance.ObserveAsync(operation); }
            catch (OperationCanceledException) { }
            // source 由下一次触发或禁用释放，保持悬停期间请求令牌有效。
        }
    }
}
