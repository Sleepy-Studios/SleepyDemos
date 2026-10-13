using System;
using Core.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class CommonUtilityTests
    {
        private sealed class Clock : ITimeSource { public long UtcNowMilliseconds { get; set; } }
        private readonly Clock clock = new Clock();
        private static readonly TimeZoneInfo TestZone = TimeZoneInfo.CreateCustomTimeZone("TipsTestUTC8", TimeSpan.FromHours(8), "UTC8", "UTC8");
        [SetUp] public void SetUp() => TimeUtil.SetTimeSource(clock);
        [TearDown] public void TearDown() => TimeUtil.SetTimeSource(null);

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(1791000000123L)]
        public void MillisecondTimestampRoundTripsWithoutGuessing(long milliseconds)
        {
            Assert.That(TimeUtil.ToUnixMilliseconds(TimeUtil.FromUnixMilliseconds(milliseconds)), Is.EqualTo(milliseconds));
            Assert.That(TimeUtil.FromUnixSeconds(1).ToUnixTimeMilliseconds(), Is.EqualTo(1000));
        }

        [Test] public void IsoSaveKeepsUtcRoundtripShapeAndAcceptsOffsets()
        {
            clock.UtcNowMilliseconds = 1791000000123;
            string iso = TimeUtil.ToIso8601();
            Assert.That(iso, Does.EndWith("Z"));
            Assert.That(TimeUtil.TryParseIso8601(iso, out var date), Is.True);
            Assert.That(date.ToUnixTimeMilliseconds(), Is.EqualTo(clock.UtcNowMilliseconds));
            Assert.That(TimeUtil.TryParseIso8601("2026-10-03T08:00:00+08:00", out date), Is.True);
            Assert.That(date.Hour, Is.Zero);
            Assert.That(TimeUtil.TryParseIso8601("broken", out _), Is.False);
        }

        [TestCase(59L, TimeDisplayFormat.Auto, "59秒")]
        [TestCase(60L, TimeDisplayFormat.Auto, "01:00")]
        [TestCase(3600L, TimeDisplayFormat.Auto, "01:00:00")]
        [TestCase(90061L, TimeDisplayFormat.HoursMinutesSeconds, "25:01:01")]
        [TestCase(3601L, TimeDisplayFormat.MinutesSeconds, "60:01")]
        [TestCase(90061L, TimeDisplayFormat.AutoWithUnits, "1天1小时")]
        [TestCase(-1L, TimeDisplayFormat.SecondsOnly, "0秒")]
        public void DurationBoundariesPreserveTotalHours(long seconds, TimeDisplayFormat format, string expected)
            => Assert.That(TimeUtil.FormatSeconds(seconds, format), Is.EqualTo(expected));

        [Test] public void CountdownRoundsUpAndTimeDifferenceSaturates()
        {
            clock.UtcNowMilliseconds = 1000;
            Assert.That(TimeUtil.GetRemainingSeconds(1001), Is.EqualTo(1));
            Assert.That(TimeUtil.GetRemainingSeconds(2001), Is.EqualTo(2));
            Assert.That(TimeUtil.GetRemainingSeconds(900), Is.Zero);
            Assert.That(TimeUtil.DifferenceMilliseconds(long.MinValue, long.MaxValue), Is.EqualTo(long.MaxValue));
            Assert.That(TimeUtil.CalculateTimeDifference(long.MaxValue, long.MinValue).Ticks, Is.LessThan(0));
        }

        [Test] public void DailyBoundaryUsesChosenTimezoneAndExplicitResetHour()
        {
            clock.UtcNowMilliseconds = new DateTimeOffset(2026, 10, 3, 3, 59, 59, TimeSpan.FromHours(8)).ToUnixTimeMilliseconds();
            Assert.That(TimeUtil.GetGameDate(clock.UtcNowMilliseconds, 4, TestZone), Is.EqualTo(new DateTime(2026, 10, 2)));
            long refresh = TimeUtil.GetNextDailyRefreshTime(4, TestZone);
            Assert.That(TimeUtil.ToDateTime(refresh, TestZone).Hour, Is.EqualTo(4));
            clock.UtcNowMilliseconds = refresh;
            Assert.That(TimeUtil.GetGameDate(refresh, 4, TestZone), Is.EqualTo(new DateTime(2026, 10, 3)));
            Assert.That(TimeUtil.ToDateTime(TimeUtil.GetNextDailyRefreshTime(4, TestZone), TestZone).Day, Is.EqualTo(4));
            Assert.That(TimeUtil.ToDateTime(TimeUtil.GetTodayAtHour(0, 0, TestZone), TestZone).Day, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => TimeUtil.GetNextDailyRefreshTime(24));
        }

        [TestCase("FF0000", "#FF0000FF")]
        [TestCase("#ff000080", "#FF000080")]
        [TestCase("#F008", "#FF000088")]
        [TestCase("  f00  ", "#FF0000FF")]
        public void HexColorSupportsPrefixesAndAlpha(string source, string expected)
        {
            Assert.That(ColorUtil.TryParse(source, out var color), Is.True);
            Assert.That(ColorUtil.ToHex(color), Is.EqualTo(expected));
        }

        [Test] public void InvalidColorUsesExplicitFallbackAndAlphaKeepsRgbPrecision()
        {
            Assert.That(ColorUtil.TryParse("orange", out _), Is.False);
            Assert.That(ColorUtil.GetColor("invalid", Color.clear), Is.EqualTo(Color.clear));
            Assert.That(ColorUtil.GetColor(null), Is.EqualTo(Color.white));
            Color color = new Color(.12345f, .45678f, .98765f, .2f);
            Color result = ColorUtil.WithAlpha(color, 2);
            Assert.That(result.r, Is.EqualTo(color.r));
            Assert.That(result.g, Is.EqualTo(color.g));
            Assert.That(result.a, Is.EqualTo(1));
            Assert.That(ColorUtil.WrapText("内容", Color.red), Is.EqualTo("<color=#FF0000FF>内容</color>"));
        }

        [TestCase("F00")]
        [TestCase("#F008")]
        [TestCase("ff0000")]
        [TestCase("#FF000080")]
        public void PercentAlphaOverridesParsedAlphaForEveryHexLength(string hex)
        {
            var color = ColorUtil.GetColor(hex, alphaPercent: 37.125f);
            Assert.That(ColorUtil.ToHex(color, includeAlpha: false), Is.EqualTo("#FF0000"));
            Assert.That(color.a, Is.EqualTo(.37125f).Within(.0000001f), "显式百分比替换 Alpha，不能与原 RGBA 透明度相乘。");
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(100f, 1f)]
        [TestCase(120f, 1f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void PercentAlphaKeepsWithAlphaLimits(float percent, float expected)
        {
            var color = ColorUtil.GetColor("#123456", alphaPercent: percent);
            Assert.That(color.a, Is.EqualTo(expected));
            Assert.That(ColorUtil.ToHex(color, includeAlpha: false), Is.EqualTo("#123456"));
        }

        [Test]
        public void PercentAlphaPreservesFallbackRgbAndByteAlphaPrecision()
        {
            var fallback = new Color(.12345f, .45678f, .98765f, .2f);
            var color = ColorUtil.GetColor("invalid", alphaPercent: 62.5f, fallback: fallback);
            Assert.That(color.r, Is.EqualTo(fallback.r));
            Assert.That(color.g, Is.EqualTo(fallback.g));
            Assert.That(color.b, Is.EqualTo(fallback.b));
            Assert.That(color.a, Is.EqualTo(.625f));
            Assert.That(ColorUtil.GetColor(null, alphaPercent: 62.5f), Is.EqualTo(new Color(1, 1, 1, .625f)));
            Assert.That(ColorUtil.GetColor("#123456", alphaPercent: 98.0392156863f).a, Is.EqualTo(250f / 255f));
        }

        [TestCase(-380, -250)]
        [TestCase(-380, 250)]
        [TestCase(380, -250)]
        [TestCase(380, 250)]
        [TestCase(0, 250)]
        [TestCase(0, -250)]
        public void TooltipClampsAllEdgesAndArrowStaysOnBody(float x, float y)
        {
            Rect boundary = new Rect(-400, -300, 800, 600);
            var result = TooltipPlacementUtil.Calculate(boundary, new Rect(x - 10, y - 10, 20, 20), new Vector2(240, 120));
            Assert.That(result.Body.xMin, Is.GreaterThanOrEqualTo(boundary.xMin));
            Assert.That(result.Body.xMax, Is.LessThanOrEqualTo(boundary.xMax));
            Assert.That(result.Body.yMin, Is.GreaterThanOrEqualTo(boundary.yMin));
            Assert.That(result.Body.yMax, Is.LessThanOrEqualTo(boundary.yMax));
            Assert.That(result.ArrowPosition.x, Is.InRange(result.Body.xMin, result.Body.xMax));
            Assert.That(result.ArrowPosition.y, Is.InRange(result.Body.yMin, result.Body.yMax));
        }

        [Test] public void TooltipTriesOppositeAndKeepsRequestedGap()
        {
            Rect boundary = new Rect(-400, -300, 800, 600);
            var result = TooltipPlacementUtil.Calculate(boundary, new Rect(-10, 270, 20, 20), new Vector2(240, 120));
            Assert.That(result.Direction, Is.EqualTo(TooltipDirection.Down));
            Assert.That(result.Body.yMax, Is.EqualTo(258));
        }
    }
}
