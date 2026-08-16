namespace Hotfix.DroneFlight
{
    /// <summary>玩家或宿主是否正在控制当前无人机的最小会话契约。</summary>
    public interface IDroneControlSession
    {
        /// 当前是否允许消费玩家和装备输入。
        bool IsActive { get; }

        /// 激活无人机控制并切换到对应的输入与相机状态。
        void Activate();

        /// 退出无人机控制、清空瞬时输入并恢复等待状态。
        void ReturnToWaiting();
    }
}
