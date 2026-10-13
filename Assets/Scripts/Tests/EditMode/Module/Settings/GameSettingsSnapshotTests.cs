using System;
using Core.Runtime.Inputs;
using Core.Runtime.Settings;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GameSettingsSnapshotTests
    {
        [Test]
        public void ManualMsaaPreservesExistingModeValuesAndRejectsUnsupportedSamples()
        {
            Assert.That((int)GraphicsAaMode.CameraDefault, Is.Zero);
            Assert.That((int)GraphicsAaMode.Off, Is.EqualTo(1));
            Assert.That((int)GraphicsAaMode.Fxaa, Is.EqualTo(2));
            Assert.That((int)GraphicsAaMode.Smaa, Is.EqualTo(3));
            Assert.That((int)GraphicsAaMode.Taa, Is.EqualTo(4));
            Assert.That((int)GraphicsAaMode.Msaa, Is.EqualTo(5));
            var saved = new GraphicsPreferences { Antialiasing = GraphicsAaMode.Msaa };
            Assert.That(saved.MsaaSamples, Is.EqualTo(4));
            foreach (int samples in new[] { 2, 4, 8 })
            {
                saved.MsaaSamples = samples;
                Assert.DoesNotThrow(saved.Validate);
                Assert.That(saved.Copy().MsaaSamples, Is.EqualTo(samples));
            }

            foreach (int samples in new[] { 0, 1, 3, 16 })
            {
                saved.MsaaSamples = samples;
                Assert.Throws<ArgumentException>(saved.Validate);
            }
        }

        [Test]
        public void ProductionOptionsRestrictCandidatesAndCopiesDoNotShareFrameChoices()
        {
            var options = new SettingsOptions { Brightness = new UnityEngine.Vector2(0.8f, 1.2f) };
            options.Validate();
            Assert.Throws<System.ArgumentException>(() => options.ValidateCandidate(new GameSettingsSnapshot
            {
                Graphics = new GraphicsPreferences { Brightness = 1.5f }
            }));
            var copy = options.Copy();
            copy.FrameLimits[0] = 31;
            Assert.That(options.FrameLimits[0], Is.Zero);
            Assert.Throws<System.ArgumentException>(() => options.ValidateCandidate(new GameSettingsSnapshot
            {
                Graphics = new GraphicsPreferences { FrameLimit = 31 }
            }));
        }

        [Test]
        public void CopyDoesNotShareEditableSectionsOrOverrides()
        {
            var saved = new GameSettingsSnapshot();
            saved.BindingOverrides.Add("demo", "original");
            var candidate = saved.Copy();
            candidate.Input.GamepadDeadzone = 0.3f;
            candidate.Input.GamepadStyle = GamepadStyle.PlayStation;
            candidate.Graphics.Brightness = 1.5f;
            candidate.Audio.Music = 0.2f;
            candidate.Accessibility.CameraShake = false;
            candidate.BindingOverrides["demo"] = "changed";

            Assert.AreEqual(0.2f, saved.Input.GamepadDeadzone);
            Assert.That(saved.Input.GamepadStyle, Is.EqualTo(GamepadStyle.Xbox));
            Assert.That(candidate.Copy().Input.GamepadStyle, Is.EqualTo(GamepadStyle.PlayStation));
            Assert.AreEqual(1, saved.Graphics.Brightness);
            Assert.AreEqual(1, saved.Audio.Music);
            Assert.IsTrue(saved.Accessibility.CameraShake);
            Assert.AreEqual("original", saved.BindingOverrides["demo"]);
        }

        [Test]
        public void InvalidCandidateIsRejectedBeforeSave()
        {
            var candidate = new GameSettingsSnapshot();
            candidate.Validate();
            candidate.Audio.Music = float.NaN;
            Assert.Throws<ArgumentException>(candidate.Validate);
            candidate.Audio.Music = 1;
            candidate.Graphics.Width = 1280;
            Assert.Throws<ArgumentException>(candidate.Validate);
            candidate.Graphics.Height = 720;
            candidate.Validate();
            candidate.Version = 2;
            Assert.Throws<ArgumentException>(candidate.Validate);
        }

        [Test]
        public void ControllerIconPreferenceKeepsStableValuesAndRejectsUndefinedCandidates()
        {
            Assert.That((int)GamepadStyle.Xbox, Is.EqualTo(1));
            Assert.That((int)GamepadStyle.PlayStation, Is.EqualTo(2));
            Assert.That((int)GamepadStyle.Switch, Is.EqualTo(3));
            var candidate = new GameSettingsSnapshot();
            foreach (var style in new[] { GamepadStyle.Xbox, GamepadStyle.PlayStation, GamepadStyle.Switch })
            {
                candidate.Input.GamepadStyle = style;
                Assert.DoesNotThrow(candidate.Validate);
            }
            foreach (int style in new[] { -1, 0, 4 })
            {
                candidate.Input.GamepadStyle = (GamepadStyle)style;
                Assert.Throws<ArgumentException>(candidate.Validate);
            }
        }
    }
}
