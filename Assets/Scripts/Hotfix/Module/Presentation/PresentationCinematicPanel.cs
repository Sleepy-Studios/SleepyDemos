using System;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Core.Runtime.Settings;
using DG.Tweening;
using UnityEngine;

namespace Hotfix
{
    /// 电影演出体验使用公共设置行，直接向本页表现 View 交付参数。
    public sealed class PresentationCinematicPanel : MonoBehaviour
    {
        [SerializeField] private UISettingRow styleRow;
        [SerializeField] private UISettingRow directionRow;
        [SerializeField] private UISettingRow aspectRow;
        [SerializeField] private UISettingRow closeRow;
        [SerializeField] private UISettingRow openRow;
        [SerializeField] private UISettingRow letterboxRow;
        [SerializeField] private UISettingRow hudRow;
        [SerializeField] private UISettingRow chapterRow;
        [SerializeField] private UISettingRow locationRow;
        private CinematicOverlayView overlay;
        private CanvasGroup hud;
        private Tween hudTween;
        private float hudAlpha;
        private bool hudInteractable;
        private bool hudBlocksRaycasts;
        private int operationVersion;
        private int style = 1;
        private int direction;
        private int aspect;
        private float closeDuration = 0.4f;
        private float openDuration = 0.4f;
        private bool letterbox = true;
        private bool hideHud = true;
        private bool chapter = true;
        private bool location = true;

        /// 使用七种样式与四方向选项。
        public void Initialize()
        {
            styleRow.SetLabel("Presentation.TransitionStyle");
            directionRow.SetLabel("Presentation.Direction");
            aspectRow.SetLabel("Presentation.Aspect");
            closeRow.SetLabel("Presentation.CloseDuration");
            openRow.SetLabel("Presentation.OpenDuration");
            letterboxRow.SetLabel("Presentation.Letterbox");
            hudRow.SetLabel("Presentation.HideHud");
            chapterRow.SetLabel("Presentation.ShowChapter");
            locationRow.SetLabel("Presentation.ShowLocation");
            ResetControls();
        }

        /// <summary>播放本页遮幕与字幕；结束后黑边和 HUD 隐藏保持到停止。</summary>
        /// <param name="world">本页制作世界，中点只重置其运动。</param>
        /// <param name="hud">可随黑边淡出的局部 HUD。</param>
        /// <param name="overlay">本次 UIManager 打开的表现 View。</param>
        /// <param name="onComplete">遮幕和字幕正常结束时调用；停止不调用。</param>
        /// <param name="onFailed">播放失败时调用；省略时记录 Console。</param>
        public void Play(PresentationWorld world, CanvasGroup hud, CinematicOverlayView overlay,
            Action onComplete = null, Action<Exception> onFailed = null)
        {
            Stop();
            int version = operationVersion;
            try
            {
                if (world == null || hud == null || overlay == null || !overlay.IsEnable)
                {
                    throw new InvalidOperationException("公共电影表现 View 或体验世界尚未就绪。");
                }
                this.overlay = overlay;
                if (hideHud)
                {
                    this.hud = hud;
                    hudAlpha = hud.alpha;
                    hudInteractable = hud.interactable;
                    hudBlocksRaycasts = hud.blocksRaycasts;
                    hud.interactable = false;
                    hud.blocksRaycasts = false;
                    hudTween = DOTween.To(() => hud.alpha, value => hud.alpha = value, 0, HudDuration)
                        .SetEase(Ease.InOutSine).SetUpdate(true);
                }
                if (letterbox)
                {
                    overlay.ShowLetterboxForAspect(aspect == 1 ? 2.39f : aspect == 2 ? 2.76f : 2.35f);
                }
                overlay.StartTransition(onCovered: () =>
                {
                    if (version == operationVersion)
                    {
                        world.ResetMotion();
                    }
                }, onComplete: () =>
                {
                    if (version != operationVersion)
                    {
                        return;
                    }
                    try
                    {
                        if (chapter || location)
                        {
                            overlay.ShowTitle(chapter ? "Presentation.Chapter" : "Presentation.Location",
                                chapter && location ? "Presentation.Location" : null,
                                onComplete: () => Complete(version, onComplete));
                        }
                        else
                        {
                            Complete(version, onComplete);
                        }
                    }
                    catch (Exception exception)
                    {
                        Fail(version, exception, onFailed);
                    }
                }, style: (CinematicTransitionStyle)style, closeDuration: closeDuration,
                    openDuration: openDuration, direction: (CinematicWipeDirection)direction,
                    onFailed: exception => Fail(version, exception, onFailed));
            }
            catch (Exception exception)
            {
                Fail(version, exception, onFailed);
            }
        }

        private static float HudDuration => GameSettingsService.Current.Accessibility.IsReducedMotion ? 0 : 0.3f;

        private void Complete(int version, Action onComplete)
        {
            if (version == operationVersion)
            {
                onComplete?.Invoke();
            }
        }

        private void Fail(int version, Exception exception, Action<Exception> onFailed)
        {
            if (version != operationVersion)
            {
                return;
            }
            Stop();
            if (onFailed == null)
            {
                Debug.LogException(exception);
            }
            else
            {
                onFailed(exception);
            }
        }

        /// <summary>演出结束后缩回黑边并淡入 HUD，完成后清理本页表现。</summary>
        /// <param name="onComplete">正常退出完成时调用；Stop 会使旧回调失效。</param>
        public void ExitLetterbox(Action onComplete = null)
        {
            int version = ++operationVersion;
            hudTween?.Kill();
            hudTween = null;
            if (overlay == null || !overlay.IsLoaded)
            {
                Stop();
                onComplete?.Invoke();
                return;
            }
            if (hud != null)
            {
                hudTween = DOTween.To(() => hud.alpha, value => hud.alpha = value, hudAlpha, HudDuration)
                    .SetEase(Ease.InOutSine).SetUpdate(true);
            }
            overlay.HideLetterbox(onComplete: () =>
            {
                if (version != operationVersion)
                {
                    return;
                }
                Stop();
                onComplete?.Invoke();
            });
        }

        /// 异常或卸载时立即恢复本页 HUD 与表现，不触发旧完成回调。
        public void Stop()
        {
            operationVersion++;
            hudTween?.Kill();
            hudTween = null;
            if (hud != null)
            {
                hud.alpha = hudAlpha;
                hud.interactable = hudInteractable;
                hud.blocksRaycasts = hudBlocksRaycasts;
                hud = null;
            }
            if (overlay != null && overlay.IsLoaded)
            {
                overlay.ResetEffects();
            }
            overlay = null;
        }

        /// 恢复已确认美术图的初始选择。
        public void ResetControls()
        {
            Stop();
            style = 1;
            direction = 0;
            aspect = 0;
            closeDuration = openDuration = 0.4f;
            letterbox = hideHud = chapter = location = true;
            styleRow.SetOptions(new[] { Word("Fade"), Word("Wipe"), Word("DoorsHorizontal"), Word("DoorsVertical"),
                Word("Iris"), Word("Blinds"), Word("Pixel") }, style, OnStyle);
            directionRow.SetOptions(new[] { Word("LeftToRight"), Word("RightToLeft"), Word("BottomToTop"), Word("TopToBottom") },
                direction, value => direction = value);
            aspectRow.SetOptions(new[] { "2.35 : 1", "2.39 : 1", "2.76 : 1" }, aspect, value => aspect = value);
            closeRow.SetSlider(new Vector2(0, 2), closeDuration, OnClose);
            openRow.SetSlider(new Vector2(0, 2), openDuration, OnOpen);
            letterboxRow.SetToggle(letterbox, value => letterbox = value);
            hudRow.SetToggle(hideHud, value => hideHud = value);
            chapterRow.SetToggle(chapter, value => chapter = value);
            locationRow.SetToggle(location, value => location = value);
            OnClose(closeDuration);
            OnOpen(openDuration);
            OnStyle(style);
        }

        private static string Word(string key) => LanguageService.Get("Presentation." + key);
        private void OnStyle(int index)
        {
            style = index;
            directionRow.SetAvailable(index == 1 || index == 5);
        }
        private void OnClose(float value)
        {
            closeDuration = value;
            closeRow.SetValue(LanguageService.Format("Presentation.Seconds", value));
        }
        private void OnOpen(float value)
        {
            openDuration = value;
            openRow.SetValue(LanguageService.Format("Presentation.Seconds", value));
        }
        private void OnDestroy() => Stop();
    }
}
