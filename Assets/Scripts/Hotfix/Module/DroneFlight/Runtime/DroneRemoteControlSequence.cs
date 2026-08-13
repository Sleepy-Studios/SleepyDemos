namespace Hotfix.DroneFlight
{
    /// <summary>遥控器接管流程阶段。</summary>
    internal enum DroneRemoteControlState
    {
        GroundIdle,
        PickingUp,
        PoweringOn,
        Connecting,
        Preview,
        Expanding,
        Fullscreen,
        Exiting
    }

    /// <summary>
    /// 与动画和 Camera 解耦的遥控器接管状态机。
    /// </summary>
    internal sealed class DroneRemoteControlSequence
    {
        private float elapsed;

        internal DroneRemoteControlState State { get; private set; } = DroneRemoteControlState.GroundIdle;

        internal float NormalizedProgress { get; private set; }

        internal void BeginEnter()
        {
            if (State != DroneRemoteControlState.GroundIdle)
            {
                return;
            }

            SetState(DroneRemoteControlState.PickingUp);
        }

        internal void ExpandToFullscreen()
        {
            if (State == DroneRemoteControlState.Preview)
            {
                SetState(DroneRemoteControlState.Expanding);
            }
        }

        internal void BeginExit()
        {
            if (State != DroneRemoteControlState.GroundIdle && State != DroneRemoteControlState.Exiting)
            {
                SetState(DroneRemoteControlState.Exiting);
            }
        }

        internal void Step(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            var duration = GetDuration(State);
            if (duration <= 0f)
            {
                NormalizedProgress = 1f;
                return;
            }

            elapsed += deltaTime;
            NormalizedProgress = System.Math.Min(1f, elapsed / duration);
            if (elapsed < duration)
            {
                return;
            }

            switch (State)
            {
                case DroneRemoteControlState.PickingUp:
                    SetState(DroneRemoteControlState.PoweringOn);
                    break;
                case DroneRemoteControlState.PoweringOn:
                    SetState(DroneRemoteControlState.Connecting);
                    break;
                case DroneRemoteControlState.Connecting:
                    SetState(DroneRemoteControlState.Preview);
                    break;
                case DroneRemoteControlState.Expanding:
                    SetState(DroneRemoteControlState.Fullscreen);
                    break;
                case DroneRemoteControlState.Exiting:
                    SetState(DroneRemoteControlState.GroundIdle);
                    break;
            }
        }

        private void SetState(DroneRemoteControlState state)
        {
            State = state;
            elapsed = 0f;
            NormalizedProgress = 0f;
        }

        private static float GetDuration(DroneRemoteControlState state)
        {
            return state switch
            {
                DroneRemoteControlState.PickingUp => 0.7f,
                DroneRemoteControlState.PoweringOn => 0.5f,
                DroneRemoteControlState.Connecting => 0.8f,
                DroneRemoteControlState.Expanding => 0.45f,
                DroneRemoteControlState.Exiting => 0.5f,
                _ => 0f
            };
        }
    }
}
