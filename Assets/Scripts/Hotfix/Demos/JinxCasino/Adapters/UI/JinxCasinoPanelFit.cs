using UnityEngine;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 保存面板适配Core提供的父容器；只缩放已有布局，不创建Canvas或运行时控件。
    [DisallowMultipleComponent]
    public sealed class JinxCasinoPanelFit : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceSize;
        [SerializeField] private Vector2 padding = new Vector2(24, 24);
        private RectTransform panel;
        private RectTransform parent;
        private Vector2 previousSize = new Vector2(-1, -1);

        /// <summary>保存面板原始尺寸及父容器留白；等比适配保留按钮相对大小。</summary>
        /// <param name="size">预制布局的正参考尺寸。</param>
        /// <param name="edgePadding">父容器坐标中的边缘留白，不使用Screen.width。</param>
        public void Configure(Vector2 size, Vector2 edgePadding)
        {
            referenceSize = size; padding = edgePadding; previousSize = new Vector2(-1, -1);
        }

        private void OnEnable() { panel = transform as RectTransform; parent = transform.parent as RectTransform; previousSize = new Vector2(-1, -1); }
        private void OnTransformParentChanged() { parent = transform.parent as RectTransform; previousSize = new Vector2(-1, -1); }
        private void LateUpdate()
        {
            if (panel == null || parent == null || referenceSize.x <= 0 || referenceSize.y <= 0) return;
            Vector2 size = parent.rect.size;
            if (size == previousSize || size.x <= 0 || size.y <= 0) return;
            previousSize = size;
            Vector2 available = size - padding * 2;
            // 顶角锚点已占用的偏移必须算入，避免工具栏从顶部210处开始却使用整个父高度。
            if (panel.anchorMin == panel.anchorMax && panel.anchorMin.y == 1 && panel.pivot.y == 1)
                available.y = size.y + Mathf.Min(0, panel.anchoredPosition.y) - padding.y;
            float scale = Mathf.Min(1, available.x / referenceSize.x, available.y / referenceSize.y);
            panel.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }
    }
}
