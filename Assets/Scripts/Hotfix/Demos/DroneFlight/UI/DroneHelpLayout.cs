using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.DroneFlight
{
    /// 指南安全区与双列/单列排版；正文由 ScrollRect 承载，窄屏不缩小文字。
    [ExecuteAlways]
    public sealed class DroneHelpLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform safeRoot;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private GridLayoutGroup grid;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private float lastViewportWidth = -1;
        private void OnEnable() => lastViewportWidth = -1;
        private void LateUpdate()
        {
            Rect safe = Screen.safeArea;
            ApplyLayout(new Rect(safe.x / Mathf.Max(1, Screen.width), safe.y / Mathf.Max(1, Screen.height),
                safe.width / Mathf.Max(1, Screen.width), safe.height / Mathf.Max(1, Screen.height)));
        }

        /// <summary>根据宿主尺寸与安全区调整指南列数，正文保持滚动。</summary>
        /// <param name="normalizedSafeArea">以屏幕左下角为原点的归一化安全区。</param>
        public void ApplyLayout(Rect normalizedSafeArea)
        {
            if (safeRoot == null || grid == null || viewport == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (lastSize == size && lastSafeArea == normalizedSafeArea &&
                Mathf.Approximately(lastViewportWidth, viewport.rect.width)) return;
            lastSize = size;
            lastSafeArea = normalizedSafeArea;
            safeRoot.anchorMin = normalizedSafeArea.min;
            safeRoot.anchorMax = normalizedSafeArea.max;
            float width = Mathf.Max(1, viewport.rect.width);
            lastViewportWidth = viewport.rect.width;
            int columns = width >= 1050 ? 2 : 1;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2((width - grid.spacing.x * (columns - 1)) / columns, 270);
        }
    }
}
