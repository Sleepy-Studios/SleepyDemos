using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 公共设置与语言重建的外部入口，回调不持有旧页面控件。
    public static class SettingsFlow
    {
        public static bool IsSubmitting => GameSettingsService.IsApplying || HubLanguageFlow.IsChanging
            || GlobalData.Get<SettingsData>()?.IsApplying == true;

        /// <summary>按当前业务场景打开宿主，未注册 Showcase 场景时使用 Hub 基底。</summary>
        /// <param name="category">默认显示与画质。</param>
        /// <param name="token">只取消本次打开，不修改保存偏好。</param>
        /// <returns>正式 UIManager 导航结果。</returns>
        public static UniTask<UIOperationResult> OpenAsync(SettingsCategory category = SettingsCategory.Graphics, CancellationToken token = default)
            => OpenAsync(GameSceneNavigator.Instance?.CurrentScene ?? GameSceneId.Hub, category, token);

        /// <summary>显示指定宿主的正式 Prefab Variant，全部 Demo 仍使用同一公共设置事务。</summary>
        /// <param name="host">Hub、无人机、DLSS、夹爆它或纸艺卡丁车。</param>
        /// <param name="category">初始分类，Demo 隐藏语言。</param>
        /// <param name="token">资源或导航取消令牌。</param>
        /// <returns>页面打开的实际结果。</returns>
        public static async UniTask<UIOperationResult> OpenAsync(GameSceneId host, SettingsCategory category = SettingsCategory.Graphics,
            CancellationToken token = default)
        {
            await GameSettingsService.InitializeAsync();
            if (host != GameSceneId.Hub && category == SettingsCategory.Language)
            {
                category = SettingsCategory.Graphics;
            }
            var options = new UIShowOptions(animated: false, hidePrevious: false);
            return host switch
            {
                GameSceneId.Hub => await UIManager.Instance.ShowAsync<HubSettingsView>(view => view.SetData(category), options, token),
                GameSceneId.DroneFlight => await UIManager.Instance.ShowAsync<DroneFlightGlobalSettingsView>(view => view.SetData(category), options, token),
                GameSceneId.Dlss => await UIManager.Instance.ShowAsync<DlssGlobalSettingsView>(view => view.SetData(category), options, token),
                GameSceneId.WallSqueeze => await UIManager.Instance.ShowAsync<WallSqueezeGlobalSettingsView>(view => view.SetData(category), options, token),
                GameSceneId.KagitKart => await UIManager.Instance.ShowAsync<KagitKartGlobalSettingsView>(view => view.SetData(category), options, token),
                _ => throw new ArgumentOutOfRangeException(nameof(host))
            };
        }

        internal static async UniTask<SettingsApplyResult> ApplyAsync(GameSceneId host, GameSettingsSnapshot candidate, CancellationToken token)
        {
            if (host == GameSceneId.Hub && candidate.Language != GameSettingsService.Current.Language)
            {
                // CloseAll 会销毁发起者，重开只捕获类型与分类，不把旧 View 或控件带进新语言。
                var result = await HubLanguageFlow.ApplyAsync(candidate,
                    () => OpenAsync(GameSceneId.Hub, SettingsCategory.Language), ConfirmDisplayAsync, token);
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
