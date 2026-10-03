namespace Core.Runtime
{
    public class View<T> : View
    {
        protected T params1;

        /// <summary>保存页面配置；导航可能在资源加载和控件绑定前调用，不在这里刷新 UI。</summary>
        /// <param name="data">本次导航数据；业务在 OnShow 的独立刷新方法中读取最新值。</param>
        /// <returns>当前页面。</returns>
        public virtual View<T> SetData(T data)
        {
            params1 = data;
            return this;
        }
    }

    public class View<T, U> : View
    {
        protected T params1;
        protected U params2;

        /// <summary>保存页面配置；可能早于 InitComponent，控件刷新由业务 OnShow 完成。</summary>
        /// <param name="data1">第一份导航数据。</param>
        /// <param name="data2">第二份导航数据。</param>
        /// <returns>当前页面。</returns>
        public virtual View<T, U> SetData(T data1, U data2)
        {
            params1 = data1;
            params2 = data2;
            return this;
        }
    }

    public class View<T, U, V> : View
    {
        protected T params1;
        protected U params2;
        protected V params3;

        /// <summary>保存页面配置；可能早于 InitComponent，控件刷新由业务 OnShow 完成。</summary>
        /// <param name="data1">第一份导航数据。</param>
        /// <param name="data2">第二份导航数据。</param>
        /// <param name="data3">第三份导航数据。</param>
        /// <returns>当前页面。</returns>
        public virtual View<T, U, V> SetData(T data1, U data2, V data3)
        {
            params1 = data1;
            params2 = data2;
            params3 = data3;
            return this;
        }
    }
}
