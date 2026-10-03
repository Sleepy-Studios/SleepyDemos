namespace Core.Runtime
{
    public class View<T> : View
    {
        protected T params1;

        /// <summary>接收页面数据；UIManager在控件初始化完成后、显示前调用。子类可直接更新控件。</summary>
        /// <param name="data">本次导航请求的数据；基类保留参数供现有页面后续行为使用。</param>
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

        /// <summary>接收页面数据；UIManager在控件初始化完成后、显示前调用。</summary>
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

        /// <summary>接收页面数据；UIManager在控件初始化完成后、显示前调用。</summary>
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
