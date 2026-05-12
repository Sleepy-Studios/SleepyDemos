using System;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public abstract class HandlerBase<TAction, TState> : IHandler
        where TAction : IAction
        where TState : IData
    {
        protected TState State { get; private set; }
        protected INetworkService NetworkService { get; private set; }

        public Type ActionType => typeof(TAction);

        public void Init(IData state)
        {
            State = (TState)state;
            OnInit();
        }

        public void SetNetworkService(INetworkService networkService)
        {
            NetworkService = networkService;
        }

        public void ReduceAny(IAction action)
        {
            if (action is TAction typed)
            {
                Reduce(typed);
            }
        }

        protected virtual void OnInit()
        {
        }

        protected abstract void Reduce(TAction action);

        protected void ApplyState()
        {
            GlobalData.Modify(State);
        }

        protected UniTask<TResponse> SendMsg<TRequest, TResponse>(TRequest request)
        {
            return NetworkService == null ? UniTask.FromResult(default(TResponse)) : NetworkService.SendAsync<TRequest, TResponse>(request);
        }
    }
}
