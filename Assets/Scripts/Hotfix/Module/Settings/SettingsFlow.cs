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

        /// <summary>按钮调用入口；成功后通过回调取得完整可用页面。</summary>
        /// <param name="category">默认打开显示与画质分类。</param>
        /// <param name="onOpened">页面及其必要资源就绪后调用；可省略。</param>
        /// <param name="onFailed">失败反馈；省略时记录 Console，正常取消不反馈错误。</param>
        /// <param name="cancellationToken">取消本次准备或打开。</param>
        public static void Show(SettingsCategory category = SettingsCategory.Graphics, Action<HubSettingsView> onOpened = null,
            Action<Exception> onFailed = null, CancellationToken cancellationToken = default)
        {
            ShowRequestAsync(category, onOpened, onFailed, cancellationToken).Forget(UnityEngine.Debug.LogException);
        }

        private static async UniTask ShowRequestAsync(SettingsCategory category, Action<HubSettingsView> onOpened,
            Action<Exception> onFailed, CancellationToken cancellationToken)
        {
            HubSettingsView view;
            try
            {
                view = await OpenAsync(category, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (onFailed == null)
                {
                    UnityEngine.Debug.LogException(exception);
                }
                else
                {
                    onFailed(exception);
                }

                return;
            }

            onOpened?.Invoke(view);
        }

        /// <summary>仅在 Hub 打开公共设置；Demo 继续使用自己的专属界面。</summary>
        /// <param name="category">默认显示与画质。</param>
        /// <param name="token">只取消本次打开，不修改保存偏好。</param>
        /// <returns>已打开的设置页；失败抛原异常，取消抛取消异常。</returns>
        public static async UniTask<HubSettingsView> OpenAsync(SettingsCategory category = SettingsCategory.Graphics, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            await GameSettingsService.InitializeAsync().AttachExternalCancellation(token);
            if (GameSceneNavigator.Instance?.CurrentScene != GameSceneId.Hub)
            {
                throw new InvalidOperationException("公共设置仅可从 Hub 打开。");
            }
            return await UIManager.Instance.OpenAsync<HubSettingsView>(view => view.SetData(category),
                new UIOpenOptions(animated: false, hidePrevious: false), token);
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
                view = await UIManager.Instance.OpenAsync<DisplayConfirmView>(item => item.SetData(request),
                    new UIOpenOptions(animated: false, hidePrevious: false), token);
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
