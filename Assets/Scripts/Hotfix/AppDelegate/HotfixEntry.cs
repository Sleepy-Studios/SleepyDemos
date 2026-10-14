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
            using (var loader = ResourceServices.CreateLoader())
            {
                var asset = await loader.LoadAssetAsync<UnityEngine.TextAsset>("LoadResources/Config/HotfixAcceptance");
                if (asset == null)
                {
                    throw new InvalidOperationException("临时热更验收资源未加载。");
                }
                UnityEngine.Debug.Log("[Jenkins-Hotfix-Acceptance] code=H1-20261015 resource=" + asset.text.Trim());
            }
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
                await UIManager.Instance.OpenAsync<MainMenuView>();

            }

            if (hotfixContext.LoadingView != null)
            {
                Object.Destroy(hotfixContext.LoadingView.gameObject);
            }
        }
    }
}
