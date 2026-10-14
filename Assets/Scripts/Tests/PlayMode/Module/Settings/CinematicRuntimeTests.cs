using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        public IEnumerator FrozenTitleUsesLocalizedFontIdsAndClearsAfterCompletionAndReset()
        {
            yield return RunTitleCaseAsync().ToCoroutine();
        }

        private static async UniTask RunTitleCaseAsync()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameSettingsConfig>("Assets/" + GameSettingsService.ConfigAddress + ".asset");
            Assert.That(config, Is.Not.Null);
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
                    bool completed = false;
                    fixture.Presenter.ShowTitle("Presentation.Chapter", "Presentation.Location", 0, 0.1f, 0.01f, () => completed = true);
                    AssertTitleMapping(fixture.Title, config, "Presentation.Chapter");
                    AssertTitleMapping(fixture.Subtitle, config, "Presentation.Location");
                    await WaitAsync(() => fixture.TitleGroup.alpha >= 1);
                    await WaitAsync(() => completed);
                    AssertTitleCleared(fixture);
                }

                bool stoppedCompletion = false;
                fixture.Presenter.ShowTitle("Presentation.Chapter", "Presentation.Location", 0, 10, 0, () => stoppedCompletion = true);
                Assert.Throws<InvalidOperationException>(() => fixture.Presenter.ShowTitle("Presentation.Location"));
                AssertTitleMapping(fixture.Title, config, "Presentation.Chapter");
                fixture.Presenter.ResetEffects();
                AssertTitleCleared(fixture);
                Assert.That(stoppedCompletion, Is.False, "停止字幕不能伪造播放完成。");
                Assert.Throws<ArgumentException>(() => fixture.Presenter.ShowTitle(string.Empty));
                AssertTitleCleared(fixture);

                fixture.Presenter.ShowTitle("Presentation.Chapter", fadeIn: 0, hold: 0, fadeOut: 0,
                    onComplete: () => fixture.Presenter.ShowTitle("Presentation.Location", fadeIn: 0, hold: 10, fadeOut: 0));
                AssertTitleMapping(fixture.Title, config, "Presentation.Location");
                Assert.That(fixture.Presenter.IsTitlePlaying, Is.True, "旧字幕完成清理不能擦掉回调中新开始的字幕。");
                fixture.Presenter.Release();
                AssertTitleCleared(fixture);
                Assert.That(fixture.Presenter.IsInitialized, Is.False);
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
            Assert.That(fixture.Presenter.IsTitlePlaying, Is.False);
            Assert.That(fixture.Title.text, Is.Empty);
            Assert.That(fixture.Subtitle.text, Is.Empty);
            Assert.That(fixture.TitleGroup.alpha, Is.Zero);
            Assert.That(fixture.TitleGroup.interactable, Is.False);
            Assert.That(fixture.TitleGroup.blocksRaycasts, Is.False);
        }
#endif

        [UnityTest, Category("RequiresGPU")]
        public IEnumerator CurtainRendersAllPatternsAndCoverageBoundaries()
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
                fixture.Frame.anchorMin = Vector2.zero;
                fixture.Frame.anchorMax = Vector2.one;
                fixture.Frame.offsetMin = fixture.Frame.offsetMax = Vector2.zero;
                foreach (var item in fixture.Root.GetComponentsInChildren<Transform>(true))
                {
                    item.gameObject.layer = 31;
                }
                fixture.TitleGroup.gameObject.SetActive(false);
                Canvas.ForceUpdateCanvases();
                fixture.Presenter.RefreshViewport();
                Directory.CreateDirectory(evidence);
                foreach (CinematicTransitionStyle style in Enum.GetValues(typeof(CinematicTransitionStyle)))
                {
                    var directions = style == CinematicTransitionStyle.Wipe
                        ? (CinematicWipeDirection[])Enum.GetValues(typeof(CinematicWipeDirection))
                        : new[] { CinematicWipeDirection.LeftToRight };
                    foreach (var direction in directions)
                    {
                        foreach (float progress in new[] { 0f, style == CinematicTransitionStyle.Iris ? 0.65f : 0.5f, 1f })
                        {
                            fixture.Presenter.ResetEffects();
                            fixture.Presenter.CloseCurtain(style, 1, ease: Ease.Linear, direction: direction,
                                color: Color.black, blindCount: 4, pixelColumns: 16);
                            // 定位实际 UI 动画，不写 Shader 属性，覆盖完整表现路径。
                            Assert.That(DOTween.Goto(fixture.Presenter, progress, false), Is.GreaterThan(0));
                            Canvas.ForceUpdateCanvases();
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
                    Covered(0.1f, 0.5f);
                    Covered(0.9f, 0.5f);
                    Open(0.5f, 0.1f);
                    break;
                case CinematicTransitionStyle.DoorsVertical:
                    Covered(0.5f, 0.1f);
                    Covered(0.5f, 0.9f);
                    Open(0.1f, 0.5f);
                    break;
                case CinematicTransitionStyle.Iris:
                    // 长方形画面上相同像素半径应有相同覆盖，不应变成按 UV 拉伸的椭圆。
                    Open(0.5f, 0.5f);
                    Covered(0.05f, 0.05f);
                    Open(0.65625f, 0.5f);
                    Open(0.5f, 0.8125f);
                    Covered(0.7265625f, 0.5f);
                    Covered(0.5f, 0.953125f);
                    break;
                case CinematicTransitionStyle.Blinds:
                    Covered(0.05f, 0.5f);
                    Open(0.18f, 0.5f);
                    Covered(0.3f, 0.5f);
                    Open(0.43f, 0.5f);
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
        public IEnumerator TransitionCallbacksRejectBusyAndRecoverFromResetAndMidpointFailure()
        {
            yield return RunTransitionCaseAsync().ToCoroutine();
        }

        private static async UniTask RunTransitionCaseAsync()
        {
            using var fixture = new PresentationFixture();
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                Assert.Throws<ArgumentException>(() => fixture.Presenter.CloseCurtain(color: new Color(0, 0, 0, 0.5f)));
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Presenter.CloseCurtain(duration: float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Presenter.ShowLetterbox(float.PositiveInfinity));
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Presenter.ShowTitle("Presentation.Chapter", hold: float.NaN));
                var order = new List<int>();
                fixture.Presenter.StartTransition(() =>
                {
                    order.Add(1);
                    Assert.That(fixture.TransitionGroup.alpha, Is.EqualTo(1));
                    Assert.That(fixture.TransitionGroup.blocksRaycasts, Is.True);
                }, 0.15f, () => order.Add(2), closeDuration: 0, openDuration: 0);
                await WaitAsync(() => order.Count == 1);
                Assert.Throws<InvalidOperationException>(() => fixture.Presenter.StartTransition());
                Assert.Throws<InvalidOperationException>(() => fixture.Presenter.CloseCurtain());
                Assert.Throws<InvalidOperationException>(() => fixture.Presenter.OpenCurtain());
                Assert.That(order, Is.EqualTo(new[] { 1 }));
                await WaitAsync(() => order.Count == 2);
                Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
                AssertCurtainCleared(fixture);

                bool closed = false;
                fixture.Presenter.CloseCurtain(duration: 0, onComplete: () => closed = true);
                Assert.That(closed, Is.True);
                Assert.That(fixture.Presenter.IsTransitioning, Is.False, "闭幕完成后允许业务显式开幕。");
                Assert.That(fixture.TransitionGroup.alpha, Is.EqualTo(1));
                Assert.That(fixture.TransitionGroup.blocksRaycasts, Is.True);
                fixture.Presenter.OpenCurtain(0);
                AssertCurtainCleared(fixture);

                fixture.Presenter.StartTransition(closeDuration: 0, openDuration: 0,
                    onComplete: () => fixture.Presenter.StartTransition(closedHold: 10, closeDuration: 0, openDuration: 0));
                Assert.That(fixture.Presenter.IsTransitioning, Is.True, "完成回调中的新演出不能被旧动画清理。");
                Assert.That(fixture.TransitionGroup.blocksRaycasts, Is.True);
                fixture.Presenter.ResetEffects();

                bool stoppedCompletion = false;
                fixture.Presenter.StartTransition(closedHold: 10, closeDuration: 0, openDuration: 0,
                    onComplete: () => stoppedCompletion = true);
                fixture.Presenter.ResetEffects();
                await UniTask.Yield();
                AssertCurtainCleared(fixture);
                Assert.That(stoppedCompletion, Is.False);

                fixture.Presenter.StartTransition(() =>
                {
                    fixture.Presenter.ResetEffects();
                    fixture.Presenter.StartTransition(closedHold: 10, closeDuration: 0, openDuration: 0);
                }, onComplete: () => stoppedCompletion = true, closeDuration: 0, openDuration: 0);
                Assert.That(fixture.Presenter.IsTransitioning, Is.True, "中点停止后重入不能被旧演出清理。");
                Assert.That(fixture.TransitionGroup.blocksRaycasts, Is.True);
                Assert.That(stoppedCompletion, Is.False);
                fixture.Presenter.ResetEffects();

                Exception failure = null;
                fixture.Presenter.StartTransition(() => throw new InvalidOperationException("Expected midpoint failure"),
                    onComplete: () => stoppedCompletion = true, closeDuration: 0, openDuration: 0,
                    onFailed: exception =>
                    {
                        failure = exception;
                        AssertCurtainCleared(fixture);
                        fixture.Presenter.StartTransition(closedHold: 10, closeDuration: 0, openDuration: 0);
                    });
                Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                Assert.That(failure.Message, Is.EqualTo("Expected midpoint failure"));
                Assert.That(fixture.Presenter.IsTransitioning, Is.True, "失败回调中新演出不能被旧流程清理。");
                Assert.That(stoppedCompletion, Is.False);
                fixture.Presenter.Release();
                AssertCurtainCleared(fixture);
                Assert.That(fixture.Presenter.IsInitialized, Is.False);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
        }

        [UnityTest]
        public IEnumerator LetterboxReplacementAndHideTrackTheCurrentViewport()
        {
            yield return RunLetterboxCaseAsync().ToCoroutine();
        }

        private static async UniTask RunLetterboxCaseAsync()
        {
            using var fixture = new PresentationFixture();
            fixture.Presenter.ShowLetterbox(0.1f, 0);
            Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(108).Within(0.001f));
            Assert.That(fixture.Presenter.IsLetterboxVisible, Is.True);
            Assert.That(fixture.TopBar.raycastTarget, Is.False);
            Assert.That(fixture.BottomBar.raycastTarget, Is.False);
            bool replacedCompletion = false;
            fixture.Presenter.ShowLetterbox(0.2f, 1, () => replacedCompletion = true);
            bool hidden = false;
            fixture.Presenter.HideLetterbox(0, () => hidden = true);
            Assert.That(hidden, Is.True, "零时长完成回调可以同步执行。");
            Assert.That(DOTween.IsTweening(fixture.Presenter), Is.False);
            Assert.That(replacedCompletion, Is.False);
            Assert.That(fixture.Presenter.IsLetterboxVisible, Is.False);
            Assert.That(fixture.TopBar.enabled, Is.False);
            Assert.That(fixture.BottomBar.enabled, Is.False);

            var viewport = fixture.CreateViewport(new Vector2(1080, 1920));
            fixture.Presenter.SetViewport(viewport);
            fixture.Presenter.ShowLetterboxForAspect(2.4f, 0);
            Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(735).Within(0.001f));
            viewport.sizeDelta = new Vector2(1920, 1080);
            fixture.Presenter.RefreshViewport();
            Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(140).Within(0.001f));
            fixture.Presenter.ShowLetterbox(0.1f, 0);
            viewport.sizeDelta = new Vector2(1000, 1000);
            fixture.Presenter.RefreshViewport();
            Assert.That(fixture.TopBar.rectTransform.rect.height, Is.EqualTo(100).Within(0.001f));
            Assert.That(fixture.BottomBar.rectTransform.rect.height, Is.EqualTo(100).Within(0.001f));
            fixture.Presenter.HideLetterbox(0.04f);
            await WaitAsync(() => !fixture.Presenter.IsLetterboxVisible);
            Assert.That(fixture.TopBar.enabled, Is.False);
            Assert.That(fixture.BottomBar.enabled, Is.False);
        }

        private static void AssertCurtainCleared(PresentationFixture fixture)
        {
            Assert.That(fixture.Presenter.IsTransitioning, Is.False);
            Assert.That(fixture.TransitionGroup.alpha, Is.Zero);
            Assert.That(fixture.TransitionGroup.blocksRaycasts, Is.False);
            Assert.That(fixture.Curtain.raycastTarget, Is.False);
        }

        private static async UniTask WaitAsync(Func<bool> predicate)
        {
            await UniTask.WaitUntil(predicate).Timeout(TimeSpan.FromSeconds(3));
        }

        private sealed class PresentationFixture : IDisposable
        {
            private readonly Material material;
            private readonly Sprite whiteSprite;
            private readonly FieldInfo settingsField;
            private readonly GameSettingsSnapshot previousSettings;
            internal readonly GameObject Root;
            internal readonly RectTransform Frame;
            internal readonly CinematicPresenter Presenter;
            internal readonly CanvasGroup TransitionGroup;
            internal readonly Image Curtain;
            internal readonly Image TopBar;
            internal readonly Image BottomBar;
            internal readonly CanvasGroup TitleGroup;
            internal readonly TMP_Text Title;
            internal readonly TMP_Text Subtitle;

            internal PresentationFixture()
            {
                settingsField = typeof(GameSettingsService).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(settingsField, Is.Not.Null);
                previousSettings = (GameSettingsSnapshot)settingsField.GetValue(null);
                var settings = previousSettings.Copy();
                settings.Accessibility.ReducedMotion = ReducedMotionMode.Off;
                settingsField.SetValue(null, settings);
                Root = new GameObject("Cinematic presenter test", typeof(RectTransform));
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Frame = Child("Frame");
                Presenter = Root.AddComponent<CinematicPresenter>();
                var transitionRoot = Child("Transition", Frame);
                Stretch(transitionRoot);
                TransitionGroup = transitionRoot.gameObject.AddComponent<CanvasGroup>();
                Curtain = Child("Curtain", transitionRoot).gameObject.AddComponent<Image>();
                whiteSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f);
                Curtain.sprite = whiteSprite;
                Stretch(Curtain.rectTransform);
                var first = Child("First door", transitionRoot).gameObject.AddComponent<Image>();
                var second = Child("Second door", transitionRoot).gameObject.AddComponent<Image>();
                first.raycastTarget = second.raycastTarget = false;
                TopBar = Child("Top bar", Frame).gameObject.AddComponent<Image>();
                BottomBar = Child("Bottom bar", Frame).gameObject.AddComponent<Image>();
                TopBar.rectTransform.anchorMin = new Vector2(0, 1);
                TopBar.rectTransform.anchorMax = Vector2.one;
                TopBar.rectTransform.pivot = new Vector2(0.5f, 1);
                BottomBar.rectTransform.anchorMin = Vector2.zero;
                BottomBar.rectTransform.anchorMax = new Vector2(1, 0);
                BottomBar.rectTransform.pivot = new Vector2(0.5f, 0);
                var titleRoot = Child("Title card", Frame);
                TitleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
                Title = Child("Title", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                Subtitle = Child("Subtitle", titleRoot).gameObject.AddComponent<TextMeshProUGUI>();
                var shader = Shader.Find("UI/CinematicCurtain");
                Assert.That(shader, Is.Not.Null, "需要导入正式遮幕 Shader");
                material = new Material(shader);
                SetField("frame", Frame);
                SetField("transitionGroup", TransitionGroup);
                SetField("curtain", Curtain);
                SetField("doorFirst", first);
                SetField("doorSecond", second);
                SetField("topBar", TopBar);
                SetField("bottomBar", BottomBar);
                SetField("titleGroup", TitleGroup);
                SetField("title", Title);
                SetField("subtitle", Subtitle);
                SetField("maskTemplate", material);
                Presenter.Initialize();
            }

            internal RectTransform CreateViewport(Vector2 size)
            {
                var viewport = Child("Viewport");
                viewport.sizeDelta = size;
                return viewport;
            }

            private void SetField(string name, object value)
            {
                var field = typeof(CinematicPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "缺少制作字段：" + name);
                field.SetValue(Presenter, value);
            }

            private RectTransform Child(string name, Transform parent = null)
            {
                var item = new GameObject(name, typeof(RectTransform));
                var rect = item.GetComponent<RectTransform>();
                rect.SetParent(parent == null ? Root.transform : parent, false);
                rect.sizeDelta = new Vector2(1920, 1080);
                return rect;
            }

            private static void Stretch(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            public void Dispose()
            {
                Presenter.Release();
                settingsField.SetValue(null, previousSettings);
                Object.Destroy(Root);
                Object.Destroy(material);
                Object.Destroy(whiteSprite);
            }
        }
    }
}
