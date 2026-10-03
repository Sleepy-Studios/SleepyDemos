using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 调试台贴安全区右侧，高度不足时由内容滚动承载，保留可读字号。
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DroneDebugPanelLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private void OnEnable() => lastSize = Vector2.zero;
        private void LateUpdate()
        {
            if (panel == null) return;
            var size = ((RectTransform)transform).rect.size;
            var safe = Screen.safeArea;
            if (size == lastSize && safe == lastSafeArea) return;
            lastSize = size;
            lastSafeArea = safe;
            ApplyLayout(new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height)));
        }

        /// <summary>按宿主尺寸应用安全区，面板内容保持独立滚动。</summary>
        /// <param name="safeArea">归一化屏幕安全区。</param>
        public void ApplyLayout(Rect safeArea)
        {
            if (panel == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            var width = Mathf.Max(1f, size.x * safeArea.width - 48f);
            var height = Mathf.Max(1f, size.y * safeArea.height - 64f);
            var scale = Mathf.Min(1f, width / 690f);
            panel.anchorMin = panel.anchorMax = new Vector2(safeArea.xMax, safeArea.yMax);
            panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-24f, -32f);
            panel.localScale = Vector3.one * scale;
            panel.sizeDelta = new Vector2(690f, Mathf.Min(1016f, height / scale));
        }
    }
}
