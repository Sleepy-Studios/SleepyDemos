using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hotfix.AppDelegate
{
    public static class HotfixEntry
    {
        public static async UniTask Awake(HotfixStartupContext hotfixContext)
        {
            hotfixContext.LoadingView?.SetProgress(0.85f, "热更初始化", "扫描热更 View 类型");
            UITypeReflection.Scan(typeof(HotfixEntry).Assembly);
            await UniTask.Yield();

            hotfixContext.LoadingView?.SetProgress(0.95f, "进入界面", "显示主界面");
            var view = UIManager.Instance.Show<MainMenuView>();
            if (view == null)
            {
                Debug.LogWarning("[HotfixEntry] MainMenuView 未注册，检查 MvcBind 生成代码和预制体地址。");
                return;
            }

            await UniTask.Yield();
            if (hotfixContext.LoadingView != null)
            {
                Object.Destroy(hotfixContext.LoadingView.gameObject);
            }
        }
    }
}
