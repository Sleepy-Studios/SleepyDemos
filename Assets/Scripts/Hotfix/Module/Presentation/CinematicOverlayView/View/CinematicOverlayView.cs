using Core.Runtime;
using Core.Runtime.Presentation;
using UnityEngine;

namespace Hotfix
{
    /// UIManager 托管的公共电影表现根，默认全屏；体验页可指定局部画面容器。
    [Module("Presentation")]
    [UIBind("CinematicOverlayView")]
    public partial class CinematicOverlayView : View
    {
        protected override void OnGameObjectInitialize()
        {
            CinematicOverlayBindings_Overlay.Initialize();
            BindUpdate(CinematicOverlayBindings_Overlay.RefreshViewport);
        }

        /// <summary>在显示前交付目标容器，null 使用全屏；不持有调用方 HUD。</summary>
        /// <param name="viewport">本次演出画面所在的 RectTransform，关闭 View 时解除引用。</param>
        public void SetData(RectTransform viewport)
        {
            CinematicOverlayBindings_Overlay.SetViewport(viewport);
            if (IsEnable)
            {
                CinematicRuntime.Register(CinematicOverlayBindings_Overlay.Presenter);
            }
        }

        protected override void OnShow()
        {
            CinematicOverlayBindings_Overlay.RefreshViewport();
            CinematicRuntime.Register(CinematicOverlayBindings_Overlay.Presenter);
            base.OnShow();
        }

        protected override void OnHide()
        {
            CinematicRuntime.Unregister(CinematicOverlayBindings_Overlay.Presenter);
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (CinematicOverlayBindings_Overlay != null)
            {
                CinematicRuntime.Unregister(CinematicOverlayBindings_Overlay.Presenter);
                CinematicOverlayBindings_Overlay.SetViewport(null);
                CinematicOverlayBindings_Overlay.Presenter.Release();
            }
            base.OnDestroy();
        }

        /// 取消令牌由调用者负责；停止后立即清理本 View 的三个表现通道。
        public void ResetEffects() => CinematicOverlayBindings_Overlay.Presenter.ResetEffects();
    }
}
