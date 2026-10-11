using Core.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 正式 Prefab 的独立遮幕、黑边和字幕引用；不生成替代 UI 模板。
    public sealed class CinematicOverlayBindings : MonoBehaviour
    {
        [SerializeField] private CinematicPresenter presenter;
        [SerializeField] private RectTransform frame;
        [SerializeField] private CanvasGroup curtainGroup;
        [SerializeField] private Image curtain;
        [SerializeField] private Image upperBar;
        [SerializeField] private Image lowerBar;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private Material material;
        private RectTransform viewport;
        private readonly Vector3[] corners = new Vector3[4];

        public CinematicPresenter Presenter => presenter;

        /// 使用制作引用初始化；材质副本与通道清理由公共 Presenter 负责。
        public void Initialize() => presenter.Initialize(curtainGroup, curtain, frame, upperBar, lowerBar, titleGroup, title, subtitle, material);

        /// <summary>选择局部容器；null 恢复本 View 的全屏几何尺寸。</summary>
        /// <param name="value">当前可见页面的画面容器，不修改其锚点或层级。</param>
        public void SetViewport(RectTransform value)
        {
            viewport = value;
            RefreshViewport();
        }

        /// 跟随制作容器的几何尺寸，不使用 Screen 尺寸或另建 Canvas。
        public void RefreshViewport()
        {
            var root = (RectTransform)transform;
            if (viewport == null)
            {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = frame.offsetMax = Vector2.zero;
                return;
            }
            viewport.GetWorldCorners(corners);
            var bottom = root.InverseTransformPoint(corners[0]);
            var top = root.InverseTransformPoint(corners[2]);
            frame.anchorMin = frame.anchorMax = root.pivot;
            frame.pivot = Vector2.one * 0.5f;
            frame.anchoredPosition = (bottom + top) * 0.5f;
            frame.sizeDelta = new Vector2(top.x - bottom.x, top.y - bottom.y);
        }
    }
}
