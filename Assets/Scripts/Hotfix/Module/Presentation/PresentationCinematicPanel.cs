using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hotfix
{
    /// 电影演出体验使用公共设置行，正式预设只读，参数只写本次运行时副本。
    public sealed class PresentationCinematicPanel : MonoBehaviour
    {
        [SerializeField] private CinematicPreset[] presets;
        [SerializeField] private UISettingRow styleRow;
        [SerializeField] private UISettingRow directionRow;
        [SerializeField] private UISettingRow aspectRow;
        [SerializeField] private UISettingRow closeRow;
        [SerializeField] private UISettingRow openRow;
        [SerializeField] private UISettingRow letterboxRow;
        [SerializeField] private UISettingRow hudRow;
        [SerializeField] private UISettingRow chapterRow;
        [SerializeField] private UISettingRow locationRow;
        private CinematicPresenter.LetterboxHandle bars;
        private CinematicPreset runtimePreset;
        private int style = 1;
        private int direction;
        private int aspect;
        private float closeDuration = 0.4f;
        private float openDuration = 0.4f;
        private bool letterbox = true;
        private bool hideHud = true;
        private bool chapter = true;
        private bool location = true;

        /// 使用已制作的七种预设与四方向选项。
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

        /// <summary>播放遮幕并在开幕后显示字幕，HUD 引用由当前体验页明确交付。</summary>
        /// <param name="world">本页制作世界，中点只重置其运动。</param>
        /// <param name="hud">可随黑边淡出的局部 HUD。</param>
        /// <param name="token">停止或退出时取消，不强行停止其它业务事务。</param>
        /// <returns>遮幕与字幕结束，黑边保持到正常退出或释放。</returns>
        public async UniTask PlayAsync(PresentationWorld world, CanvasGroup hud, CancellationToken token)
        {
            if (!CinematicRuntime.IsRegistered)
            {
                throw new InvalidOperationException("公共电影表现 View 尚未就绪。");
            }
            Stop();
            var preset = Object.Instantiate(presets[style]);
            runtimePreset = preset;
            try
            {
                preset.ConfigureTransition(preset.TransitionStyle, (CinematicWipeDirection)direction,
                    closeDuration, 0, openDuration, preset.CurtainColor, preset.BlindCount, preset.PixelColumns);
                var hudRect = ((RectTransform)hud.transform).rect;
                float contentAspect = letterbox ? aspect == 1 ? 2.39f : aspect == 2 ? 2.76f : 2.35f
                    : Mathf.Clamp(hudRect.width / Mathf.Max(1, hudRect.height), 1, 4);
                preset.ConfigureLetterbox(contentAspect, 0.3f);
                if (letterbox || hideHud)
                {
                    bars = CinematicRuntime.PushLetterbox(preset, hideHud ? new[] { hud } : null);
                }
                await CinematicRuntime.PlayTransitionAsync(preset, _ =>
                {
                    world.ResetMotion();
                    return UniTask.CompletedTask;
                }, token);
                if (chapter || location)
                {
                    await CinematicRuntime.ShowTitleAsync(chapter ? "Presentation.Chapter" : "Presentation.Location",
                        chapter && location ? "Presentation.Location" : null,
                        preset.TitleFadeIn, preset.TitleHold, preset.TitleFadeOut, token);
                }
            }
            catch
            {
                if (ReferenceEquals(runtimePreset, preset))
                {
                    Stop();
                }
                throw;
            }
            finally
            {
                if (bars == null && ReferenceEquals(runtimePreset, preset))
                {
                    runtimePreset = null;
                    Object.Destroy(preset);
                }
            }
        }

        /// <summary>正常缩回黑边并淡入本页 HUD；异常或退出直接 Stop。</summary>
        /// <param name="token">取消时立即恢复，正常结束沿用制作时长。</param>
        /// <returns>黑边退出任务。</returns>
        public async UniTask ExitLetterboxAsync(CancellationToken token)
        {
            var previous = bars;
            if (previous == null)
            {
                return;
            }
            try
            {
                await previous.ExitAsync(cancellationToken: token);
            }
            finally
            {
                if (ReferenceEquals(bars, previous))
                {
                    Stop();
                }
            }
        }

        /// 异常或卸载时立即归还黑边，制作副本保留到句柄退出后才销毁。
        public void Stop()
        {
            bars?.Dispose();
            bars = null;
            if (runtimePreset != null)
            {
                Object.Destroy(runtimePreset);
                runtimePreset = null;
            }
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
