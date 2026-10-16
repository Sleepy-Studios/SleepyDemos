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
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            // WebGL 的 Application.identifier 为空，持久身份直接取统一发布配置。
            Platform.TapTap.TapTapStorage.Initialize(Context.Config.GameId);
            Platform.TapTap.TapTapPlatform.Initialize();
#endif
            await UniTask.Yield();
            Report(1f, "基础启动上下文就绪");
        }
    }
}
