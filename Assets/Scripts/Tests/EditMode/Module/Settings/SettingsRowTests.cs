using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Hotfix;
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
        public void CancelledHubSettingsOpenReturnsCancelledBeforeNavigation()
        {
            using var cancellation = new System.Threading.CancellationTokenSource();
            cancellation.Cancel();
            var result = SettingsFlow.OpenAsync(token: cancellation.Token).GetAwaiter().GetResult();
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(result.View, Is.Null);
            Assert.That(result.Exception, Is.Null);
        }

        [Test]
        public void GraphicsPageKeepsNativeFallbackEditableAndPreviewsNewDlssCandidates()
        {
            int quality = (int)StreamlineDlssMode.Quality;
            int dlaa = (int)StreamlineDlssMode.Dlaa;
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(quality, quality, true, false, false, false), Is.False,
                "同一请求已经失败回原生时不能只因结构可尝试就锁住手动 AA。");
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(quality, quality, true, false, false, true), Is.True,
                "用户显式重试同一档位仍属于新候选，等待应用时预告接管。");
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(dlaa, quality, true, false, false, false), Is.True);
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(quality, quality, true, true, false, false), Is.True,
                "等待首帧不能让手动 AA 覆盖会话输入。");
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(quality, quality, true, false, true, false), Is.True);
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(0, quality, true, false, true, true), Is.False,
                "关闭候选允许同一次提交选择后续原生 AA。");
            Assert.That(GraphicsSettingsPanelView.ManagesNativeAntialiasing(quality, 0, false, false, false, true), Is.False,
                "当前结构不可尝试时保留请求，但不能锁住原生编辑。");
        }

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
