using System;
using System.Collections.Generic;

namespace Core.Runtime
{
    public abstract class ActionBase
    {
        public int refCount;
        public abstract void Invoke(object data);
    }

    public sealed class ActionConvert<T> : ActionBase
    {
        private readonly List<Action<T>> actions = new List<Action<T>>();
        private Action<T>[] snapshot = Array.Empty<Action<T>>();

        public void Add(Action<T> action)
        {
            if (action == null || actions.Contains(action))
            {
                return;
            }

            actions.Add(action);
            snapshot = actions.ToArray();
            refCount = actions.Count;
        }

        public void Remove(Action<T> action)
        {
            if (action == null)
            {
                return;
            }

            if (actions.Remove(action))
            {
                snapshot = actions.ToArray();
            }
            refCount = actions.Count;
        }

        public void Invoke(T data)
        {
            var current = snapshot;
            for (int i = 0; i < current.Length; i++)
            {
                current[i]?.Invoke(data);
            }
        }

        public override void Invoke(object data)
        {
            if (data is T typed)
            {
                Invoke(typed);
            }
        }
    }

    public sealed class ActionConvert<T, U>
    {
        private readonly List<Action<T, U>> actions = new List<Action<T, U>>();
        private Action<T, U>[] snapshot = Array.Empty<Action<T, U>>();

        public void Add(Action<T, U> action)
        {
            if (action != null && !actions.Contains(action))
            {
                actions.Add(action);
                snapshot = actions.ToArray();
            }
        }

        public void Remove(Action<T, U> action)
        {
            if (action == null)
            {
                return;
            }

            if (actions.Remove(action))
            {
                snapshot = actions.ToArray();
            }
        }

        public void Invoke(T data1, U data2)
        {
            var current = snapshot;
            for (int i = 0; i < current.Length; i++)
            {
                current[i]?.Invoke(data1, data2);
            }
        }
    }
}
