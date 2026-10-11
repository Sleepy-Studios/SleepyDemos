using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime.Presentation;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class CinematicRuntimeTests
    {
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
            internal readonly CinematicPresenter Presenter;
            internal readonly CanvasGroup CurtainGroup;
            internal readonly Image TopBar;

            internal PresentationFixture()
            {
                CinematicRuntime.Shutdown();
                root = new GameObject("Cinematic runtime test", typeof(RectTransform));
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Presenter = root.AddComponent<CinematicPresenter>();
                var curtainRoot = Child("Curtain");
                CurtainGroup = curtainRoot.gameObject.AddComponent<CanvasGroup>();
                var curtain = curtainRoot.gameObject.AddComponent<Image>();
                var barContainer = Child("Bars");
                TopBar = Child("Top bar", barContainer).gameObject.AddComponent<Image>();
                var bottom = Child("Bottom bar", barContainer).gameObject.AddComponent<Image>();
                var titleRoot = Child("Title card");
                var titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
                var title = Child("Title", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                var subtitle = Child("Subtitle", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                var shader = Shader.Find("UI/CinematicCurtain");
                Assert.That(shader, Is.Not.Null, "需要导入正式遮幕 Shader");
                material = new Material(shader);
                Presenter.Initialize(CurtainGroup, curtain, barContainer, TopBar, bottom, titleGroup, title, subtitle, material);
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
