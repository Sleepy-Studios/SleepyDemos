using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;

namespace Hotfix.SceneManagement
{
    internal interface IGameSceneLoadingPresenter
    {
        UniTask<string> BeginAsync(GameSceneDefinition target);
        void SetProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args);
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
            loadingView.SetTitle("正在加载", "Loading.Title");
            string name = LanguageService.IsInitialized ? LanguageService.Get(target.DisplayNameKey) : target.DisplayName;
            SetProgress(0.05f, "准备切换", $"即将进入{target.DisplayName}", "Loading.Prepare", "Loading.Enter", name);
            return null;
        }

        /// <summary>交付源步骤的明确文案键及原文，语言未就绪时保留原加载表现。</summary>
        /// <param name="progress">单调进度。</param>
        /// <param name="step">步骤原文。</param>
        /// <param name="description">描述原文。</param>
        /// <param name="stepKey">稳定步骤键。</param>
        /// <param name="descriptionKey">完整描述句式键；空表示清空描述。</param>
        /// <param name="args">描述句式参数。</param>
        public void SetProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args)
        {
            loadingView?.SetProgress(progress, step, description, stepKey: stepKey, descriptionKey: descriptionKey, args: args);
        }

        public async UniTask<string> CompleteAsync(GameSceneId target)
        {
            SetProgress(1f, "加载完成", "", "Loading.Complete", null);
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
