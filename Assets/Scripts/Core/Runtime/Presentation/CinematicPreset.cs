using System;
using UnityEngine;

namespace Core.Runtime.Presentation
{
    /// 七类遮幕；黑边由独立 Letterbox 作用域控制。
    public enum CinematicTransitionStyle { Fade, Wipe, DoorsHorizontal, DoorsVertical, Iris, Blinds, Pixel }

    /// 擦幕和百叶闭合的行进方向。
    public enum CinematicWipeDirection { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

    /// 制作侧电影演出参数；运行时不修改 SO。
    [CreateAssetMenu(menuName = "SleepyDemos/Presentation/Cinematic Preset")]
    public sealed class CinematicPreset : ScriptableObject
    {
        [SerializeField] private CinematicTransitionStyle transitionStyle;
        [SerializeField] private CinematicWipeDirection direction;
        [SerializeField, Min(0)] private float closeDuration = 0.35f;
        [SerializeField, Min(0)] private float closedHold;
        [SerializeField, Min(0)] private float openDuration = 0.35f;
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private Color curtainColor = Color.black;
        [SerializeField, Range(2, 64)] private int blindCount = 12;
        [SerializeField, Range(8, 128)] private int pixelColumns = 48;
        [SerializeField, Range(1, 4)] private float targetAspect = 2.39f;
        [SerializeField, Min(0)] private float letterboxDuration = 0.3f;
        [SerializeField, Min(0)] private float titleFadeIn = 0.3f;
        [SerializeField, Min(0)] private float titleHold = 2;
        [SerializeField, Min(0)] private float titleFadeOut = 0.3f;

        /// 遮幕图案。
        public CinematicTransitionStyle TransitionStyle => transitionStyle;

        /// 图案行进方向。
        public CinematicWipeDirection Direction => direction;

        /// 完全闭幕所需非缩放秒数。
        public float CloseDuration => closeDuration;

        /// 中点任务完成后额外保持全遮幕的非缩放秒数。
        public float ClosedHold => closedHold;

        /// 完全开幕所需非缩放秒数。
        public float OpenDuration => openDuration;

        /// 遮幕颜色。
        public Color CurtainColor => curtainColor;

        /// 百叶分段数量。
        public int BlindCount => blindCount;

        /// 像素遮幕横向格数；纵向格数按容器比例计算。
        public int PixelColumns => pixelColumns;

        /// Letterbox 希望呈现的内容宽高比，宽屏下不会产生负黑边。
        public float TargetAspect => targetAspect;

        /// 黑边与指定 HUD 的进入时间。
        public float LetterboxDuration => letterboxDuration;

        /// 标题淡入时间。
        public float TitleFadeIn => titleFadeIn;

        /// 标题可读保持时间。
        public float TitleHold => titleHold;

        /// 标题淡出时间。
        public float TitleFadeOut => titleFadeOut;

        /// <summary>配置遮幕制作参数，校验成功后才修改 SO。</summary>
        /// <param name="style">七种正式图案之一。</param>
        /// <param name="wipeDirection">Wipe 与 Blinds 使用的行进方向。</param>
        /// <param name="closeSeconds">非负闭幕时长。</param>
        /// <param name="holdSeconds">中点完成后的保持时长。</param>
        /// <param name="openSeconds">非负开幕时长。</param>
        /// <param name="color">不透明遮幕颜色。</param>
        /// <param name="blinds">2–64 的百叶数量。</param>
        /// <param name="pixels">8–128 的横向像素格数。</param>
        public void ConfigureTransition(CinematicTransitionStyle style, CinematicWipeDirection wipeDirection,
            float closeSeconds, float holdSeconds, float openSeconds, Color color, int blinds = 12, int pixels = 48)
        {
            ValidateTransition(style, wipeDirection, closeSeconds, holdSeconds, openSeconds, color, blinds, pixels);
            transitionStyle = style;
            direction = wipeDirection;
            closeDuration = closeSeconds;
            closedHold = holdSeconds;
            openDuration = openSeconds;
            curtainColor = color;
            blindCount = blinds;
            pixelColumns = pixels;
        }

        /// <summary>配置 Letterbox 的内容比例与进入时长。</summary>
        /// <param name="aspect">1–4 的有限内容宽高比。</param>
        /// <param name="duration">零到三十秒的进入时长。</param>
        public void ConfigureLetterbox(float aspect, float duration = 0.3f)
        {
            if (!FiniteRange(aspect, 1, 4) || !FiniteRange(duration, 0, 30))
            {
                throw new ArgumentException("电影黑边参数无效。");
            }

            targetAspect = aspect;
            letterboxDuration = duration;
        }

        /// <summary>配置标题卡时长，全部采用非缩放时间。</summary>
        /// <param name="fadeIn">零到三十秒的淡入时间。</param>
        /// <param name="hold">零到六十秒的保持时间。</param>
        /// <param name="fadeOut">零到三十秒的淡出时间。</param>
        public void ConfigureTitle(float fadeIn, float hold, float fadeOut)
        {
            ValidateTitleTiming(fadeIn, hold, fadeOut);
            titleFadeIn = fadeIn;
            titleHold = hold;
            titleFadeOut = fadeOut;
        }

        /// <summary>采样制作侧缓动曲线，输出限制在视觉终态范围内。</summary>
        /// <param name="progress">零到一的线性时间进度。</param>
        /// <returns>零到一的表现进度。</returns>
        public float Evaluate(float progress) => Mathf.Clamp01(easing.Evaluate(Mathf.Clamp01(progress)));

        /// 校验 Inspector 保存的全部参数。
        public void Validate()
        {
            ValidateTransition(transitionStyle, direction, closeDuration, closedHold, openDuration, curtainColor, blindCount, pixelColumns);
            ValidateTitleTiming(titleFadeIn, titleHold, titleFadeOut);
            if (!FiniteRange(targetAspect, 1, 4) || !FiniteRange(letterboxDuration, 0, 30) || easing == null)
            {
                throw new ArgumentException("电影演出预设无效。");
            }

            foreach (var key in easing.keys)
            {
                if (!FiniteRange(key.time, -10000, 10000) || !FiniteRange(key.value, -10000, 10000))
                {
                    throw new ArgumentException("电影演出缓动曲线无效。");
                }
            }
        }

        internal static void ValidateTitleTiming(float fadeIn, float hold, float fadeOut)
        {
            if (!FiniteRange(fadeIn, 0, 30) || !FiniteRange(hold, 0, 60) || !FiniteRange(fadeOut, 0, 30))
            {
                throw new ArgumentException("标题卡时长无效。");
            }
        }

        private static void ValidateTransition(CinematicTransitionStyle style, CinematicWipeDirection wipeDirection,
            float close, float hold, float open, Color color, int blinds, int pixels)
        {
            if (!Enum.IsDefined(typeof(CinematicTransitionStyle), style)
                || !Enum.IsDefined(typeof(CinematicWipeDirection), wipeDirection)
                || !FiniteRange(close, 0, 30) || !FiniteRange(hold, 0, 60) || !FiniteRange(open, 0, 30)
                || !FiniteRange(color.r, 0, 1) || !FiniteRange(color.g, 0, 1) || !FiniteRange(color.b, 0, 1)
                || !Mathf.Approximately(color.a, 1) || blinds < 2 || blinds > 64 || pixels < 8 || pixels > 128)
            {
                throw new ArgumentException("遮幕预设无效。");
            }
        }

        private static bool FiniteRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
