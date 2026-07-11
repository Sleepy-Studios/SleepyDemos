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
            lock (stateGate)
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
