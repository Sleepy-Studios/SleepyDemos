using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using UnityEngine;

namespace Hotfix
{
    /// Hub 语言提交与页面重建；任务由协调入口持有，不依赖即将销毁的设置页。
    public static class HubLanguageFlow
    {
        /// 重建期间 Hub 入口不能开始另一个导航。
        public static bool IsChanging { get; private set; }

        /// <summary>仅在稳定 Hub 切语言；销毁业务 UI 后重建，失败恢复旧语言和入口选择。</summary>
        /// <param name="candidate">完整设置候选，正常设置页在每次打开时从 Current 创建。</param>
        /// <param name="reopenSettings">重建 Hub 后重新打开语言分类；由调用方提供可等待导航。</param>
        /// <param name="confirmDisplay">独立显示确认入口，不得捕获旧页面控件。</param>
        /// <param name="cancellationToken">只用于开始前取消，页面销毁后事务自行完成或回滚。</param>
        /// <returns>应用和重建的实际结果。</returns>
        public static async UniTask<SettingsApplyResult> ApplyAsync(GameSettingsSnapshot candidate,
            Func<UniTask<HubSettingsView>> reopenSettings,
            Func<CancellationToken, UniTask<bool>> confirmDisplay = null, CancellationToken cancellationToken = default)
        {
            var navigator = GameSceneNavigator.Instance;
            var hub = GlobalData.Get<MainMenuData>();
            if (IsChanging || GameSettingsService.IsApplying || navigator == null || navigator.CurrentScene != GameSceneId.Hub
                || navigator.IsTransitioning || hub?.IsEntering == true)
            {
                return new SettingsApplyResult(false, "请在 Hub 空闲时切换语言。");
            }

            if (candidate == null || reopenSettings == null)
            {
                return new SettingsApplyResult(false, "语言候选或重建入口缺失。");
            }

            candidate = candidate.Copy();
            candidate.Validate();
            LanguageService.ValidateLanguage(candidate.Language);
            cancellationToken.ThrowIfCancellationRequested();
            var previous = GameSettingsService.Current;
            int selectedIndex = hub?.SelectedIndex ?? 0;
            IsChanging = true;
            try
            {
                await UIManager.Instance.CloseAllAsync();
                var applied = await GameSettingsService.ApplyAsync(candidate, confirmDisplay, allowLanguageChange: true);
                if (!applied.Succeeded)
                {
                    await RebuildAsync(selectedIndex, reopenSettings);
                    return applied;
                }

                await RebuildAsync(selectedIndex, reopenSettings);
                return applied;
            }
            catch (Exception exception)
            {
                var restored = await GameSettingsService.ApplyAsync(previous, _ => UniTask.FromResult(true), allowLanguageChange: true);
                string recoveryFeedback = restored.Succeeded ? "已恢复旧语言" : "旧语言恢复失败：" + restored.Message;
                try
                {
                    await UIManager.Instance.CloseAllAsync();
                    await RebuildAsync(selectedIndex, reopenSettings);
                }
                catch (Exception recoveryError)
                {
                    Debug.LogException(recoveryError);
                    recoveryFeedback += "；页面恢复失败：" + recoveryError.Message;
                }

                return new SettingsApplyResult(false, "语言页面重建失败：" + exception.Message + "；" + recoveryFeedback);
            }
            finally
            {
                IsChanging = false;
            }
        }

        private static async UniTask RebuildAsync(int selectedIndex, Func<UniTask<HubSettingsView>> reopenSettings)
        {
            GlobalData.Remove<MainMenuData>();
            GlobalData.Add<MainMenuData>();
            GlobalData.Dispatch(new MainMenuSelectAction(selectedIndex));
            await UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(animated: false));
            await reopenSettings();
        }

    }
}
