using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;

namespace Hotfix
{
    /// 请求应用公共设置，页面等待 Completion 后显示实际结果。
    public sealed class SettingsApplyAction : IAction
    {
        public GameSettingsSnapshot Candidate;
        public Func<CancellationToken, UniTask<bool>> ConfirmDisplay;
        public CancellationToken CancellationToken;
        private readonly UniTaskCompletionSource<SettingsApplyResult> completion = new();

        /// 本次提交完成，不与页面销毁绑定服务状态。
        public UniTask<SettingsApplyResult> Completion => completion.Task;

        /// <summary>构造后通过 GlobalData.Dispatch 派发，普通页面不允许切换语言。</summary>
        /// <param name="candidate">独立候选。</param>
        /// <param name="confirmDisplay">显示变更的保留确认。</param>
        /// <param name="cancellationToken">取消本次提交。</param>
        public SettingsApplyAction(GameSettingsSnapshot candidate,
            Func<CancellationToken, UniTask<bool>> confirmDisplay = null, CancellationToken cancellationToken = default)
        {
            Candidate = candidate?.Copy();
            ConfirmDisplay = confirmDisplay;
            CancellationToken = cancellationToken;
        }

        internal void Complete(SettingsApplyResult result) => completion.TrySetResult(result);
    }
}
