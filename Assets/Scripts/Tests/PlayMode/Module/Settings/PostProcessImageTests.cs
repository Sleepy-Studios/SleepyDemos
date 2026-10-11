#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Core.Runtime;
using Core.Runtime.Rendering;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.Dlss;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 正式启动、真实世界及 UI 相机栈的 GPU 回归；不依赖 DLSS 硬件可用性。
    public sealed class PostProcessImageTests
    {
        private GameSettingsSnapshot savedSettings;
        private GameSceneId savedScene;
        private StreamlineDlssMode? savedMode;
        private bool capturedState;
        private DlssDemoController owner;
        private bool ownerEnabled;
        private Camera worldCamera;
        private AntialiasingMode savedAa;
        private IResourceLoader loader;
        private IDisposable effect;
        private GameObject marker;
        private RectInt uiRegion;
        private string evidenceFolder;

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedPresetChangesWorldRestoresAfterReleaseAndLeavesOverlayUiUnchanged()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("后处理图像回归需要真实 GPU，当前是 Null 图形设备。");
            }
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                while (!startup.isDone)
                {
                    yield return null;
                }
                yield return WaitFor(() => GameSceneNavigator.Instance != null
                    && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "正式 Hub 启动");
            }
            yield return WaitFor(() => !GameSceneNavigator.Instance.IsTransitioning && UIRootManager.Instance.Root != null, "启动及切换完成");
            savedSettings = GameSettingsService.Current;
            savedScene = GameSceneNavigator.Instance.CurrentScene;
            savedMode = StreamlineRuntime.RequestedMode;
            capturedState = true;
            yield return StreamlineRuntime.ApplyModeAsync(null).ToCoroutine();
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Dlss).ToCoroutine();
            yield return WaitFor(() => (owner = Object.FindFirstObjectByType<DlssDemoController>()) != null
                && owner.Actions != null && UIManager.Instance.Get<DlssControlsView>()?.State == ViewState.Visible, "正式 DLSS 世界及 HUD");
            ownerEnabled = owner.enabled;
            owner.enabled = false;
            worldCamera = UIRootManager.Instance.BaseCamera;
            Assert.That(worldCamera, Is.Not.Null);
            Assert.That(UIRootManager.Instance.UICamera, Is.Not.SameAs(worldCamera));
            var cameraData = worldCamera.GetUniversalAdditionalCameraData();
            Assert.That(cameraData.cameraStack, Does.Contain(UIRootManager.Instance.UICamera), "必须使用正式世界/UI相机栈。");
            Assert.That(UIRootManager.Instance.UICamera.GetUniversalAdditionalCameraData().renderType, Is.EqualTo(CameraRenderType.Overlay));
            savedAa = cameraData.antialiasing;
            cameraData.antialiasing = AntialiasingMode.None;
            Assert.That(PostProcessRuntime.ActiveScopeCount, Is.Zero, "测试不应覆盖外部演出作用域。");

            loader = ResourceServices.CreateLoader();
            PostProcessPreset preset = null;
            yield return loader.LoadAssetAsync<PostProcessPreset>("LoadResources/Config/Settings/DemoPostProcessPreset")
                .ToCoroutine(value => preset = value);
            Assert.That(preset, Is.Not.Null, "正式 ResourceLoader 必须能加载保存的制作预设。");
            preset.Validate();
            string[] authoredComponents = new string[preset.Profile.components.Count];
            for (int i = 0; i < authoredComponents.Length; i++)
            {
                authoredComponents[i] = JsonUtility.ToJson(preset.Profile.components[i]);
            }

            // 不透明色块挂在实际公共 Canvas 上，隔离世界背景，检查 UI 是否被后处理污染。
            marker = new GameObject("PostProcessUiColorProbe", typeof(RectTransform), typeof(Image));
            marker.layer = LayerMask.NameToLayer("UI");
            marker.transform.SetParent(UIRootManager.Instance.GetRoot(UILayer.Foreground), false);
            var markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.82f, 0.86f);
            markerRect.anchorMax = new Vector2(0.97f, 0.94f);
            markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
            marker.GetComponent<Image>().color = new Color(0.32f, 0.62f, 0.24f, 1);
            marker.GetComponent<Image>().raycastTarget = false;
            Canvas.ForceUpdateCanvases();
            uiRegion = GetInteriorScreenRect(markerRect, UIRootManager.Instance.UICamera);
            evidenceFolder = Path.GetFullPath("Library/Settings/evidence/post-process/" + SystemInfo.graphicsDeviceType);
            Directory.CreateDirectory(evidenceFolder);
            var graphics = savedSettings.Graphics.Copy();
            graphics.Brightness = 1;
            graphics.AllowedEffects = PostProcessEffects.ColorGrading | PostProcessEffects.Bloom | PostProcessEffects.ChromaticAberration;
            var accessibility = savedSettings.Accessibility.Copy();
            accessibility.ReducedMotion = ReducedMotionMode.Off;
            PostProcessRuntime.ApplySettings(graphics, accessibility);
            yield return Settle();
            Frame baseline = Capture("01Baseline");
            yield return Settle();
            Frame repeated = Capture("02StableBaseline");
            var region = new RectInt(baseline.Width / 5, baseline.Height / 5, baseline.Width * 3 / 5, baseline.Height * 2 / 5);
            float noise = Difference(baseline, repeated, region);
            Assert.That(WorldRange(baseline, region), Is.GreaterThan(0.1f), "正式世界不应是空白或单色截图。");
            Assert.That(noise, Is.LessThan(0.003f), "禁用观察动画后基线仍在变化，不能稳定验收效果。");

            effect = PostProcessRuntime.Push(preset);
            yield return Settle();
            Frame processed = Capture("03SavedPreset");
            float presetChange = Difference(baseline, processed, region);
            Assert.That(presetChange, Is.GreaterThan(Mathf.Max(0.002f, noise * 4)), "保存的调色/泛光/色差预设没有改变真实世界像素。");
            AssertUiUnchanged(baseline, processed);
            graphics.AllowedEffects = PostProcessEffects.ColorGrading;
            PostProcessRuntime.ApplySettings(graphics, accessibility);
            yield return Settle();
            Frame filtered = Capture("04BloomAndChromaticDisabled");
            Assert.That(Difference(processed, filtered, region), Is.GreaterThan(Mathf.Max(0.0005f, noise * 4)),
                "关闭泛光/色差应在真实像素中生效，不能只改变静态 Volume 参数。");
            AssertUiUnchanged(baseline, filtered);

            effect.Dispose();
            effect = null;
            graphics.AllowedEffects |= PostProcessEffects.Bloom | PostProcessEffects.ChromaticAberration;
            PostProcessRuntime.ApplySettings(graphics, accessibility);
            yield return Settle();
            Frame restored = Capture("05ScopeReleased");
            Assert.That(PostProcessRuntime.ActiveScopeCount, Is.Zero);
            Assert.That(Difference(baseline, restored, region), Is.LessThan(Mathf.Max(0.003f, noise * 3)), "释放作用域后应恢复制作画面。");
            AssertUiUnchanged(baseline, restored);

            graphics.Brightness = 0.5f;
            PostProcessRuntime.ApplySettings(graphics, accessibility);
            yield return Settle();
            Frame darker = Capture("06HalfBrightness");
            Assert.That(WorldMean(darker, region), Is.LessThan(WorldMean(baseline, region) * 0.9f), "公共亮度没有降低呈现世界的曝光。");
            AssertUiUnchanged(baseline, darker);
            graphics.Brightness = 1;
            PostProcessRuntime.ApplySettings(graphics, accessibility);
            yield return Settle();
            Frame final = Capture("07BrightnessRestored");
            Assert.That(Difference(baseline, final, region), Is.LessThan(Mathf.Max(0.003f, noise * 3)));
            AssertUiUnchanged(baseline, final);
            for (int i = 0; i < authoredComponents.Length; i++)
            {
                Assert.That(JsonUtility.ToJson(preset.Profile.components[i]), Is.EqualTo(authoredComponents[i]), "运行时不能修改正式预设组件。");
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreRuntimeAndScene()
        {
            effect?.Dispose();
            effect = null;
            if (marker != null)
            {
                Object.Destroy(marker);
                marker = null;
            }
            loader?.Dispose();
            loader = null;
            if (!capturedState)
            {
                yield break;
            }
            PostProcessRuntime.ApplySettings(savedSettings.Graphics, savedSettings.Accessibility);
            if (worldCamera != null)
            {
                worldCamera.GetUniversalAdditionalCameraData().antialiasing = savedAa;
            }
            if (owner != null)
            {
                owner.enabled = ownerEnabled;
            }
            if (GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene != savedScene)
            {
                yield return GameSceneNavigator.Instance.SwitchAsync(savedScene).ToCoroutine();
            }
            yield return StreamlineRuntime.ApplyModeAsync(savedMode).ToCoroutine();
            capturedState = false;
            yield return null;
        }

        private readonly struct Frame
        {
            internal readonly int Width;
            internal readonly int Height;
            internal readonly Color[] Pixels;

            internal Frame(Texture2D image)
            {
                Width = image.width;
                Height = image.height;
                Pixels = image.GetPixels();
            }
        }

        private Frame Capture(string name)
        {
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(evidenceFolder, name + ".png"), image.EncodeToPNG());
                return new Frame(image);
            }
            finally
            {
                Object.Destroy(image);
            }
        }

        private void AssertUiUnchanged(Frame baseline, Frame changed)
        {
            Assert.That(Difference(baseline, changed, uiRegion), Is.LessThan(1f / 255), "世界效果改变了正式 Overlay UI 的颜色。");
        }

        private static float Difference(Frame first, Frame second, RectInt region)
        {
            Assert.That(second.Width, Is.EqualTo(first.Width));
            Assert.That(second.Height, Is.EqualTo(first.Height));
            float total = 0;
            int count = 0;
            for (int y = region.yMin; y < region.yMax; y += 3)
            {
                for (int x = region.xMin; x < region.xMax; x += 3)
                {
                    Color a = first.Pixels[y * first.Width + x];
                    Color b = second.Pixels[y * second.Width + x];
                    total += (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3;
                    count++;
                }
            }
            Assert.That(count, Is.GreaterThan(0));
            return total / count;
        }

        private static float WorldMean(Frame frame, RectInt region)
        {
            float total = 0;
            int count = 0;
            for (int y = region.yMin; y < region.yMax; y += 3)
            {
                for (int x = region.xMin; x < region.xMax; x += 3)
                {
                    total += frame.Pixels[y * frame.Width + x].grayscale;
                    count++;
                }
            }
            return total / count;
        }

        private static float WorldRange(Frame frame, RectInt region)
        {
            float min = 1;
            float max = 0;
            for (int y = region.yMin; y < region.yMax; y += 3)
            {
                for (int x = region.xMin; x < region.xMax; x += 3)
                {
                    float value = frame.Pixels[y * frame.Width + x].grayscale;
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }
            }
            return max - min;
        }

        private static RectInt GetInteriorScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            int left = Mathf.Clamp(Mathf.CeilToInt(min.x) + 5, 0, Screen.width - 1);
            int bottom = Mathf.Clamp(Mathf.CeilToInt(min.y) + 5, 0, Screen.height - 1);
            int right = Mathf.Clamp(Mathf.FloorToInt(max.x) - 5, left + 1, Screen.width);
            int top = Mathf.Clamp(Mathf.FloorToInt(max.y) - 5, bottom + 1, Screen.height);
            return new RectInt(left, bottom, right - left, top - bottom);
        }

        private static IEnumerator Settle()
        {
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
        }

        private static IEnumerator WaitFor(Func<bool> condition, string stage)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, "图像回归等待超时：" + stage);
        }
    }
}
#endif
