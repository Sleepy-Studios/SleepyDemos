using Core.Runtime;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证 DroneFlight 正式 UI 的资源地址、Widget 层级、MvcBind 结构和关键布局锚点。
     */
    public sealed class DroneFlightUiContractTests
    {
        private const string HudPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab";
        private const string DebugPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightDebugView.prefab";
        private const string SelectPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightVehicleSelectView.prefab";

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
            Assert.That(hud.GetComponent<ComponentItemIndex>().Components.Length, Is.EqualTo(8));
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
            Assert.That(hud.transform.Find("ControlsPanel").GetComponent<RectTransform>().anchorMin.x, Is.EqualTo(1f));
            Assert.That(debug.transform.Find("DebugPanel").GetComponent<RectTransform>().anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        }
    }
}
