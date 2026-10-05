using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using Object = UnityEngine.Object;

namespace Hotfix.AppDelegate
{
    public static class HotfixEntry
    {
        /// <summary>完成热更服务初始化并进入配置入口，导航成功后释放启动Loading。</summary>
        /// <param name="hotfixContext">Core提供的资源配置与启动视图；StartupScene为空时保持Hub入口。</param>
        public static async UniTask Awake(HotfixStartupContext hotfixContext)
        {
            hotfixContext.LoadingView?.SetProgress(0.85f, "热更初始化", "发现业务页面");
            UITypeReflection.Scan(typeof(HotfixEntry).Assembly);
            await HotfixBootService.RunBootSystems(hotfixContext);
            await UniTask.Yield();

            hotfixContext.LoadingView?.SetProgress(0.95f, "进入界面", "显示主界面");
            GameSceneId? standaloneScene = null;
            string requestedScene = hotfixContext.Config?.StartupScene;
            if (!string.IsNullOrWhiteSpace(requestedScene))
            {
                if (!Enum.TryParse(requestedScene, false, out GameSceneId target) || !GameSceneCatalog.TryGet(target, out _))
                    throw new InvalidOperationException("启动目标未登记：" + requestedScene);
                if (target != GameSceneId.Hub) standaloneScene = target;
            }
            GameSceneNavigator.Initialize(standaloneScene);
            UIManager.Instance.RegisterWorldTransitionProvider(new HotfixWorldTransitionProvider());
            if (standaloneScene.HasValue)
            {
                hotfixContext.LoadingView?.SetProgress(0.95f, "进入游戏", "加载游戏主菜单");
                var navigation = await GameSceneNavigator.Instance.SwitchAsync(standaloneScene.Value);
                if (navigation.Status != GameSceneSwitchStatus.Succeeded && navigation.Status != GameSceneSwitchStatus.Ignored)
                    throw new InvalidOperationException("独立游戏启动失败：" + navigation.Error);
            }
            else
            {
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
                            "Hotfix 启动无法稳定进入 MainMenuView，请检查 UIBind 生成代码和预制体地址。",
                            result.Exception);
                    default:
                        throw new InvalidOperationException($"Hotfix 启动收到未知 UI 导航状态: {result.Status}");
                }

            }

            if (hotfixContext.LoadingView != null)
            {
                Object.Destroy(hotfixContext.LoadingView.gameObject);
            }
        }
    }
}
