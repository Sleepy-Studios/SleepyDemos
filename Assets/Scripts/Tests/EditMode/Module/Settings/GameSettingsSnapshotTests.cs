using System;
using Core.Runtime.Settings;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GameSettingsSnapshotTests
    {
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
            candidate.Graphics.Brightness = 1.5f;
            candidate.Audio.Music = 0.2f;
            candidate.Accessibility.CameraShake = false;
            candidate.BindingOverrides["demo"] = "changed";

            Assert.AreEqual(0.2f, saved.Input.GamepadDeadzone);
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
    }
}
