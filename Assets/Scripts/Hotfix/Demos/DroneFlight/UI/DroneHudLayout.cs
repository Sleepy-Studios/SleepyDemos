using Core.Runtime.Inputs;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// HUD 只对独立仪表和菜单适配，场景中心与触控区域始终贴安全区边缘。
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DroneHudLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform safeRoot;
        [SerializeField] private RectTransform telemetry;
        [SerializeField] private RectTransform equipment;
        [SerializeField] private RectTransform gear;
        [SerializeField] private RectTransform operationPanel;
        [SerializeField] private RectTransform[] instruments;
        private Vector2 lastSize;
        private Rect lastSafe;
        private bool lastTouch;

        private void OnEnable() => lastSize = Vector2.zero;
        private void LateUpdate()
        {
            var size = ((RectTransform)transform).rect.size;
            var safe = Screen.safeArea;
            bool touch = Application.isPlaying ? InputDeviceState.ActiveKind == InputDeviceKind.Touch : Application.isMobilePlatform;
            if (size == lastSize && safe == lastSafe && touch == lastTouch) return;
            lastSize = size; lastSafe = safe; lastTouch = touch;
            ApplyLayout(new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height)), touch);
        }

        /// <summary>按安全区放置仪表；触屏将读数移离双摇杆。</summary>
        /// <param name="safeArea">归一化屏幕安全区。</param>
        /// <param name="touch">是否采用双摇杆布局。</param>
        public void ApplyLayout(Rect safeArea, bool touch)
        {
            if (safeRoot == null) return;
            safeRoot.anchorMin = safeArea.min; safeRoot.anchorMax = safeArea.max;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            float width = ((RectTransform)transform).rect.width * safeArea.width;
            float height = ((RectTransform)transform).rect.height * safeArea.height;
            float scale = Mathf.Min(1, width / 1550f, height / 820f);
            if (scale <= 0) return;
            foreach (var instrument in instruments) instrument.localScale = Vector3.one * scale;
            telemetry.anchorMin = telemetry.anchorMax = telemetry.pivot = touch ? new Vector2(0, 1) : Vector2.zero;
            telemetry.anchoredPosition = touch ? new Vector2(28, -130 * scale) : new Vector2(28, 28);
            equipment.anchoredPosition = new Vector2(0, touch ? 310 * scale : 155 * scale);
            gear.anchoredPosition = new Vector2(-28, touch ? 310 * scale : 28);
            operationPanel.localScale = Vector3.one * Mathf.Min(1, width / 800, height / 780);
        }
    }
}
