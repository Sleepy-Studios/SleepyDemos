using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hotfix.BlockPorters.Adapters
{
    public enum PorterRewardResult { Completed, Canceled, Unavailable }

    public interface IBlockPortersReward
    {
        /// <summary>请求复活奖励；取消不得转换为成功。</summary>
        /// <param name="cancellationToken">当前场景会话的取消令牌。</param>
        UniTask<PorterRewardResult> RequestReviveAsync(CancellationToken cancellationToken);
    }

    /// 本地模拟，无广告 SDK、网络请求或收益承诺。
    public sealed class SimulatedBlockPortersReward : IBlockPortersReward
    {
        public async UniTask<PorterRewardResult> RequestReviveAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            return PorterRewardResult.Completed;
        }
    }
}
