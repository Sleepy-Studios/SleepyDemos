using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using Hotfix;
using Hotfix.DroneFlight;
using Hotfix.Editor.DroneFlight;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 正式 UI 的资源地址、Widget 层级、MvcBind 结构和关键布局锚点。
     */
    public sealed class DroneFlightUiContractTests
    {
        private const string HudPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab";
        private const string DebugPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightDebugView.prefab";
        private const string SelectPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightVehicleSelectView.prefab";
        private const string ViewRoot =
            "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/SleepyDemos/UI";

        private static readonly string[] ControlTextNames =
        {
            "ControlsHeaderText", "FlightControlsText", "CameraControlsText", "SystemControlsText"
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
        public void Prefabs_AreCanvasFreeMvcBindWidgetsWithExpectedLayoutAnchors()
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
            Assert.That(hudIndex.Components.Length, Is.EqualTo(12));
            Assert.That(debug.GetComponent<ComponentItemIndex>().Components.Length, Is.EqualTo(1));
            var selectIndex = select.GetComponent<ComponentItemIndex>();
            Assert.That(selectIndex.Components.Length, Is.EqualTo(3));
            CollectionAssert.AreEqual(
                new[] { "OnPlainButtonClick", "OnGrappleButtonClick", "OnHarpoonButtonClick" },
                selectIndex.BindingMethods);
            Assert.That(select.transform.Find("Panel/PlainButton"), Is.Not.Null);
            Assert.That(hud.GetComponents<MonoBehaviour>(), Has.None.Matches<MonoBehaviour>(
                component => component.GetType().Name.EndsWith("Presenter")));
            Assert.That(debug.GetComponents<MonoBehaviour>(), Has.None.Matches<MonoBehaviour>(
                component => component.GetType().Name.EndsWith("Presenter")));
            var controlsPanel = hud.transform.Find("ControlsPanel");
            Assert.That(controlsPanel.GetComponent<RectTransform>().anchorMin.x, Is.EqualTo(1f));
            Assert.That(controlsPanel.gameObject.activeSelf, Is.True);
            Assert.That(controlsPanel.Find("ControlsHeaderText"), Is.Not.Null);
            Assert.That(controlsPanel.Find("FlightControlsText"), Is.Not.Null);
            Assert.That(controlsPanel.Find("CameraControlsText"), Is.Not.Null);
            Assert.That(controlsPanel.Find("SystemControlsText"), Is.Not.Null);
            Assert.That(debug.transform.Find("DebugPanel").GetComponent<RectTransform>().anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        }

        [Test]
        public void HudPrefab_ControlTexts_AreBoundExactlyOnceAsTextMeshProUGUI()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var index = hud.GetComponent<ComponentItemIndex>();

            foreach (var nodeName in ControlTextNames)
            {
                var text = hud.transform.Find($"ControlsPanel/{nodeName}")
                    ?.GetComponent<TextMeshProUGUI>();
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
        public void HudBuilder_WhenControlBindingsAreMissing_RestoresCompleteSelection()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var clone = Object.Instantiate(prefab);
            try
            {
                RemoveControlTextBindings(clone.GetComponent<ComponentItemIndex>());

                var nodes = DroneFlightMechanismBuilder.CreateHudBindingNodes(clone);
                var components = MvcCodeGenerator.CollectComponents(nodes);

                Assert.That(components.Count, Is.EqualTo(12));
                foreach (var nodeName in ControlTextNames)
                {
                    Assert.That(
                        components.Count(item =>
                            item.componentType == typeof(TextMeshProUGUI) &&
                            item.component.gameObject.name == nodeName),
                        Is.EqualTo(1));
                }
            }
            finally
            {
                Object.DestroyImmediate(clone);
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
                "固定 View 节点必须通过 ComponentItemIndex 与 MvcBind 生成字段访问。\n" +
                string.Join("\n", violations));
        }

        private static void AssertIndexArraysAreAligned(ComponentItemIndex index)
        {
            Assert.That(index, Is.Not.Null);
            Assert.That(index.ComponentTypes.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingKeys.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingMethods.Length, Is.EqualTo(index.Components.Length));
        }

        private static void RemoveControlTextBindings(ComponentItemIndex index)
        {
            var keep = Enumerable.Range(0, index.Components.Length)
                .Where(position => !ControlTextNames.Contains(index.Components[position].gameObject.name))
                .ToArray();
            index.Components = keep.Select(position => index.Components[position]).ToArray();
            index.ComponentTypes = keep.Select(position => index.ComponentTypes[position]).ToArray();
            index.BindingKeys = keep.Select(position => index.BindingKeys[position]).ToArray();
            index.BindingMethods = keep.Select(position => index.BindingMethods[position]).ToArray();
        }
    }
}
