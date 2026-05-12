namespace Core.Runtime
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = new T();
                instance.OnSingletonInit();
                return instance;
            }
        }

        protected virtual void OnSingletonInit()
        {
        }
    }
}
