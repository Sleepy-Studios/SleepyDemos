using System;
using System.Collections.Generic;

namespace Core.Runtime
{
    public sealed class UICache
    {
        private readonly Dictionary<Type, View> views = new Dictionary<Type, View>();

        public View GetOrCreateView(Type type)
        {
            if (views.TryGetValue(type, out var view))
            {
                return view;
            }

            view = Activator.CreateInstance(type) as View;
            if (view != null)
            {
                views.Add(type, view);
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
    }
}
