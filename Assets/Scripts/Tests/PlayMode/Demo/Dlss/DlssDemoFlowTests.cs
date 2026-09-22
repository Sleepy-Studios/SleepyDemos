#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Hotfix;
using Hotfix.Dlss;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class DlssDemoFlowTests
    {
        [UnityTest]
        public IEnumerator FormalStartupModesAndHubReturn()
        {
            if (!StreamlineRuntime.IsBackendSupported) Assert.Ignore("Requires Windows Editor DX12/Vulkan.");
            // Only the real startup shell is loaded directly. All Demo transitions use the Hub button/navigation.
            var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
            while (!startup.isDone) yield return null;
            yield return WaitUntil(() => GameSceneNavigator.Instance != null &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Formal Hub startup");
            Assert.IsTrue(StreamlineRuntime.IsInitialized);
            string folder = Path.GetFullPath("Library/Streamline/evidence/demo/" + SystemInfo.graphicsDeviceType);
            Directory.CreateDirectory(folder);
            yield return new WaitForEndOfFrame();
            var hubScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "Hub.png"), hubScreenshot.EncodeToPNG());
            Object.Destroy(hubScreenshot);
            for (int visit = 0; visit < 2; ++visit)
            {
                Click(UIManager.Instance.Get<MainMenuView>(), "DlssButton");
                yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Dlss &&
                    UIManager.Instance.Get<DlssSettingsView>()?.State == ViewState.Visible, "DLSS scene and panel");
                var controller = Object.FindFirstObjectByType<DlssDemoController>();
                Assert.IsNotNull(controller);
                yield return WaitUntil(() => !controller.IsBusy && controller.Session.HasEvaluatedFrame, "Initial Quality evaluation");
                Assert.That(controller.Session.Mode, Is.EqualTo(StreamlineDlssMode.Quality), controller.Session.Error);
                if (visit == 0)
                {
                    string[] buttons = { "OffButton", "BalancedButton", "PerformanceButton", "UltraPerformanceButton", "DlaaButton", "QualityButton" };
                    StreamlineDlssMode?[] modes = { null, StreamlineDlssMode.Balanced, StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa, StreamlineDlssMode.Quality };
                    for (int index = 0; index < buttons.Length; ++index)
                    {
                        var mode = modes[index];
                        Click(UIManager.Instance.Get<DlssSettingsView>(), buttons[index]);
                        yield return WaitUntil(() => !controller.IsBusy && controller.Session.Mode == mode &&
                            (!mode.HasValue || controller.Session.HasEvaluatedFrame), "Mode " + buttons[index]);
                        Assert.IsNull(controller.Session.Error);
                        yield return new WaitForEndOfFrame();
                        var readback = AsyncGPUReadback.Request(controller.Session.OutputTexture, 0, TextureFormat.RGBAFloat);
                        while (!readback.done) yield return null;
                        Assert.IsFalse(readback.hasError);
                        var pixels = readback.GetData<Color>();
                        float minimum = float.MaxValue, maximum = float.MinValue;
                        for (int pixel = 0; pixel < pixels.Length; pixel += 17)
                        {
                            Assert.IsFalse(float.IsNaN(pixels[pixel].r) || float.IsInfinity(pixels[pixel].r));
                            minimum = Mathf.Min(minimum, pixels[pixel].r);
                            maximum = Mathf.Max(maximum, pixels[pixel].r);
                        }
                        Assert.That(maximum - minimum, Is.GreaterThan(0.1f), "World output is blank: " + buttons[index]);
                        yield return new WaitForEndOfFrame();
                        var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(folder, buttons[index] + ".png"), screenshot.EncodeToPNG());
                        Object.Destroy(screenshot);
                    }
                }
                var previousSession = controller.Session;
                Click(UIManager.Instance.Get<DlssSettingsView>(), "BackButton");
                yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                    UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Return to Hub");
                Assert.IsTrue(controller == null, "Demo controller survived unload.");
                Assert.IsNull(previousSession.OutputTexture);
                Assert.IsNull(StreamlineCaptureFeature.Session);
                Assert.IsFalse(StreamlineRuntime.RequiresRestart);
            }
        }

        [UnityTearDown]
        public IEnumerator CloseDemoIfNeeded()
        {
            var controller = Object.FindFirstObjectByType<DlssDemoController>();
            if (controller != null)
            {
                controller.RequestExit();
                yield return WaitUntil(() => controller == null, "Demo cleanup after test");
            }
        }

        private static void Click(View view, string name)
        {
            Assert.IsNotNull(view);
            foreach (var button in view.gameObject.GetComponentsInChildren<Button>(true))
            {
                if (button.name != name) continue;
                Assert.IsTrue(button.interactable, name + " is disabled.");
                button.onClick.Invoke();
                return;
            }
            Assert.Fail("Button not found: " + name);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string step)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out: " + step);
        }
    }
}
#endif
