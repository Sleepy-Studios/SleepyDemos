using System;
using Hotfix.JinxCasino.Adapters.Input;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 纯换算和暂停门闩回归，不要求测试程序集额外引用InputSystem。
    public sealed class JinxCasinoInputStateTests
    {
        [Test]
        public void MouseAndTouchAreDeltasWhileGamepadIsRate()
        {
            var settings = new JinxCasinoInputSettings();
            Vector2 mouse = new Vector2(50, -20), touch = new Vector2(10, 30);
            var shortFrame = JinxCasinoInputMath.LookDegrees(mouse, touch, Vector2.zero, 1f / 120, 0.12f, settings);
            var longFrame = JinxCasinoInputMath.LookDegrees(mouse, touch, Vector2.zero, 1f / 30, 0.12f, settings);
            AssertVector(shortFrame, new Vector2(7.2f, 1.2f)); AssertVector(longFrame, shortFrame);
            Vector2 axis = new Vector2(0.5f, -1);
            Vector2 oneStep = JinxCasinoInputMath.LookDegrees(Vector2.zero, Vector2.zero, axis, 1, 0.12f, settings);
            Vector2 sixtySteps = Vector2.zero;
            for (int i = 0; i < 60; i++) sixtySteps += JinxCasinoInputMath.LookDegrees(Vector2.zero, Vector2.zero, axis, 1f / 60, 0.12f, settings);
            AssertVector(oneStep, new Vector2(45, -90)); AssertVector(sixtySteps, oneStep);
        }

        [Test]
        public void DeadzoneUsesRadiusOnceAndInvertOnlyChangesGamepadY()
        {
            AssertVector(JinxCasinoInputMath.ApplyDeadzone(new Vector2(0.12f, 0.12f), 0.2f, 1), Vector2.zero);
            AssertVector(JinxCasinoInputMath.ApplyDeadzone(new Vector2(0.6f, 0), 0.2f, 1), new Vector2(0.5f, 0));
            AssertVector(JinxCasinoInputMath.ApplyDeadzone(new Vector2(1, 1), 0.2f, 1), new Vector2(1, 1).normalized);
            var settings = new JinxCasinoInputSettings { GamepadInvertY = true };
            AssertVector(JinxCasinoInputMath.LookDegrees(Vector2.up * 10, Vector2.zero, Vector2.up, 1, 0.12f, settings), new Vector2(0, -88.8f));
        }

        [Test]
        public void InvalidPreferencesCannotSilentlyCreateExtremeLookOrRumble()
        {
            var settings = new JinxCasinoInputSettings(); Assert.That(settings.IsValid, Is.True);
            settings.GamepadDeadzone = float.NaN; Assert.That(settings.IsValid, Is.False);
            settings.GamepadDeadzone = 0.2f; settings.GamepadLookMultiplier = float.PositiveInfinity; Assert.That(settings.IsValid, Is.False);
            settings.GamepadLookMultiplier = 1; settings.RumbleStrength = 2; Assert.That(settings.IsValid, Is.False);
            settings.RumbleStrength = 1; settings.SchemaVersion = 999; Assert.That(settings.IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => JinxCasinoInputMath.LookDegrees(Vector2.zero, Vector2.zero, Vector2.zero, -1, 0.12f, new JinxCasinoInputSettings()));
        }

        [Test]
        public void FocusAndBackgroundMustBothClearThenExplicitResume()
        {
            var pause = new JinxCasinoPauseState();
            pause.SetApplicationFocus(false); pause.SetApplicationPaused(true);
            Assert.That(pause.IsPaused, Is.True); Assert.That(pause.TryResume(), Is.False);
            pause.SetApplicationFocus(true);
            Assert.That(pause.BlockingReasons, Is.EqualTo(JinxCasinoPauseReason.Background)); Assert.That(pause.TryResume(), Is.False);
            pause.SetApplicationPaused(false);
            Assert.That(pause.CanResume, Is.True); Assert.That(pause.IsPaused, Is.True, "回前台不能自动继续。");
            Assert.That(pause.TryResume(), Is.True); Assert.That(pause.IsPaused, Is.False); Assert.That(pause.Reasons, Is.EqualTo(JinxCasinoPauseReason.None));
        }

        [Test]
        public void ReconnectOrFallbackDoesNotDismissUserPauseAndRepeatedSignalsDoNotSpam()
        {
            var pause = new JinxCasinoPauseState(); int changes = 0; pause.Changed += () => changes++;
            pause.RequestPause(JinxCasinoPauseReason.User); pause.RequestPause(JinxCasinoPauseReason.User);
            Assert.That(changes, Is.EqualTo(1));
            pause.SetGamepadAvailable(false); pause.SetGamepadAvailable(false);
            Assert.That(changes, Is.EqualTo(2)); Assert.That(pause.TryResume(), Is.False);
            pause.SetGamepadAvailable(true);
            Assert.That(pause.IsPaused, Is.True); Assert.That(pause.Reasons.HasFlag(JinxCasinoPauseReason.User), Is.True);
            Assert.That(pause.TryResume(), Is.True); Assert.That(pause.TryResume(), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => pause.RequestPause((JinxCasinoPauseReason)64));
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f)); Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }
    }
}
