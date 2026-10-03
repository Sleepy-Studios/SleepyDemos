using System.Collections;
using System.Collections.Generic;
using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class LoopScrollItemViewBridgeTests
    {
        public sealed class TestItem : ItemView { public static int Created; public TestItem() { Created++; } }
        private sealed class DataItem : ItemView<string>
        {
            private Text label;
            public int RefreshCount { get; private set; }
            protected override void InitComponent() { label = gameObject.GetComponent<Text>(); }
            protected override void RefreshUI()
            {
                Assert.That(IsInitialized, Is.True);
                label.text = params1;
                RefreshCount++;
            }
        }

        [Test]
        public void ItemDataRefreshesAfterBindingAndUsesLatestPendingValue()
        {
            var root = new GameObject("DataItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            try
            {
                var item = new DataItem();
                item.SetData("old");
                item.SetData("latest");
                Assert.That(item.RefreshCount, Is.Zero, "提前接收数据不能访问尚未绑定的控件");
                item.Init(root, 0);
                Assert.That(root.GetComponent<Text>().text, Is.EqualTo("latest"));
                Assert.That(item.RefreshCount, Is.EqualTo(1));
                item.SetData("rebound");
                Assert.That(root.GetComponent<Text>().text, Is.EqualTo("rebound"));
                Assert.That(item.RefreshCount, Is.EqualTo(2));

                var initializedFirst = new DataItem();
                initializedFirst.Init(root, 1);
                Assert.That(initializedFirst.RefreshCount, Is.Zero);
                initializedFirst.SetData(null);
                Assert.That(root.GetComponent<Text>().text, Is.Empty);
                Assert.That(initializedFirst.RefreshCount, Is.EqualTo(1), "空值同样是一次有效的数据提交");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [UnityTest]
        public IEnumerator PhysicalCellReuseUpdatesItemIndexAndCancelsPreviousBinding()
        {
            var root = CreateList(out var list);
            var items = new List<string>(); for (var i = 0; i < 100; i++) items.Add(i.ToString());
            TestItem.Created = 0; var unbound = 0; CellBindContext old = default; TestItem first = null;
            try
            {
                list.ItemViews().Configure<TestItem>();
                list.ItemViews().CellBound += (view, index, context) => { if (index == 0) { first = (TestItem)view; old = context; } Assert.That(view.Index, Is.EqualTo(index)); };
                list.ItemViews().CellUnbound += (view, context) => { Assert.That(context.IsCurrent, Is.False); unbound++; };
                list.SetTotalCount(items, getItemKey: item => (string)item);
                yield return null; var created = TestItem.Created; var token = old.CancellationToken;
                list.ScrollToCell(80); yield return null; list.ScrollToCell(20); yield return null;
                Assert.That(TestItem.Created, Is.LessThanOrEqualTo(created + 3));
                Assert.That(old.IsCurrent, Is.False); Assert.That(token.IsCancellationRequested, Is.True); Assert.That(unbound, Is.GreaterThan(0));
                var clicked = -1; list.ItemViews().CellClicked += (view, index, context) => clicked = index;
                first.TriggerClick(); Assert.That(clicked, Is.EqualTo(first.Index));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [UnityTest]
        public IEnumerator RepeatedRegistrationIsDeduplicatedAndOwnerDisposalUnsubscribes()
        {
            var root = CreateList(out var list); var owner = new View();
            var calls = 0;
            Action<ItemView, int, CellBindContext> callback = (cell, index, context) => calls++;
            try
            {
                owner.RegisterLoopScrollRect(list, callback); owner.RegisterLoopScrollRect(list, callback);
                var items = new List<string> { "a", "b", "c" };
                list.ItemViews().Configure<TestItem>();
                list.SetTotalCount(items, getItemKey: item => (string)item);
                Assert.That(calls, Is.EqualTo(list.ActiveCellCount));
                yield return owner.DestroyAsync().ToCoroutine(); var before = calls;
                list.RefreshCells(); Assert.That(calls, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static GameObject CreateList(out LoopScrollView list, LoopLayout layout = LoopLayout.Vertical, bool menu = false)
        {
            var root = new GameObject("BridgeTest", typeof(RectTransform), typeof(ScrollRect));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>(); viewport.SetParent(root.transform); viewport.sizeDelta = new Vector2(300, 200);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport);
            var template = new GameObject("Cell", typeof(RectTransform), typeof(LoopCell)).GetComponent<LoopCell>(); template.transform.SetParent(root.transform); template.gameObject.SetActive(false);
            if (menu)
            {
                var button = new GameObject("Enter", typeof(RectTransform), typeof(Image), typeof(LoopScrollMenuButton));
                button.transform.SetParent(template.transform, false);
            }
            var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            list = root.AddComponent<LoopScrollView>(); list.Configure(scroll, new[] { new LoopCellPrefab { Type = 0, Prefab = template, Prewarm = 30 } }, layout, new Vector2(menu ? 150 : 300, 40));
            return root;
        }
        [UnityTest]
        public IEnumerator RegisteredFactoryRefreshesAndClicksCurrentIdentity()
        {
            var root = CreateList(out var list); var owner = new View();
            var bound = 0; var hidden = 0; var clicked = -1; var clickCalls = 0; TestItem first = null;
            Action<TestItem, int> bind = (cell, index) => { bound++; first = cell; Assert.That(cell.Index, Is.EqualTo(index)); };
            Action<TestItem, int> click = (cell, index) =>
            { Assert.That(cell.Index, Is.EqualTo(index)); clicked = index; clickCalls++; };
            Action<TestItem> hide = cell => hidden++;
            try
            {
                owner.RegisterLoopScrollRect<TestItem>(list, bind); owner.RegisterLoopScrollRect<TestItem>(list, bind);
                owner.RegisterLoopScrollClick<TestItem>(list, click); owner.RegisterLoopScrollClick<TestItem>(list, click);
                owner.RegisterLoopScrollItemHide<TestItem>(list, hide); owner.RegisterLoopScrollItemHide<TestItem>(list, hide);
                var items = new List<string>(); for (var i = 0; i < 100; i++) items.Add(i.ToString());
                list.SetTotalCount(items, getItemKey: item => (string)item); yield return null;
                Assert.That(bound, Is.EqualTo(list.ActiveCellCount));
                list.ScrollToCell(80); yield return null; first.TriggerClick();
                Assert.That(clicked, Is.EqualTo(first.Index)); Assert.That(clicked, Is.GreaterThan(70));
                Assert.That(hidden, Is.GreaterThan(0)); Assert.That(clickCalls, Is.EqualTo(1));
                var beforeClear = hidden; var active = list.ActiveCellCount; list.SetTotalCount(null);
                Assert.That(hidden - beforeClear, Is.EqualTo(active)); first.TriggerClick();
                Assert.That(clickCalls, Is.EqualTo(1), "回收后点击不得使用过期身份");
                list.SetTotalCount(items, getItemKey: item => (string)item);
                yield return owner.DestroyAsync().ToCoroutine(); var before = bound;
                list.RefreshCells(); Assert.That(bound, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [UnityTest]
        public IEnumerator NestedItemViewCanRegisterAndExplicitlyUnsubscribe()
        {
            var root = CreateList(out var list); var owner = new ItemView(); var calls = 0;
            var subscription = owner.RegisterLoopScrollRect<TestItem>(list, (cell, index) => calls++);
            try
            {
                var items = new List<string> { "a", "b" };
                list.ItemViews().Configure<TestItem>();
                list.SetTotalCount(items, getItemKey: item => (string)item);
                Assert.That(calls, Is.EqualTo(2)); subscription.Dispose(); subscription.Dispose();
                list.RefreshCells(); Assert.That(calls, Is.EqualTo(2)); yield return null;
            }
            finally { subscription.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        [UnityTest]
        public IEnumerator InactiveHostCanSubmitBeforeBridgeAwake()
        {
            var root = CreateList(out var list); root.SetActive(false);
            try
            {
                list.ItemViews().Configure<TestItem>();
                list.SetTotalCount(new List<string> { "a", "b" }, getItemKey: item => (string)item);
                Assert.That(list.Count, Is.EqualTo(2)); Assert.That(list.ActiveCellCount, Is.Zero);
                root.SetActive(true); yield return null; yield return null;
                Assert.That(list.ActiveCellCount, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator GridMenuMovesByLaneAndRecycledButtonCannotSubmitNewIdentity()
        {
            var root = CreateList(out var list, LoopLayout.VerticalGrid, true);
            var eventRoot = EventSystem.current == null ? new GameObject("MenuEvents", typeof(EventSystem)) : null;
            var events = EventSystem.current;
            var previousSelection = events.currentSelectedGameObject;
            var menu = root.AddComponent<LoopScrollMenuNavigation>();
            var items = new List<string>(); for (var i = 0; i < 100; i++) items.Add(i.ToString());
            try
            {
                list.ItemViews().Configure<TestItem>();
                list.SetTotalCount(items, getItemKey: item => (string)item);
                yield return null;
                events.SetSelectedGameObject(menu.FirstSelection);
                var original = events.currentSelectedGameObject;
                var clicks = 0; original.GetComponent<Button>().onClick.AddListener(() => clicks++);
                var pointer = new PointerEventData(events) { pointerId = 1, button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(original, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(original, new AxisEventData(events) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                yield return null;
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(list.GetVisibleCell(2).GetComponentInChildren<LoopScrollMenuButton>().gameObject));
                ExecuteEvents.Execute(events.currentSelectedGameObject, new AxisEventData(events) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                yield return null;
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(list.GetVisibleCell(3).GetComponentInChildren<LoopScrollMenuButton>().gameObject));
                list.ScrollToCell(80); yield return null;
                Assert.That(events.currentSelectedGameObject, Is.Null, "物理按钮回收应清除旧焦点");
                ExecuteEvents.Execute(original, new BaseEventData(events), ExecuteEvents.submitHandler);
                Assert.That(clicks, Is.Zero, "未重新选择的回收按钮不能提交新数据身份");
                ExecuteEvents.Execute(original, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(original, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(clicks, Is.Zero, "按下后换绑，抬手不能点击另一份数据");
                items.Reverse(); list.SetTotalCount(items, getItemKey: item => (string)item);
                list.ScrollToCell(items.IndexOf("3")); yield return null;
                Assert.That(menu.FirstSelection, Is.EqualTo(list.GetVisibleCell(items.IndexOf("3")).GetComponentInChildren<LoopScrollMenuButton>().gameObject), "排序后焦点按 Key 识别，不保存旧索引");

                // 三列只有两项的末行：视口和缓冲仅容纳末行时仍应按真实三列导航。
                typeof(LoopScrollView).GetField("overscan", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(list, 0f);
                list.ScrollRect.viewport.sizeDelta = new Vector2(450, 20);
                list.SetTotalCount(new List<string> { "a", "b", "c", "d", "e" }, getItemKey: item => (string)item);
                yield return null;
                list.ScrollToCell(4, ScrollAlignment.Center); yield return null;
                Assert.That(list.GetVisibleCell(0), Is.Null);
                var lastRow = list.GetVisibleCell(3).GetComponentInChildren<LoopScrollMenuButton>().gameObject;
                events.SetSelectedGameObject(lastRow);
                ExecuteEvents.Execute(lastRow, new AxisEventData(events) { moveDir = MoveDirection.Up }, ExecuteEvents.moveHandler);
                yield return null;
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(list.GetVisibleCell(0).GetComponentInChildren<LoopScrollMenuButton>().gameObject), "末行第一列向上应回到第一行第一列");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                events.SetSelectedGameObject(previousSelection);
                if (eventRoot != null) UnityEngine.Object.DestroyImmediate(eventRoot);
            }
        }
    }
}
