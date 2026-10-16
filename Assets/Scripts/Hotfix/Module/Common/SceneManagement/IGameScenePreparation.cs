using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hotfix.SceneManagement
{
    /// 场景激活后、Loading 结束前完成宿主必需资源与首界面准备。
    public interface IGameScenePreparation
    {
        /// <summary>准备可交互场景；重复调用共享宿主的在途任务。</summary>
        /// <param name="report">从零到一的准备进度及当前步骤。</param>
        /// <param name="cancellationToken">本次等待的取消令牌。</param>
        UniTask PrepareSceneAsync(Action<float, string> report, CancellationToken cancellationToken);
    }
}
