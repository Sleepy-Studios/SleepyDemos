using System;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;

namespace Core.Runtime
{
    /// 将项目普通 C# ItemView 适配到包的物理 Cell；包不引用此类。
    [DisallowMultipleComponent, RequireComponent(typeof(LoopScrollView))]
    public sealed class LoopScrollItemViewBridge : MonoBehaviour
    {
        private sealed class Entry
        {
            public ItemView View;
            public CellBindContext Context;
        }
        private readonly Dictionary<LoopCell, Entry> entries = new Dictionary<LoopCell, Entry>();
        private LoopScrollView list;
        private bool initialized;
        public event Action<ItemView, int, CellBindContext> CellBound;
        public event Action<ItemView, CellBindContext> CellUnbound;
        public event Action<ItemView, int, CellBindContext> CellClicked;
        private void Awake() { Initialize(); }
        private void Initialize()
        {
            if (initialized) return;
            list = GetComponent<LoopScrollView>();
            list.CellBound += OnBound; list.CellUnbound += OnUnbound; list.CellClicked += OnClick;
            initialized = true;
        }
        /// <summary>用现有 ItemView 绑定列表；每个物理 Cell 仅创建一次对应 View。</summary>
        /// <typeparam name="TItem">宿主业务数据。</typeparam>
        /// <typeparam name="TView">普通 C# ItemView 子类；必须有无参构造。</typeparam>
        /// <param name="items">调用方拥有的集合。</param>
        /// <param name="bind">绑定数据，异步完成时检查 context.IsCurrent。</param>
        /// <param name="unbind">解绑时释放业务资源；包已取消 context Token。</param>
        /// <param name="keySelector">稳定业务 Key，跨 Reload 锚点必须提供。</param>
        /// <param name="options">重载位置策略，默认起点。</param>
        public void SetItems<TItem, TView>(IReadOnlyList<TItem> items, Action<TView, TItem, CellBindContext> bind,
            Action<TView, CellBindContext> unbind = null, Func<TItem, string> keySelector = null, ReloadOptions options = default)
            where TView : ItemView, new()
        {
            // View.InitWithGameObject 在 inactive 根节点上调用；不能依赖 Awake 已发生。
            Initialize();
            list.SetData<TItem, LoopCell>(items, (cell, item, context) =>
            {
                var view = GetOrCreate<TView>(cell, context); bind?.Invoke(view, item, context);
            }, (cell, context) =>
            {
                if (entries.TryGetValue(cell, out var entry) && entry.View is TView view) unbind?.Invoke(view, context);
            }, keySelector, options);
        }
        /// <summary>高级多类型数据源在 BindCell 中获取对应 ItemView；每个物理 Cell 复用实例。</summary>
        /// <typeparam name="TView">此 Cell 类型对应的 ItemView 类型。</typeparam>
        /// <param name="cell">本次 BindCell 收到的物理 Cell。</param>
        /// <param name="context">与 cell 对应的当前有效绑定上下文。</param>
        /// <returns>更新过索引与上下文的 ItemView。</returns>
        public TView GetOrCreate<TView>(LoopCell cell, CellBindContext context) where TView : ItemView, new()
        {
            Initialize();
            if (cell == null || !context.IsCurrent) throw new ArgumentException("需要当前有效 Cell 绑定。");
            if (!entries.TryGetValue(cell, out var entry)) { entry = new Entry(); entries.Add(cell, entry); }
            if (!(entry.View is TView))
            {
                if (entry.View != null) throw new InvalidOperationException("同一 Cell 类型必须对应固定 ItemView 类型；不同 ItemView 请使用独立 Prefab 类型池。");
                var view = new TView(); view.Init(cell.gameObject, context.Index); entry.View = view;
                // 捕获物理 Cell 的 Entry，点击时读取当前 context，避免保存旧索引。
                view.onClick = index => { if (entry.Context.IsCurrent) CellClicked?.Invoke(entry.View, entry.Context.Index, entry.Context); };
            }
            entry.Context = context; entry.View.SetIndex(context.Index); return (TView)entry.View;
        }
        /// <summary>高级数据源在 UnbindCell 中读取缓存 View，释放业务资源。</summary>
        /// <param name="cell">已解绑的物理 Cell。</param>
        /// <param name="view">对应缓存，没有创建过时为空。</param>
        /// <returns>是否找到缓存 View。</returns>
        public bool TryGetItemView(LoopCell cell, out ItemView view)
        { if (cell != null && entries.TryGetValue(cell, out var entry)) { view = entry.View; return true; } view = null; return false; }
        private void OnBound(LoopCell cell, CellBindContext context)
        { if (entries.TryGetValue(cell, out var entry)) CellBound?.Invoke(entry.View, context.Index, context); }
        private void OnUnbound(LoopCell cell, CellBindContext context)
        { if (entries.TryGetValue(cell, out var entry)) CellUnbound?.Invoke(entry.View, context); }
        private void OnClick(LoopCell cell, CellBindContext context)
        { if (context.IsCurrent && entries.TryGetValue(cell, out var entry)) CellClicked?.Invoke(entry.View, context.Index, context); }
        private void OnDestroy()
        {
            if (list != null) { list.CellBound -= OnBound; list.CellUnbound -= OnUnbound; list.CellClicked -= OnClick; }
            foreach (var entry in entries.Values) if (entry.View != null) entry.View.onClick = null;
            entries.Clear(); CellBound = null; CellUnbound = null; CellClicked = null;
        }
    }
}
