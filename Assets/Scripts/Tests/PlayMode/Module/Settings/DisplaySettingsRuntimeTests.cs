using System;
using System.Collections;
using System.Collections.Generic;
using Core.Runtime.Rendering;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class DisplaySettingsRuntimeTests
    {
        [UnityTest]
        public IEnumerator ManualAntialiasingSurvivesUnsupportedDlssAndRestoresCameraAuthors()
        {
            DisplaySettingsRuntime.Shutdown();
            PostProcessRuntime.Shutdown();
            var worldObject = new GameObject("Manual AA world", typeof(Camera));
            var nextWorldObject = new GameObject("Manual AA next world", typeof(Camera));
            var uiObject = new GameObject("Manual AA UI", typeof(Camera));
            var world = worldObject.GetComponent<Camera>();
            var nextWorld = nextWorldObject.GetComponent<Camera>();
            var ui = uiObject.GetComponent<Camera>();
            world.enabled = nextWorld.enabled = ui.enabled = false;
            world.orthographic = nextWorld.orthographic = true;
            world.allowMSAA = false;
            nextWorld.allowMSAA = true;
            var data = world.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderPostProcessing = false;
            var nextData = nextWorld.GetUniversalAdditionalCameraData();
            nextData.antialiasing = AntialiasingMode.None;
            nextData.renderPostProcessing = false;
            ui.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            data.cameraStack.Add(ui);
            nextData.cameraStack.Add(ui);
            var singleSampleTarget = new RenderTexture(64, 64, 24) { antiAliasing = 1 };
            try
            {
                DisplaySettingsRuntime.BindCamera(world, ui);
                foreach (var mode in new[] { GraphicsAaMode.Off, GraphicsAaMode.Fxaa, GraphicsAaMode.Smaa, GraphicsAaMode.Taa })
                {
                    var preferences = new GraphicsPreferences { Antialiasing = mode };
                    yield return DisplaySettingsRuntime.ApplyAsync(preferences, Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                    Assert.That((GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).msaaSampleCount, Is.EqualTo(1));
                    Assert.That(world.allowMSAA, Is.False, "手动后处理AA不能偷偷叠加MSAA");
                    Assert.That(DisplaySettingsRuntime.EffectiveMsaaSamples, Is.EqualTo(1));
                }

                foreach (var mode in new[] { GraphicsAaMode.Fxaa, GraphicsAaMode.Msaa })
                {
                    var preferences = new GraphicsPreferences { Antialiasing = mode, MsaaSamples = 2 };
                    yield return DisplaySettingsRuntime.ApplyAsync(preferences, Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                    var nativeMode = DisplaySettingsRuntime.EffectiveAntialiasing;
                    int nativeSamples = DisplaySettingsRuntime.EffectiveMsaaSamples;
                    preferences.DlssMode = (int)StreamlineDlssMode.Quality;
                    yield return DisplaySettingsRuntime.ApplyAsync(preferences, Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                    Assert.That(StreamlineRuntime.EffectiveMode, Is.Null, "正交相机真实不支持DLSS，不依赖GPU型号");
                    Assert.That(StreamlineRuntime.RequestedMode, Is.EqualTo(StreamlineDlssMode.Quality));
                    Assert.That(DisplaySettingsRuntime.IsAntialiasingManagedByUpscaler, Is.False);
                    Assert.That(DisplaySettingsRuntime.EffectiveAntialiasing, Is.EqualTo(nativeMode));
                    Assert.That(DisplaySettingsRuntime.EffectiveMsaaSamples, Is.EqualTo(nativeSamples));
                    Assert.That(DisplaySettingsRuntime.Capture().Antialiasing, Is.EqualTo(mode));
                    Assert.That(DisplaySettingsRuntime.Capture().MsaaSamples, Is.EqualTo(2));
                    preferences.DlssMode = 0;
                    yield return DisplaySettingsRuntime.ApplyAsync(preferences, Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                    Assert.That(DisplaySettingsRuntime.EffectiveAntialiasing, Is.EqualTo(nativeMode));
                    Assert.That(world.allowMSAA, Is.EqualTo(nativeMode == GraphicsAaMode.Msaa));
                    Assert.That(data.antialiasing, Is.EqualTo(nativeMode == GraphicsAaMode.Msaa
                        ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing));
                    PostProcessRuntime.ApplySettings(new GraphicsPreferences { AllowedEffects = PostProcessEffects.None }, new AccessibilityPreferences());
                    Assert.That(data.renderPostProcessing, Is.EqualTo(nativeMode == GraphicsAaMode.Fxaa), "玩家关效果不应使手动FXAA失效");
                }

                world.targetTexture = singleSampleTarget;
                Assert.That(DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, 2, out string reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                yield return DisplaySettingsRuntime.ApplyAsync(new GraphicsPreferences
                {
                    Antialiasing = GraphicsAaMode.Msaa, MsaaSamples = 2
                }, Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                Assert.That(DisplaySettingsRuntime.EffectiveAntialiasing, Is.EqualTo(GraphicsAaMode.Fxaa));
                Assert.That(DisplaySettingsRuntime.EffectiveMsaaSamples, Is.EqualTo(1));
                Assert.That(world.targetTexture, Is.SameAs(singleSampleTarget), "不修改或重建作者目标纹理");
                world.targetTexture = null;
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                UniversalRendererData rendererCopy = null;
                for (int index = 0; index < pipeline.rendererDataList.Length; index++)
                {
                    if (ReferenceEquals(pipeline.GetRenderer(index), data.scriptableRenderer))
                    {
                        rendererCopy = pipeline.rendererDataList[index] as UniversalRendererData;
                        break;
                    }
                }

                Assert.That(rendererCopy, Is.Not.Null);
                var originalRenderingMode = rendererCopy.renderingMode;
                try
                {
                    rendererCopy.renderingMode = RenderingMode.Deferred;
                    Assert.That(DisplaySettingsRuntime.CanUseAntialiasing(GraphicsAaMode.Msaa, 2, out reason), Is.False);
                    Assert.That(reason, Does.Contain("Renderer"));
                }
                finally
                {
                    rendererCopy.renderingMode = originalRenderingMode;
                }

                DisplaySettingsRuntime.BindCamera(nextWorld, ui);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(world.allowMSAA, Is.False);
                Assert.That(data.renderPostProcessing, Is.False);
                DisplaySettingsRuntime.Shutdown();
                Assert.That(nextData.antialiasing, Is.EqualTo(AntialiasingMode.None));
                Assert.That(nextWorld.allowMSAA, Is.True);
                Assert.That(nextData.renderPostProcessing, Is.False);
            }
            finally
            {
                DisplaySettingsRuntime.Shutdown();
                PostProcessRuntime.Shutdown();
                UnityEngine.Object.Destroy(worldObject);
                UnityEngine.Object.Destroy(nextWorldObject);
                UnityEngine.Object.Destroy(uiObject);
                UnityEngine.Object.Destroy(singleSampleTarget);
            }
        }

        [UnityTest]
        public IEnumerator UnsupportedDlssKeepsNativeMsaaAndObserverFailureCannotRollBackCommittedPipeline()
        {
            DisplaySettingsRuntime.Shutdown();
            var authored = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(authored, Is.Not.Null);
            int authoredMsaa = authored.msaaSampleCount;
            var preset = ScriptableObject.CreateInstance<GraphicsQualityPreset>();
            var worldObject = new GameObject("Unsupported DLSS orthographic camera", typeof(Camera));
            var uiObject = new GameObject("Unsupported DLSS overlay camera", typeof(Camera));
            var worldCamera = worldObject.GetComponent<Camera>();
            var uiCamera = uiObject.GetComponent<Camera>();
            worldCamera.enabled = false;
            worldCamera.orthographic = true;
            uiCamera.enabled = false;
            uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            int notifications = 0;
            Action failing = () => throw new InvalidOperationException("Expected display observer failure");
            Action later = () => notifications++;
            try
            {
                preset.Configure(GraphicsQualityLevel.High, 1, 40, 4, 0, 2);
                DisplaySettingsRuntime.BindCamera(worldCamera, uiCamera);
                Assert.That(StreamlineRuntime.CanEnable(out _), Is.False, "正交相机提供与硬件无关的真实不支持路径");
                yield return DisplaySettingsRuntime.ApplyAsync(new GraphicsPreferences(), Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                var previous = GraphicsSettings.currentRenderPipeline;
                DisplaySettingsRuntime.Changed += failing;
                DisplaySettingsRuntime.Changed += later;
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: Expected display observer failure");
                yield return DisplaySettingsRuntime.ApplyAsync(new GraphicsPreferences
                {
                    Quality = GraphicsQualityLevel.High,
                    DlssMode = (int)StreamlineDlssMode.Quality
                }, new[] { preset }).ToCoroutine();
                var actual = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual, Is.Not.SameAs(previous), "订阅异常不回滚到已销毁的旧管线");
                Assert.That(actual.msaaSampleCount, Is.EqualTo(4), "原生回退保留制作预设MSAA");
                Assert.That(authored.msaaSampleCount, Is.EqualTo(authoredMsaa));
                Assert.That(DisplaySettingsRuntime.Capture().Quality, Is.EqualTo(GraphicsQualityLevel.High));
                Assert.That(StreamlineRuntime.RequestedMode, Is.EqualTo(StreamlineDlssMode.Quality));
                Assert.That(StreamlineRuntime.EffectiveMode, Is.Null);
                Assert.That(StreamlineRuntime.Status, Is.Not.Empty);
                Assert.That(notifications, Is.EqualTo(1));
            }
            finally
            {
                DisplaySettingsRuntime.Changed -= failing;
                DisplaySettingsRuntime.Changed -= later;
                DisplaySettingsRuntime.Shutdown();
                UnityEngine.Object.Destroy(worldObject);
                UnityEngine.Object.Destroy(uiObject);
                UnityEngine.Object.Destroy(preset);
            }
        }

        [UnityTest]
        public IEnumerator CurrentQualityClonesRendererWithoutEditingAuthorAssets()
        {
            DisplaySettingsRuntime.Shutdown();
            var graphics = GraphicsSettings.defaultRenderPipeline;
            var quality = QualitySettings.renderPipeline;
            var authored = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(authored, Is.Not.Null);
            var renderers = new List<ScriptableRendererData>();
            var featureCounts = new List<int>();
            for (int index = 0; index < authored.rendererDataList.Length; index++)
            {
                var renderer = authored.rendererDataList[index];
                renderers.Add(renderer);
                featureCounts.Add(renderer == null ? 0 : renderer.rendererFeatures.Count);
            }

            try
            {
                yield return DisplaySettingsRuntime.ApplyAsync(new GraphicsPreferences(), Array.Empty<GraphicsQualityPreset>()).ToCoroutine();
                var actual = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Assert.That(actual, Is.Not.SameAs(authored));
                Assert.That(actual.renderScale, Is.EqualTo(authored.renderScale));
                Assert.That(actual.shadowDistance, Is.EqualTo(authored.shadowDistance));
                for (int index = 0; index < renderers.Count; index++)
                {
                    if (renderers[index] == null)
                    {
                        continue;
                    }

                    Assert.That(actual.rendererDataList[index], Is.Not.SameAs(renderers[index]));
                    Assert.That(renderers[index].rendererFeatures.Count, Is.EqualTo(featureCounts[index]));
                    Assert.That(actual.rendererDataList[index].TryGetRendererFeature<PostProcessSettingsFeature>(out _), Is.True);
                }
            }
            finally
            {
                DisplaySettingsRuntime.Shutdown();
                Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(graphics));
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(quality));
            }
        }
    }
}
