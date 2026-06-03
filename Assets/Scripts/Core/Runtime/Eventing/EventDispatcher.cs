using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Runtime
{
    public static class EventDispatcher
    {
        private static readonly Dictionary<string, EventSignalBase> signals = new Dictionary<string, EventSignalBase>();
        private static readonly List<string> removeBuffer = new List<string>();

        public static void AddEventListener(string eventName, Action handler, bool permanent = false)
        {
            if (!ValidateHandler(eventName, handler))
            {
                return;
            }

            var signal = GetOrCreateSignal(eventName, () => new EventSignal(), GetSignature());
            signal?.Add(handler, permanent);
        }

        public static void AddEventListener<T>(string eventName, Action<T> handler, bool permanent = false)
        {
            if (!ValidateHandler(eventName, handler))
            {
                return;
            }

            var signal = GetOrCreateSignal(eventName, () => new EventSignal<T>(), GetSignature<T>());
            signal?.Add(handler, permanent);
        }

        public static void AddEventListener<T, U>(string eventName, Action<T, U> handler, bool permanent = false)
        {
            if (!ValidateHandler(eventName, handler))
            {
                return;
            }

            var signal = GetOrCreateSignal(eventName, () => new EventSignal<T, U>(), GetSignature<T, U>());
            signal?.Add(handler, permanent);
        }

        public static void AddEventListener<T, U, V>(string eventName, Action<T, U, V> handler, bool permanent = false)
        {
            if (!ValidateHandler(eventName, handler))
            {
                return;
            }

            var signal = GetOrCreateSignal(eventName, () => new EventSignal<T, U, V>(), GetSignature<T, U, V>());
            signal?.Add(handler, permanent);
        }

        public static void AddEventListener<T, U, V, W>(string eventName, Action<T, U, V, W> handler, bool permanent = false)
        {
            if (!ValidateHandler(eventName, handler))
            {
                return;
            }

            var signal = GetOrCreateSignal(eventName, () => new EventSignal<T, U, V, W>(), GetSignature<T, U, V, W>());
            signal?.Add(handler, permanent);
        }

        public static void RemoveEventListener(string eventName, Action handler)
        {
            RemoveEventListenerInternal<EventSignal>(eventName, handler, GetSignature());
        }

        public static void RemoveEventListener<T>(string eventName, Action<T> handler)
        {
            RemoveEventListenerInternal<EventSignal<T>>(eventName, handler, GetSignature<T>());
        }

        public static void RemoveEventListener<T, U>(string eventName, Action<T, U> handler)
        {
            RemoveEventListenerInternal<EventSignal<T, U>>(eventName, handler, GetSignature<T, U>());
        }

        public static void RemoveEventListener<T, U, V>(string eventName, Action<T, U, V> handler)
        {
            RemoveEventListenerInternal<EventSignal<T, U, V>>(eventName, handler, GetSignature<T, U, V>());
        }

        public static void RemoveEventListener<T, U, V, W>(string eventName, Action<T, U, V, W> handler)
        {
            RemoveEventListenerInternal<EventSignal<T, U, V, W>>(eventName, handler, GetSignature<T, U, V, W>());
        }

        public static void TriggerEvent(string eventName)
        {
            if (TryGetSignal<EventSignal>(eventName, GetSignature(), out var signal))
            {
                signal.Invoke(eventName);
            }
        }

        public static void TriggerEvent<T>(string eventName, T arg)
        {
            if (TryGetSignal<EventSignal<T>>(eventName, GetSignature<T>(), out var signal))
            {
                signal.Invoke(eventName, arg);
            }
        }

        public static void TriggerEvent<T, U>(string eventName, T arg1, U arg2)
        {
            if (TryGetSignal<EventSignal<T, U>>(eventName, GetSignature<T, U>(), out var signal))
            {
                signal.Invoke(eventName, arg1, arg2);
            }
        }

        public static void TriggerEvent<T, U, V>(string eventName, T arg1, U arg2, V arg3)
        {
            if (TryGetSignal<EventSignal<T, U, V>>(eventName, GetSignature<T, U, V>(), out var signal))
            {
                signal.Invoke(eventName, arg1, arg2, arg3);
            }
        }

        public static void TriggerEvent<T, U, V, W>(string eventName, T arg1, U arg2, V arg3, W arg4)
        {
            if (TryGetSignal<EventSignal<T, U, V, W>>(eventName, GetSignature<T, U, V, W>(), out var signal))
            {
                signal.Invoke(eventName, arg1, arg2, arg3, arg4);
            }
        }

        public static void RemoveEvent(string eventName)
        {
            if (!ValidateEventName(eventName))
            {
                return;
            }

            signals.Remove(eventName);
        }

        public static bool HasEvent(string eventName)
        {
            return !string.IsNullOrWhiteSpace(eventName) && signals.ContainsKey(eventName);
        }

        public static int GetListenerCount(string eventName)
        {
            return !string.IsNullOrWhiteSpace(eventName) && signals.TryGetValue(eventName, out var signal) ? signal.ListenerCount : 0;
        }

        public static void RemoveAll(bool includePermanent = true)
        {
            if (includePermanent)
            {
                signals.Clear();
                return;
            }

            removeBuffer.Clear();
            foreach (var item in signals)
            {
                item.Value.RemoveAll(false);
                if (item.Value.ListenerCount <= 0)
                {
                    removeBuffer.Add(item.Key);
                }
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                signals.Remove(removeBuffer[i]);
            }

            removeBuffer.Clear();
        }

        private static TSignal GetOrCreateSignal<TSignal>(string eventName, Func<TSignal> factory, string requestedSignature)
            where TSignal : EventSignalBase
        {
            if (!ValidateEventName(eventName))
            {
                return null;
            }

            if (!signals.TryGetValue(eventName, out var signal))
            {
                var created = factory();
                signals.Add(eventName, created);
                return created;
            }

            if (signal is TSignal typedSignal)
            {
                return typedSignal;
            }

            LogSignatureMismatch(eventName, signal, requestedSignature);
            return null;
        }

        private static bool TryGetSignal<TSignal>(string eventName, string requestedSignature, out TSignal typedSignal)
            where TSignal : EventSignalBase
        {
            typedSignal = null;
            if (!ValidateEventName(eventName))
            {
                return false;
            }

            if (!signals.TryGetValue(eventName, out var signal))
            {
                return false;
            }

            if (signal is TSignal matched)
            {
                typedSignal = matched;
                return true;
            }

            LogSignatureMismatch(eventName, signal, requestedSignature);
            return false;
        }

        private static void RemoveEventListenerInternal<TSignal>(string eventName, Delegate handler, string requestedSignature)
            where TSignal : EventSignalBase
        {
            if (!ValidateEventName(eventName) || handler == null)
            {
                return;
            }

            if (!signals.TryGetValue(eventName, out var signal))
            {
                return;
            }

            if (signal is not TSignal)
            {
                LogSignatureMismatch(eventName, signal, requestedSignature);
                return;
            }

            signal.Remove(handler);
            if (signal.ListenerCount <= 0)
            {
                signals.Remove(eventName);
            }
        }

        private static bool ValidateEventName(string eventName)
        {
            if (!string.IsNullOrWhiteSpace(eventName))
            {
                return true;
            }

            Debug.LogError("[EventDispatcher] 事件名不能为空。");
            return false;
        }

        private static bool ValidateHandler(string eventName, Delegate handler)
        {
            if (handler != null)
            {
                return true;
            }

            Debug.LogError($"[EventDispatcher] 事件 {eventName} 的监听回调不能为空。");
            return false;
        }

        private static string GetSignature()
        {
            return "Action";
        }

        private static string GetSignature<T>()
        {
            return $"Action<{typeof(T).Name}>";
        }

        private static string GetSignature<T, U>()
        {
            return $"Action<{typeof(T).Name}, {typeof(U).Name}>";
        }

        private static string GetSignature<T, U, V>()
        {
            return $"Action<{typeof(T).Name}, {typeof(U).Name}, {typeof(V).Name}>";
        }

        private static string GetSignature<T, U, V, W>()
        {
            return $"Action<{typeof(T).Name}, {typeof(U).Name}, {typeof(V).Name}, {typeof(W).Name}>";
        }

        private static void LogSignatureMismatch(string eventName, EventSignalBase existingSignal, string requestedSignature)
        {
            Debug.LogError($"[EventDispatcher] 事件 {eventName} 的参数签名不一致。已有: {existingSignal.Signature}，请求: {requestedSignature}。");
        }
    }
}
