using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机玩家输入和平滑复位参数。</summary>
    [CreateAssetMenu(fileName = "DroneInputConfig", menuName = "SleepyDemos/Drone Flight/Input Config")]
    public sealed class DroneInputConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f), InspectorName("键盘输入备用上升率")]
        [Tooltip("飞控档位未提供有效输入上升率时使用的备用值。")]
        private float keyboardFallbackRiseRate = 3f;

        [SerializeField, Min(0.1f), InspectorName("键盘输入回中速度")]
        [Tooltip("松开按键后输入回到零的速度。数值越大，停止操作越干脆。")]
        private float keyboardFallRate = 5f;

        [SerializeField, Min(0.5f), InspectorName("长按重载时间 (秒)")]
        [Tooltip("持续按住 R 达到此时长后请求重新加载 DroneFlight 场景。")]
        private float resetHoldSeconds = 5f;

        public float KeyboardFallbackRiseRate => keyboardFallbackRiseRate;
        public float KeyboardFallRate => keyboardFallRate;
        public float ResetHoldSeconds => resetHoldSeconds;
    }
}
