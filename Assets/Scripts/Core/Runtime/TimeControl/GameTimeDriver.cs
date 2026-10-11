using UnityEngine;

namespace Core.Runtime.TimeControls
{
    /// 仅有演出作用域时存在的未缩放时间驱动，销毁时恢复全局时间状态。
    [DefaultExecutionOrder(-10000)]
    internal sealed class GameTimeDriver : MonoBehaviour
    {
        private void Update() => GameTimeService.Tick();

        private void OnDestroy() => GameTimeService.OnDriverDestroyed(this);
    }
}
