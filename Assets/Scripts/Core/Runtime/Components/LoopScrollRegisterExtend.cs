using System;
using SleepyStudios.LoopScroll;

namespace Core.Runtime
{
    /// <summary>宿主注册入口；View 持有生命周期，嵌套 ItemView 显式持有订阅句柄。</summary>
    public static class LoopScrollRegisterExtend
    {
        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { var callback = dispose; dispose = null; callback?.Invoke(); }
        }
        /// <summary>获取物理 Cell 的 ItemView 缓存，每个列表仅创建一个桥接。</summary>
        /// <param name="list">包列表组件。</param>
        /// <returns>宿主桥接。</returns>
        public static LoopScrollItemViewBridge ItemViews(this LoopScrollView list)
        {
            var bridge = list.GetComponent<LoopScrollItemViewBridge>();
            return bridge != null ? bridge : list.gameObject.AddComponent<LoopScrollItemViewBridge>();
        }
        /// <summary>配置固定 ItemView 工厂并注册绑定；后续使用 SetTotalCount 提交集合。</summary>
        /// <typeparam name="TView">物理 Cell 对应的固定 ItemView 类型。</typeparam>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件。</param>
        /// <param name="callback">当前 ItemView、索引和绑定上下文；异步写入前检查 IsCurrent。</param>
        public static void RegisterLoopScrollRect<TView>(this View view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback) where TView : ItemView, new()
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            list.ItemViews().Configure<TView>();
            view.RegisterLoopScrollRect(list, callback);
        }
        /// <summary>配置固定 ItemView 工厂并注册绑定；后续使用 SetTotalCount 提交集合。</summary>
        /// <typeparam name="TView">物理 Cell 对应的固定 ItemView 类型。</typeparam>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件。</param>
        /// <param name="callback">当前 ItemView、索引和绑定上下文；异步写入前检查 IsCurrent。</param>
        /// <returns>需要在嵌套 ItemView 生命周期结束时释放的句柄。</returns>
        public static IDisposable RegisterLoopScrollRect<TView>(this ItemView view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback) where TView : ItemView, new()
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            list.ItemViews().Configure<TView>();
            return view.RegisterLoopScrollRect(list, callback);
        }
        /// <summary>注册绑定，重复注册同一委托只保留一次。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        [ComponentAttribute("On{0}RectData")]
        public static void RegisterLoopScrollRect(this View view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        { view.AddBinding(SubscribeRect(list, callback)); }
        /// <summary>注册绑定，重复注册同一委托只保留一次。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        /// <returns>幂等解除订阅句柄，嵌套 ItemView 应在回收时释放。</returns>
        [ComponentAttribute("On{0}RectData")]
        public static IDisposable RegisterLoopScrollRect(this ItemView view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        { return SubscribeRect(list, callback); }
        private static IDisposable SubscribeRect(LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var bridge = list.ItemViews(); bridge.CellBound -= callback; bridge.CellBound += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellBound -= callback; });
        }
        /// <summary>注册点击，事件只接收有效的当前绑定身份。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterLoopScrollClick(this View view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        { view.AddBinding(SubscribeClick(list, callback)); }
        /// <summary>注册点击，事件只接收有效的当前绑定身份。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        /// <returns>幂等解除订阅句柄，嵌套 ItemView 应在回收时释放。</returns>
        [ComponentAttribute("On{0}Click")]
        public static IDisposable RegisterLoopScrollClick(this ItemView view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        { return SubscribeClick(list, callback); }
        private static IDisposable SubscribeClick(LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var bridge = list.ItemViews(); bridge.CellClicked -= callback; bridge.CellClicked += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellClicked -= callback; });
        }
        /// <summary>注册解绑，此时旧绑定上下文已经失效。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        [ComponentAttribute("On{0}ItemHide")]
        public static void RegisterLoopScrollItemHide(this View view, LoopScrollView list, Action<ItemView, CellBindContext> callback)
        { view.AddBinding(SubscribeItemHide(list, callback)); }
        /// <summary>注册解绑，此时旧绑定上下文已经失效。</summary>
        /// <param name="view">订阅拥有者。</param>
        /// <param name="list">列表组件；生成回调前由宿主配置 ItemView 工厂。</param>
        /// <param name="callback">ItemView 与绑定上下文。</param>
        /// <returns>幂等解除订阅句柄，嵌套 ItemView 应在回收时释放。</returns>
        [ComponentAttribute("On{0}ItemHide")]
        public static IDisposable RegisterLoopScrollItemHide(this ItemView view, LoopScrollView list, Action<ItemView, CellBindContext> callback)
        { return SubscribeItemHide(list, callback); }
        private static IDisposable SubscribeItemHide(LoopScrollView list, Action<ItemView, CellBindContext> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var bridge = list.ItemViews(); bridge.CellUnbound -= callback; bridge.CellUnbound += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellUnbound -= callback; });
        }
    }
}
