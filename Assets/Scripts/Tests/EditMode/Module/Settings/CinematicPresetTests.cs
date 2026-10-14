using System;
using Core.Runtime.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class CinematicGeometryTests
    {
        [Test]
        public void BarHeightUsesContainerAndNeverBecomesNegative()
        {
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(1920, 1080), 2.4f), Is.EqualTo(140).Within(0.0001f));
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(3840, 1440), 2.4f), Is.Zero);
            Assert.That(CinematicPresenter.CalculateBarHeight(new Vector2(1080, 1920), 2.4f), Is.EqualTo(735).Within(0.0001f));
        }

        [Test]
        public void BarHeightRejectsInvalidAspect()
        {
            foreach (float aspect in new[] { float.NaN, float.PositiveInfinity, 0, -1 })
            {
                Assert.Throws<ArgumentException>(() => CinematicPresenter.CalculateBarHeight(new Vector2(100, 100), aspect));
            }
        }
    }
}
