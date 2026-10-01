using System;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;

namespace Core.Runtime
{
    public static class LoopScrollRegisterExtend
    {
        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { var callback = dispose; dispose = null; callback?.Invoke(); }
        }
        /// <summary>获取列表宿主桥接；每个列表只添加一个实例。</summary>
        /// <param name="list">包的列表组件。</param>
        /// <returns>缓存物理 Cell 的 ItemView 桥接。</returns>
        public static LoopScrollItemViewBridge ItemViews(this LoopScrollView list)
        {
            var bridge = list.GetComponent<LoopScrollItemViewBridge>();
            return bridge != null ? bridge : list.gameObject.AddComponent<LoopScrollItemViewBridge>();
        }
        /// <summary>注册 MvcBind 绑定回调，重复注册同一委托只保留一份。</summary>
        /// <param name="view">拥有订阅生命周期的宿主 View。</param>
        /// <param name="list">列表组件。</param>
        /// <param name="callback">当前 ItemView、索引和绑定上下文。</param>
        [ComponentAttribute("On{0}CellBind")]
        public static void RegisterLoopCellBind(this View view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellBound -= callback; bridge.CellBound += callback;
            view.AddBinding(new Subscription(() => { if (bridge != null) bridge.CellBound -= callback; }));
        }
        /// <summary>注册 MvcBind 解绑回调；上下文此时已经失效。</summary>
        /// <param name="view">拥有订阅生命周期的 View。</param>
        /// <param name="list">列表组件。</param>
        /// <param name="callback">回收的 ItemView 与旧上下文。</param>
        [ComponentAttribute("On{0}CellUnbind")]
        public static void RegisterLoopCellUnbind(this View view, LoopScrollView list, Action<ItemView, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellUnbound -= callback; bridge.CellUnbound += callback;
            view.AddBinding(new Subscription(() => { if (bridge != null) bridge.CellUnbound -= callback; }));
        }
        /// <summary>注册 MvcBind 点击回调；只接受仍有效的当前绑定身份。</summary>
        /// <param name="view">拥有订阅生命周期的 View。</param>
        /// <param name="list">列表组件。</param>
        /// <param name="callback">当前 ItemView、索引和上下文。</param>
        [ComponentAttribute("On{0}CellClick")]
        public static void RegisterLoopCellClick(this View view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellClicked -= callback; bridge.CellClicked += callback;
            view.AddBinding(new Subscription(() => { if (bridge != null) bridge.CellClicked -= callback; }));
        }
        /// <summary>嵌套 ItemView 注册绑定回调；列表组件销毁时清理，也可显式 Dispose。</summary>
        /// <param name="view">包含该列表的 ItemView。</param>
        /// <param name="list">嵌套列表。</param>
        /// <param name="callback">当前子 ItemView、索引和上下文。</param>
        /// <returns>幂等解除订阅句柄。</returns>
        [ComponentAttribute("On{0}CellBind")]
        public static IDisposable RegisterLoopCellBind(this ItemView view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellBound -= callback; bridge.CellBound += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellBound -= callback; });
        }
        /// <summary>嵌套 ItemView 注册解绑回调；列表销毁清理，也可显式 Dispose。</summary>
        /// <param name="view">包含该列表的 ItemView。</param>
        /// <param name="list">嵌套列表。</param>
        /// <param name="callback">已回收的子 ItemView 与旧上下文。</param>
        /// <returns>解除订阅句柄。</returns>
        [ComponentAttribute("On{0}CellUnbind")]
        public static IDisposable RegisterLoopCellUnbind(this ItemView view, LoopScrollView list, Action<ItemView, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellUnbound -= callback; bridge.CellUnbound += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellUnbound -= callback; });
        }
        /// <summary>嵌套 ItemView 注册点击回调；列表销毁清理，也可显式 Dispose。</summary>
        /// <param name="view">包含该列表的 ItemView。</param>
        /// <param name="list">嵌套列表。</param>
        /// <param name="callback">当前子 ItemView、索引和上下文。</param>
        /// <returns>解除订阅句柄。</returns>
        [ComponentAttribute("On{0}CellClick")]
        public static IDisposable RegisterLoopCellClick(this ItemView view, LoopScrollView list, Action<ItemView, int, CellBindContext> callback)
        {
            var bridge = list.ItemViews(); bridge.CellClicked -= callback; bridge.CellClicked += callback;
            return new Subscription(() => { if (bridge != null) bridge.CellClicked -= callback; });
        }
    }
}
