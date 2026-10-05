using System;

namespace Core.Runtime
{
    /// 随页面或条目释放一次的订阅回调。
    internal sealed class CallbackBinding : IDisposable
    {
        private Action dispose;
        internal CallbackBinding(Action dispose) => this.dispose = dispose;
        public void Dispose() { var callback = dispose; dispose = null; callback?.Invoke(); }
    }
}
