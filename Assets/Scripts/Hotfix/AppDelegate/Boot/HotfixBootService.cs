using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.AppDelegate
{
    public static class HotfixBootService
    {
        private static readonly IHotfixBootSystem[] systems =
        {
            new GlobalDataSystem()
        };

        public static async UniTask RunBootSystems(HotfixStartupContext context)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                var progress = Mathf.Min(0.94f, 0.86f + i * 0.02f);
                context?.LoadingView?.SetProgress(progress, "热更初始化", system.Description);
                await system.RunAsync(context);
            }
        }
    }
}
