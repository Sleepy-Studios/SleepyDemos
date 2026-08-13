using Core.Runtime;
using Cysharp.Threading.Tasks;

namespace Hotfix.SceneManagement
{
    internal interface IGameSceneLoadingPresenter
    {
        UniTask<string> BeginAsync(GameSceneDefinition target);
        void SetProgress(float progress, string step, string description);
        UniTask<string> CompleteAsync(GameSceneId target);
        UniTask RestoreAsync(GameSceneId source);
    }

    internal sealed class GameSceneLoadingPresenter : IGameSceneLoadingPresenter
    {
        private CommonLoadingView loadingView;

        public async UniTask<string> BeginAsync(GameSceneDefinition target)
        {
            var options = new UIShowOptions(animated: false);
            var result = target.IsHub
                ? await UIManager.Instance.ShowAsync<CommonLoadingView>(options)
                : await UIManager.Instance.ReplaceAsync<CommonLoadingView>(options);
            if (result.Status != UIOperationStatus.Succeeded &&
                result.Status != UIOperationStatus.Ignored)
            {
                return result.Exception?.Message ?? $"无法打开通用 Loading: {result.Status}";
            }

            loadingView = result.View as CommonLoadingView;
            if (loadingView == null)
            {
                return "通用 Loading 已打开，但无法取得 CommonLoadingView 实例。";
            }

            loadingView.ResetProgress();
            loadingView.SetTitle("正在加载");
            loadingView.SetProgress(0.05f, "准备切换", $"即将进入{target.DisplayName}");
            return null;
        }

        public void SetProgress(float progress, string step, string description)
        {
            loadingView?.SetProgress(progress, step, description);
        }

        public async UniTask<string> CompleteAsync(GameSceneId target)
        {
            loadingView?.SetProgress(1f, "加载完成", "");
            var result = target == GameSceneId.Hub
                ? await UIManager.Instance.ReplaceAsync<MainMenuView>(new UIShowOptions(false))
                : await UIManager.Instance.CloseAsync<CommonLoadingView>(false);
            loadingView = null;
            return result.Status == UIOperationStatus.Succeeded || result.Status == UIOperationStatus.Ignored
                ? null
                : result.Exception?.Message ?? $"场景完成后 UI 收口失败: {result.Status}";
        }

        public async UniTask RestoreAsync(GameSceneId source)
        {
            if (source == GameSceneId.Hub)
            {
                await UIManager.Instance.ReplaceAsync<MainMenuView>(new UIShowOptions(false));
            }
            else
            {
                await UIManager.Instance.CloseAsync<CommonLoadingView>(false);
            }

            loadingView = null;
        }
    }
}
