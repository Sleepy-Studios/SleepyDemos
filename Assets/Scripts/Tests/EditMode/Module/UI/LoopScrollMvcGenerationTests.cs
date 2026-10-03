using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using NUnit.Framework;
using SleepyStudios.LoopScroll;

namespace Tests.Module
{
    public sealed class LoopScrollMvcGenerationTests
    {
        [Test]
        public void RegisterDiscovery_ExposesOnlyThreeCanonicalSimpleCallbacks()
        {
            var methods = MvcCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages");
            var names = methods.Select(method => method.registerMethodName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "OnMessagesRectData", "OnMessagesClick", "OnMessagesItemHide" }, names);
            Assert.That(methods.All(method => !method.parameterText.Contains("CellBindContext")), Is.True);
            foreach (var method in methods)
            {
                var expected = method.registerMethodName.EndsWith("ItemHide")
                    ? new[] { typeof(ItemView) } : new[] { typeof(ItemView), typeof(int) };
                CollectionAssert.AreEqual(expected, method.parameterTypes);
            }
        }
        [Test]
        public void Generator_EmitsNamespaceAndAllThreeRegistrations()
        {
            var component = new MvcBindComponentInfo { fieldName = "messages", componentType = typeof(LoopScrollView), index = 0 };
            component.methods.AddRange(MvcCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages"));
            var settings = new MvcBindSettings { viewName = "LoopExampleView", namespaceName = "Hotfix.Demos.LoopScroll" };
            var generated = MvcCodeGenerator.CreateComponentScriptText(settings, new[] { component });
            Assert.That(generated, Does.Contain("using SleepyStudios.LoopScroll;"));
            Assert.That(generated, Does.Not.Contain("RegisterLoopCell"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollRect(messages, OnMessagesRectData)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollClick(messages, OnMessagesClick)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollItemHide(messages, OnMessagesItemHide)"));
        }
    }
}
