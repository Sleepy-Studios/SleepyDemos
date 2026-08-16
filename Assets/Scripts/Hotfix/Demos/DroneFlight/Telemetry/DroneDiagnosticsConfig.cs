using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机遥测采样和调试界面刷新参数。</summary>
    [CreateAssetMenu(fileName = "DroneDiagnosticsConfig", menuName = "SleepyDemos/Drone Flight/Diagnostics Config")]
    public sealed class DroneDiagnosticsConfig : ScriptableObject
    {
        [SerializeField, Min(16), InspectorName("遥测保留样本数")]
        [Tooltip("内存中保留的最近遥测样本数量。提高数值会增加少量常驻内存。")]
        private int sampleCapacity = 500;

        [SerializeField, Min(0.02f), InspectorName("界面刷新间隔 (秒)")]
        [Tooltip("HUD 和 F3 调试文本重新生成的最短间隔，不影响真实物理采样。")]
        private float uiRefreshIntervalSeconds = 0.1f;

        public int SampleCapacity => sampleCapacity;
        public float UiRefreshIntervalSeconds => uiRefreshIntervalSeconds;
    }
}
