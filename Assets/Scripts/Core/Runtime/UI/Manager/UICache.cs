using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Runtime
{
    public sealed class UICache
    {
        private readonly Dictionary<Type, View> views = new Dictionary<Type, View>();

        public View GetOrCreateView(Type type)
        {
            if (views.TryGetValue(type, out var view))
            {
                if (view != null && view.State != ViewState.Destroyed)
                {
                    return view;
                }

                views.Remove(type);
            }

            view = Activator.CreateInstance(type) as View;
            if (view != null)
            {
                views[type] = view;
            }

            return view;
        }

        public T GetOrCreateView<T>() where T : View
        {
            return GetOrCreateView(typeof(T)) as T;
        }

        public View GetView(Type type)
        {
            return views.TryGetValue(type, out var view) ? view : null;
        }

        public void Remove(Type type)
        {
            views.Remove(type);
        }

        public List<View> GetAllViews()
        {
            return views.Values.Where(view => view != null).ToList();
        }

        public void Clear()
        {
            views.Clear();
        }
    }
}
