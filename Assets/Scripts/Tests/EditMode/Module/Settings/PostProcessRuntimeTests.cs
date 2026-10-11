using System;
using System.Threading.Tasks;
using Core.Runtime.Rendering;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class PostProcessRuntimeTests
    {
        [Test]
        public void DynamicDepthOfFieldRequestsFrameDepthWithoutChangingCameraPreferences()
        {
            var manager = VolumeManager.instance;
            bool initializedHere = !manager.isInitialized;
            if (initializedHere) manager.Initialize();
            var previousStack = manager.stack;
            var stack = manager.CreateStack();
            manager.stack = stack;
            var owner = new GameObject("Dynamic DoF world", typeof(Camera));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var preset = ScriptableObject.CreateInstance<PostProcessPreset>();
            var feature = ScriptableObject.CreateInstance<PostProcessSettingsFeature>();
            try
            {
                var camera = owner.GetComponent<Camera>();
                camera.enabled = false;
                var authored = camera.GetUniversalAdditionalCameraData();
                authored.requiresDepthOption = CameraOverrideOption.Off;
                authored.renderPostProcessing = false;
                var depth = profile.Add<DepthOfField>();
                depth.mode.Override(DepthOfFieldMode.Bokeh);
                preset.Configure(profile);
                PostProcessRuntime.ApplySettings(new GraphicsPreferences(), new AccessibilityPreferences());
                PostProcessRuntime.BindCamera(camera);
                using var scope = PostProcessRuntime.Push(preset);
                using var frame = new ContextContainer();
                var data = frame.Create<UniversalCameraData>();
                data.camera = camera;
                data.postProcessEnabled = authored.renderPostProcessing;
                data.requiresDepthTexture = false;
                data.postProcessingRequiresDepthTexture = false;
                var rendering = (RenderingData)Activator.CreateInstance(typeof(RenderingData),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new object[] { frame }, null);
                feature.AddRenderPasses(null, ref rendering);
                Assert.That(data.postProcessingRequiresDepthTexture, Is.True,
                    "临时景深必须更新真正的 URP 帧数据，不能依赖作者提前开启深度。");
                Assert.That(data.requiresDepthTexture, Is.False);
                Assert.That(authored.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));

                data.camera = null;
                data.postProcessingRequiresDepthTexture = false;
                feature.AddRenderPasses(null, ref rendering);
                Assert.That(data.postProcessingRequiresDepthTexture, Is.False, "其它相机不接收世界作用域的深度要求。");
                data.camera = camera;
                PostProcessRuntime.ApplySettings(new GraphicsPreferences { AllowedEffects = PostProcessEffects.None }, new AccessibilityPreferences());
                feature.AddRenderPasses(null, ref rendering);
                Assert.That(data.postProcessingRequiresDepthTexture, Is.False, "玩家禁止景深后不再请求额外深度。");
                scope.Dispose();
                data.postProcessEnabled = authored.renderPostProcessing;
                feature.AddRenderPasses(null, ref rendering);
                Assert.That(data.postProcessingRequiresDepthTexture, Is.False);
                Assert.That(authored.renderPostProcessing, Is.False);
                Assert.That(authored.requiresDepthOption, Is.EqualTo(CameraOverrideOption.Off));
                Assert.That(depth.mode.value, Is.EqualTo(DepthOfFieldMode.Bokeh), "原制作预设不被过滤或释放修改。");
            }
            finally
            {
                PostProcessRuntime.Shutdown();
                manager.stack = previousStack;
                manager.DestroyStack(stack);
                foreach (var component in profile.components) Object.DestroyImmediate(component);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(owner);
                if (initializedHere) manager.Deinitialize();
            }
        }

        [Test]
        public async Task PureNativeFxaaDoesNotActivateAuthoredPostEffects()
        {
            var existingDrivers = Object.FindObjectsByType<StreamlineRuntimeDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            DisplaySettingsRuntime.Shutdown();
            var manager = VolumeManager.instance;
            bool initializedHere = !manager.isInitialized;
            if (initializedHere)
            {
                manager.Initialize();
            }

            var previousStack = manager.stack;
            var stack = manager.CreateStack();
            var owner = new GameObject("Pure native AA world", typeof(Camera));
            var uiOwner = new GameObject("Pure native AA UI", typeof(Camera));
            try
            {
                var camera = owner.GetComponent<Camera>();
                camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var uiCamera = uiOwner.GetComponent<Camera>();
                uiCamera.enabled = false;
                uiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                DisplaySettingsRuntime.BindCamera(camera, uiCamera);
                await DisplaySettingsRuntime.ApplyAsync(new GraphicsPreferences { Antialiasing = GraphicsAaMode.Fxaa },
                    Array.Empty<GraphicsQualityPreset>());
                manager.stack = stack;
                var bloom = stack.GetComponent<Bloom>();
                var grading = stack.GetComponent<ColorAdjustments>();
                var motion = stack.GetComponent<MotionBlur>();
                bloom.intensity.value = 0.6f;
                grading.postExposure.value = 2;
                motion.intensity.value = 0.4f;
                PostProcessRuntime.ApplySettings(new GraphicsPreferences(),
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.Off });
                Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True, "FXAA需要后处理路径");
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.Zero, "纯AA不能启用作者原来没有渲染的效果");
                Assert.That(grading.postExposure.value, Is.Zero);
                Assert.That(motion.intensity.value, Is.Zero);
                PostProcessRuntime.BindCamera(null);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(grading.postExposure.value, Is.EqualTo(2));
                Assert.That(motion.intensity.value, Is.EqualTo(0.4f).Within(0.00001f));
            }
            finally
            {
                DisplaySettingsRuntime.Shutdown();
                PostProcessRuntime.Shutdown();
                manager.stack = previousStack;
                manager.DestroyStack(stack);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(uiOwner);
                foreach (var driver in Object.FindObjectsByType<StreamlineRuntimeDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (Array.IndexOf(existingDrivers, driver) < 0)
                    {
                        Object.DestroyImmediate(driver.gameObject);
                    }
                }

                if (initializedHere)
                {
                    manager.Deinitialize();
                }
            }
        }

        [Test]
        public void BasePresetAndAtomicParameterUpdatesKeepPriorityAndPlayerLimits()
        {
            var manager = VolumeManager.instance;
            bool initializedHere = !manager.isInitialized;
            if (initializedHere)
            {
                manager.Initialize();
            }

            var previousStack = manager.stack;
            var stack = manager.CreateStack();
            manager.stack = stack;
            var owner = new GameObject("Post process base and update test");
            var camera = owner.AddComponent<Camera>();
            var baseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var firstProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var lastProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var basePreset = ScriptableObject.CreateInstance<PostProcessPreset>();
            var firstPreset = ScriptableObject.CreateInstance<PostProcessPreset>();
            var lastPreset = ScriptableObject.CreateInstance<PostProcessPreset>();
            var invalidPreset = ScriptableObject.CreateInstance<PostProcessPreset>();
            try
            {
                var bloom = stack.GetComponent<Bloom>();
                bloom.intensity.value = 0.1f;
                bloom.threshold.value = 0.9f;
                baseProfile.Add<Bloom>().intensity.Override(0.2f);
                firstProfile.Add<Bloom>().intensity.Override(0.5f);
                lastProfile.Add<Bloom>().intensity.Override(0.9f);
                basePreset.Configure(baseProfile);
                firstPreset.Configure(firstProfile);
                lastPreset.Configure(lastProfile);
                PostProcessRuntime.ApplySettings(new GraphicsPreferences(),
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.Off });
                PostProcessRuntime.BindCamera(camera);
                PostProcessRuntime.SetBasePreset(basePreset);
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.2f).Within(0.00001f));

                var first = PostProcessRuntime.Push(firstPreset);
                var last = PostProcessRuntime.Push(lastPreset);
                first.Update(basePreset);
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.9f).Within(0.00001f), "更新旧作用域不能提升优先级");
                last.Dispose();
                VolumeProfile retainedCandidate = null;
                Bloom retainedBloom = null;
                first.Update(candidate =>
                {
                    retainedCandidate = candidate;
                    candidate.TryGet(out retainedBloom);
                    retainedBloom.intensity.Override(0.7f);
                });
                Assert.That(retainedCandidate == null, Is.True, "回调候选不作为公开可写生效状态保留");
                retainedBloom.intensity.Override(0.01f);
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.7f).Within(0.00001f));
                Assert.That(baseProfile.components[0] is Bloom authored && authored.intensity.value == 0.2f, Is.True);

                Assert.Throws<ArgumentException>(() => first.Update(candidate =>
                {
                    candidate.TryGet<Bloom>(out var component);
                    component.intensity.Override(float.NaN);
                }));
                Assert.Throws<ArgumentException>(() => first.Update(invalidPreset));
                Assert.Throws<ArgumentException>(() => PostProcessRuntime.SetBasePreset(invalidPreset));
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.7f).Within(0.00001f), "无效更新保持上次有效参数");

                PostProcessRuntime.ApplySettings(new GraphicsPreferences { AllowedEffects = PostProcessEffects.All & ~PostProcessEffects.Bloom },
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.Off });
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.Zero, "玩家过滤优先于基础和临时参数更新");
                PostProcessRuntime.ApplySettings(new GraphicsPreferences(),
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.Off });
                first.Dispose();
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.2f).Within(0.00001f));
                PostProcessRuntime.SetBasePreset(null);
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.1f).Within(0.00001f));
                PostProcessRuntime.SetBasePreset(basePreset);
                PostProcessRuntime.BindCamera(null);
                Assert.That(bloom.intensity.value, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(PostProcessRuntime.ActiveScopeCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => first.Update(firstPreset));
            }
            finally
            {
                PostProcessRuntime.Shutdown();
                manager.stack = previousStack;
                manager.DestroyStack(stack);
                foreach (var profile in new[] { baseProfile, firstProfile, lastProfile })
                {
                    foreach (var component in profile.components)
                    {
                        Object.DestroyImmediate(component);
                    }

                    Object.DestroyImmediate(profile);
                }

                Object.DestroyImmediate(basePreset);
                Object.DestroyImmediate(firstPreset);
                Object.DestroyImmediate(lastPreset);
                Object.DestroyImmediate(invalidPreset);
                Object.DestroyImmediate(owner);
                if (initializedHere)
                {
                    manager.Deinitialize();
                }
            }
        }

        [Test]
        public void ScopesOverrideOnlyCheckedParametersAndRestoreSceneStack()
        {
            var manager = VolumeManager.instance;
            bool initializedHere = !manager.isInitialized;
            if (initializedHere)
            {
                manager.Initialize();
            }

            var previousStack = manager.stack;
            var stack = manager.CreateStack();
            manager.stack = stack;
            var owner = new GameObject("Post process scope test");
            var camera = owner.AddComponent<Camera>();
            var firstProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var secondProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var first = ScriptableObject.CreateInstance<PostProcessPreset>();
            var second = ScriptableObject.CreateInstance<PostProcessPreset>();
            try
            {
                var sceneBloom = stack.GetComponent<Bloom>();
                var sceneColor = stack.GetComponent<ColorAdjustments>();
                Assert.That(sceneBloom, Is.Not.Null);
                sceneBloom.intensity.value = 0.1f;
                sceneBloom.threshold.value = 0.9f;
                sceneColor.postExposure.value = 2;
                var authored = firstProfile.Add<Bloom>();
                authored.intensity.Override(0.6f);
                var later = secondProfile.Add<Bloom>();
                later.threshold.Override(1.4f);
                first.Configure(firstProfile);
                second.Configure(secondProfile);
                PostProcessRuntime.ApplySettings(new GraphicsPreferences { Brightness = 2 },
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.Off });
                PostProcessRuntime.BindCamera(camera);
                var firstScope = PostProcessRuntime.Push(first);
                var secondScope = PostProcessRuntime.Push(second);
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(sceneBloom.intensity.value, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(sceneBloom.threshold.value, Is.EqualTo(1.4f).Within(0.00001f));
                Assert.That(sceneColor.postExposure.value, Is.EqualTo(3).Within(0.00001f), "亮度只在原曝光上累加");
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(sceneColor.postExposure.value, Is.EqualTo(3).Within(0.00001f), "重复渲染不累积曝光");

                firstScope.Dispose();
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(sceneBloom.intensity.value, Is.EqualTo(0.1f).Within(0.00001f), "乱序移除只恢复自己的参数");
                Assert.That(sceneBloom.threshold.value, Is.EqualTo(1.4f).Within(0.00001f));
                PostProcessRuntime.ApplySettings(new GraphicsPreferences { AllowedEffects = PostProcessEffects.None },
                    new AccessibilityPreferences { ReducedMotion = ReducedMotionMode.On });
                PostProcessRuntime.ApplyToStack(camera);
                Assert.That(sceneBloom.intensity.value, Is.Zero, "玩家过滤优先于演出预设");
                PostProcessRuntime.BindCamera(null);
                Assert.That(sceneBloom.intensity.value, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(sceneBloom.threshold.value, Is.EqualTo(0.9f).Within(0.00001f));
                Assert.That(sceneColor.postExposure.value, Is.EqualTo(2).Within(0.00001f));
                Assert.That(PostProcessRuntime.ActiveScopeCount, Is.Zero);
                secondScope.Dispose();
                secondScope.Dispose();
                Assert.That(authored.intensity.value, Is.EqualTo(0.6f));
                Assert.That(later.threshold.value, Is.EqualTo(1.4f));
            }
            finally
            {
                PostProcessRuntime.Shutdown();
                manager.stack = previousStack;
                manager.DestroyStack(stack);
                foreach (var component in firstProfile.components)
                {
                    Object.DestroyImmediate(component);
                }

                foreach (var component in secondProfile.components)
                {
                    Object.DestroyImmediate(component);
                }

                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(firstProfile);
                Object.DestroyImmediate(secondProfile);
                Object.DestroyImmediate(owner);
                if (initializedHere)
                {
                    manager.Deinitialize();
                }
            }
        }
    }
}
