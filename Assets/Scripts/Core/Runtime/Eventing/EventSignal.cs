using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Runtime
{
    internal abstract class EventSignalBase
    {
        public abstract int ListenerCount { get; }
        public abstract string Signature { get; }
        public abstract void Remove(Delegate handler);
        public abstract void RemoveAll(bool includePermanent);
    }

    internal sealed class EventSignal : EventSignalBase
    {
        private readonly List<Listener> listeners = new List<Listener>();

        public override int ListenerCount => listeners.Count;
        public override string Signature => "Action";

        public void Add(Action handler, bool permanent)
        {
            if (handler == null || Contains(handler))
            {
                return;
            }

            listeners.Add(new Listener(handler, permanent));
        }

        public void Invoke(string eventName)
        {
            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception($"事件 {eventName} 的监听回调执行失败。", exception));
                }
            }
        }

        public void Remove(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Handler == handler)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Remove(Delegate handler)
        {
            Remove(handler as Action);
        }

        public override void RemoveAll(bool includePermanent)
        {
            if (includePermanent)
            {
                listeners.Clear();
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (!listeners[i].Permanent)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private bool Contains(Action handler)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Handler == handler)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Listener
        {
            public readonly Action Handler;
            public readonly bool Permanent;

            public Listener(Action handler, bool permanent)
            {
                Handler = handler;
                Permanent = permanent;
            }
        }
    }

    internal sealed class EventSignal<T> : EventSignalBase
    {
        private readonly List<Listener> listeners = new List<Listener>();

        public override int ListenerCount => listeners.Count;
        public override string Signature => $"Action<{typeof(T).Name}>";

        public void Add(Action<T> handler, bool permanent)
        {
            if (handler == null || Contains(handler))
            {
                return;
            }

            listeners.Add(new Listener(handler, permanent));
        }

        public void Invoke(string eventName, T arg)
        {
            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler?.Invoke(arg);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception($"事件 {eventName} 的监听回调执行失败。", exception));
                }
            }
        }

        public void Remove(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Handler == handler)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Remove(Delegate handler)
        {
            Remove(handler as Action<T>);
        }

        public override void RemoveAll(bool includePermanent)
        {
            if (includePermanent)
            {
                listeners.Clear();
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (!listeners[i].Permanent)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private bool Contains(Action<T> handler)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Handler == handler)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Listener
        {
            public readonly Action<T> Handler;
            public readonly bool Permanent;

            public Listener(Action<T> handler, bool permanent)
            {
                Handler = handler;
                Permanent = permanent;
            }
        }
    }

    internal sealed class EventSignal<T, U> : EventSignalBase
    {
        private readonly List<Listener> listeners = new List<Listener>();

        public override int ListenerCount => listeners.Count;
        public override string Signature => $"Action<{typeof(T).Name}, {typeof(U).Name}>";

        public void Add(Action<T, U> handler, bool permanent)
        {
            if (handler == null || Contains(handler))
            {
                return;
            }

            listeners.Add(new Listener(handler, permanent));
        }

        public void Invoke(string eventName, T arg1, U arg2)
        {
            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler?.Invoke(arg1, arg2);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception($"事件 {eventName} 的监听回调执行失败。", exception));
                }
            }
        }

        public void Remove(Action<T, U> handler)
        {
            if (handler == null)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Handler == handler)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Remove(Delegate handler)
        {
            Remove(handler as Action<T, U>);
        }

        public override void RemoveAll(bool includePermanent)
        {
            if (includePermanent)
            {
                listeners.Clear();
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (!listeners[i].Permanent)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private bool Contains(Action<T, U> handler)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Handler == handler)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Listener
        {
            public readonly Action<T, U> Handler;
            public readonly bool Permanent;

            public Listener(Action<T, U> handler, bool permanent)
            {
                Handler = handler;
                Permanent = permanent;
            }
        }
    }

    internal sealed class EventSignal<T, U, V> : EventSignalBase
    {
        private readonly List<Listener> listeners = new List<Listener>();

        public override int ListenerCount => listeners.Count;
        public override string Signature => $"Action<{typeof(T).Name}, {typeof(U).Name}, {typeof(V).Name}>";

        public void Add(Action<T, U, V> handler, bool permanent)
        {
            if (handler == null || Contains(handler))
            {
                return;
            }

            listeners.Add(new Listener(handler, permanent));
        }

        public void Invoke(string eventName, T arg1, U arg2, V arg3)
        {
            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler?.Invoke(arg1, arg2, arg3);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception($"事件 {eventName} 的监听回调执行失败。", exception));
                }
            }
        }

        public void Remove(Action<T, U, V> handler)
        {
            if (handler == null)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Handler == handler)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Remove(Delegate handler)
        {
            Remove(handler as Action<T, U, V>);
        }

        public override void RemoveAll(bool includePermanent)
        {
            if (includePermanent)
            {
                listeners.Clear();
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (!listeners[i].Permanent)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private bool Contains(Action<T, U, V> handler)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Handler == handler)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Listener
        {
            public readonly Action<T, U, V> Handler;
            public readonly bool Permanent;

            public Listener(Action<T, U, V> handler, bool permanent)
            {
                Handler = handler;
                Permanent = permanent;
            }
        }
    }

    internal sealed class EventSignal<T, U, V, W> : EventSignalBase
    {
        private readonly List<Listener> listeners = new List<Listener>();

        public override int ListenerCount => listeners.Count;
        public override string Signature => $"Action<{typeof(T).Name}, {typeof(U).Name}, {typeof(V).Name}, {typeof(W).Name}>";

        public void Add(Action<T, U, V, W> handler, bool permanent)
        {
            if (handler == null || Contains(handler))
            {
                return;
            }

            listeners.Add(new Listener(handler, permanent));
        }

        public void Invoke(string eventName, T arg1, U arg2, V arg3, W arg4)
        {
            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Handler?.Invoke(arg1, arg2, arg3, arg4);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception($"事件 {eventName} 的监听回调执行失败。", exception));
                }
            }
        }

        public void Remove(Action<T, U, V, W> handler)
        {
            if (handler == null)
            {
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Handler == handler)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public override void Remove(Delegate handler)
        {
            Remove(handler as Action<T, U, V, W>);
        }

        public override void RemoveAll(bool includePermanent)
        {
            if (includePermanent)
            {
                listeners.Clear();
                return;
            }

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (!listeners[i].Permanent)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private bool Contains(Action<T, U, V, W> handler)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Handler == handler)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Listener
        {
            public readonly Action<T, U, V, W> Handler;
            public readonly bool Permanent;

            public Listener(Action<T, U, V, W> handler, bool permanent)
            {
                Handler = handler;
                Permanent = permanent;
            }
        }
    }
}
