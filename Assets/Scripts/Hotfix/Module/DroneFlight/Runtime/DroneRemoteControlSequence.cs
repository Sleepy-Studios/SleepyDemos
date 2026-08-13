namespace Hotfix.DroneFlight
{
    /// <summary>无人机直接控制会话状态。</summary>
    internal enum DroneControlSessionState
    {
        Waiting,
        Active
    }

    /// <summary>与 Camera 和输入组件解耦的两态控制会话。</summary>
    internal sealed class DroneControlSession
    {
        internal DroneControlSessionState State { get; private set; } = DroneControlSessionState.Waiting;

        internal bool Activate()
        {
            if (State == DroneControlSessionState.Active)
            {
                return false;
            }

            State = DroneControlSessionState.Active;
            return true;
        }

        internal bool ReturnToWaiting()
        {
            if (State == DroneControlSessionState.Waiting)
            {
                return false;
            }

            State = DroneControlSessionState.Waiting;
            return true;
        }
    }
}
