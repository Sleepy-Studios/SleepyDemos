using System;
using Core.Runtime;
using Core.Runtime.Presentation;
using DG.Tweening;
using UnityEngine;

namespace Hotfix
{
    /// UIManager 托管的综合电影表现 View，默认全屏，也可指定局部画面。
    [Module("Presentation")]
    [UIBind("CinematicOverlayView")]
    public partial class CinematicOverlayView : View
    {
        public bool IsTransitioning => CinematicPresenter_Overlay.IsTransitioning;
        public bool IsTitlePlaying => CinematicPresenter_Overlay.IsTitlePlaying;
        public bool IsLetterboxVisible => CinematicPresenter_Overlay.IsLetterboxVisible;

        protected override void OnGameObjectInitialize() => CinematicPresenter_Overlay.Initialize();

        /// <summary>指定目标画面，null 使用全屏；切换目标会停止当前演出。</summary>
        /// <param name="viewport">当前页面的画面容器，不改变其层级和布局。</param>
        public void SetData(RectTransform viewport) => CinematicPresenter_Overlay.SetViewport(viewport);

        /// <inheritdoc cref="CinematicPresenter.StartTransition"/>
        public void StartTransition(Action onCovered = null, float closedHold = 0, Action onComplete = null,
            CinematicTransitionStyle style = CinematicTransitionStyle.Fade, float closeDuration = 0.35f,
            float openDuration = 0.35f, Ease ease = Ease.InOutSine,
            CinematicWipeDirection direction = CinematicWipeDirection.LeftToRight, Color? color = null,
            int blindCount = 12, int pixelColumns = 48, Action<Exception> onFailed = null)
        {
            CinematicPresenter_Overlay.StartTransition(onCovered, closedHold, onComplete, style,
                closeDuration, openDuration, ease, direction, color, blindCount, pixelColumns, onFailed);
        }

        /// <inheritdoc cref="CinematicPresenter.CloseCurtain"/>
        public void CloseCurtain(CinematicTransitionStyle style = CinematicTransitionStyle.Fade, float duration = 0.35f,
            Action onComplete = null, Ease ease = Ease.InOutSine,
            CinematicWipeDirection direction = CinematicWipeDirection.LeftToRight, Color? color = null,
            int blindCount = 12, int pixelColumns = 48)
        {
            CinematicPresenter_Overlay.CloseCurtain(style, duration, onComplete, ease, direction, color, blindCount, pixelColumns);
        }

        /// <inheritdoc cref="CinematicPresenter.OpenCurtain"/>
        public void OpenCurtain(float duration = 0.35f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            CinematicPresenter_Overlay.OpenCurtain(duration, onComplete, ease);
        }

        /// <inheritdoc cref="CinematicPresenter.ShowLetterbox"/>
        public void ShowLetterbox(float heightRatio = 0.12f, float duration = 0.3f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            CinematicPresenter_Overlay.ShowLetterbox(heightRatio, duration, onComplete, ease);
        }

        /// <inheritdoc cref="CinematicPresenter.ShowLetterboxForAspect"/>
        public void ShowLetterboxForAspect(float targetAspect = 2.39f, float duration = 0.3f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            CinematicPresenter_Overlay.ShowLetterboxForAspect(targetAspect, duration, onComplete, ease);
        }

        /// <inheritdoc cref="CinematicPresenter.HideLetterbox"/>
        public void HideLetterbox(float duration = 0.3f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            CinematicPresenter_Overlay.HideLetterbox(duration, onComplete, ease);
        }

        /// <inheritdoc cref="CinematicPresenter.ShowTitle"/>
        public void ShowTitle(string titleKey, string subtitleKey = null, float fadeIn = 0.3f, float hold = 2,
            float fadeOut = 0.3f, Action onComplete = null)
        {
            CinematicPresenter_Overlay.ShowTitle(titleKey, subtitleKey, fadeIn, hold, fadeOut, onComplete);
        }

        /// 停止三个通道并清空表现，不触发完成回调。
        public void ResetEffects() => CinematicPresenter_Overlay.ResetEffects();

        protected override void OnShow()
        {
            CinematicPresenter_Overlay.RefreshViewport();
            base.OnShow();
        }

        protected override void OnHide()
        {
            ResetEffects();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (CinematicPresenter_Overlay != null)
            {
                CinematicPresenter_Overlay.Release();
            }
            base.OnDestroy();
        }
    }
}
