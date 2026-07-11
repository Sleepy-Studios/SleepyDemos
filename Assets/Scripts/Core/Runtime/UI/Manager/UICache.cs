using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Runtime
{
    public sealed class UICache
    {
        private readonly Dictionary<Type, View> views = new Dictionary<Type, View>();
        private readonly object stateGate = new object();

        public View GetOrCreateView(Type type)
        {
            return GetOrCreateView(type, out _);
        }

        /// <summary>
        /// 获取指定类型的缓存 View；不存在可复用实例时创建并缓存新实例。
        /// </summary>
        /// <param name="type">目标 View 类型。</param>
        /// <param name="created">返回 true 表示本次调用创建了新实例。</param>
        /// <returns>缓存或新建的 View；类型无法实例化为 View 时返回 null。</returns>
        public View GetOrCreateView(Type type, out bool created)
        {
            lock (stateGate)
            {
                if (views.TryGetValue(type, out var view))
                {
                    if (view != null && view.State != ViewState.Destroyed)
                    {
                        created = false;
                        return view;
                    }

                    views.Remove(type);
                }

                view = Activator.CreateInstance(type) as View;
                created = view != null;
                if (view != null)
                {
                    views[type] = view;
                }

                return view;
            }
        }

        public T GetOrCreateView<T>() where T : View
        {
            return GetOrCreateView(typeof(T)) as T;
        }

        public View GetView(Type type)
        {
            lock (stateGate)
            {
                return views.TryGetValue(type, out var view) ? view : null;
            }
        }

        public bool TryGet(Type type, out View view)
        {
            lock (stateGate)
            {
                return views.TryGetValue(type, out view) && view != null;
            }
        }

        public void Remove(Type type)
        {
            lock (stateGate)
            {
                views.Remove(type);
            }
        }

        public bool Remove(View view)
        {
            lock (stateGate)
            {
                return view != null && views.TryGetValue(view.GetType(), out var cached) &&
                       ReferenceEquals(view, cached) && views.Remove(view.GetType());
            }
        }

        public List<View> GetAllViews()
        {
            lock (stateGate)
            {
                return views.Values.Where(view => view != null).ToList();
            }
        }

        public void Clear()
        {
            lock (stateGate)
            {
                views.Clear();
            }
        }
    }
}
