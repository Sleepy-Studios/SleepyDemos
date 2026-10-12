#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 正式启动、真实世界及 UI 相机栈的 GPU 回归；不依赖 DLSS 硬件可用性。
    [Category("RequiresGPU")]
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

            PostProcessPreset depthPreset = null;
            yield return loader.LoadAssetAsync<PostProcessPreset>("LoadResources/Config/Presentation/Lab/DepthOfField")
                .ToCoroutine(value => depthPreset = value);
            Assert.That(depthPreset, Is.Not.Null);
            var uiData = UIRootManager.Instance.UICamera.GetUniversalAdditionalCameraData();
            var authoredWorldDepth = cameraData.requiresDepthOption;
            var authoredUiDepth = uiData.requiresDepthOption;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool authoredPipelineDepth = pipeline.supportsCameraDepthTexture;
            string authoredDepthProfile = JsonUtility.ToJson(depthPreset.Profile.components[0]);
            var rendererFeatures = (List<ScriptableRendererFeature>)typeof(ScriptableRenderer)
                .GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(cameraData.scriptableRenderer);
            Assert.That(rendererFeatures, Is.Not.Null, "测试必须观察当前运行时 Renderer，不能修改制作资产。");
            var depthObserver = ScriptableObject.CreateInstance<DepthInputObserver>();
            depthObserver.Camera = worldCamera;
            depthObserver.Create();
            try
            {
                rendererFeatures.Add(depthObserver);
                cameraData.requiresDepthOption = uiData.requiresDepthOption = CameraOverrideOption.Off;
                graphics.AllowedEffects |= PostProcessEffects.DepthOfField;
                PostProcessRuntime.ApplySettings(graphics, accessibility);
                yield return Settle();
                Frame withoutDepthEffect = Capture("08AuthoredDepthOff");
                Assert.That(depthObserver.PostProcessingRequiresDepth, Is.False,
                    "临时景深启用前，本帧后处理不应提出深度需求；其它 URP 用途可保有深度纹理。");
                var scope = PostProcessRuntime.Push(depthPreset);
                effect = scope;
                scope.Update(profile =>
                {
                    profile.TryGet<DepthOfField>(out var depth);
                    depth.mode.Override(DepthOfFieldMode.Bokeh);
                    depth.focusDistance.Override(1);
                    depth.focalLength.Override(160);
                    depth.aperture.Override(1);
                });
                yield return Settle();
                Frame dynamicDepth = Capture("09DynamicDepthOfField");
                Assert.That(depthObserver.PostProcessingRequiresDepth, Is.True,
                    "作者深度选项为 Off 时，临时景深仍须向本帧 URP 声明深度需求。");
                Assert.That(depthObserver.DepthWidth, Is.GreaterThan(32),
                    "景深的真实图内输入不能是 URP 默认深度纹理。");
                Assert.That(Difference(withoutDepthEffect, dynamicDepth, region), Is.GreaterThan(Mathf.Max(0.0005f, noise * 4)),
                    "作者关闭深度时临时 Bokeh 景深仍须改变真实世界画面。");
                AssertUiUnchanged(withoutDepthEffect, dynamicDepth);
                Assert.That(cameraData.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
                Assert.That(uiData.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
                Assert.That(pipeline.supportsCameraDepthTexture, Is.EqualTo(authoredPipelineDepth));
                scope.Dispose();
                effect = null;
                yield return Settle();
                Frame depthRestored = Capture("10DepthOfFieldReleased");
                Assert.That(depthObserver.PostProcessingRequiresDepth, Is.False,
                    "释放临时景深后，不得遗留本帧后处理的深度需求。");
                Assert.That(Difference(withoutDepthEffect, depthRestored, region), Is.LessThan(Mathf.Max(0.003f, noise * 3)));
                AssertUiUnchanged(withoutDepthEffect, depthRestored);
                Assert.That(cameraData.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
                Assert.That(JsonUtility.ToJson(depthPreset.Profile.components[0]), Is.EqualTo(authoredDepthProfile));
            }
            finally
            {
                rendererFeatures.Remove(depthObserver);
                depthObserver.Dispose();
                Object.Destroy(depthObserver);
                effect?.Dispose();
                effect = null;
                cameraData.requiresDepthOption = authoredWorldDepth;
                uiData.requiresDepthOption = authoredUiDepth;
            }

            string[] labNames =
            {
                "ColorAdjustments", "Bloom", "ChromaticAberration", "Vignette", "LensDistortion", "FilmGrain"
            };
            PostProcessEffects[] labEffects =
            {
                PostProcessEffects.ColorGrading, PostProcessEffects.Bloom, PostProcessEffects.ChromaticAberration,
                PostProcessEffects.Vignette, PostProcessEffects.LensDistortion, PostProcessEffects.FilmGrain
            };
            // 下部地面避开 Overlay HUD；中心小窗和外围棋盘共同区分暗角/畸变的空间表现。
            var edgeRegion = new RectInt(baseline.Width * 8 / 100, baseline.Height * 8 / 100,
                baseline.Width / 5, baseline.Height * 16 / 100);
            var centerRegion = new RectInt(baseline.Width * 49 / 100, baseline.Height * 49 / 100,
                baseline.Width * 2 / 100, baseline.Height * 2 / 100);
            for (int index = 0; index < labNames.Length; index++)
            {
                PostProcessPreset labPreset = null;
                yield return loader.LoadAssetAsync<PostProcessPreset>("LoadResources/Config/Presentation/Lab/" + labNames[index])
                    .ToCoroutine(value => labPreset = value);
                Assert.That(labPreset, Is.Not.Null, "必须通过正式资源入口加载 Lab 预设。");
                labPreset.Validate();
                var authoredLabComponents = new string[labPreset.Profile.components.Count];
                for (int component = 0; component < authoredLabComponents.Length; component++)
                {
                    authoredLabComponents[component] = JsonUtility.ToJson(labPreset.Profile.components[component]);
                }

                graphics.AllowedEffects = labEffects[index];
                PostProcessRuntime.ApplySettings(graphics, accessibility);
                yield return Settle();
                Frame labBaseline = Capture("Lab" + labNames[index] + "-Baseline");
                effect = PostProcessRuntime.Push(labPreset);
                yield return Settle();
                Frame labApplied = Capture("Lab" + labNames[index] + "-Applied");
                float worldChange = Difference(labBaseline, labApplied, region);
                Assert.That(worldChange, Is.GreaterThan(Mathf.Max(0.0005f, noise * 4)),
                    labNames[index] + " 保存预设没有改变真实世界像素。");
                AssertUiUnchanged(labBaseline, labApplied);

                if (labEffects[index] == PostProcessEffects.Vignette)
                {
                    float edgeDimming = 1 - WorldMean(labApplied, edgeRegion) / Mathf.Max(0.01f, WorldMean(labBaseline, edgeRegion));
                    float centerDimming = 1 - WorldMean(labApplied, centerRegion) / Mathf.Max(0.01f, WorldMean(labBaseline, centerRegion));
                    Assert.That(edgeDimming, Is.GreaterThan(0.05f), "暗角必须实际压暗世界边缘。");
                    Assert.That(edgeDimming, Is.GreaterThan(centerDimming + 0.05f), "暗角应比画面中心更明显地压暗边缘。");
                }
                else if (labEffects[index] == PostProcessEffects.LensDistortion)
                {
                    Assert.That(Difference(labBaseline, labApplied, centerRegion), Is.LessThan(worldChange),
                        "镜头畸变应改变外围棋盘/细线结构，并保留靠近中心的局部画面。");
                }
                else if (labEffects[index] == PostProcessEffects.FilmGrain)
                {
                    Assert.That(NeighborDifference(labApplied, edgeRegion),
                        Is.GreaterThan(NeighborDifference(labBaseline, edgeRegion) + 0.001f),
                        "胶片颗粒应在平坦地面增加真实的邻像素纹理变化。");
                    yield return Settle();
                    Frame grainSecond = Capture("LabFilmGrain-SecondFrame");
                    yield return Settle();
                    Frame grainThird = Capture("LabFilmGrain-ThirdFrame");
                    Assert.That(Difference(labApplied, grainSecond, region), Is.GreaterThan(Mathf.Max(0.001f, noise * 4)),
                        "胶片颗粒必须随帧变化，且显著超过静止基线的噪声。");
                    Assert.That(Difference(grainSecond, grainThird, region), Is.GreaterThan(Mathf.Max(0.001f, noise * 4)));
                    AssertUiUnchanged(labBaseline, grainSecond);
                    AssertUiUnchanged(labBaseline, grainThird);
                }
                if (labEffects[index] != PostProcessEffects.FilmGrain)
                {
                    yield return Settle();
                    Frame labStable = Capture("Lab" + labNames[index] + "-Stable");
                    Assert.That(Difference(labApplied, labStable, region), Is.LessThan(Mathf.Max(0.003f, noise * 3)),
                        labNames[index] + " 静止画面应稳定，不得误以动态噪声作为效果变化。");
                    AssertUiUnchanged(labBaseline, labStable);
                }

                effect.Dispose();
                effect = null;
                yield return Settle();
                Frame labReleased = Capture("Lab" + labNames[index] + "-Released");
                Assert.That(PostProcessRuntime.ActiveScopeCount, Is.Zero);
                Assert.That(Difference(labBaseline, labReleased, region), Is.LessThan(Mathf.Max(0.003f, noise * 3)),
                    labNames[index] + " 释放后应恢复世界画面，不得残留效果或动态噪声。");
                AssertUiUnchanged(labBaseline, labReleased);
                for (int component = 0; component < authoredLabComponents.Length; component++)
                {
                    Assert.That(JsonUtility.ToJson(labPreset.Profile.components[component]), Is.EqualTo(authoredLabComponents[component]),
                        "运行时不能修改正式 Lab 预设组件。");
                }
            }
        }

        // RenderGraph 退出时会清空全局纹理绑定；仅在图内观察与原生 DoF 相同的输入。
        // 本 Pass 不申请深度，不绘制或修改世界输出，也不将图内资源保留到帧外。
        private sealed class DepthInputObserver : ScriptableRendererFeature
        {
            public Camera Camera;
            public int DepthWidth;
            public bool PostProcessingRequiresDepth;
            private DepthInputPass pass;

            public override void Create()
            {
                pass = new DepthInputPass(this);
            }

            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.camera == Camera)
                {
                    renderer.EnqueuePass(pass);
                }
            }

            private sealed class DepthInputPass : ScriptableRenderPass
            {
                private readonly DepthInputObserver owner;

                private sealed class PassData
                {
                    public TextureHandle Depth;
                    public DepthInputObserver Owner;
                }

                public DepthInputPass(DepthInputObserver owner)
                {
                    this.owner = owner;
                    renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                    ConfigureInput(ScriptableRenderPassInput.None);
                }

                public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
                {
                    owner.PostProcessingRequiresDepth = frameData.Get<UniversalCameraData>().postProcessingRequiresDepthTexture;
                    owner.DepthWidth = 0;
                    var depth = frameData.Get<UniversalResourceData>().cameraDepthTexture;
                    if (!depth.IsValid())
                    {
                        return;
                    }
                    using var builder = renderGraph.AddRasterRenderPass<PassData>("Observe DoF depth input", out var data);
                    data.Depth = depth;
                    data.Owner = owner;
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Texture input = data.Depth;
                        data.Owner.DepthWidth = input != null ? input.width : 0;
                    });
                }
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

        private static float NeighborDifference(Frame frame, RectInt region)
        {
            float total = 0;
            int count = 0;
            for (int y = region.yMin; y < region.yMax - 1; y += 3)
            {
                for (int x = region.xMin; x < region.xMax - 1; x += 3)
                {
                    float current = frame.Pixels[y * frame.Width + x].grayscale;
                    float right = frame.Pixels[y * frame.Width + x + 1].grayscale;
                    float above = frame.Pixels[(y + 1) * frame.Width + x].grayscale;
                    total += (Mathf.Abs(current - right) + Mathf.Abs(current - above)) * 0.5f;
                    count++;
                }
            }
            Assert.That(count, Is.GreaterThan(0));
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
