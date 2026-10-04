using System.IO;
using System.Linq;
using Core.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class UIFoundationAssetTests
    {
        [Test]
        public void LinearProgress_UsesWhiteSpriteAndSharedComponent()
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/LoadResources/UI/Common/Sprites/White.png");
            foreach (var path in Directory.GetFiles("Assets/LoadResources", "*.prefab", SearchOption.AllDirectories))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.type != Image.Type.Filled || image.fillMethod != Image.FillMethod.Horizontal && image.fillMethod != Image.FillMethod.Vertical) continue;
                    Assert.That(image.sprite, Is.SameAs(white), path + "/" + image.name);
                    Assert.That(image.GetComponent<UIProgressBar>(), Is.Not.Null, path + "/" + image.name);
                    Assert.That(image.raycastTarget, Is.False, path + "/" + image.name);
                }
            }
        }

        [Test]
        public void DemoPages_HaveLocalBindingsAndSeparateHudParts()
        {
            foreach (var demo in new[] { "how_to_fish", "jinx_casino", "block_porters", "drone_flight" })
                foreach (var path in Directory.GetFiles("Assets/LoadResources/Demos/" + demo + "/Prefabs/UI", "*.prefab", SearchOption.AllDirectories))
                {
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    foreach (var node in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, path + "/" + node.name);
                    foreach (var index in root.GetComponentsInChildren<ComponentItemIndex>(true))
                        foreach (var component in index.Components)
                        {
                            Assert.That(component, Is.Not.Null, path);
                            Assert.That(component.transform == root.transform || component.transform.IsChildOf(root.transform), Is.True, path);
                        }
                    foreach (var state in root.GetComponentsInChildren<UIState>(true))
                        foreach (var definition in state.States)
                            Assert.That(definition.properties.All(property => property.target != null), Is.True, path + "/" + definition.stateName);
                }
            var drone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab");
            foreach (var name in new[] { "TelemetryRoot", "TouchControls", "OperationPanel" })
            {
                var region = drone.GetComponentsInChildren<Transform>(true).Single(node => node.name == name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(region), Is.Not.Null, name);
            }
            var casinoHud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/jinx_casino/Prefabs/UI/JinxCasinoImmersionHudView.prefab");
            foreach (var name in new[] { "MainMenu", "PauseMenu", "SaveSlotsPanel", "StandardEndingPanel", "LocalSettings" })
                Assert.That(casinoHud.GetComponentsInChildren<Transform>(true).Any(node => node.name == name), Is.False, name);
        }

        [Test]
        public void State_InspectorRenameInvalidatesCachedLookup()
        {
            var obj = new GameObject("StateInspection", typeof(UIState));
            try
            {
                var state = obj.GetComponent<UIState>();
                Assert.That(state.GetState("Normal"), Is.Not.Null);
                var serialized = new SerializedObject(state);
                serialized.FindProperty("states").GetArrayElementAtIndex(0).FindPropertyRelative("stateName").stringValue = "Edited";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(state.GetState("Normal"), Is.Null);
                Assert.That(state.GetState("Edited"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void SavedCommonPrefabs_HaveValidFontsBindingsAndDistinctButtonStates()
        {
            foreach (string path in Directory.GetFiles("Assets/LoadResources/UI/Common", "*.prefab", SearchOption.AllDirectories))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, path);
                Assert.That(root.GetComponentsInChildren<Text>(true), Is.Empty, path);
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, path + "/" + node.name);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(text.font, Is.Not.Null, path + "/" + text.name);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.type == Image.Type.Filled) Assert.That(image.sprite, Is.Not.Null, path + "/" + image.name);
                    if (image.type == Image.Type.Sliced && image.sprite != null)
                        Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0), path + "/" + image.name);
                }
                foreach (var interaction in root.GetComponentsInChildren<UIStateInteraction>(true))
                    Assert.That(interaction.GetComponent<Selectable>(), Is.Not.Null, path + "/" + interaction.name);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None), path + "/" + button.name);
                    Assert.That(button.GetComponent<UIStateInteraction>(), Is.Not.Null, path + "/" + button.name);
                    var state = button.transform.Find("InteractionFeedback").GetComponent<UIState>();
                    foreach (string name in new[] { "Normal", "Hover", "Focused", "Pressed", "Disabled" })
                    {
                        var definition = state.GetState(name);
                        Assert.That(definition, Is.Not.Null, path + "/" + name);
                        Assert.That(definition.properties.All(property => property.target != null), Is.True, path + "/" + name);
                    }
                    var normalScaleProperty = state.GetState("Normal").properties.FirstOrDefault(property => property.propertyType == UIStatePropertyType.TransformLocalScale);
                    var pressedScaleProperty = state.GetState("Pressed").properties.FirstOrDefault(property => property.propertyType == UIStatePropertyType.TransformLocalScale);
                    Assert.That(normalScaleProperty, Is.Not.Null, path + "/" + button.name + "/Normal");
                    Assert.That(pressedScaleProperty, Is.Not.Null, path + "/" + button.name + "/Pressed");
                    var normalScale = normalScaleProperty.vector3Value;
                    var pressedScale = pressedScaleProperty.vector3Value;
                    Assert.That(pressedScale.sqrMagnitude, Is.LessThan(normalScale.sqrMagnitude), path);
                    Assert.That(pressedScale.sqrMagnitude, Is.GreaterThan(0), path);
                    foreach (var graphic in button.transform.Find("InteractionFeedback").GetComponentsInChildren<Graphic>(true))
                        Assert.That(graphic.raycastTarget, Is.False, path);
                }
            }
        }
    }
}
