using Core.Runtime;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DroneFlightUiContractTests
    {
        private const string HudPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab";
        private const string DebugPrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightDebugView.prefab";

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

            Assert.That(hud, Is.Not.Null);
            Assert.That(debug, Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>(), Is.Null);
            Assert.That(debug.GetComponent<Canvas>(), Is.Null);
            Assert.That(hud.GetComponent<ComponentItemIndex>(), Is.Not.Null);
            Assert.That(debug.GetComponent<ComponentItemIndex>(), Is.Not.Null);
            Assert.That(hud.GetComponent<DroneHudPresenter>(), Is.Not.Null);
            Assert.That(debug.GetComponent<DroneDebugPresenter>(), Is.Not.Null);
            Assert.That(hud.transform.Find("ControlsPanel").GetComponent<RectTransform>().anchorMin.x, Is.EqualTo(1f));
            Assert.That(debug.transform.Find("DebugPanel").GetComponent<RectTransform>().anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
        }
    }
}
