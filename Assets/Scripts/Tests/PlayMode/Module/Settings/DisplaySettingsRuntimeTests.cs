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
