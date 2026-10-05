using System.Linq;
using Core.Editor.UIBind;
using Core.Runtime;
using NUnit.Framework;
using SleepyStudios.LoopScroll;

namespace Tests.Module
{
    public sealed class LoopScrollUIBindGenerationTests
    {
        [Test]
        public void InputCommandGenerationKeepsClickAndHoldSignatures()
        {
            var component = new UIBindComponentInfo { fieldName = "command", componentType = typeof(Core.Runtime.Inputs.InputCommandButton), index = 0 };
            var methods = UIBindCodeGenerator.GetRegisterMethods(component.componentType, "Command");
            Assert.That(methods.Single(method => method.registerMethodName == "OnCommandClick").parameterTypes, Is.EqualTo(new[] { typeof(string) }));
            Assert.That(methods.Single(method => method.registerMethodName == "OnCommandHoldChanged").parameterTypes, Is.EqualTo(new[] { typeof(string), typeof(bool) }));
            component.methods.AddRange(methods);
            var generated = UIBindCodeGenerator.CreateComponentScriptText(new UIBindSettings { viewName="InputView", namespaceName="Hotfix" }, new[] { component });
            Assert.That(generated, Does.Contain("this.RegisterInputCommandButton(command, OnCommandClick)"));
            Assert.That(generated, Does.Contain("this.RegisterInputCommandHold(command, OnCommandHoldChanged)"));
        }
        [Test]
        public void RegisterDiscovery_ExposesOnlyThreeCanonicalSimpleCallbacks()
        {
            var methods = UIBindCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages");
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
            var component = new UIBindComponentInfo { fieldName = "messages", componentType = typeof(LoopScrollView), index = 0 };
            component.methods.AddRange(UIBindCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages"));
            var settings = new UIBindSettings { viewName = "LoopExampleView", namespaceName = "Hotfix.Demos.LoopScroll" };
            var generated = UIBindCodeGenerator.CreateComponentScriptText(settings, new[] { component });
            Assert.That(generated, Does.Contain("using SleepyStudios.LoopScroll;"));
            Assert.That(generated, Does.Not.Contain("RegisterLoopCell"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollRect(messages, OnMessagesRectData)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollClick(messages, OnMessagesClick)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopScrollItemHide(messages, OnMessagesItemHide)"));
        }
    }
}
