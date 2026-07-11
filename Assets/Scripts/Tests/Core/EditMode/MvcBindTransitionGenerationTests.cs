using System;
using System.Collections.Generic;
using Core.Editor.MvcBind;
using Core.Runtime;
using NUnit.Framework;

namespace Core.Tests.UI
{
    public sealed class MvcBindTransitionGenerationTests
    {
        [Test]
        public void CreateComponentScriptText_GeneratesTransitionFactoryAndExplicitViewMode()
        {
            var settings = CreateSettings();

            var source = MvcCodeGenerator.CreateComponentScriptText(
                settings,
                Array.Empty<MvcBindComponentInfo>());

            StringAssert.Contains("protected override IUITransition CreateUITransition()", source);
            StringAssert.Contains("return new Core.Runtime.EmptyUITransition();", source);
            StringAssert.Contains("public override UIViewMode ViewMode => UIViewMode.Modal;", source);
            StringAssert.DoesNotContain("public override IUITransition UITransition", source);
            StringAssert.DoesNotContain("IUIAnimation", source);
            StringAssert.DoesNotContain("ICameraAnimation", source);
        }

        [Test]
        public void CreateComponentScriptText_EscapesWorldTransitionKeyWithoutInstantiation()
        {
            var settings = CreateSettings();
            settings.worldTransitionKey = "camera\\\"main";

            var source = MvcCodeGenerator.CreateComponentScriptText(
                settings,
                new List<MvcBindComponentInfo>());

            StringAssert.Contains("public override string WorldTransitionKey => \"camera\\\\\\\"main\";", source);
            StringAssert.DoesNotContain("new IUIWorldTransition", source);
        }

        private static MvcBindSettings CreateSettings()
        {
            return new MvcBindSettings
            {
                namespaceName = "Hotfix",
                viewName = "GeneratedView",
                address = "LoadResources/UI/GeneratedView",
                viewType = ViewType.View,
                layer = UILayer.Pop,
                viewMode = UIViewMode.Modal,
                uiTransitionType = typeof(EmptyUITransition).FullName
            };
        }
    }
}
