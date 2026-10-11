using System;
using Core.Runtime.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class CinematicPresetTests
    {
        [Test]
        public void BarHeightUsesContainerAndNeverBecomesNegative()
        {
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(1920, 1080), 2.4f), Is.EqualTo(140).Within(0.0001f));
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(3840, 1440), 2.4f), Is.Zero);
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(1080, 1920), 2.4f), Is.EqualTo(735).Within(0.0001f));
            Assert.Throws<ArgumentException>(() => CinematicPresenter.CalculateBarHeight(new Vector2(100, 100), float.NaN));
        }

        [Test]
        public void AllCurtainStylesAcceptAuthoredTimingAndRejectPartialCoverageColor()
        {
            var preset = ScriptableObject.CreateInstance<CinematicPreset>();
            try
            {
                foreach (CinematicTransitionStyle style in Enum.GetValues(typeof(CinematicTransitionStyle)))
                {
                    preset.ConfigureTransition(style, CinematicWipeDirection.LeftToRight, 0.3f, 0, 0.3f, Color.black);
                    preset.Validate();
                }

                Assert.Throws<ArgumentException>(() => preset.ConfigureTransition(CinematicTransitionStyle.Fade,
                    CinematicWipeDirection.LeftToRight, 0.3f, 0, 0.3f, new Color(0, 0, 0, 0.5f)));
                Assert.Throws<ArgumentException>(() => preset.ConfigureLetterbox(float.PositiveInfinity));
                Assert.Throws<ArgumentException>(() => preset.ConfigureTitle(0.3f, float.NaN, 0.3f));
                Assert.That(preset.TransitionStyle, Is.EqualTo(CinematicTransitionStyle.Pixel));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }
    }
}
