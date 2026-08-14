using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.AppDelegate
{
    public static class HotfixBootService
    {
        private static bool isCompleted;
        private static bool isRunning;
        private static UniTaskCompletionSource<bool> runningCompletion;

        private static readonly IHotfixBootSystem[] systems =
        {
            new LubanConfigSystem(),
            new GlobalDataSystem()
        };

        public static async UniTask RunBootSystems(HotfixStartupContext context)
        {
            if (isCompleted)
            {
                return;
            }

            if (isRunning)
            {
                await runningCompletion.Task;
                return;
            }

            isRunning = true;
            runningCompletion = new UniTaskCompletionSource<bool>();
            try
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    var system = systems[i];
                    var progress = Mathf.Min(0.94f, 0.86f + i * 0.02f);
                    context?.LoadingView?.SetProgress(progress, "热更初始化", system.Description);
                    await system.RunAsync(context);
                }

                isCompleted = true;
                runningCompletion.TrySetResult(true);
            }
            catch (System.Exception exception)
            {
                runningCompletion.TrySetException(exception);
                throw;
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}
