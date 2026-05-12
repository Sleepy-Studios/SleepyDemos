using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class StartupPipeline
    {
        private const int AotStateCount = 3;
        private readonly StartupContext context;
        private StartupStateMachine stateMachine;

        public StartupPipeline(HotUpdateConfig config, StartupLoadingView loadingView, MonoBehaviour runner)
        {
            context = new StartupContext(config, loadingView, runner);
        }

        public async UniTask RunAsync()
        {
            stateMachine = new StartupStateMachine(context, AotStateCount, 0f, 0.7f);
            stateMachine.AddState(new PrepareStartupState());
            stateMachine.AddState(new ResourceStartupState());
            stateMachine.AddState(new BeforeHotfixStartupState());

            await stateMachine.RunAsync((int)StartupStateId.Prepare);
        }
    }
}
