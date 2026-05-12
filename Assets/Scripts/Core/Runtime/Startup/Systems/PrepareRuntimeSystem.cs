using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class PrepareRuntimeSystem : StartupSystemBase
    {
        public PrepareRuntimeSystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "创建基础启动上下文");
            Context.LoadingView?.SetTitle("SleepyDemos");
            await UniTask.Yield();
            Report(1f, "基础启动上下文就绪");
        }
    }
}
