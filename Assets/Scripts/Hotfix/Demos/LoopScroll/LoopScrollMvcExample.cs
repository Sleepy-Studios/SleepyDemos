using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using SleepyStudios.LoopScroll;
using SleepyStudios.LoopScroll.Samples;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.Demos.LoopScroll
{
    /// <summary>宿主独有的接入示例，通过 Showcase 主菜单进入和返回。</summary>
    public sealed class LoopScrollMvcExample : LoopSamplePage
    {
        private readonly List<LoopSampleItem> items = new List<LoopSampleItem>();
        private LoopScrollView list;
        private LoopScrollExampleView view;
        private ScrollResult? lastScrollResult;
        private bool scrollPending;
        protected override string TitleKey => "mvc";
        protected override void BuildPage()
        {
            for (var i = 0; i < 1000; i++) items.Add(new LoopSampleItem(i, "mvcItem"));
            list = MakeList("MvcBind", new Vector2(840, 460), new Vector2(0, -10), LoopLayout.Vertical);
            view = new LoopScrollExampleView(items, index => SetStatus("clicked", index, list.GetItemKey(index)));
            view.InitWithGameObject(list.gameObject);
            ActionButton("refresh", new Vector2(-205, 250), list.RefreshCells);
            ActionButton("reset", new Vector2(0, 250), () => list.RefillCells());
            ActionButton("goto500", new Vector2(205, 250), () => Locate(0));
            ActionButton("offsetPositive", new Vector2(-205, -265), () => Locate(60));
            ActionButton("cancelScroll", new Vector2(0, -265), list.CancelAnimation);
            ActionButton("offsetNegative", new Vector2(205, -265), () => Locate(-60));
            SetStatus("mvcDesc");
        }
        private void Locate(float offsetPixels)
        {
            scrollPending = true; lastScrollResult = null; SetStatus("scrollPending");
            list.ScrollToCell(500, ScrollAlignment.Center, new ScrollAnimation(.8f), offsetPixels, OnScrollFinished);
            // 替代旧请求会同步报告旧取消；页面随后显示当前新请求的进度。
            if (list.IsAnimating) { scrollPending = true; lastScrollResult = null; SetStatus("scrollPending"); }
        }
        private void OnScrollFinished(ScrollResult result)
        {
            scrollPending = false; lastScrollResult = result; ShowScrollStatus();
        }
        private void ShowScrollStatus()
        {
            if (scrollPending) SetStatus("scrollPending");
            else if (lastScrollResult.HasValue)
            {
                var result = lastScrollResult.Value;
                if (result.Status == ScrollStatus.Completed) SetStatus("scrollCompleted");
                else SetStatus("scrollCanceled", LoopSampleLanguage.Get("scrollReason" + result.CancelReason));
            }
        }
        protected override void UpdateDataLanguage() { list?.RefreshCells(); ShowScrollStatus(); }
        protected override void OnDestroy()
        { if (view != null) view.DestroyAsync().Forget(); base.OnDestroy(); }
    }
    public sealed class LoopScrollExampleItem : ItemView<LoopSampleItem>
    {
        private Text label;
        protected override void InitComponent() { label = gameObject.GetComponentInChildren<Text>(true); }
        public override ItemView<LoopSampleItem> SetData(LoopSampleItem data)
        { base.SetData(data); if (label != null) label.text = data.Display; return this; }
    }
    public sealed class LoopScrollExampleView : View
    {
        private readonly List<LoopSampleItem> items;
        private readonly System.Action<int> clicked;
        private LoopScrollView messages;
        public LoopScrollExampleView(List<LoopSampleItem> items, System.Action<int> clicked)
        { this.items = items; this.clicked = clicked; }
        protected override IUITransition CreateUITransition() => new EmptyUITransition();
        protected override void InitComponent()
        {
            messages = gameObject.GetComponent<LoopScrollView>();
            // 与钓鱼项目一样：先注册一次，再提交集合。异步业务写入前检查 context.IsCurrent。
            this.RegisterLoopScrollRect<LoopScrollExampleItem>(messages, OnMessagesRectData);
            this.RegisterLoopScrollItemHide(messages, OnMessagesItemHide);
            this.RegisterLoopScrollClick(messages, OnMessagesClick);
        }
        protected override void OnGameObjectInitialize()
        {
            messages.SetTotalCount(items, getItemKey: item => ((LoopSampleItem)item).Key);
            gameObject.SetActive(true);
        }
        private void OnMessagesRectData(ItemView cell, int index, CellBindContext context) { ((LoopScrollExampleItem)cell).SetData(items[index]); }
        private void OnMessagesItemHide(ItemView cell, CellBindContext context)
        {
            // 此处清理 ItemView 的业务资源；异步任务写入前仍需检查 CellBindContext.IsCurrent。
        }
        private void OnMessagesClick(ItemView cell, int index, CellBindContext context) { if (context.IsCurrent) clicked(index); }
    }
}
