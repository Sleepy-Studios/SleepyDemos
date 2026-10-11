using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class CinematicRuntimeTests
    {
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FrozenTitleUsesLocalizedFontIdsAndClearsAfterCompletionCancellationAndFailure()
        {
            yield return RunTitleCaseAsync().ToCoroutine();
        }

        private static async UniTask RunTitleCaseAsync()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameSettingsConfig>("Assets/" + GameSettingsService.ConfigAddress + ".asset");
            Assert.That(config, Is.Not.Null);
            // 仅保存测试触及的语言服务状态，避免独立运行此用例后改变启动初始化条件。
            var state = new[] { "texts", "fonts", "languages", "<CurrentLanguage>k__BackingField" };
            var fields = new FieldInfo[state.Length];
            var values = new object[state.Length];
            for (int index = 0; index < state.Length; index++)
            {
                fields[index] = typeof(LanguageService).GetField(state[index], BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(fields[index], Is.Not.Null);
                values[index] = fields[index].GetValue(null);
            }

            using var fixture = new PresentationFixture();
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                foreach (string language in new[] { "zh-CN", "en" })
                {
                    LanguageService.Initialize(config.Language, config.Fonts, language);
                    var completed = CinematicRuntime.ShowTitleAsync("Presentation.Chapter", "Presentation.Location", 0, 0.04f, 0.01f);
                    AssertTitleMapping(fixture.Title, config, "Presentation.Chapter");
                    AssertTitleMapping(fixture.Subtitle, config, "Presentation.Location");
                    Assert.That(fixture.TitleGroup.alpha, Is.EqualTo(1));
                    await completed.Timeout(TimeSpan.FromSeconds(3));
                    AssertTitleCleared(fixture);
                }

                using var cancellation = new CancellationTokenSource();
                var canceled = CinematicRuntime.ShowTitleAsync("Presentation.Chapter", "Presentation.Location", 0, 10, 0, cancellation.Token);
                Assert.That(fixture.TitleGroup.alpha, Is.EqualTo(1));
                cancellation.Cancel();
                bool wasCanceled = false;
                try
                {
                    await canceled;
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }
                Assert.That(wasCanceled, Is.True);
                AssertTitleCleared(fixture);

                // 空键是真实非法调用；缺失词条会返回键本身，不能用它伪造失败路径。
                fixture.Title.text = "Existing title";
                fixture.Subtitle.text = "Existing subtitle";
                fixture.TitleGroup.alpha = 1;
                bool failed = false;
                try
                {
                    await CinematicRuntime.ShowTitleAsync(string.Empty, "Presentation.Location", 0, 0, 0);
                }
                catch (ArgumentException)
                {
                    failed = true;
                }
                Assert.That(failed, Is.True);
                AssertTitleCleared(fixture);
            }
            finally
            {
                Time.timeScale = previousScale;
                for (int index = 0; index < fields.Length; index++)
                {
                    fields[index].SetValue(null, values[index]);
                }
            }
        }

        private static void AssertTitleMapping(TMP_Text target, GameSettingsConfig config, string key)
        {
            Assert.That(target.text, Is.EqualTo(LanguageService.Get(key)));
            int fontId = -1;
            foreach (var entry in config.Language.Entries)
            {
                if (entry.Key == key)
                {
                    fontId = entry.FontId;
                    break;
                }
            }
            Assert.That(fontId, Is.GreaterThanOrEqualTo(0), "测试使用正式词条的 FontId");
            foreach (var mapping in config.Fonts.Entries)
            {
                if (mapping.FontId == fontId && mapping.Language == LanguageService.CurrentLanguage)
                {
                    Assert.That(target.font, Is.SameAs(mapping.Font));
                    Assert.That(target.fontSharedMaterial, Is.SameAs(mapping.Material));
                    return;
                }
            }
            Assert.Fail("正式字幕缺少对应字体映射：" + key);
        }

        private static void AssertTitleCleared(PresentationFixture fixture)
        {
            Assert.That(fixture.Title.text, Is.Empty);
            Assert.That(fixture.Subtitle.text, Is.Empty);
            Assert.That(fixture.TitleGroup.alpha, Is.Zero);
            Assert.That(fixture.TitleGroup.interactable, Is.False);
            Assert.That(fixture.TitleGroup.blocksRaycasts, Is.False);
        }
#endif

        [UnityTest]
        public IEnumerator CurtainShaderRendersAllPatternsAndCoverageBoundaries()
        {
            yield return RunCurtainImageCaseAsync().ToCoroutine();
        }

        private static async UniTask RunCurtainImageCaseAsync()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("遮幕像素验收需要真实图形设备。");
            }

            using var fixture = new PresentationFixture();
            var cameraObject = new GameObject("Cinematic image test camera", typeof(Camera));
            var output = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(256, 128, TextureFormat.RGBA32, false, true);
            var preset = ScriptableObject.CreateInstance<CinematicPreset>();
            var previousTarget = RenderTexture.active;
            string evidence = Path.Combine(Application.dataPath, "../Library/Settings/evidence/cinematic", SystemInfo.graphicsDeviceType.ToString());
            try
            {
                output.Create();
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 64;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = output;
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.volumeLayerMask = 0;
                var canvas = fixture.Root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                foreach (var item in fixture.Root.GetComponentsInChildren<Transform>(true))
                {
                    item.gameObject.layer = 31;
                }
                fixture.TopBar.transform.parent.gameObject.SetActive(false);
                fixture.TitleGroup.gameObject.SetActive(false);
                var rect = fixture.Curtain.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Canvas.ForceUpdateCanvases();
                Directory.CreateDirectory(evidence);

                foreach (CinematicTransitionStyle style in Enum.GetValues(typeof(CinematicTransitionStyle)))
                {
                    var directions = style == CinematicTransitionStyle.Wipe
                        ? (CinematicWipeDirection[])Enum.GetValues(typeof(CinematicWipeDirection))
                        : new[] { CinematicWipeDirection.LeftToRight };
                    foreach (var direction in directions)
                    {
                        preset.ConfigureTransition(style, direction, 0, 0, 0, Color.black, 4, 16);
                        var midpoint = new UniTaskCompletionSource();
                        var transition = CinematicRuntime.PlayTransitionAsync(preset, _ => midpoint.Task);
                        try
                        {
                            foreach (float progress in new[] { 0f, 1f, style == CinematicTransitionStyle.Iris ? 0.65f : 0.5f })
                            {
                                // 在真实公共转场中点固定时间采样材质，避免测试依赖帧速率和缓动时间。
                                fixture.Curtain.material.SetFloat("_Progress", progress);
                                await UniTask.WaitForEndOfFrame();
                                RenderTexture.active = output;
                                readback.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
                                readback.Apply();
                                RenderTexture.active = previousTarget;
                                if (progress == 0 || progress == 1)
                                {
                                    foreach (var pixel in readback.GetPixels())
                                    {
                                        Assert.That(pixel.r, Is.EqualTo(1 - progress).Within(0.1f), style + "覆盖端点");
                                    }
                                }
                                else
                                {
                                    AssertCurtainShape(readback, style, direction);
                                    File.WriteAllBytes(Path.Combine(evidence, style + "-" + direction + ".png"), readback.EncodeToPNG());
                                }
                            }
                        }
                        finally
                        {
                            midpoint.TrySetResult();
                            await transition;
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                cameraObject.GetComponent<Camera>().targetTexture = null;
                Object.Destroy(cameraObject);
                output.Release();
                Object.Destroy(output);
                Object.Destroy(readback);
                Object.Destroy(preset);
            }
        }

        private static void AssertCurtainShape(Texture2D image, CinematicTransitionStyle style, CinematicWipeDirection direction)
        {
            float Sample(float x, float y) => image.GetPixel(Mathf.FloorToInt(x * image.width), Mathf.FloorToInt(y * image.height)).r;
            void Covered(float x, float y) => Assert.That(Sample(x, y), Is.LessThan(0.1f), style + "应为黑幕");
            void Open(float x, float y) => Assert.That(Sample(x, y), Is.GreaterThan(0.9f), style + "应露出背景");
            switch (style)
            {
                case CinematicTransitionStyle.Fade:
                    Assert.That(Sample(0.2f, 0.2f), Is.InRange(0.35f, 0.8f));
                    Assert.That(Sample(0.8f, 0.8f), Is.EqualTo(Sample(0.2f, 0.2f)).Within(0.02f));
                    break;
                case CinematicTransitionStyle.Wipe:
                    if (direction == CinematicWipeDirection.LeftToRight)
                    {
                        Covered(0.2f, 0.5f);
                        Open(0.8f, 0.5f);
                    }
                    else if (direction == CinematicWipeDirection.RightToLeft)
                    {
                        Open(0.2f, 0.5f);
                        Covered(0.8f, 0.5f);
                    }
                    else if (direction == CinematicWipeDirection.BottomToTop)
                    {
                        Covered(0.5f, 0.2f);
                        Open(0.5f, 0.8f);
                    }
                    else
                    {
                        Open(0.5f, 0.2f);
                        Covered(0.5f, 0.8f);
                    }
                    break;
                case CinematicTransitionStyle.DoorsHorizontal:
                    Covered(0.1f, 0.5f); Covered(0.9f, 0.5f); Open(0.5f, 0.1f);
                    break;
                case CinematicTransitionStyle.DoorsVertical:
                    Covered(0.5f, 0.1f); Covered(0.5f, 0.9f); Open(0.1f, 0.5f);
                    break;
                case CinematicTransitionStyle.Iris:
                    // 长方形画面上相同像素半径应有相同覆盖，不应变成按 UV 拉伸的椭圆。
                    Open(0.5f, 0.5f); Covered(0.05f, 0.05f);
                    Open(0.65625f, 0.5f); Open(0.5f, 0.8125f);
                    Covered(0.7265625f, 0.5f); Covered(0.5f, 0.953125f);
                    break;
                case CinematicTransitionStyle.Blinds:
                    Covered(0.05f, 0.5f); Open(0.18f, 0.5f);
                    Covered(0.3f, 0.5f); Open(0.43f, 0.5f);
                    break;
                case CinematicTransitionStyle.Pixel:
                    int covered = 0;
                    for (int row = 0; row < 8; row++)
                    {
                        for (int column = 0; column < 16; column++)
                        {
                            float sample = Sample((column + 0.5f) / 16, (row + 0.5f) / 8);
                            Assert.That(sample < 0.1f || sample > 0.9f, Is.True, "像素转场单元不能变成整体半透明淡入");
                            if (sample < 0.1f)
                            {
                                covered++;
                            }
                        }
                    }
                    Assert.That(covered, Is.InRange(16, 112), "只要求同时有开闭单元，不绑定 GPU 的随机散列顺序");
                    break;
            }
        }

        [UnityTest]
        public IEnumerator TransitionQueueCancellationAndFailureRestoreCurtain()
        {
            yield return RunQueueCaseAsync().ToCoroutine();
        }

        private static async UniTask RunQueueCaseAsync()
        {
            using var fixture = new PresentationFixture();
            var preset = ScriptableObject.CreateInstance<CinematicPreset>();
            float previousScale = Time.timeScale;
            using var cancellation = new CancellationTokenSource();
            try
            {
                Time.timeScale = 0;
                preset.ConfigureTransition(CinematicTransitionStyle.Fade, CinematicWipeDirection.LeftToRight, 0, 0.02f, 0, Color.black);
                var order = new List<int>();
                bool finishFirst = false;
                var first = CinematicRuntime.PlayTransitionAsync(preset, async token =>
                {
                    order.Add(1);
                    await UniTask.WaitUntil(() => finishFirst, cancellationToken: token);
                    order.Add(2);
                }).Preserve();
                var second = CinematicRuntime.PlayTransitionAsync(preset, _ =>
                {
                    order.Add(3);
                    return UniTask.CompletedTask;
                }).Preserve();
                var canceled = CinematicRuntime.PlayTransitionAsync(preset, _ =>
                {
                    order.Add(99);
                    return UniTask.CompletedTask;
                }, cancellation.Token).Preserve();
                await UniTask.Yield();
                Assert.That(order, Is.EqualTo(new[] { 1 }));
                cancellation.Cancel();
                bool wasCanceled = false;
                try
                {
                    await canceled;
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }

                Assert.That(wasCanceled, Is.True);
                Assert.That(fixture.CurtainGroup.blocksRaycasts, Is.True, "取消排队不能清除其它正在演出的遮幕");
                finishFirst = true;
                await first;
                await second;
                Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(fixture.CurtainGroup.alpha, Is.Zero);
                Assert.That(fixture.CurtainGroup.blocksRaycasts, Is.False);

                bool failed = false;
                try
                {
                    await CinematicRuntime.PlayTransitionAsync(preset, _ => throw new InvalidOperationException("Expected midpoint failure"));
                }
                catch (InvalidOperationException exception)
                {
                    failed = exception.Message == "Expected midpoint failure";
                }

                Assert.That(failed, Is.True);
                Assert.That(fixture.CurtainGroup.alpha, Is.Zero);
                Assert.That(fixture.CurtainGroup.blocksRaycasts, Is.False);

                using var midpointCancellation = new CancellationTokenSource();
                var nonCooperativeMidpoint = new UniTaskCompletionSource();
                try
                {
                    var interrupted = CinematicRuntime.PlayTransitionAsync(preset,
                        _ => nonCooperativeMidpoint.Task, midpointCancellation.Token);
                    Assert.That(fixture.CurtainGroup.blocksRaycasts, Is.True);
                    midpointCancellation.Cancel();
                    bool midpointWasCanceled = false;
                    try
                    {
                        await interrupted;
                    }
                    catch (OperationCanceledException)
                    {
                        midpointWasCanceled = true;
                    }

                    Assert.That(midpointWasCanceled, Is.True, "中点不处理令牌也不能阻塞视觉取消");
                    Assert.That(nonCooperativeMidpoint.Task.Status, Is.EqualTo(UniTaskStatus.Pending), "取消视觉等待不会强杀业务事务");
                    Assert.That(fixture.CurtainGroup.alpha, Is.Zero);
                    Assert.That(fixture.CurtainGroup.blocksRaycasts, Is.False);
                }
                finally
                {
                    nonCooperativeMidpoint.TrySetResult();
                }
            }
            finally
            {
                Time.timeScale = previousScale;
                Object.Destroy(preset);
            }
        }

        [UnityTest]
        public IEnumerator NestedLetterboxRestoresHudAfterOutOfOrderReleaseAndRootShutdown()
        {
            using var fixture = new PresentationFixture();
            var preset = ScriptableObject.CreateInstance<CinematicPreset>();
            var hudObject = new GameObject("Explicit test HUD", typeof(CanvasGroup));
            var hud = hudObject.GetComponent<CanvasGroup>();
            hud.alpha = 0.65f;
            hud.interactable = true;
            hud.blocksRaycasts = true;
            try
            {
                preset.ConfigureLetterbox(2.4f, 0);
                var first = CinematicRuntime.PushLetterbox(preset, new[] { hud });
                var second = CinematicRuntime.PushLetterbox(preset, new[] { hud });
                Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(140).Within(0.001f));
                Assert.That(hud.alpha, Is.Zero);
                first.Dispose();
                Assert.That(hud.alpha, Is.Zero);
                second.Dispose();
                second.Dispose();
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(hud.interactable, Is.True);
                Assert.That(hud.blocksRaycasts, Is.True);
                Assert.That(fixture.TopBar.enabled, Is.False);

                using var cancellation = new CancellationTokenSource();
                var canceledScope = CinematicRuntime.PushLetterbox(preset, new[] { hud }, cancellation.Token);
                cancellation.Cancel();
                yield return null;
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f));
                canceledScope.Dispose();
                var remaining = CinematicRuntime.PushLetterbox(preset, new[] { hud });
                CinematicRuntime.Unregister(fixture.Presenter);
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(CinematicRuntime.IsRegistered, Is.False);
                remaining.Dispose();
                Assert.Throws<InvalidOperationException>(() => CinematicRuntime.PushLetterbox(preset));
            }
            finally
            {
                Object.Destroy(preset);
                Object.Destroy(hudObject);
            }
        }

        [UnityTest]
        public IEnumerator LetterboxExitRestoresLowerOwnerAndSharesConcurrentWaiters()
        {
            yield return RunLetterboxExitAsync().ToCoroutine();
        }

        private static async UniTask RunLetterboxExitAsync()
        {
            using var fixture = new PresentationFixture();
            var lowerPreset = ScriptableObject.CreateInstance<CinematicPreset>();
            var upperPreset = ScriptableObject.CreateInstance<CinematicPreset>();
            var hudObject = new GameObject("Letterbox exit HUD", typeof(CanvasGroup));
            var hud = hudObject.GetComponent<CanvasGroup>();
            hud.alpha = 0.65f;
            hud.interactable = true;
            hud.blocksRaycasts = true;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                lowerPreset.ConfigureLetterbox(2.4f, 0);
                upperPreset.ConfigureLetterbox(3, 0);
                var lower = CinematicRuntime.PushLetterbox(lowerPreset, new[] { hud });
                var upper = CinematicRuntime.PushLetterbox(upperPreset, new[] { hud });
                Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(220).Within(0.001f));
                var firstWaiter = upper.ExitAsync(0.2f);
                var secondWaiter = upper.ExitAsync(1);
                var bothWaiters = UniTask.WhenAll(firstWaiter, secondWaiter);
                if (!GameSettingsService.Current.Accessibility.IsReducedMotion)
                {
                    Assert.That(bothWaiters.Status, Is.EqualTo(UniTaskStatus.Pending), "非零退出时长必须可等待");
                    Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(220).Within(0.001f));
                }

                await UniTask.Yield();
                Assert.That(hud.alpha, Is.Zero, "下层仍隐藏同一 HUD 时，退出上层不能提前显现");
                if (!GameSettingsService.Current.Accessibility.IsReducedMotion && bothWaiters.Status == UniTaskStatus.Pending)
                {
                    Assert.That(fixture.TopBar.rectTransform.rect.height, Is.InRange(140, 220));
                    Assert.That(fixture.Presenter.LetterboxScopeCount, Is.EqualTo(2), "正常退出应保持句柄直到动画结束");
                }

                await bothWaiters;
                Assert.That(fixture.Presenter.LetterboxScopeCount, Is.EqualTo(1));
                Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(140).Within(0.001f));
                Assert.That(hud.alpha, Is.Zero);
                upper.Dispose();
                var hudFadeIn = lower.ExitAsync(0.15f);
                if (!GameSettingsService.Current.Accessibility.IsReducedMotion)
                {
                    Assert.That(hudFadeIn.Status, Is.EqualTo(UniTaskStatus.Pending));
                    Assert.That(hud.alpha, Is.Zero, "正常退出从当前隐藏状态开始淡入");
                }

                await UniTask.Yield();
                Assert.That(hud.alpha, Is.InRange(0, 0.65f));
                await hudFadeIn;
                Assert.That(fixture.Presenter.LetterboxScopeCount, Is.Zero);
                Assert.That(fixture.TopBar.enabled, Is.False);
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(hud.interactable, Is.True);
                Assert.That(hud.blocksRaycasts, Is.True);

                // 进入尚未结束就退出，不允许旧进入动画再次写回黑边或 HUD。
                upperPreset.ConfigureLetterbox(3, 0.1f);
                var interruptedEnter = CinematicRuntime.PushLetterbox(upperPreset, new[] { hud });
                await interruptedEnter.ExitAsync();
                await UniTask.Delay(TimeSpan.FromSeconds(0.12f), ignoreTimeScale: true);
                Assert.That(fixture.TopBar.enabled, Is.False);
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f));

                using var cancellation = new CancellationTokenSource();
                lower = CinematicRuntime.PushLetterbox(lowerPreset, new[] { hud });
                var canceledExit = lower.ExitAsync(1, cancellation.Token);
                cancellation.Cancel();
                Assert.That(hud.alpha, Is.EqualTo(0.65f).Within(0.00001f), "主线程取消退出立即恢复 HUD");
                bool wasCanceled = false;
                try
                {
                    await canceledExit;
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }

                Assert.That(wasCanceled || GameSettingsService.Current.Accessibility.IsReducedMotion, Is.True);
                Assert.That(fixture.Presenter.LetterboxScopeCount, Is.Zero);
            }
            finally
            {
                Time.timeScale = previousScale;
                Object.Destroy(lowerPreset);
                Object.Destroy(upperPreset);
                Object.Destroy(hudObject);
            }
        }

        private sealed class PresentationFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Material material;
            internal GameObject Root => root;
            internal readonly CinematicPresenter Presenter;
            internal readonly CanvasGroup CurtainGroup;
            internal readonly Image Curtain;
            internal readonly Image TopBar;
            internal readonly CanvasGroup TitleGroup;
            internal readonly TMP_Text Title;
            internal readonly TMP_Text Subtitle;

            internal PresentationFixture()
            {
                CinematicRuntime.Shutdown();
                root = new GameObject("Cinematic runtime test", typeof(RectTransform));
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Presenter = root.AddComponent<CinematicPresenter>();
                var curtainRoot = Child("Curtain");
                CurtainGroup = curtainRoot.gameObject.AddComponent<CanvasGroup>();
                Curtain = curtainRoot.gameObject.AddComponent<Image>();
                var barContainer = Child("Bars");
                TopBar = Child("Top bar", barContainer).gameObject.AddComponent<Image>();
                var bottom = Child("Bottom bar", barContainer).gameObject.AddComponent<Image>();
                var titleRoot = Child("Title card");
                TitleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
                Title = Child("Title", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                Subtitle = Child("Subtitle", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                var shader = Shader.Find("UI/CinematicCurtain");
                Assert.That(shader, Is.Not.Null, "需要导入正式遮幕 Shader");
                material = new Material(shader);
                Presenter.Initialize(CurtainGroup, Curtain, barContainer, TopBar, bottom, TitleGroup, Title, Subtitle, material);
                CinematicRuntime.Register(Presenter);
            }

            private RectTransform Child(string name, Transform parent = null)
            {
                var item = new GameObject(name, typeof(RectTransform));
                var rect = item.GetComponent<RectTransform>();
                rect.SetParent(parent == null ? root.transform : parent, false);
                rect.sizeDelta = new Vector2(1920, 1080);
                return rect;
            }

            public void Dispose()
            {
                Presenter.Release();
                Object.Destroy(root);
                Object.Destroy(material);
            }
        }
    }
}
