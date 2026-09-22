#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class DlssDemoFlowTests
    {
        [UnityTest]
        public IEnumerator EditorDirectStartupUsesSharedSettings()
        {
            if (!StreamlineRuntime.IsBackendSupported) Assert.Ignore("Windows Editor DX12/Vulkan required.");
            var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return WaitUntil(() => GameSceneNavigator.Instance?.IsEditorDirect == true &&
                UIManager.Instance.Get<DlssSettingsView>()?.State == ViewState.Visible, "Editor direct public settings");
            StreamlineRuntime.SetMode(StreamlineDlssMode.Quality);
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Editor direct Quality");
            Assert.AreSame(UIRootManager.Instance.BaseCamera, StreamlineRuntime.BoundCamera);
        }

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
            StreamlineRuntime.SetMode(null);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Off");
            Assert.IsNull(StreamlineRuntime.RequestedMode);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.DroneFlight).AsTask();
            yield return WaitUntil(() => travel.IsCompleted, "DroneFlight");
            Assert.That(travel.Result.Status, Is.Not.EqualTo(GameSceneSwitchStatus.Failed));
            yield return WaitUntil(() => UIManager.Instance.Get<DroneFlightVehicleSelectView>()?.State == ViewState.Visible, "Vehicle selection");
            Click(UIManager.Instance.Get<DroneFlightVehicleSelectView>(), "PlainButton");
            yield return WaitUntil(() => UIManager.Instance.Get<DroneFlightHudView>()?.State == ViewState.Visible, "Flight HUD");
            StreamlineRuntime.SetMode(StreamlineDlssMode.Quality);
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "DroneFlight Quality: " + StreamlineRuntime.Status);
            Assert.AreEqual(UIRootManager.Instance.BaseCamera, StreamlineRuntime.BoundCamera);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Hub preserved Quality");
            yield return new WaitForEndOfFrame();
            int uiHosts = 0;
            foreach (var candidate in Camera.allCameras)
            {
                var candidateData = candidate.GetUniversalAdditionalCameraData();
                if (candidate.enabled && candidateData.renderType == CameraRenderType.Base && candidateData.cameraStack.Contains(UIRootManager.Instance.UICamera)) uiHosts++;
            }
            Assert.AreEqual(1, uiHosts, "UI lost its presentation camera after unloading the previous Demo.");
            StreamlineRuntime.SetMode(null);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Disable in returned Hub");
            Assert.IsTrue(UIRootManager.Instance.BaseCamera.enabled, "Hub camera stayed disabled after DLSS shutdown.");
            StreamlineRuntime.SetMode(StreamlineDlssMode.Quality);
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Re-enable in Hub");
            travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Dlss).AsTask();
            yield return WaitUntil(() => travel.IsCompleted && StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Lab inherited Quality");
            StreamlineDlssMode?[] modes = { null, StreamlineDlssMode.Balanced, StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa, StreamlineDlssMode.Quality };
            foreach (var mode in modes)
            {
                StreamlineRuntime.SetMode(mode);
                yield return WaitUntil(() => !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode == mode, "Mode " + mode);
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, (mode?.ToString() ?? "Off") + ".png"), screenshot.EncodeToPNG());
                var pixels = screenshot.GetPixels();
                float minimum = float.MaxValue, maximum = float.MinValue;
                for (int y = screenshot.height / 3; y < screenshot.height * 2 / 3; y += 9)
                for (int x = screenshot.width / 4; x < screenshot.width * 3 / 4; x += 9)
                {
                    float r = pixels[y * screenshot.width + x].r;
                    minimum = Mathf.Min(minimum, r); maximum = Mathf.Max(maximum, r);
                }
                Assert.Greater(maximum - minimum, 0.1f, "Blank world: " + StreamlineRuntime.Status);
                Object.Destroy(screenshot);
            }
            var view = UIManager.Instance.Get<DlssSettingsView>();
            Assert.IsNotNull(view);
            Click(view, "OpenButton");
            yield return new WaitForEndOfFrame();
            var panelScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "Panel.png"), panelScreenshot.EncodeToPNG());
            Object.Destroy(panelScreenshot);
            Click(view, "CloseButton");
            Assert.That(StreamlineRuntime.RequestedMode, Is.EqualTo(StreamlineDlssMode.Quality));
            var main = StreamlineRuntime.BoundCamera;
            Vector3 screen = main.WorldToScreenPoint(main.transform.position + main.transform.forward * 10);
            Assert.That(Vector2.Distance(screen, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.LessThan(0.5f));
            Assert.That(Vector3.Dot(main.ScreenPointToRay(screen).direction, main.transform.forward), Is.GreaterThan(0.999f));
            yield return VerifyPostProcessing(main);
            using (var resize = new GameViewResolution(1280, 720))
            {
                yield return WaitUntil(() => StreamlineRuntime.OutputSize == new Vector2Int(1280, 720) && !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue, "Resize to 720p");
            }
            yield return null;
            yield return null;
            yield return WaitUntil(() => StreamlineRuntime.OutputSize == new Vector2Int(Screen.width, Screen.height) && !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue, "Restore resolution");
            double resumeAt = UnityEditor.EditorApplication.timeSinceStartup + 0.3;
            UnityEditor.EditorApplication.CallbackFunction resume = null;
            resume = () =>
            {
                if (UnityEditor.EditorApplication.timeSinceStartup < resumeAt) return;
                UnityEditor.EditorApplication.update -= resume;
                UnityEditor.EditorApplication.isPaused = false;
            };
            UnityEditor.EditorApplication.update += resume;
            UnityEditor.EditorApplication.isPaused = true;
            yield return null;
            int rendered = StreamlineCaptureFeature.Session.CapturedFrames;
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue && StreamlineCaptureFeature.Session.CapturedFrames > rendered + 3, "Resume");
            StreamlineRuntime.SetMode(null);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Before unsupported camera");
            main.orthographic = true;
            StreamlineRuntime.SetMode(StreamlineDlssMode.Quality);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Unsupported camera fallback");
            Assert.IsNull(StreamlineRuntime.EffectiveMode);
            Assert.AreEqual(StreamlineDlssMode.Quality, StreamlineRuntime.RequestedMode);
            Assert.IsFalse(StreamlineRuntime.CanEnable(out _));
            main.orthographic = false;
            StreamlineRuntime.SetMode(null);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Disable");
            Assert.IsNull(StreamlineCaptureFeature.Session);
            Assert.IsFalse(StreamlineRuntime.RequiresRestart);
            travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).AsTask();
            yield return WaitUntil(() => travel.IsCompleted, "Final Hub");
        }

        [UnityTearDown]
        public IEnumerator CloseDemoIfNeeded()
        {
            StreamlineRuntime.SetMode(null);
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Cleanup");
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

        private static IEnumerator VerifyPostProcessing(Camera camera)
        {
            yield return new WaitForEndOfFrame();
            float before = CaptureWorldMean();
            var data = camera.GetUniversalAdditionalCameraData();
            LayerMask previousMask = data.volumeLayerMask;
            var item = new GameObject("Temporary exposure validation") { layer = 31 };
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = profile.Add<ColorAdjustments>();
            exposure.postExposure.Override(-3);
            var volume = item.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10000; volume.sharedProfile = profile;
            try
            {
                data.volumeLayerMask = 1 << 31;
                for (int frame = 0; frame < 8; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                Assert.Less(CaptureWorldMean(), before * 0.65f, "Post exposure did not reach the presented world image.");
            }
            finally
            {
                data.volumeLayerMask = previousMask;
                Object.Destroy(item); Object.Destroy(exposure); Object.Destroy(profile);
            }
        }

        private static float CaptureWorldMean()
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var pixels = screenshot.GetPixels();
            float total = 0; int count = 0;
            for (int y = screenshot.height / 4; y < screenshot.height / 2; y += 8)
            for (int x = screenshot.width / 4; x < screenshot.width / 2; x += 8)
            {
                total += pixels[y * screenshot.width + x].grayscale; count++;
            }
            Object.Destroy(screenshot);
            return total / count;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string step)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out: " + step + " / " + StreamlineRuntime.Status);
        }
    }
}
#endif
