using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public interface IAction
    {
    }

    public interface IData
    {
        List<IHandler> Handlers { get; }
        void ClearData();
    }

    public interface IHandler
    {
        Type ActionType { get; }
        void Init(IData state);
        void ReduceAny(IAction action);
    }

    public interface INetworkServiceReceiver
    {
        void SetNetworkService(INetworkService networkService);
    }

    public interface INetworkService
    {
        UniTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request);
        UniTask<object> SendAsync(string command, object request, Type responseType);
    }
}
