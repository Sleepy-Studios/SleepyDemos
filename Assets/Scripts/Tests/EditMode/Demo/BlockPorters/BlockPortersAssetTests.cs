using System.IO;
using System.Linq;
using Core.Editor.AssetNaming;
using Core.Runtime;
using Hotfix.BlockPorters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tests.Demo
{
    public sealed class BlockPortersAssetTests
    {
        private const string Root = "Assets/LoadResources/Demos/block_porters";

        [Test]
        public void CatalogPreservesTeachingLevelsAndAllWitnessesWin()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BlockPortersLevelCatalog>(Root + "/Data/LevelCatalog.asset");
            Assert.That(catalog, Is.Not.Null); Assert.That(catalog.Levels.Length, Is.GreaterThanOrEqualTo(8));
            for (int i = 0; i < 5; i++) Assert.That(catalog.Levels[i], Is.SameAs(AssetDatabase.LoadAssetAtPath<BlockPortersLevel>($"{Root}/Data/Level{i + 1}.asset")));
            foreach (var level in catalog.Levels)
            {
                var scheduler = new BlockPortersScheduler(new BlockPortersSession(level.CreateData()));
                foreach (int column in level.Solution) { Assert.That(scheduler.Dispatch(column), Is.True); scheduler.Settle(); }
                Assert.That(scheduler.Session.Status, Is.EqualTo(BlockPortersStatus.Won), level.name);
            }
        }

        [Test]
        public void DemoResourcesFollowNamingAndLabels()
        {
            foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (LoadResourcesAssetNamingRules.ShouldSkipAssetPath(path)) continue;
                Assert.That(LoadResourcesAssetNamingRules.Validate(path).Where(issue => issue.Severity == NamingSeverity.Error),
                    Is.Empty, path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(asset, Is.Not.Null, path);
                foreach (string label in LoadResourcesAssetNamingRules.GetLabels(path))
                    Assert.That(AssetDatabase.GetLabels(asset), Does.Contain(label), path);
            }
            Assert.That(LoadResourcesAssetNamingRules.GetLabels(Root + "/Audio/SFX/Drop.wav"),
                Is.EquivalentTo(new[] { "demo", "audio", "sfx" }));
        }

        [Test]
        public void HudPrefabHasCompleteBindingsWithoutIndependentCanvas()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UI/BlockPortersHudView.prefab");
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>(), Is.Null);
            Assert.That(hud.GetComponent<CanvasScaler>(), Is.Null);
            Assert.That(hud.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(hud.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Preview") && !t.name.StartsWith("PreviewLabel")), Is.EqualTo(4));
            var index = hud.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Components, Is.Not.Empty);
            Assert.That(index.Components.All(component => component != null), Is.True);
            Assert.That(index.BindingKeys.Length, Is.EqualTo(index.Components.Length));
            foreach (var transform in hud.GetComponentsInChildren<Transform>(true))
                Assert.That(transform.GetComponents<Component>().All(component => component != null), Is.True, transform.name);
            var font = hud.GetComponentInChildren<TextMeshProUGUI>(true).font;
            foreach (char character in "抬坑堵") Assert.That(font.characterLookupTable.ContainsKey(character), Is.True, character.ToString());
        }

        [Test]
        public void PorterPrefabPreservesCarryAnchorAndHasNoPhysicsBodies()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Porter.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<PorterAvatar>().CarryAnchor, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
