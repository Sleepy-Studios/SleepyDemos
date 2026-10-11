using System;
using Core.Runtime.Rendering;
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
