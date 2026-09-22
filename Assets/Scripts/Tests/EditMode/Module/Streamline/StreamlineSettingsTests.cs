using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEngine;

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
            int previous = PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.IsNull(StreamlineRuntime.ReadSavedMode());
                foreach (var mode in new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Balanced,
                    StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa })
                {
                    PlayerPrefs.SetInt(key, (int)mode);
                    PlayerPrefs.Save();
                    Assert.AreEqual(mode, StreamlineRuntime.ReadSavedMode());
                }
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
