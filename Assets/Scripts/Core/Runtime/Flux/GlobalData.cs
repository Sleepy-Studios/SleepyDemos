using System;
using System.Collections.Generic;

namespace Core.Runtime
{
    public static class GlobalData
    {
        private static readonly Type objectType = typeof(object);
        private static readonly Dictionary<Type, IData> state = new Dictionary<Type, IData>();
        private static readonly Dictionary<Type, List<IHandler>> reducers = new Dictionary<Type, List<IHandler>>();
        private static readonly Dictionary<Type, ActionBase> subjects = new Dictionary<Type, ActionBase>();
        private static readonly HashSet<Type> processingList = new HashSet<Type>();

        public static T Add<T>() where T : IData, new()
        {
            var type = typeof(T);
            if (state.TryGetValue(type, out var existing))
            {
                return (T)existing;
            }

            var instance = new T();
            state.Add(type, instance);
            subjects.TryAdd(type, new ActionConvert<T>());
            InitHandlers(instance.Handlers, instance);
            return instance;
        }

        public static T Add<T>(T instance) where T : IData
        {
            var type = typeof(T);
            if (!state.ContainsKey(type))
            {
                state.Add(type, instance);
                subjects.TryAdd(type, new ActionConvert<T>());
            }

            InitHandlers(instance.Handlers, instance);
            return (T)state[type];
        }

        internal static bool Modify<T>(T instance) where T : IData
        {
            var type = typeof(T);
            processingList.Remove(type);
            if (!state.ContainsKey(type))
            {
                return false;
            }

            state[type] = instance;
            if (subjects.TryGetValue(type, out var actionData))
            {
                actionData.Invoke(instance);
            }

            return true;
        }

        public static T Get<T>() where T : IData
        {
            return state.TryGetValue(typeof(T), out var data) ? (T)data : default;
        }

        public static bool Remove<T>() where T : IData
        {
            return state.Remove(typeof(T));
        }

        public static void ClearData()
        {
            foreach (var item in state.Values)
            {
                item.ClearData();
            }
        }

        public static void Dispatch(IAction action)
        {
            var actionType = action.GetType();
            if (!reducers.TryGetValue(actionType, out var handlers))
            {
                var baseType = actionType.BaseType;
                while (baseType != null && baseType != objectType && !baseType.IsInterface)
                {
                    if (reducers.TryGetValue(baseType, out handlers))
                    {
                        break;
                    }
                    baseType = baseType.BaseType;
                }
            }

            if (handlers == null)
            {
                return;
            }

            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].ReduceAny(action);
            }
        }

        public static void Subscribe<T>(Action<T> action, bool triggerOnSub = true) where T : IData
        {
            var type = typeof(T);
            if (!subjects.TryGetValue(type, out var data))
            {
                data = new ActionConvert<T>();
                subjects.Add(type, data);
            }

            var subject = (ActionConvert<T>)data;
            subject.Add(action);
            if (triggerOnSub && !processingList.Contains(type) && state.TryGetValue(type, out var current))
            {
                action((T)current);
            }
        }

        public static bool UnSubscribe<T>(Action<T> action) where T : IData
        {
            if (!subjects.TryGetValue(typeof(T), out var data))
            {
                return false;
            }

            ((ActionConvert<T>)data).Remove(action);
            return true;
        }

        public static void Processing<T>() where T : IData
        {
            processingList.Add(typeof(T));
        }

        public static void DispatchAll()
        {
            foreach (var item in subjects)
            {
                if (item.Value.refCount > 0 && state.TryGetValue(item.Key, out var data))
                {
                    item.Value.Invoke(data);
                }
            }
        }

        private static void InitHandlers(List<IHandler> handlers, IData data)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers)
            {
                handler.Init(data);
                if (!reducers.TryGetValue(handler.ActionType, out var list))
                {
                    list = new List<IHandler>();
                    reducers.Add(handler.ActionType, list);
                }

                if (!list.Contains(handler))
                {
                    list.Add(handler);
                }
            }
        }
    }
}
