#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class CommonTipsPlayModeTests
    {
        private sealed class Clock : ITimeSource { public long UtcNowMilliseconds { get; set; } }
        private readonly List<GameObject> objects = new List<GameObject>();
        private IResourceService previousResources;
        private Func<View, UniTask> previousBeforeOpen;
        private readonly Clock clock = new Clock();

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            yield return UIManager.Instance.CloseAllAsync().ToCoroutine();
            previousResources = ResourceServices.Default;
            previousBeforeOpen = UIManager.Instance.OnBeforeOpen;
            ResourceServices.RegisterDefault(new EditorPrefabService());
            yield return UIManager.Instance.InitializeAsync().ToCoroutine();
            TimeUtil.SetTimeSource(clock);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            TimeUtil.SetTimeSource(null);
            UIManager.Instance.OnBeforeOpen = previousBeforeOpen;
            yield return TipsUI.HideSimpleAsync().ToCoroutine();
            yield return TipsUI.HideAsync().ToCoroutine();
            yield return UIManager.Instance.CloseAllAsync().ToCoroutine();
            ResourceServices.RegisterDefault(previousResources);
            foreach (var value in objects) if (value != null) Object.Destroy(value);
            objects.Clear();
            yield return null;
        }

        [UnityTest] public IEnumerator CountdownImmediatelyRefreshesAndCompletesOnceDuringPause()
        {
            var countdown = Countdown(out TMP_Text text);
            int completed = 0; countdown.Completed += () => completed++;
            clock.UtcNowMilliseconds = 1000;
            countdown.StartCountdown(2001, text: text);
            Assert.That(text.text, Is.EqualTo("2秒"));
            Time.timeScale = 0;
            clock.UtcNowMilliseconds = 2000;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(text.text, Is.EqualTo("1秒"));
            clock.UtcNowMilliseconds = 2001;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(text.text, Is.EqualTo("0秒"));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(countdown.IsRunning, Is.False);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CountdownReentryCancellationAndDisableDoNotFinishOldRound()
        {
            var countdown = Countdown(out TMP_Text text);
            int completed = 0;
            countdown.Completed += () => { completed++; if (completed == 1) countdown.StartCountdown(5000, text: text); };
            clock.UtcNowMilliseconds = 1000;
            countdown.StartCountdown(900, text: text);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(countdown.IsRunning, Is.True);
            Assert.That(text.text, Is.EqualTo("4秒"));
            countdown.StartCountdown(6000, text: text);
            countdown.StopCountdown();
            clock.UtcNowMilliseconds = 7000;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(completed, Is.EqualTo(1));
            clock.UtcNowMilliseconds = 1000;
            countdown.StartCountdown(3000, text: text);
            countdown.gameObject.SetActive(false);
            clock.UtcNowMilliseconds = 4000;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(countdown.IsRunning, Is.False);
            Assert.That(completed, Is.EqualTo(1));
            countdown.gameObject.SetActive(true);
            countdown.StartCountdown(5000, text: text);
            Object.Destroy(countdown.gameObject);
            clock.UtcNowMilliseconds = 6000;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MessageReplacementKeepsNewTextBeyondOldExpiryAndExpiresDuringPause()
        {
            yield return Show(TipsUI.ShowAsync("旧提示", CommonTipsType.Warning, .1f));
            yield return new WaitForSecondsRealtime(.04f);
            Time.timeScale = 0;
            yield return Show(TipsUI.ShowAsync("新提示", CommonTipsType.Success, .3f));
            var view = UIManager.Instance.Get<CommonTipsView>();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.gameObject.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo("新提示"));
            Assert.That(view.gameObject.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.Null, "Widget 不进入 Page/Modal 返回栈");
            yield return UIManager.Instance.BackAsync(false).ToCoroutine();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator ThreeMessageIconsHaveStableReferencesAndDoNotTakeFocus()
        {
            Button selected = Anchor();
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            Sprite previous = null;
            foreach (CommonTipsType type in new[] { CommonTipsType.Warning, CommonTipsType.Success, CommonTipsType.Notice })
            {
                yield return Show(TipsUI.ShowAsync("状态 " + type, type, 5));
                var view = UIManager.Instance.Get<CommonTipsView>();
                Image icon = Array.Find(view.gameObject.GetComponentsInChildren<Image>(), image => image.name == "Icon");
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.sprite, Is.Not.SameAs(previous));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
                previous = icon.sprite;
            }
        }

        [UnityTest] public IEnumerator SimpleOutsideClickAndCancelRestoreOriginalSelection()
        {
            Button selected = Anchor();
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            yield return Show(TipsUI.ShowSimpleAsync(selected.transform as RectTransform, "说明", "标题"));
            yield return null;
            yield return null;
            var view = UIManager.Instance.Get<SimpleTipsView>();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            view.gameObject.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
            yield return Show(TipsUI.ShowSimpleAsync(selected.transform as RectTransform, "再打开"));
            yield return null;
            var data = new BaseEventData(EventSystem.current);
            view.gameObject.GetComponent<UIMenuScope>().OnCancel(data);
            yield return null;
            Assert.That(data.used, Is.True);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator SimpleFollowsArbitraryPivotThenClosesWhenTargetDestroyed()
        {
            Button target = Anchor();
            RectTransform rect = target.transform as RectTransform;
            rect.pivot = new Vector2(.1f, .9f);
            yield return Show(TipsUI.ShowSimpleAsync(rect, "目标中心不依赖 Pivot"));
            var view = UIManager.Instance.Get<SimpleTipsView>();
            UITipsPanel panel = view.gameObject.GetComponent<UITipsPanel>();
            Vector3 old = panel.Body.position;
            rect.anchoredPosition += new Vector2(160, 0);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(old, panel.Body.position), Is.GreaterThan(1));
            Object.Destroy(target.gameObject);
            yield return null;
            yield return null;
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator SimpleLongContentScrollsAndShortContentShrinksAtSameFontSize()
        {
            RectTransform target = Anchor().transform as RectTransform;
            string longText = string.Concat(System.Linq.Enumerable.Repeat("这是一段很长的说明，用于验证正文换行和安全区域内的滚动。\n", 70));
            yield return Show(TipsUI.ShowSimpleAsync(target, longText, "长标题与正文尺寸验证", new SimpleTipsOptions(maxWidth: 360)));
            yield return null;
            var view = UIManager.Instance.Get<SimpleTipsView>();
            var panel = view.gameObject.GetComponent<UITipsPanel>();
            Rect safe = TooltipPlacementUtil.GetSafeRect(view.transform as RectTransform);
            Assert.That(panel.Body.rect.width, Is.LessThanOrEqualTo(360.1f));
            Assert.That(panel.Body.rect.height, Is.LessThanOrEqualTo(safe.height + .1f));
            Assert.That(panel.ScrollDistance, Is.GreaterThan(0));
            float height = panel.Body.rect.height;
            TMP_Text content = Array.Find(view.gameObject.GetComponentsInChildren<TMP_Text>(), text => text.name == "Text");
            Assert.That(content.fontSize, Is.EqualTo(24));
            yield return Show(TipsUI.ShowSimpleAsync(target, "短说明", options: new SimpleTipsOptions(maxWidth: 360)));
            yield return null;
            Assert.That(panel.Body.rect.height, Is.LessThan(height));
            Assert.That(panel.Body.rect.width, Is.LessThan(360));
            Assert.That(panel.ScrollDistance, Is.LessThan(.1f));
            Assert.That(content.fontSize, Is.EqualTo(24));
        }

        [UnityTest] public IEnumerator OldTriggerCannotCloseNewOwnersTipAndHoverIsNonBlocking()
        {
            Button first = Anchor();
            var trigger = first.gameObject.AddComponent<SimpleTipsTrigger>();
            trigger.SetContent("第一个目标");
            trigger.Show();
            yield return null;
            Button second = Anchor();
            yield return Show(TipsUI.ShowSimpleAsync(second.transform as RectTransform, "第二个目标", options: new SimpleTipsOptions(closeOnOutside: false)));
            trigger.Hide();
            yield return null;
            var view = UIManager.Instance.Get<SimpleTipsView>();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.gameObject.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(view.gameObject.GetComponent<UIMenuScope>().enabled, Is.False);
        }

        [UnityTest] public IEnumerator SimpleClosesWhenTargetIsClippedByScrollViewport()
        {
            Button target = Anchor();
            var viewport = new GameObject("TargetViewport", typeof(RectTransform), typeof(RectMask2D)); objects.Add(viewport);
            viewport.transform.SetParent(UIRootManager.Instance.GetRoot(UILayer.Base), false);
            var viewportRect = viewport.transform as RectTransform;
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(.5f, .5f); viewportRect.sizeDelta = new Vector2(160, 90);
            target.transform.SetParent(viewport.transform, false);
            yield return Show(TipsUI.ShowSimpleAsync(target.transform as RectTransform, "列表说明"));
            var view = UIManager.Instance.Get<SimpleTipsView>();
            (target.transform as RectTransform).anchoredPosition = new Vector2(0, 300);
            yield return null;
            yield return null;
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator CanceledShowWaitDoesNotDisableVisibleTipsCloseButton()
        {
            using (var source = new CancellationTokenSource())
            {
                yield return Show(TipsUI.ShowSimpleAsync(Anchor().transform as RectTransform, "可关闭的提示", cancellationToken: source.Token));
                var view = UIManager.Instance.Get<SimpleTipsView>();
                source.Cancel();
                view.gameObject.GetComponentInChildren<Button>().onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            }
        }

        [UnityTest] public IEnumerator LongMessageScrollsBeforeStayTimerStarts()
        {
            yield return Show(TipsUI.ShowAsync("第一行说明\n第二行说明\n第三行说明\n第四行说明\n第五行说明\n第六行说明", CommonTipsType.Notice, .2f));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var rect = view.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(800, 180);
            yield return null;
            var panel = view.gameObject.GetComponent<UITipsPanel>();
            Assert.That(panel.ScrollDistance, Is.GreaterThan(0));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible), "长消息不能按短文本时长提前关闭");
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(panel.ScrollProgress, Is.GreaterThan(0));
            float remaining = panel.ScrollDistance / 48 + 1;
            yield return new WaitForSecondsRealtime(remaining);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator PreCanceledRequestsKeepVisibleTipsAndTheirOwners()
        {
            var target = Anchor().transform as RectTransform;
            yield return Show(TipsUI.ShowSimpleAsync(target, "保留说明"));
            yield return Show(TipsUI.ShowAsync("保留消息", CommonTipsType.Success, 5));
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                UIOperationResult canceled = default;
                yield return TipsUI.ShowSimpleAsync(target, "已取消", cancellationToken: source.Token).ToCoroutine(value => canceled = value);
                Assert.That(canceled.Status, Is.EqualTo(UIOperationStatus.Canceled));
                yield return TipsUI.ShowAsync("已取消", CommonTipsType.Warning, cancellationToken: source.Token).ToCoroutine(value => canceled = value);
                Assert.That(canceled.Status, Is.EqualTo(UIOperationStatus.Canceled));
                Assert.That(UIManager.Instance.Get<CommonTipsView>().State, Is.EqualTo(ViewState.Visible));
                var simple = UIManager.Instance.Get<SimpleTipsView>();
                simple.gameObject.GetComponentInChildren<Button>().onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(simple.State, Is.EqualTo(ViewState.LoadedHidden));
            }
        }

        [UnityTest] public IEnumerator CanceledQueuedReplacementDoesNotLeaveOldBlocker()
        {
            var target = Anchor().transform as RectTransform;
            yield return Show(TipsUI.ShowSimpleAsync(target, "旧说明"));
            var gate = new UniTaskCompletionSource();
            UIManager.Instance.OnBeforeOpen = async view => { if (view is CommonTipsView) await gate.Task; };
            var blockingOperation = TipsUI.ShowAsync("另一项导航", CommonTipsType.Notice, 5);
            yield return null;
            using (var source = new CancellationTokenSource())
            {
                var replacement = TipsUI.ShowSimpleAsync(target, "排队说明", cancellationToken: source.Token);
                source.Cancel();
                gate.TrySetResult();
                yield return Show(blockingOperation);
                UIOperationResult canceled = default;
                yield return replacement.ToCoroutine(value => canceled = value);
                Assert.That(canceled.Status, Is.EqualTo(UIOperationStatus.Canceled));
                Assert.That(UIManager.Instance.Get<SimpleTipsView>().State, Is.EqualTo(ViewState.LoadedHidden));
            }
        }

        [UnityTest] public IEnumerator MessageLifetimeCancellationClosesWithoutWaitingForDuration()
        {
            using (var source = new CancellationTokenSource())
            {
                yield return Show(TipsUI.ShowAsync("取消后关闭", CommonTipsType.Notice, 30, source.Token));
                var view = UIManager.Instance.Get<CommonTipsView>();
                Time.timeScale = 0;
                source.Cancel();
                yield return null;
                yield return null;
                yield return null;
                Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            }
        }

        private UICountdown Countdown(out TMP_Text text)
        {
            var root = new GameObject("Countdown", typeof(RectTransform)); objects.Add(root);
            text = root.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
            return root.AddComponent<UICountdown>();
        }
        private Button Anchor()
        {
            var root = new GameObject("Anchor", typeof(RectTransform), typeof(Image), typeof(Button)); objects.Add(root);
            root.transform.SetParent(UIRootManager.Instance.GetRoot(UILayer.Base), false);
            var rect = root.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(120, 60);
            return root.GetComponent<Button>();
        }
        private static IEnumerator Show(UniTask<UIOperationResult> operation)
            => operation.ToCoroutine(result => Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded).Or.EqualTo(UIOperationStatus.Ignored), result.Exception?.ToString()));

        // 只替换资源加载边界；使用正式 Prefab、真实 UIManager、Canvas 和 EventSystem。
        private sealed class EditorPrefabService : IResourceService
        {
            public bool IsInitialized => true;
            public string NormalizeAddress(string address) => "Assets/" + address + ".prefab";
            public UniTask InitializeAsync(ResourceInitializeOptions options) => UniTask.CompletedTask;
            public UniTask<DownloadReport> DownloadPackageAsync(int n, int retry, Action<DownloadProgress> progress = null) => throw new NotSupportedException();
            public IResourceLoader CreateLoader() => new EditorPrefabLoader();
            public IResourceSceneLoader CreateSceneLoader() => throw new NotSupportedException();
            public ResourceLoadResult<T> LoadAsset<T>(string address) where T : Object
                => ResourceLoadResult<T>.SuccessResult(AssetDatabase.LoadAssetAtPath<T>(NormalizeAddress(address)), address);
            public UniTask<ResourceLoadResult<T>> LoadAssetAsync<T>(string address) where T : Object => UniTask.FromResult(LoadAsset<T>(address));
            public UniTask<ResourceLoadResult<TextAsset>> LoadTextAssetAsync(string address) => throw new NotSupportedException();
            public void ReleaseAsset(Object asset) { }
        }
        private sealed class EditorPrefabLoader : IResourceLoader
        {
            private readonly List<GameObject> instances = new List<GameObject>();
            public GameObject Instantiate(string address, Transform parent) => Instantiate(address, parent, false);
            public GameObject Instantiate(string address, Transform parent, bool stays)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/" + address + ".prefab");
                var instance = Object.Instantiate(prefab, parent, stays); instances.Add(instance); return instance;
            }
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) => UniTask.FromResult(Instantiate(address, parent));
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool stays) => UniTask.FromResult(Instantiate(address, parent, stays));
            public T LoadAsset<T>(string address) where T : Object => throw new NotSupportedException();
            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object => throw new NotSupportedException();
            public void ReleaseAsset(Object asset) { }
            public void ReleaseInstance(GameObject instance) { instances.Remove(instance); if (instance != null) Object.Destroy(instance); }
            public void Dispose() { foreach (var instance in instances) if (instance != null) Object.Destroy(instance); instances.Clear(); }
        }
    }
}
#endif
