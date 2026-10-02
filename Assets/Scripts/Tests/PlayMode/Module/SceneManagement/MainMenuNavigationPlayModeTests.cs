#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 通过真实指针验证 Hub 失败回滚后的新页面可重试，不调用按钮监听器模拟操作。
    public sealed class MainMenuNavigationPlayModeTests
    {
        private GameSceneNavigator originalNavigator;
        private FailingSceneRuntime runtime;
        private Mouse mouse;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedEntryRestoresNewMenuAndAllowsRetry()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null);
                yield return Wait(() => startup.isDone, "AppEntrance 启动", 90);
            }
            yield return Wait(IsStableHub, "Hub 稳定", 90);
            originalInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            originalNavigator = GameSceneNavigator.Instance;
            runtime = new FailingSceneRuntime();
            SetNavigator(new GameSceneNavigator(runtime, new GameSceneLoadingPresenter()));

            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            // 直接使用保存的绑定，避免把 Prefab 的层级路径变成测试契约。
            var button = Field<Button>(firstMenu, "Button_JinxCasinoButton");
            yield return null; yield return null;
            Vector2 position = ButtonPosition(button);
            yield return Click(position);
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Loading 替换旧 Hub", 15);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.EqualTo(1), "加载期间重复点击不能再发起进入请求。");

            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "加载失败恢复 Hub", 15);
            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            Assert.That(restoredMenu, Is.Not.SameAs(firstMenu));
            var feedback = Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Title");
            Assert.That(feedback.text, Does.Contain("进入失败"));
            var feedbackSize = feedback.GetPreferredValues();
            Assert.That(feedbackSize.x, Is.LessThanOrEqualTo(feedback.rectTransform.rect.width), "失败提示应在已有控件内完整显示。");
            Assert.That(feedbackSize.y, Is.LessThanOrEqualTo(feedback.rectTransform.rect.height), "失败提示不能溢出遮住入口。");
            Assert.That(Field<Button>(restoredMenu, "Button_UIFrameworkValidationButton").interactable, Is.False);
            foreach (string field in new[] { "Button_DroneFlightButton", "Button_DlssButton", "Button_BlockPortersButton", "Button_JinxCasinoButton" })
                Assert.That(Field<Button>(restoredMenu, field).interactable, Is.True, field);

            // 新实例需要经过帧末 Canvas 注册/布局；随后仍验证真实射线并通过 InputSystem 点击。
            yield return null; yield return null;
            yield return Click(ButtonPosition(Field<Button>(restoredMenu, "Button_JinxCasinoButton")));
            yield return Wait(() => runtime.LoadCount == 2, "新 Hub 可以实际点击重试", 15);
            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "重试失败也能恢复", 15);
            Assert.That(Field<Button>(UIManager.Instance.Get<MainMenuView>(), "Button_JinxCasinoButton").interactable, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (runtime?.HasPendingEntry == true)
                {
                    LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
                    runtime.FailEntry();
                    yield return Wait(IsStableHub, "清理待完成的进入请求", 15);
                }
            }
            finally
            {
                if (originalNavigator != null) SetNavigator(originalNavigator);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
            }
        }

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }

        private static Vector2 ButtonPosition(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(position.x, Is.InRange(0, Screen.width));
            Assert.That(position.y, Is.InRange(0, Screen.height));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "实际入口不能被其它 UI 遮挡。");
            return position;
        }

        private static void SetNavigator(GameSceneNavigator navigator) =>
            typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null, navigator);

        private static T Field<T>(object instance, string name) =>
            (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        private static bool IsStableHub() => GameSceneNavigator.Instance != null &&
            GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning &&
            UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible &&
            !UIRootManager.Instance.InteractionGate.IsBlocking && Object.FindFirstObjectByType<StartupLoadingView>() == null;

        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + "超时");
        }

        private sealed class FailingSceneRuntime : IGameSceneRuntime
        {
            private UniTaskCompletionSource<GameSceneRuntimeResult> pending;
            internal int LoadCount { get; private set; }
            internal bool HasPendingEntry => pending != null;

            public async UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress)
            {
                LoadCount++;
                pending = new UniTaskCompletionSource<GameSceneRuntimeResult>();
                return await pending.Task;
            }

            internal void FailEntry()
            {
                var completion = pending;
                pending = null;
                completion.TrySetResult(GameSceneRuntimeResult.Failure("测试场景加载失败"));
            }

            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress) =>
                UniTask.FromResult(GameSceneRuntimeResult.Success());
        }
    }
}
#endif
