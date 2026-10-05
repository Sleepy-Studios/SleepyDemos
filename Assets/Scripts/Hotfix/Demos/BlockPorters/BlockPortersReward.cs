using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hotfix.BlockPorters
{
    public enum PorterRewardResult { Completed, Canceled, Unavailable }

    public interface IBlockPortersReward
    {
        /// <summary>请求单侧额外任务位奖励；取消不得转换为成功。</summary>
        /// <param name="side">0 为左侧，1 为右侧。</param>
        /// <param name="cancellationToken">当前场景会话的取消令牌。</param>
        UniTask<PorterRewardResult> RequestExtraSlotAsync(int side, CancellationToken cancellationToken);
    }

    /// 本地模拟，无广告 SDK、网络请求或收益承诺。
    public sealed class SimulatedBlockPortersReward : IBlockPortersReward
    {
        public async UniTask<PorterRewardResult> RequestExtraSlotAsync(int side, CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            return PorterRewardResult.Completed;
        }
    }
}
