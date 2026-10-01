using System.Linq;
using Core.Editor.MvcBind;
using NUnit.Framework;
using SleepyStudios.LoopScroll;

namespace Tests.Module
{
    public sealed class LoopScrollMvcGenerationTests
    {
        [Test]
        public void RegisterDiscovery_ExposesBindUnbindAndClickWithContext()
        {
            var methods = MvcCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages");
            var names = methods.Select(method => method.registerMethodName).ToArray();
            CollectionAssert.Contains(names, "OnMessagesCellBind");
            CollectionAssert.Contains(names, "OnMessagesCellUnbind");
            CollectionAssert.Contains(names, "OnMessagesCellClick");
            Assert.That(methods.Single(method => method.registerMethodName == "OnMessagesCellBind").parameterText,
                Does.Contain("CellBindContext"));
        }
        [Test]
        public void Generator_EmitsNamespaceAndAllThreeRegistrations()
        {
            var component = new MvcBindComponentInfo { fieldName = "messages", componentType = typeof(LoopScrollView), index = 0 };
            component.methods.AddRange(MvcCodeGenerator.GetRegisterMethods(typeof(LoopScrollView), "Messages"));
            var settings = new MvcBindSettings { viewName = "LoopExampleView", namespaceName = "Hotfix.Demos.LoopScroll" };
            var generated = MvcCodeGenerator.CreateComponentScriptText(settings, new[] { component });
            Assert.That(generated, Does.Contain("using SleepyStudios.LoopScroll;"));
            Assert.That(generated, Does.Contain("this.RegisterLoopCellBind(messages, OnMessagesCellBind)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopCellUnbind(messages, OnMessagesCellUnbind)"));
            Assert.That(generated, Does.Contain("this.RegisterLoopCellClick(messages, OnMessagesCellClick)"));
        }
    }
}
