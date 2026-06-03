using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.AppDelegate
{
    public sealed class GlobalDataSystem : IHotfixBootSystem
    {
        public string Name => "GlobalDataSystem";
        public string Description => "注册全局 Flux Data";

        public UniTask RunAsync(HotfixStartupContext context)
        {
            FluxService.InitializeGlobalData();
            Debug.Log($"[{Name}] {Description}完成。");
            return UniTask.CompletedTask;
        }
    }
}
