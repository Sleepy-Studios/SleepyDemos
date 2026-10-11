using System;
using Core.Runtime;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;

namespace Hotfix
{
    /// 将一次公共设置提交的进度和结果交给页面。
    public sealed class SettingsHandler : HandlerBase<SettingsApplyAction, SettingsData>
    {
        /// <summary>启动可等待设置事务，不在 Reduce 中直接修改制作资产。</summary>
        /// <param name="action">页面构造的候选。</param>
        protected override void Reduce(SettingsApplyAction action)
        {
            if (State.IsApplying)
            {
                action.Complete(new SettingsApplyResult(false, "设置正在应用，请稍候。"));
                return;
            }

            State.IsApplying = true;
            State.Feedback = null;
            int version = ++State.Version;
            ApplyState();
            ApplyAsync(action, version).Forget();
        }

        private async UniTask ApplyAsync(SettingsApplyAction action, int version)
        {
            SettingsApplyResult result;
            try
            {
                result = await GameSettingsService.ApplyAsync(action.Candidate, action.ConfirmDisplay,
                    cancellationToken: action.CancellationToken);
            }
            catch (Exception exception)
            {
                result = new SettingsApplyResult(false, exception.Message);
            }

            if (ReferenceEquals(GlobalData.Get<SettingsData>(), State) && State.Version == version)
            {
                State.IsApplying = false;
                State.Feedback = result.Message;
                ApplyState();
            }

            action.Complete(result);
        }
    }
}
