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

        public void Add(Action<T> action)
        {
            if (action == null || actions.Contains(action))
            {
                return;
            }

            actions.Add(action);
            refCount = actions.Count;
        }

        public void Remove(Action<T> action)
        {
            if (action == null)
            {
                return;
            }

            actions.Remove(action);
            refCount = actions.Count;
        }

        public void Invoke(T data)
        {
            var snapshot = actions.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]?.Invoke(data);
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

        public void Add(Action<T, U> action)
        {
            if (action != null && !actions.Contains(action))
            {
                actions.Add(action);
            }
        }

        public void Remove(Action<T, U> action)
        {
            if (action == null)
            {
                return;
            }

            actions.Remove(action);
        }

        public void Invoke(T data1, U data2)
        {
            var snapshot = actions.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]?.Invoke(data1, data2);
            }
        }
    }
}
