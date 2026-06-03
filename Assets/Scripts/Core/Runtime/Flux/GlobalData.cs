using System;
using System.Collections.Generic;

namespace Core.Runtime
{
    /// <summary>
    /// 全局 Flux 状态中心，负责 Data 注册、Action 派发、状态订阅、网络服务注入和状态清理。
    /// </summary>
    public static class GlobalData
    {
        private static readonly Type objectType = typeof(object);
        private static readonly Dictionary<Type, IData> state = new Dictionary<Type, IData>();
        private static readonly Dictionary<Type, List<IHandler>> reducers = new Dictionary<Type, List<IHandler>>();
        private static readonly Dictionary<Type, List<IHandler>> stateHandlers = new Dictionary<Type, List<IHandler>>();
        private static readonly Dictionary<Type, ActionBase> subjects = new Dictionary<Type, ActionBase>();
        private static readonly HashSet<Type> processingList = new HashSet<Type>();
        private static INetworkService networkService;

        /// <summary>
        /// 注册一个 Data 类型。如果该类型已经注册，则直接返回已存在的实例。
        /// </summary>
        /// <typeparam name="T">要注册的 Data 类型。</typeparam>
        /// <returns>注册后的 Data 实例。</returns>
        public static T Add<T>() where T : IData, new()
        {
            var type = typeof(T);
            if (state.TryGetValue(type, out var existing))
            {
                return (T)existing;
            }

            var instance = new T();
            return RegisterState(type, instance);
        }

        /// <summary>
        /// 注册一个已有 Data 实例。如果该类型已经注册，则直接返回已存在的实例，不会替换为传入实例。
        /// </summary>
        /// <param name="instance">要注册的 Data 实例。</param>
        /// <typeparam name="T">要注册的 Data 类型。</typeparam>
        /// <returns>注册后的 Data 实例。</returns>
        public static T Add<T>(T instance) where T : IData
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var type = typeof(T);
            if (state.TryGetValue(type, out var existing))
            {
                return (T)existing;
            }

            return RegisterState(type, instance);
        }

        /// <summary>
        /// 设置 Flux Handler 使用的网络服务。已注册和后续注册的 Handler 都会收到该服务。
        /// </summary>
        /// <param name="service">网络服务实例；传入 null 表示清空网络服务。</param>
        public static void SetNetworkService(INetworkService service)
        {
            networkService = service;

            foreach (var handlers in stateHandlers.Values)
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    SetHandlerNetworkService(handlers[i]);
                }
            }
        }

        internal static bool Modify<T>(T instance) where T : IData
        {
            if (instance == null)
            {
                return false;
            }

            return Modify(typeof(T), instance);
        }

        /// <summary>
        /// 获取已注册的 Data 实例。
        /// </summary>
        /// <typeparam name="T">要获取的 Data 类型。</typeparam>
        /// <returns>已注册的 Data 实例；未注册时返回默认值。</returns>
        public static T Get<T>() where T : IData
        {
            return state.TryGetValue(typeof(T), out var data) ? (T)data : default;
        }

        /// <summary>
        /// 移除指定 Data 类型，并清理它绑定的 Handler、订阅者和处理中标记。
        /// </summary>
        /// <typeparam name="T">要移除的 Data 类型。</typeparam>
        /// <returns>成功移除返回 true；未注册该 Data 时返回 false。</returns>
        public static bool Remove<T>() where T : IData
        {
            var type = typeof(T);
            if (!state.Remove(type))
            {
                return false;
            }

            RemoveHandlers(type);
            subjects.Remove(type);
            processingList.Remove(type);
            return true;
        }

        /// <summary>
        /// 清理所有已注册 Data 的内部状态，并通知对应订阅者。
        /// </summary>
        public static void ClearData()
        {
            var items = new List<KeyValuePair<Type, IData>>(state);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.Value.ClearData();
                Modify(item.Key, item.Value);
            }
        }

        /// <summary>
        /// 派发 Action。GlobalData 会按 Action 类型或其父类类型查找可处理的 Handler。
        /// </summary>
        /// <param name="action">要派发的 Action。</param>
        public static void Dispatch(IAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

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

            var snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]?.ReduceAny(action);
            }
        }

        /// <summary>
        /// 订阅指定 Data 的状态变化。
        /// </summary>
        /// <param name="action">状态变化时触发的回调。</param>
        /// <param name="triggerOnSub">是否在订阅成功后立即回调当前状态；true 表示立即通知当前状态，false 表示只接收后续变化。</param>
        /// <typeparam name="T">要订阅的 Data 类型。</typeparam>
        public static void Subscribe<T>(Action<T> action, bool triggerOnSub = true) where T : IData
        {
            if (action == null)
            {
                return;
            }

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

        /// <summary>
        /// 取消订阅指定 Data 的状态变化。
        /// </summary>
        /// <param name="action">要取消的订阅回调。</param>
        /// <typeparam name="T">要取消订阅的 Data 类型。</typeparam>
        /// <returns>存在对应订阅容器时返回 true；未找到订阅容器或回调为空时返回 false。</returns>
        public static bool UnSubscribe<T>(Action<T> action) where T : IData
        {
            if (action == null)
            {
                return false;
            }

            if (!subjects.TryGetValue(typeof(T), out var data))
            {
                return false;
            }

            ((ActionConvert<T>)data).Remove(action);
            return true;
        }

        /// <summary>
        /// 标记指定 Data 正在处理中。处理期间新增订阅不会立即触发当前状态回调。
        /// </summary>
        /// <typeparam name="T">正在处理的 Data 类型。</typeparam>
        public static void Processing<T>() where T : IData
        {
            processingList.Add(typeof(T));
        }

        /// <summary>
        /// 主动向所有已有订阅者派发当前状态。
        /// </summary>
        public static void DispatchAll()
        {
            var items = new List<KeyValuePair<Type, ActionBase>>(subjects);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Value.refCount > 0 && state.TryGetValue(item.Key, out var data))
                {
                    item.Value.Invoke(data);
                }
            }
        }

        private static T RegisterState<T>(Type type, T instance) where T : IData
        {
            state.Add(type, instance);
            if (!subjects.ContainsKey(type))
            {
                subjects.Add(type, new ActionConvert<T>());
            }

            InitHandlers(type, instance.Handlers, instance);
            return instance;
        }

        private static bool Modify(Type type, IData instance)
        {
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

        private static void InitHandlers(Type stateType, List<IHandler> handlers, IData data)
        {
            if (handlers == null)
            {
                return;
            }

            if (!stateHandlers.TryGetValue(stateType, out var stateHandlerList))
            {
                stateHandlerList = new List<IHandler>();
                stateHandlers.Add(stateType, stateHandlerList);
            }

            foreach (var handler in handlers)
            {
                if (handler == null)
                {
                    continue;
                }

                handler.Init(data);
                SetHandlerNetworkService(handler);

                if (!stateHandlerList.Contains(handler))
                {
                    stateHandlerList.Add(handler);
                }

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

        private static void RemoveHandlers(Type stateType)
        {
            if (!stateHandlers.TryGetValue(stateType, out var handlers))
            {
                return;
            }

            for (int i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                if (reducers.TryGetValue(handler.ActionType, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        reducers.Remove(handler.ActionType);
                    }
                }
            }

            stateHandlers.Remove(stateType);
        }

        private static void SetHandlerNetworkService(IHandler handler)
        {
            if (handler is INetworkServiceReceiver receiver)
            {
                receiver.SetNetworkService(networkService);
            }
        }
    }
}
