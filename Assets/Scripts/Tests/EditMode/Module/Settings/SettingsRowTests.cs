using Core.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class SettingsRowTests
    {
        [Test]
        public void SliderRefreshDoesNotEditCandidatesAndReplacesCallback()
        {
            var root = new GameObject("Settings row");
            root.SetActive(false);
            try
            {
                var slider = root.AddComponent<Slider>();
                var row = root.AddComponent<UISettingRow>();
                var serialized = new SerializedObject(row);
                serialized.FindProperty("slider").objectReferenceValue = slider;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(UISettingRow).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(row, null);
                int oldEdits = 0;
                int newEdits = 0;
                row.SetSlider(new Vector2(0, 100), 90, _ => oldEdits++);
                row.SetSlider(new Vector2(0, 1), 0.5f, _ => newEdits++);
                Assert.That(oldEdits, Is.Zero);
                Assert.That(newEdits, Is.Zero);
                slider.value = 0.7f;
                Assert.That(oldEdits, Is.Zero);
                Assert.That(newEdits, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
