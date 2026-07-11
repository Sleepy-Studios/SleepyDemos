using System;
using Core.Runtime;
using NUnit.Framework;

namespace Core.Tests.UI
{
    public sealed class UINavigationContractsTests
    {
        [TestCase(UILayer.Underground, UIViewMode.Page)]
        [TestCase(UILayer.Base, UIViewMode.Page)]
        [TestCase(UILayer.Foreground, UIViewMode.Page)]
        [TestCase(UILayer.Pop, UIViewMode.Modal)]
        [TestCase(UILayer.Decorate, UIViewMode.Widget)]
        [TestCase(UILayer.Tip, UIViewMode.Widget)]
        public void Resolve_ReturnsDefaultModeForLayer(UILayer layer, UIViewMode expected)
        {
            Assert.That(UIViewModeResolver.Resolve(layer), Is.EqualTo(expected));
        }

        [Test]
        public void SucceededResult_ContainsViewAndNoException()
        {
            var view = new View();

            var result = UIOperationResult.Succeeded(7, UINavigationAction.Push, view);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.OperationId, Is.EqualTo(7));
            Assert.That(result.Action, Is.EqualTo(UINavigationAction.Push));
            Assert.That(result.View, Is.SameAs(view));
            Assert.That(result.Exception, Is.Null);
        }

        [Test]
        public void DefaultResult_IsNotSucceededAndContainsNoPayload()
        {
            var result = default(UIOperationResult);

            Assert.That(result.Status, Is.Not.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.View, Is.Null);
            Assert.That(result.Exception, Is.Null);
        }

        [Test]
        public void DefaultShowOptions_EnablesAnimation()
        {
            Assert.That(default(UIShowOptions).Animated, Is.True);
        }

        [Test]
        public void ExplicitFalseShowOptions_DisablesAnimation()
        {
            Assert.That(new UIShowOptions(false).Animated, Is.False);
        }

        [Test]
        public void Succeeded_ThrowsWhenViewIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => UIOperationResult.Succeeded(1, UINavigationAction.Push, null));
        }

        [Test]
        public void Ignored_ThrowsWhenViewIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => UIOperationResult.Ignored(2, UINavigationAction.Preload, null));
        }

        [Test]
        public void Failed_ThrowsWhenExceptionIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => UIOperationResult.Failed(3, UINavigationAction.Replace, new View(), null));
        }

        [Test]
        public void IgnoredResult_ContainsOperationPayloadAndNoException()
        {
            var view = new View();

            var result = UIOperationResult.Ignored(4, UINavigationAction.Preload, view);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Ignored));
            Assert.That(result.OperationId, Is.EqualTo(4));
            Assert.That(result.Action, Is.EqualTo(UINavigationAction.Preload));
            Assert.That(result.View, Is.SameAs(view));
            Assert.That(result.Exception, Is.Null);
        }

        [Test]
        public void CanceledResult_AllowsNullViewAndContainsNoException()
        {
            var result = UIOperationResult.Canceled(5, UINavigationAction.Close, null);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(result.OperationId, Is.EqualTo(5));
            Assert.That(result.Action, Is.EqualTo(UINavigationAction.Close));
            Assert.That(result.View, Is.Null);
            Assert.That(result.Exception, Is.Null);
        }

        [Test]
        public void FailedResult_ContainsOperationPayloadAndException()
        {
            var view = new View();
            var exception = new InvalidOperationException("failed");

            var result = UIOperationResult.Failed(
                6,
                UINavigationAction.Replace,
                view,
                exception);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.OperationId, Is.EqualTo(6));
            Assert.That(result.Action, Is.EqualTo(UINavigationAction.Replace));
            Assert.That(result.View, Is.SameAs(view));
            Assert.That(result.Exception, Is.SameAs(exception));
        }
    }
}
