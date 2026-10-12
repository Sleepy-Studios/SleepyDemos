using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// Hub 设置与语言重建的外部入口，回调不持有旧页面控件。
    public static class SettingsFlow
    {
        public static bool IsSubmitting => GameSettingsService.IsApplying || HubLanguageFlow.IsChanging
            || GlobalData.Get<SettingsData>()?.IsApplying == true;

        /// <summary>仅在 Hub 打开公共设置；Demo 继续使用自己的专属界面。</summary>
        /// <param name="category">默认显示与画质。</param>
        /// <param name="token">只取消本次打开，不修改保存偏好。</param>
        /// <returns>正式 UIManager 导航结果。</returns>
        public static async UniTask<UIOperationResult> OpenAsync(SettingsCategory category = SettingsCategory.Graphics, CancellationToken token = default)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                await GameSettingsService.InitializeAsync().AttachExternalCancellation(token);
            }
            catch (OperationCanceledException)
            {
                return UIOperationResult.Canceled(0, UINavigationAction.Push, null);
            }
            if (GameSceneNavigator.Instance?.CurrentScene != GameSceneId.Hub)
            {
                return UIOperationResult.Failed(0, UINavigationAction.Push, null,
                    new InvalidOperationException("公共设置仅可从 Hub 打开。"));
            }
            return await UIManager.Instance.ShowAsync<HubSettingsView>(view => view.SetData(category),
                new UIShowOptions(animated: false, hidePrevious: false), token);
        }

        internal static async UniTask<SettingsApplyResult> ApplyAsync(GameSettingsSnapshot candidate, CancellationToken token)
        {
            if (candidate.Language != GameSettingsService.Current.Language)
            {
                // CloseAll 会销毁发起者，重开只捕获类型与分类，不把旧 View 或控件带进新语言。
                var result = await HubLanguageFlow.ApplyAsync(candidate,
                    () => OpenAsync(SettingsCategory.Language), ConfirmDisplayAsync, token);
                var current = UIManager.Instance.Get<HubSettingsView>();
                if (current != null && current.IsEnable)
                {
                    current.ShowApplyResult(result);
                }
                return result;
            }
            if (GlobalData.Get<SettingsData>() == null)
            {
                throw new InvalidOperationException("公共设置模块尚未由正式启动注册。");
            }
            var action = new SettingsApplyAction(candidate, ConfirmDisplayAsync, token);
            GlobalData.Dispatch(action);
            return await action.Completion;
        }

        private static async UniTask<bool> ConfirmDisplayAsync(CancellationToken token)
        {
            var request = new DisplayConfirmationRequest(TimeSeconds() + 15);
            DisplayConfirmView view = null;
            try
            {
                var result = await UIManager.Instance.ShowAsync<DisplayConfirmView>(item => item.SetData(request),
                    new UIShowOptions(animated: false, hidePrevious: false), token);
                view = result.View as DisplayConfirmView;
                if (result.Status == UIOperationStatus.Failed)
                {
                    throw result.Exception ?? new InvalidOperationException("显示确认窗口加载失败。");
                }
                if (result.Status == UIOperationStatus.Canceled)
                {
                    return false;
                }
                return await request.Completion.Task.AttachExternalCancellation(token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                request.Completion.TrySetResult(false);
                await UniTask.SwitchToMainThread();
                if (view != null)
                {
                    await UIManager.Instance.CloseAsync(view, false);
                }
            }
        }

        private static double TimeSeconds() => UnityEngine.Time.realtimeSinceStartupAsDouble;
    }
}
