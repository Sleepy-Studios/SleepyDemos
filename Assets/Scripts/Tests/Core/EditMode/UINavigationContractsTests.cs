using Core.Runtime;
using NUnit.Framework;

namespace Core.Tests.UI
{
    public sealed class UINavigationContractsTests
    {
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
            Assert.That(result.View, Is.SameAs(view));
            Assert.That(result.Exception, Is.Null);
        }
    }
}
