using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Editor.UIBind;
using Core.Runtime;
using Hotfix;
using Hotfix.DroneFlight;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 正式 UI 的资源地址、Widget 层级、UIBind 结构和关键布局锚点。
     */
    public sealed class DroneFlightUiContractTests
    {
        private const string HudPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab";
        private const string DebugPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightDebugView.prefab";
        private const string SelectPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightVehicleSelectView.prefab";
        private const string ViewRoot = "Assets/Scripts/Hotfix/Demos/DroneFlight";

        private static readonly string[] ControlTextNames =
        {
            "StatusText", "CameraText", "HeightText", "DistanceText", "HorizontalText", "VerticalText", "GearText", "EquipmentText", "WarningText"
        };

        [Test]
        public void Views_UseFormalWidgetLayersAndExpectedAddresses()
        {
            var hud = new DroneFlightHudView();
            var debug = new DroneFlightDebugView();

            Assert.That(hud.Level, Is.EqualTo(UILayer.Decorate));
            Assert.That(hud.ViewMode, Is.EqualTo(UIViewMode.Widget));
            StringAssert.EndsWith("DroneFlightHudView", hud.Address);
            Assert.That(debug.Level, Is.EqualTo(UILayer.Tip));
            Assert.That(debug.ViewMode, Is.EqualTo(UIViewMode.Widget));
            StringAssert.EndsWith("DroneFlightDebugView", debug.Address);
        }

        [Test]
        public void Prefabs_AreCanvasFreeUIBindWidgetsWithExpectedLayoutAnchors()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var debug = AssetDatabase.LoadAssetAtPath<GameObject>(DebugPrefabPath);
            var select = AssetDatabase.LoadAssetAtPath<GameObject>(SelectPrefabPath);

            Assert.That(hud, Is.Not.Null);
            Assert.That(debug, Is.Not.Null);
            Assert.That(select, Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>(), Is.Null);
            Assert.That(debug.GetComponent<Canvas>(), Is.Null);
            Assert.That(hud.GetComponent<ComponentItemIndex>(), Is.Not.Null);
            Assert.That(debug.GetComponent<ComponentItemIndex>(), Is.Not.Null);
            Assert.That(select.GetComponent<ComponentItemIndex>(), Is.Not.Null);
            var hudIndex = hud.GetComponent<ComponentItemIndex>();
            AssertIndexArraysAreAligned(hudIndex);
            Assert.That(hudIndex.Components, Has.None.Null);
            AssertIndexArraysAreAligned(debug.GetComponent<ComponentItemIndex>());
            var selectIndex = select.GetComponent<ComponentItemIndex>();
            AssertIndexArraysAreAligned(selectIndex);
            foreach (string method in new[] { "OnPlainButtonClick", "OnGrappleButtonClick", "OnHarpoonButtonClick", "OnStartButtonClick", "OnBackButtonClick" })
                Assert.That(selectIndex.BindingMethods.Count(value => value == method), Is.EqualTo(1), method);
            Assert.That(selectIndex.Components, Has.None.Null);
            Assert.That(select.GetComponent<Canvas>(), Is.Null);
            foreach (string name in new[] { "PlainPreview", "GrapplePreview", "HarpoonPreview", "Hero" })
            {
                var image = selectIndex.Components.OfType<UnityEngine.UI.Image>().Single(value => value.name == name);
                Assert.That(image.sprite, Is.Not.Null, name);
                Assert.That(image.preserveAspect, Is.True, name + " 不得拉伸实际模型");
            }
            Assert.That(hudIndex.Components.OfType<Core.Runtime.Inputs.TouchInputPad>().Count(), Is.EqualTo(2), "两个触控区承载四轴操作。");
            Assert.That(hudIndex.Components.OfType<Core.Runtime.Inputs.InputCommandButton>(), Is.Not.Empty);
            Assert.That(hud.GetComponent<DroneHudLayout>(), Is.Not.Null);
            var group = hudIndex.Components.OfType<CanvasGroup>().Single(value => value.name == "TelemetryRoot");
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        }

        [Test]
        public void HudPrefab_ControlTexts_AreBoundExactlyOnceAsTextMeshProUGUI()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var index = hud.GetComponent<ComponentItemIndex>();

            foreach (var nodeName in ControlTextNames)
            {
                var text = hud.GetComponentsInChildren<TextMeshProUGUI>(true).SingleOrDefault(value => value.name == nodeName);
                Assert.That(text, Is.Not.Null, $"HUD 缺少固定文本节点：{nodeName}");
                Assert.That(
                    index.Components.Count(component => component == text),
                    Is.EqualTo(1),
                    $"{nodeName} 必须且只能进入 ComponentItemIndex 一次。");

                var bindingIndex = System.Array.IndexOf(index.Components, text);
                Assert.That(index.ComponentTypes[bindingIndex], Is.EqualTo(typeof(TextMeshProUGUI).FullName));
                StringAssert.Contains(nodeName, index.BindingKeys[bindingIndex]);
            }
        }

        [Test]
        public void HandwrittenViews_DoNotSearchFixedPrefabNodesAtRuntime()
        {
            var forbiddenTokens = new[]
            {
                "Transform.Find(", "GameObject.Find(", ".Find(\"", "GetComponent<",
                "GetComponentInChildren<", "GetComponentsInChildren<"
            };
            var violations = Directory.GetFiles(Path.GetFullPath(ViewRoot), "*View.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("ViewComponent.cs", System.StringComparison.Ordinal))
                .SelectMany(path => forbiddenTokens
                    .Where(token => File.ReadAllText(path).Contains(token))
                    .Select(token => $"{Path.GetRelativePath(Path.GetFullPath(ViewRoot), path)}: {token}"))
                .ToArray();

            Assert.That(
                violations,
                Is.Empty,
                "固定 View 节点必须通过 ComponentItemIndex 与 UIBind 生成字段访问。\n" +
                string.Join("\n", violations));
        }

        private static void AssertIndexArraysAreAligned(ComponentItemIndex index)
        {
            Assert.That(index, Is.Not.Null);
            Assert.That(index.ComponentTypes.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingKeys.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingMethods.Length, Is.EqualTo(index.Components.Length));
        }

    }
}
