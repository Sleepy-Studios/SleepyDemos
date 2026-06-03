using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    /// <summary>
    /// 标记网络命令回包处理方法。HandlerBase 会根据命令和状态自动路由网络响应。
    /// </summary>
    public sealed class MessageHandler : Attribute
    {
        /// <summary>
        /// 网络命令回包处理状态。
        /// </summary>
        public enum State
        {
            /// <summary>
            /// 成功回包处理方法。
            /// </summary>
            Success,

            /// <summary>
            /// 错误回包处理方法。
            /// </summary>
            Error
        }

        /// <summary>
        /// 网络协议命令。
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// 当前方法处理成功回包还是错误回包。
        /// </summary>
        public State HandlerState { get; }

        /// <summary>
        /// 创建一个网络命令回包处理标记。
        /// </summary>
        /// <param name="command">网络协议命令。</param>
        /// <param name="state">当前方法处理成功回包还是错误回包。</param>
        public MessageHandler(string command, State state = State.Success)
        {
            Command = command;
            HandlerState = state;
        }
    }

    internal sealed class MessageHandlerInfo
    {
        public string Command;
        public MethodInfo SuccessMethod;
        public MethodInfo ErrorMethod;
        public Type SuccessParameterType;
    }

    /// <summary>
    /// Flux Handler 基类，负责绑定 Data、处理 Action、发送网络请求，并在状态变更后通知 GlobalData。
    /// </summary>
    /// <typeparam name="TAction">当前 Handler 可处理的 Action 基类或具体类型。</typeparam>
    /// <typeparam name="TState">当前 Handler 绑定的 Data 类型。</typeparam>
    public abstract class HandlerBase<TAction, TState> : IHandler, INetworkServiceReceiver
        where TAction : IAction
        where TState : IData
    {
        private readonly Dictionary<string, MessageHandlerInfo> messageHandlers = new Dictionary<string, MessageHandlerInfo>();

        /// <summary>
        /// 当前 Handler 绑定的 Data 实例。
        /// </summary>
        protected TState State { get; private set; }

        /// <summary>
        /// 当前 Handler 使用的网络服务。未注入时为 null。
        /// </summary>
        protected INetworkService NetworkService { get; private set; }

        /// <summary>
        /// 当前 Handler 可处理的 Action 类型。
        /// </summary>
        public Type ActionType => typeof(TAction);

        /// <summary>
        /// 初始化 Handler，并绑定对应 Data。
        /// </summary>
        /// <param name="state">注册到 GlobalData 中的 Data 实例。</param>
        public void Init(IData state)
        {
            State = (TState)state;
            InitMessageHandlers();
            OnInit();
        }

        /// <summary>
        /// 设置当前 Handler 使用的网络服务。
        /// </summary>
        /// <param name="networkService">网络服务实例；传入 null 表示清空网络服务。</param>
        public void SetNetworkService(INetworkService networkService)
        {
            NetworkService = networkService;
        }

        /// <summary>
        /// 接收任意 Action，并在类型匹配时转交给 Reduce。
        /// </summary>
        /// <param name="action">本次派发的 Action。</param>
        public void ReduceAny(IAction action)
        {
            if (action is TAction typed)
            {
                Reduce(typed);
            }
        }

        /// <summary>
        /// Handler 初始化完成后的扩展点。
        /// </summary>
        protected virtual void OnInit()
        {
        }

        /// <summary>
        /// 处理当前 Handler 支持的 Action。纯本地修改 State 后需要手动调用 ApplyState。
        /// </summary>
        /// <param name="action">当前 Handler 支持的 Action。</param>
        protected abstract void Reduce(TAction action);

        /// <summary>
        /// 应用当前 State，并通知 GlobalData 订阅者。
        /// </summary>
        protected void ApplyState()
        {
            GlobalData.Modify(State);
        }

        /// <summary>
        /// 发送泛型网络请求。该重载只返回响应，不会自动 ApplyState。
        /// </summary>
        /// <param name="request">请求对象。</param>
        /// <typeparam name="TRequest">请求对象类型。</typeparam>
        /// <typeparam name="TResponse">响应对象类型。</typeparam>
        /// <returns>响应对象；未注入网络服务时返回默认值。</returns>
        protected UniTask<TResponse> SendMsg<TRequest, TResponse>(TRequest request)
        {
            return NetworkService == null ? UniTask.FromResult(default(TResponse)) : NetworkService.SendAsync<TRequest, TResponse>(request);
        }

        /// <summary>
        /// 发送泛型网络请求，并在成功回包后执行同步回调和自动 ApplyState。
        /// </summary>
        /// <param name="request">请求对象。</param>
        /// <param name="onSuccess">成功回包后的状态修改回调。</param>
        /// <typeparam name="TRequest">请求对象类型。</typeparam>
        /// <typeparam name="TResponse">响应对象类型。</typeparam>
        /// <returns>响应对象；未注入网络服务时返回默认值。</returns>
        protected async UniTask<TResponse> SendMsg<TRequest, TResponse>(TRequest request, Action<TResponse> onSuccess)
        {
            if (NetworkService == null)
            {
                return default;
            }

            var response = await NetworkService.SendAsync<TRequest, TResponse>(request);
            onSuccess?.Invoke(response);
            ApplyState();
            return response;
        }

        /// <summary>
        /// 发送泛型网络请求，并在成功回包后执行异步回调和自动 ApplyState。
        /// </summary>
        /// <param name="request">请求对象。</param>
        /// <param name="onSuccess">成功回包后的异步状态修改回调。</param>
        /// <typeparam name="TRequest">请求对象类型。</typeparam>
        /// <typeparam name="TResponse">响应对象类型。</typeparam>
        /// <returns>响应对象；未注入网络服务时返回默认值。</returns>
        protected async UniTask<TResponse> SendMsg<TRequest, TResponse>(TRequest request, Func<TResponse, UniTask> onSuccess)
        {
            if (NetworkService == null)
            {
                return default;
            }

            var response = await NetworkService.SendAsync<TRequest, TResponse>(request);
            if (onSuccess != null)
            {
                await onSuccess(response);
            }

            ApplyState();
            return response;
        }

        /// <summary>
        /// 按协议命令发送网络请求，并通过 MessageHandler 标记的方法处理回包。
        /// </summary>
        /// <param name="command">网络协议命令。</param>
        /// <param name="request">请求对象。</param>
        /// <param name="triggerStateEvent">回包处理完成后是否自动 ApplyState 并通知订阅者。</param>
        protected void SendMsg(string command, object request, bool triggerStateEvent = true)
        {
            SendMsgAsync(command, request, triggerStateEvent).Forget();
        }

        /// <summary>
        /// 按协议命令发送网络请求，并等待 MessageHandler 标记的方法处理完成。
        /// </summary>
        /// <param name="command">网络协议命令。</param>
        /// <param name="request">请求对象。</param>
        /// <param name="triggerStateEvent">回包处理完成后是否自动 ApplyState 并通知订阅者。</param>
        /// <returns>异步任务。</returns>
        protected async UniTask SendMsgAsync(string command, object request, bool triggerStateEvent = true)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentException("Network command cannot be null or empty.", nameof(command));
            }

            if (NetworkService == null)
            {
                return;
            }

            if (!messageHandlers.TryGetValue(command, out var handlerInfo))
            {
                throw new InvalidOperationException($"No MessageHandler found for command: {command}");
            }

            EnsureSuccessHandler(handlerInfo);

            object response;
            try
            {
                response = await NetworkService.SendAsync(command, request, handlerInfo.SuccessParameterType);
            }
            catch (Exception exception)
            {
                InvokeErrorHandler(handlerInfo, exception.Message, triggerStateEvent);
                return;
            }

            InvokeSuccessHandler(handlerInfo, response, triggerStateEvent);
        }

        /// <summary>
        /// 手动触发指定命令的成功回包处理，常用于测试或由外部网络层回灌结果。
        /// </summary>
        /// <param name="command">网络协议命令。</param>
        /// <param name="response">成功响应对象。</param>
        /// <param name="triggerStateEvent">处理完成后是否自动 ApplyState 并通知订阅者。</param>
        protected void InvokeSuccess(string command, object response, bool triggerStateEvent = true)
        {
            if (!messageHandlers.TryGetValue(command, out var handlerInfo))
            {
                throw new InvalidOperationException($"No MessageHandler found for command: {command}");
            }

            InvokeSuccessHandler(handlerInfo, response, triggerStateEvent);
        }

        /// <summary>
        /// 手动触发指定命令的错误回包处理，常用于测试或由外部网络层回灌错误。
        /// </summary>
        /// <param name="command">网络协议命令。</param>
        /// <param name="errorMessage">错误消息。</param>
        /// <param name="triggerStateEvent">处理完成后是否自动 ApplyState 并通知订阅者。</param>
        protected void InvokeError(string command, string errorMessage, bool triggerStateEvent = true)
        {
            if (!messageHandlers.TryGetValue(command, out var handlerInfo))
            {
                throw new InvalidOperationException($"No MessageHandler found for command: {command}");
            }

            InvokeErrorHandler(handlerInfo, errorMessage, triggerStateEvent);
        }

        private void InitMessageHandlers()
        {
            messageHandlers.Clear();

            var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                var handler = method.GetCustomAttribute<MessageHandler>();
                if (handler == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(handler.Command))
                {
                    throw new InvalidOperationException($"{method.Name}: MessageHandler command cannot be null or empty.");
                }

                if (!messageHandlers.TryGetValue(handler.Command, out var info))
                {
                    info = new MessageHandlerInfo
                    {
                        Command = handler.Command
                    };
                    messageHandlers.Add(handler.Command, info);
                }

                var parameters = method.GetParameters();
                if (handler.HandlerState == MessageHandler.State.Success)
                {
                    if (parameters.Length != 1)
                    {
                        throw new InvalidOperationException($"{method.Name}: Success MessageHandler must have exactly one response parameter.");
                    }

                    if (info.SuccessMethod != null)
                    {
                        throw new InvalidOperationException($"{method.Name}: Duplicate Success MessageHandler for command {handler.Command}.");
                    }

                    info.SuccessMethod = method;
                    info.SuccessParameterType = parameters[0].ParameterType;
                }
                else
                {
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                    {
                        throw new InvalidOperationException($"{method.Name}: Error MessageHandler must have exactly one string parameter.");
                    }

                    if (info.ErrorMethod != null)
                    {
                        throw new InvalidOperationException($"{method.Name}: Duplicate Error MessageHandler for command {handler.Command}.");
                    }

                    info.ErrorMethod = method;
                }
            }
        }

        private static void EnsureSuccessHandler(MessageHandlerInfo handlerInfo)
        {
            if (handlerInfo.SuccessMethod == null || handlerInfo.SuccessParameterType == null)
            {
                throw new InvalidOperationException($"No Success MessageHandler found for command: {handlerInfo.Command}");
            }
        }

        private void InvokeSuccessHandler(MessageHandlerInfo handlerInfo, object response, bool triggerStateEvent)
        {
            EnsureSuccessHandler(handlerInfo);

            if (response != null && !handlerInfo.SuccessParameterType.IsInstanceOfType(response))
            {
                throw new InvalidOperationException(
                    $"Response type mismatch for {handlerInfo.Command}. Expected {handlerInfo.SuccessParameterType.FullName}, got {response.GetType().FullName}.");
            }

            if (response == null && handlerInfo.SuccessParameterType.IsValueType)
            {
                throw new InvalidOperationException(
                    $"Response type mismatch for {handlerInfo.Command}. Expected {handlerInfo.SuccessParameterType.FullName}, got null.");
            }

            handlerInfo.SuccessMethod.Invoke(this, new[] { response });
            if (triggerStateEvent)
            {
                ApplyState();
            }
        }

        private void InvokeErrorHandler(MessageHandlerInfo handlerInfo, string errorMessage, bool triggerStateEvent)
        {
            if (handlerInfo.ErrorMethod != null)
            {
                handlerInfo.ErrorMethod.Invoke(this, new object[] { errorMessage });
                if (triggerStateEvent)
                {
                    ApplyState();
                }
                return;
            }

            EventDispatcher.TriggerEvent("ChannelErrorCode", errorMessage);
        }
    }
}
