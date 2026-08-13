using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using Object = UnityEngine.Object;

namespace Hotfix.AppDelegate
{
    public static class HotfixEntry
    {
        public static async UniTask Awake(HotfixStartupContext hotfixContext)
        {
            hotfixContext.LoadingView?.SetProgress(0.85f, "热更初始化", "扫描热更 View 类型");
            UITypeReflection.Scan(typeof(HotfixEntry).Assembly);
            await HotfixBootService.RunBootSystems(hotfixContext);
            await UniTask.Yield();

            hotfixContext.LoadingView?.SetProgress(0.95f, "进入界面", "显示主界面");
            GameSceneNavigator.Initialize();
            UIManager.Instance.RegisterWorldTransitionProvider(new HotfixWorldTransitionProvider());
            var result = await UIManager.Instance.ShowAsync<MainMenuView>();
            switch (result.Status)
            {
                case UIOperationStatus.Succeeded:
                case UIOperationStatus.Ignored:
                    break;
                case UIOperationStatus.Canceled:
                    throw new OperationCanceledException("Hotfix 启动在主界面稳定进入前被中断。");
                case UIOperationStatus.Failed:
                    throw new InvalidOperationException(
                        "Hotfix 启动无法稳定进入 MainMenuView，请检查 MvcBind 生成代码和预制体地址。",
                        result.Exception);
                default:
                    throw new InvalidOperationException($"Hotfix 启动收到未知 UI 导航状态: {result.Status}");
            }

            if (hotfixContext.LoadingView != null)
            {
                Object.Destroy(hotfixContext.LoadingView.gameObject);
            }
        }
    }
}
