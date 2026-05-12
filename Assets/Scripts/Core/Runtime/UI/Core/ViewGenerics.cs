namespace Core.Runtime
{
    public class View<T> : View
    {
        protected T params1;

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

        public virtual View<T, U, V> SetData(T data1, U data2, V data3)
        {
            params1 = data1;
            params2 = data2;
            params3 = data3;
            return this;
        }
    }
}
