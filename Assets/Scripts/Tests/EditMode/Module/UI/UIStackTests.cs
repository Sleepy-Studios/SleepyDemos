using System;
using System.Collections.Generic;
using Core.Runtime;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class UIStackTests
    {
        [Test]
        public void CommitShow_Page_AddsPageAndMakesItCurrent()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);

            stack.CommitShow(page);

            Assert.That(stack.Pages, Has.Count.EqualTo(1));
            Assert.That(stack.CurrentPage, Is.SameAs(page));
        }

        [Test]
        public void CommitShow_Modal_AddsModalWithoutChangingCurrentPage()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);
            var modal = new FakeView(UIViewMode.Modal, UILayer.Pop);
            stack.CommitShow(page);

            stack.CommitShow(modal);

            Assert.That(stack.Modals, Has.Count.EqualTo(1));
            Assert.That(stack.TopModal, Is.SameAs(modal));
            Assert.That(stack.CurrentPage, Is.SameAs(page));
        }

        [Test]
        public void CommitShow_Widget_AddsWidgetWithoutChangingCurrentPage()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);
            var widget = new FakeView(UIViewMode.Widget, UILayer.Decorate);
            stack.CommitShow(page);

            stack.CommitShow(widget);

            Assert.That(stack.Widgets, Has.Count.EqualTo(1));
            Assert.That(stack.Widgets[0], Is.SameAs(widget));
            Assert.That(stack.CurrentPage, Is.SameAs(page));
        }

        [Test]
        public void Back_WhenModalExists_RemovesModalBeforePage()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);
            var modal = new FakeView(UIViewMode.Modal, UILayer.Pop);
            stack.CommitShow(page);
            stack.CommitShow(modal);

            var removed = stack.CommitBack();

            Assert.That(removed, Is.SameAs(modal));
            Assert.That(stack.TopModal, Is.Null);
            Assert.That(stack.CurrentPage, Is.SameAs(page));
        }

        [Test]
        public void CommitClose_Widget_DoesNotChangeCurrentPage()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);
            var widget = new FakeView(UIViewMode.Widget, UILayer.Decorate);
            stack.CommitShow(page);
            stack.CommitShow(widget);

            var removed = stack.CommitClose(widget);

            Assert.That(removed, Is.True);
            Assert.That(stack.Widgets, Is.Empty);
            Assert.That(stack.CurrentPage, Is.SameAs(page));
        }

        [Test]
        public void CommitShow_ExistingView_MovesItToTopWithoutDuplicatingReference()
        {
            var stack = new UIStack();
            var firstPage = new FakeView(UIViewMode.Page, UILayer.Base);
            var secondPage = new FakeView(UIViewMode.Page, UILayer.Foreground);
            stack.CommitShow(firstPage);
            stack.CommitShow(secondPage);

            stack.CommitShow(firstPage);

            Assert.That(stack.Pages, Has.Count.EqualTo(2));
            Assert.That(stack.CurrentPage, Is.SameAs(firstPage));
        }

        [Test]
        public void Restore_ReplacesCurrentStateWithCapturedSnapshot()
        {
            var stack = new UIStack();
            var page = new FakeView(UIViewMode.Page, UILayer.Base);
            var modal = new FakeView(UIViewMode.Modal, UILayer.Pop);
            stack.CommitShow(page);
            var snapshot = stack.Capture();
            stack.CommitShow(modal);

            Assert.That(snapshot.Pages.Count, Is.EqualTo(1));
            Assert.That(snapshot.Modals.Count, Is.Zero);
            stack.Restore(snapshot);

            Assert.That(stack.CurrentPage, Is.SameAs(page));
            Assert.That(stack.Modals, Is.Empty);
        }

        [Test]
        public void StackCollections_CannotBeDowncastOrMutated()
        {
            var stack = new UIStack();
            stack.CommitShow(new FakeView(UIViewMode.Page, UILayer.Base));
            stack.CommitShow(new FakeView(UIViewMode.Modal, UILayer.Pop));
            stack.CommitShow(new FakeView(UIViewMode.Widget, UILayer.Decorate));

            AssertReadOnly(stack.Pages, typeof(List<View>));
            AssertReadOnly(stack.Modals, typeof(List<View>));
            AssertReadOnly(stack.Widgets, typeof(List<View>));
        }

        [Test]
        public void SnapshotCollections_CannotBeDowncastOrMutated()
        {
            var stack = new UIStack();
            stack.CommitShow(new FakeView(UIViewMode.Page, UILayer.Base));
            stack.CommitShow(new FakeView(UIViewMode.Modal, UILayer.Pop));
            stack.CommitShow(new FakeView(UIViewMode.Widget, UILayer.Decorate));
            var snapshot = stack.Capture();

            AssertReadOnly(snapshot.Pages, typeof(View[]));
            AssertReadOnly(snapshot.Modals, typeof(View[]));
            AssertReadOnly(snapshot.Widgets, typeof(View[]));
        }

        [Test]
        public void Clear_RemovesAllViewModes()
        {
            var stack = new UIStack();
            stack.CommitShow(new FakeView(UIViewMode.Page, UILayer.Base));
            stack.CommitShow(new FakeView(UIViewMode.Modal, UILayer.Pop));
            stack.CommitShow(new FakeView(UIViewMode.Widget, UILayer.Decorate));

            stack.Clear();

            Assert.That(stack.Pages, Is.Empty);
            Assert.That(stack.Modals, Is.Empty);
            Assert.That(stack.Widgets, Is.Empty);
        }

        private static void AssertReadOnly(IReadOnlyList<View> views, Type mutableType)
        {
            Assert.That(views, Is.Not.InstanceOf(mutableType));
            var mutableView = views as IList<View>;
            Assert.That(mutableView, Is.Not.Null);
            Assert.Throws<NotSupportedException>(
                () => mutableView[0] = new FakeView(UIViewMode.Page, UILayer.Base));
        }

        private sealed class FakeView : View
        {
            private readonly UIViewMode viewMode;
            private readonly UILayer level;

            public FakeView(UIViewMode viewMode, UILayer level)
            {
                this.viewMode = viewMode;
                this.level = level;
            }

            public override UIViewMode ViewMode => viewMode;
            public override UILayer Level => level;
        }
    }
}
