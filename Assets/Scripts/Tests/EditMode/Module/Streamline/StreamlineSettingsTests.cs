using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEngine;
using Core.Runtime;

namespace Tests.Module
{
    public sealed class StreamlineSettingsTests
    {
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
            const string key = "SleepyDemos.Graphics.DlssMode";
            bool existed = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.IsNull(StreamlineRuntime.ReadSavedMode());
                foreach (var mode in new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Balanced,
                    StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa })
                {
                    LocalDataManager.SaveData(key, (int)mode);
                    PlayerPrefs.Save();
                    Assert.AreEqual(mode, StreamlineRuntime.ReadSavedMode());
                }
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
