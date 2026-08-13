using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>长按复位输入释放时的确定性结果。</summary>
    internal enum DroneResetReleaseResult
    {
        None,
        ShortPress
    }

    /// <summary>区分 R 短按解锁与长按复位的纯状态机。</summary>
    internal sealed class DroneResetHoldTracker
    {
        private readonly float holdSeconds;
        private float elapsedSeconds;
        private bool isHolding;
        private bool completed;

        internal DroneResetHoldTracker(float requiredHoldSeconds)
        {
            holdSeconds = Mathf.Max(0.1f, requiredHoldSeconds);
        }

        internal float Progress => isHolding ? Mathf.Clamp01(elapsedSeconds / holdSeconds) : 0f;

        internal bool IsHolding => isHolding;

        internal void Begin()
        {
            elapsedSeconds = 0f;
            isHolding = true;
            completed = false;
        }

        internal bool Step(float deltaTime)
        {
            if (!isHolding || completed || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return false;
            }

            elapsedSeconds += deltaTime;
            if (elapsedSeconds < holdSeconds)
            {
                return false;
            }

            elapsedSeconds = holdSeconds;
            completed = true;
            return true;
        }

        internal DroneResetReleaseResult Release()
        {
            if (!isHolding)
            {
                return DroneResetReleaseResult.None;
            }

            isHolding = false;
            var result = completed ? DroneResetReleaseResult.None : DroneResetReleaseResult.ShortPress;
            elapsedSeconds = 0f;
            completed = false;
            return result;
        }
    }
}
