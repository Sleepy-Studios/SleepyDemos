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
            yield return SingleUIManager.Instance.HideSimpleTipsAsync().ToCoroutine();
            yield return SingleUIManager.Instance.HideTipsMessageBarsAsync().ToCoroutine();
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

        [UnityTest] public IEnumerator MessagesStackAndExpireIndependentlyDuringPause()
        {
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("旧提示", CommonTipsType.Warning, .25f));
            Time.timeScale = 0;
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("新提示", CommonTipsType.Success, .6f));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var stack = view.gameObject.GetComponent<UITipsStack>();
            Assert.That(stack.Count, Is.EqualTo(2), "新消息追加，不能覆盖旧消息");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(stack.Count, Is.EqualTo(1));
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.gameObject.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo("新提示"));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.Null, "Widget 不进入 Page/Modal 返回栈");
            yield return UIManager.Instance.BackAsync(false).ToCoroutine();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator ThreeMessageIconsHaveStableReferencesAndDoNotTakeFocus()
        {
            Button selected = Anchor();
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            Sprite previous = null;
            foreach (CommonTipsType type in new[] { CommonTipsType.Warning, CommonTipsType.Success, CommonTipsType.Notice })
            {
                yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("状态 " + type, type, 5));
                var view = UIManager.Instance.Get<CommonTipsView>();
                Image icon = Array.FindLast(view.gameObject.GetComponentsInChildren<Image>(), image => image.name == "Icon");
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.sprite, Is.Not.SameAs(previous));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
                previous = icon.sprite;
            }
        }

        [UnityTest] public IEnumerator HoverExpandsAllMessagesAndPausesRemainingLifetime()
        {
            for (int i = 0; i < 5; i++)
                yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("消息 " + i, CommonTipsType.Success, 1));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var stack = view.gameObject.GetComponent<UITipsStack>();
            yield return new WaitForSecondsRealtime(.2f);
            var panels = view.gameObject.GetComponentsInChildren<UITipsPanel>();
            var latest = panels[panels.Length - 1];
            Assert.That(latest.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("消息 4"));
            Assert.That(latest.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.9f));
            Assert.That(panels[0].GetComponent<CanvasGroup>().alpha, Is.LessThan(.1f), "收起只显示前面三层");
            stack.OnPointerEnter(new PointerEventData(EventSystem.current));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(stack.Expanded, Is.True);
            Assert.That(stack.Count, Is.EqualTo(5));
            for (int i = panels.Length - 2; i >= 0; i--)
            {
                Assert.That(panels[i].GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.9f));
                Assert.That(panels[i].Body.anchoredPosition.y + panels[i + 1].Body.rect.height,
                    Is.LessThan(panels[i + 1].Body.anchoredPosition.y), "展开从新到旧排列，不互相遮挡");
            }
            stack.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(stack.Count, Is.EqualTo(5), "移开后继续剩余时间，不能立即清空");
            yield return new WaitForSecondsRealtime(1);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator CloseButtonOnlyDismissesItsMessageAndKeepsPageFocus()
        {
            Button selected = Anchor();
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("保留", CommonTipsType.Success, 5));
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("关闭这一条", CommonTipsType.Notice, 5));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var panels = view.gameObject.GetComponentsInChildren<UITipsPanel>();
            var close = panels[1].GetComponentInChildren<Button>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            close.OnPointerDown(pointer); close.OnPointerUp(pointer); close.OnPointerClick(pointer);
            yield return null;
            Assert.That(view.gameObject.GetComponent<UITipsStack>().Count, Is.EqualTo(1));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
            Assert.That(view.gameObject.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo("保留"));
            // 通知区域以外没有全屏射线遮挡。
            pointer.position = RectTransformUtility.WorldToScreenPoint(UIRootManager.Instance.UICamera, selected.transform.position);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject.transform.IsChildOf(view.transform)), Is.False);
        }

        [UnityTest] public IEnumerator CancelingOneMessageKeepsOthersAndManyMessagesScrollInsideSafeArea()
        {
            using var source = new CancellationTokenSource();
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("取消这一条", CommonTipsType.Warning, 30, source.Token));
            for (int i = 0; i < 8; i++)
                yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("保留 " + i, CommonTipsType.Notice, 30));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var stack = view.gameObject.GetComponent<UITipsStack>();
            var rect = view.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(420, 280);
            stack.OnPointerEnter(new PointerEventData(EventSystem.current));
            source.Cancel();
            yield return null;
            var scroll = Array.Find(view.gameObject.GetComponentsInChildren<ScrollRect>(), value => value.name == "MessageRegion");
            Rect safe = TooltipPlacementUtil.GetSafeRect(rect);
            Assert.That(scroll.viewport.rect.height, Is.LessThanOrEqualTo(safe.height + .1f), "缩小安全区时动画也不得越界");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(stack.Count, Is.EqualTo(8));
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.viewport.rect.height, Is.LessThanOrEqualTo(safe.height + .1f));
            Assert.That(scroll.viewport.rect.width, Is.LessThanOrEqualTo(safe.width + .1f));
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator EmptyCloseQueuedAfterNewShowCannotCloseTheNewMessage()
        {
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("即将到期", CommonTipsType.Success, .15f));
            var gate = new UniTaskCompletionSource();
            UIManager.Instance.OnBeforeOpen = async view => { if (view is SimpleTipsView) await gate.Task; };
            var blocking = SingleUIManager.Instance.ShowSimpleTipsAsync(Anchor().transform as RectTransform, "阻塞其他导航");
            yield return null;
            var next = SingleUIManager.Instance.ShowTipsMessageBarAsync("不能被旧关闭清掉", CommonTipsType.Notice, 5);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(UIManager.Instance.Get<CommonTipsView>().transform.Find("MessageRegion").gameObject.activeSelf,
                Is.False, "空列表等待导航关闭期间不留下透明挡区");
            gate.TrySetResult();
            yield return Show(blocking);
            yield return Show(next);
            yield return null; yield return null;
            var messages = UIManager.Instance.Get<CommonTipsView>();
            Assert.That(messages.State, Is.EqualTo(ViewState.Visible));
            Assert.That(messages.gameObject.GetComponent<UITipsStack>().Count, Is.EqualTo(1));
            Assert.That(messages.gameObject.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo("不能被旧关闭清掉"));
        }

        [UnityTest] public IEnumerator HideAllCancelsPendingMessagesButAllowsLaterRequests()
        {
            var gate = new UniTaskCompletionSource();
            UIManager.Instance.OnBeforeOpen = async view => { if (view is SimpleTipsView) await gate.Task; };
            var blocking = SingleUIManager.Instance.ShowSimpleTipsAsync(Anchor().transform as RectTransform, "等待");
            yield return null;
            var canceled = SingleUIManager.Instance.ShowTipsMessageBarAsync("排队取消", CommonTipsType.Warning, 5);
            var hide = SingleUIManager.Instance.HideTipsMessageBarsAsync();
            var later = SingleUIManager.Instance.ShowTipsMessageBarAsync("后来请求", CommonTipsType.Success, 5);
            gate.TrySetResult();
            yield return Show(blocking);
            yield return canceled.ToCoroutine(result => Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled)));
            yield return hide.ToCoroutine();
            yield return Show(later);
            var view = UIManager.Instance.Get<CommonTipsView>();
            Assert.That(view.gameObject.GetComponent<UITipsStack>().Count, Is.EqualTo(1));
            Assert.That(view.gameObject.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo("后来请求"));
        }

        [UnityTest] public IEnumerator SimpleOutsideClickAndCancelRestoreOriginalSelection()
        {
            Button selected = Anchor();
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(selected.transform as RectTransform, "说明", "标题"));
            yield return null;
            yield return null;
            var view = UIManager.Instance.Get<SimpleTipsView>();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            view.gameObject.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected.gameObject));
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(selected.transform as RectTransform, "再打开"));
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(rect, "目标中心不依赖 Pivot"));
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(target, longText, "长标题与正文尺寸验证", new SimpleTipsOptions(maxWidth: 360)));
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(target, "短说明", options: new SimpleTipsOptions(maxWidth: 360)));
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(second.transform as RectTransform, "第二个目标", options: new SimpleTipsOptions(closeOnOutside: false)));
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(target.transform as RectTransform, "列表说明"));
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
                yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(Anchor().transform as RectTransform, "可关闭的提示", cancellationToken: source.Token));
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
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("第一行说明\n第二行说明\n第三行说明\n第四行说明\n第五行说明\n第六行说明", CommonTipsType.Notice, .2f));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var rect = view.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(800, 180);
            yield return null;
            var panel = view.gameObject.GetComponentInChildren<UITipsPanel>();
            var autoScroll = panel.GetComponentInChildren<TMPAutoScrollEnableBehaviour>();
            var text = autoScroll.GetComponentInChildren<TextMeshProUGUI>();
            Assert.That(text.text, Does.Not.Contain("\n"));
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(autoScroll.IsReading, Is.True);
            var options = TMPAutoScrollOptions.Default;
            options.StartDelay = .05f; options.EndStayTime = .05f;
            options.PixelsPerSecond = 600; options.Loop = false; options.UseUnscaledTime = true;
            autoScroll.SetOptions(options);
            var stack = view.gameObject.GetComponent<UITipsStack>();
            stack.OnPointerEnter(new PointerEventData(EventSystem.current));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(text.rectTransform.anchoredPosition.x, Is.LessThan(0), "悬停和暂停游戏时文字仍滚动");
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            float deadline = Time.realtimeSinceStartup + 8;
            while (autoScroll.IsReading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(autoScroll.IsReading, Is.False, "本轮必须完成，不能因布局反馈反复重启");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible), "悬停仍阻止自动消失");
            stack.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator ChangingTextOrWidthRestartsReadingAndFullStayDuration()
        {
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("短消息", CommonTipsType.Notice, .5f));
            var view = UIManager.Instance.Get<CommonTipsView>();
            var panel = view.gameObject.GetComponentInChildren<UITipsPanel>();
            var autoScroll = panel.GetComponentInChildren<TMPAutoScrollEnableBehaviour>();
            var text = autoScroll.GetComponentInChildren<TextMeshProUGUI>();
            yield return new WaitForSecondsRealtime(.25f);
            text.text = "直接修改 TMP 后，这条长消息需要重新阅读并重新计算完整停留时长。";
            var options = TMPAutoScrollOptions.Default;
            options.StartDelay = .01f; options.EndStayTime = .01f;
            options.PixelsPerSecond = 3000; options.Loop = false; options.UseUnscaledTime = true;
            autoScroll.SetOptions(options);
            float deadline = Time.realtimeSinceStartup + 3;
            while (autoScroll.IsReading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(autoScroll.IsReading, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            // 新短文本与安全区变化，不应沿用上一轮已经消耗的停留时间。
            text.text = "新短消息";
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(400, 300);
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
        }

        [UnityTest] public IEnumerator PreCanceledRequestsKeepVisibleTipsAndTheirOwners()
        {
            var target = Anchor().transform as RectTransform;
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(target, "保留说明"));
            yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("保留消息", CommonTipsType.Success, 5));
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                UIOperationResult canceled = default;
                yield return SingleUIManager.Instance.ShowSimpleTipsAsync(target, "已取消", cancellationToken: source.Token).ToCoroutine(value => canceled = value);
                Assert.That(canceled.Status, Is.EqualTo(UIOperationStatus.Canceled));
                yield return SingleUIManager.Instance.ShowTipsMessageBarAsync("已取消", CommonTipsType.Warning, cancellationToken: source.Token).ToCoroutine(value => canceled = value);
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
            yield return Show(SingleUIManager.Instance.ShowSimpleTipsAsync(target, "旧说明"));
            var gate = new UniTaskCompletionSource();
            UIManager.Instance.OnBeforeOpen = async view => { if (view is CommonTipsView) await gate.Task; };
            var blockingOperation = SingleUIManager.Instance.ShowTipsMessageBarAsync("另一项导航", CommonTipsType.Notice, 5);
            yield return null;
            using (var source = new CancellationTokenSource())
            {
                var replacement = SingleUIManager.Instance.ShowSimpleTipsAsync(target, "排队说明", cancellationToken: source.Token);
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
                yield return Show(SingleUIManager.Instance.ShowTipsMessageBarAsync("取消后关闭", CommonTipsType.Notice, 30, source.Token));
                var view = UIManager.Instance.Get<CommonTipsView>();
                Time.timeScale = 0;
                source.Cancel();
                yield return null;
                yield return null;
                yield return null;
                Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            }
        }

        [UnityTest] public IEnumerator MessageLoadFailureReturnsFailedWithoutLeavingAnUnboundView()
        {
            ResourceServices.RegisterDefault(new EditorPrefabService(missingMessagePrefab: true));
            UIOperationResult result = default;
            yield return SingleUIManager.Instance.ShowTipsMessageBarAsync("加载失败", CommonTipsType.Warning)
                .ToCoroutine(value => result = value);
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.Not.Null);
            Assert.That(UIManager.Instance.Get<CommonTipsView>(), Is.Null);
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
            private readonly bool missingMessagePrefab;
            public EditorPrefabService(bool missingMessagePrefab = false) { this.missingMessagePrefab = missingMessagePrefab; }
            public bool IsInitialized => true;
            public string NormalizeAddress(string address) => "Assets/" + address + ".prefab";
            public UniTask InitializeAsync(ResourceInitializeOptions options) => UniTask.CompletedTask;
            public UniTask<DownloadReport> DownloadPackageAsync(int n, int retry, Action<DownloadProgress> progress = null) => throw new NotSupportedException();
            public IResourceLoader CreateLoader() => new EditorPrefabLoader(missingMessagePrefab);
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
            private readonly bool missingMessagePrefab;
            public EditorPrefabLoader(bool missingMessagePrefab) { this.missingMessagePrefab = missingMessagePrefab; }
            public GameObject Instantiate(string address, Transform parent) => Instantiate(address, parent, false);
            public GameObject Instantiate(string address, Transform parent, bool stays)
            {
                if (missingMessagePrefab && address == "LoadResources/UI/Common/CommonTips") return null;
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
