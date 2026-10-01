using System.Collections;
using System.Collections.Generic;
using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class LoopScrollItemViewBridgeTests
    {
        public sealed class TestItem : ItemView { public static int Created; public TestItem() { Created++; } }
        [UnityTest]
        public IEnumerator PhysicalCellReuseUpdatesItemIndexAndCancelsPreviousBinding()
        {
            var root = CreateList(out var list);
            var items = new List<string>(); for (var i = 0; i < 100; i++) items.Add(i.ToString());
            TestItem.Created = 0; var unbound = 0; CellBindContext old = default; TestItem first = null;
            try
            {
                list.ItemViews().SetItems<string, TestItem>(items, (view, item, context) => { if (context.Index == 0) { first = view; old = context; } Assert.That(view.Index, Is.EqualTo(context.Index)); },
                    (view, context) => { Assert.That(context.IsCurrent, Is.False); unbound++; }, item => item);
                yield return null; var created = TestItem.Created; var token = old.CancellationToken;
                list.ScrollTo(80); yield return null; list.ScrollTo(20); yield return null;
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
                owner.RegisterLoopCellBind(list, callback); owner.RegisterLoopCellBind(list, callback);
                var items = new List<string> { "a", "b", "c" };
                list.ItemViews().SetItems<string, TestItem>(items, (cell, item, context) => { }, null, item => item);
                Assert.That(calls, Is.EqualTo(list.ActiveCellCount));
                yield return owner.DestroyAsync().ToCoroutine(); var before = calls;
                list.RefreshVisible(); Assert.That(calls, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static GameObject CreateList(out LoopScrollView list)
        {
            var root = new GameObject("BridgeTest", typeof(RectTransform), typeof(ScrollRect));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>(); viewport.SetParent(root.transform); viewport.sizeDelta = new Vector2(300, 200);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport);
            var template = new GameObject("Cell", typeof(RectTransform), typeof(LoopCell)).GetComponent<LoopCell>(); template.transform.SetParent(root.transform); template.gameObject.SetActive(false);
            var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            list = root.AddComponent<LoopScrollView>(); list.Configure(scroll, new[] { new LoopCellPrefab { Type = 0, Prefab = template, Prewarm = 30 } }, LoopLayout.Vertical, new Vector2(300, 40));
            return root;
        }
        [UnityTest]
        public IEnumerator NestedItemViewCanRegisterAndExplicitlyUnsubscribe()
        {
            var root = CreateList(out var list); var owner = new ItemView(); var calls = 0;
            var subscription = owner.RegisterLoopCellBind(list, (cell, index, context) => calls++);
            try
            {
                var items = new List<string> { "a", "b" };
                list.ItemViews().SetItems<string, TestItem>(items, (cell, item, context) => { }, null, item => item);
                Assert.That(calls, Is.EqualTo(2)); subscription.Dispose(); subscription.Dispose();
                list.RefreshVisible(); Assert.That(calls, Is.EqualTo(2)); yield return null;
            }
            finally { subscription.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
        [UnityTest]
        public IEnumerator InactiveHostCanSubmitBeforeBridgeAwake()
        {
            var root = CreateList(out var list); root.SetActive(false);
            try
            {
                list.ItemViews().SetItems<string, TestItem>(new List<string> { "a", "b" }, (cell, item, context) => { }, null, item => item);
                Assert.That(list.Count, Is.EqualTo(2)); Assert.That(list.ActiveCellCount, Is.Zero);
                root.SetActive(true); yield return null; yield return null;
                Assert.That(list.ActiveCellCount, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
