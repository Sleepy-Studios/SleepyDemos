using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Core.Editor.MvcBind;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Module
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

        [Test]
        public void GetUITransitionTypeChoices_OnlyReturnsConstructiblePlayerTypes()
        {
            var choices = MvcBindWindow.GetUITransitionTypeChoices();

            CollectionAssert.Contains(choices, typeof(EmptyUITransition).FullName);
            CollectionAssert.DoesNotContain(choices, typeof(EditorOnlyTransition).FullName);
            CollectionAssert.DoesNotContain(choices, typeof(NoPublicConstructorTransition).FullName);
            CollectionAssert.DoesNotContain(choices, typeof(GenericTransition<>).FullName);
        }

        [TestCase(typeof(EditorOnlyTransition))]
        [TestCase(typeof(NoPublicConstructorTransition))]
        [TestCase(typeof(GenericTransition<>))]
        public void CreateComponentScriptText_InvalidTransitionType_ThrowsClearException(Type transitionType)
        {
            var settings = CreateSettings();
            settings.uiTransitionType = transitionType.FullName;

            var exception = Assert.Throws<InvalidDataException>(() =>
                MvcCodeGenerator.CreateComponentScriptText(
                    settings,
                    Array.Empty<MvcBindComponentInfo>()));

            StringAssert.Contains(transitionType.FullName, exception.Message);
        }

        [Test]
        public void GenerateAndBind_InvalidTransitionType_HasNoPrefabOrFileSideEffects()
        {
            var uniqueModuleName = $"__MvcBindPreflight_{Guid.NewGuid():N}";
            var moduleRoot = $"Assets/Scripts/Hotfix/Module/{uniqueModuleName}";
            var viewName = "InvalidTransitionView";
            var outputFolder = MvcBindPathUtility.ToOutputFolder(uniqueModuleName, viewName);
            var target = new GameObject("MvcBindPreflightTarget", typeof(RectTransform));

            try
            {
                var node = new MvcBindNode
                {
                    name = target.name,
                    path = target.name,
                    gameObject = target,
                    selectedComponentType = typeof(RectTransform)
                };
                var settings = new MvcBindSettings
                {
                    prefabPath = $"Assets/LoadResources/UI/{viewName}.prefab",
                    moduleName = uniqueModuleName,
                    viewName = viewName,
                    namespaceName = "Hotfix",
                    address = $"LoadResources/UI/{viewName}",
                    viewType = ViewType.View,
                    uiTransitionType = typeof(EditorOnlyTransition).FullName
                };

                var success = MvcBindComponentWindowBridge.GenerateAndBind(
                    target,
                    settings,
                    new[] { node },
                    false,
                    out var generatedPath,
                    out var message);

                Assert.That(success, Is.False);
                Assert.That(target.GetComponent<ComponentItemIndex>(), Is.Null);
                Assert.That(Directory.Exists(outputFolder), Is.False);
                Assert.That(generatedPath, Is.Empty);
                StringAssert.Contains(typeof(EditorOnlyTransition).FullName, message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                if (AssetDatabase.IsValidFolder(moduleRoot))
                {
                    AssetDatabase.DeleteAsset(moduleRoot);
                }
                else if (Directory.Exists(moduleRoot))
                {
                    Directory.Delete(moduleRoot, true);
                    AssetDatabase.Refresh();
                }
            }
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

    public abstract class TestTransitionBase : IUITransition
    {
        void IUITransition.Initialize(UnityEngine.Transform root)
        {
        }

        UniTask IUITransition.EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        UniTask IUITransition.ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        void IUITransition.CompleteImmediately(UITransitionDirection direction)
        {
        }

        void IDisposable.Dispose()
        {
        }
    }

    public sealed class EditorOnlyTransition : TestTransitionBase
    {
    }

    public sealed class NoPublicConstructorTransition : TestTransitionBase
    {
        private NoPublicConstructorTransition()
        {
        }
    }

    public sealed class GenericTransition<T> : TestTransitionBase
    {
    }
}
