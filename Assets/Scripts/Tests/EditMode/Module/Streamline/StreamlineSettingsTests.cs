using System;
using System.Collections;
using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Core.Runtime;
using Core.Runtime.Settings;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class StreamlineSettingsTests
    {
        [UnityTest]
        public IEnumerator RuntimeDriverIsReleasedWhenPlayModeEnds()
        {
            yield return new EnterPlayMode();
            StreamlineRuntime.Initialize();
            // 不手动销毁驱动，验证 Editor 退出 Play 的真实清理。
            yield return new ExitPlayMode();
            AssertNoRuntimeObjects();
        }

        private static void AssertNoRuntimeObjects()
        {
            Assert.IsFalse(Application.isPlaying);
            var leftovers = Array.FindAll(Resources.FindObjectsOfTypeAll<GameObject>(), go =>
                !EditorUtility.IsPersistent(go) &&
                (go.name == "Streamline Presentation" || go.name == "Streamline Runtime"));
            Assert.That(leftovers, Is.Empty, string.Join("\n", Array.ConvertAll(leftovers, go =>
                $"{go.name}: id={go.GetInstanceID()}, flags={go.hideFlags}, scene={go.scene.name}")));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(999)]
        public void UnknownSavedValueMeansOff(int value)
        {
            Assert.IsNull(StreamlineRuntime.DecodePreference(value));
        }

        [Test]
        public void PreferencesRoundTripWithoutCreatingGraphicsResources()
        {
            const string key = GameSettingsService.PreferenceKey;
            bool existed = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.IsNull(ReadPreference());
                foreach (var mode in new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Balanced,
                    StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa })
                {
                    var value = new GameSettingsSnapshot();
                    value.Graphics.DlssMode = (int)mode;
                    LocalDataManager.SaveData(key, value, candidate => candidate.Validate());
                    PlayerPrefs.Save();
                    Assert.AreEqual(mode, ReadPreference());
                }
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static StreamlineDlssMode? ReadPreference()
        {
            var value = LocalDataManager.LoadData(GameSettingsService.PreferenceKey, new GameSettingsSnapshot(),
                candidate => candidate.Validate());
            return StreamlineRuntime.DecodePreference(value.Graphics.DlssMode);
        }
    }
}
