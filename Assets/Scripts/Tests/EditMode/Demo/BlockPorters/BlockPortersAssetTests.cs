using System.IO;
using System.Linq;
using Core.Editor.AssetNaming;
using Core.Editor.UIBind;
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
            Assert.That(hud.GetComponentsInChildren<Image>(true).Count(t => t.name.StartsWith("Preview") && !t.name.StartsWith("PreviewLabel")), Is.EqualTo(15));
            var index = hud.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Components, Is.Not.Empty);
            Assert.That(index.Components.All(component => component != null), Is.True);
            Assert.That(index.BindingKeys.Length, Is.EqualTo(index.Components.Length));
            var images = hud.GetComponentsInChildren<Image>(true);
            var fill = images.Single(item => item.name == "ProgressFill");
            var track = images.Single(item => item.name == "ProgressTrack");
            Assert.That(fill.transform.parent.parent, Is.EqualTo(track.transform), "填充必须位于背景内的圆角遮罩中");
            var progressMask = fill.transform.parent.GetComponent<Mask>();
            Assert.That(progressMask, Is.Not.Null);
            Assert.That(progressMask.showMaskGraphic, Is.False);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(fill.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            for (int i = 0; i < 5; i++)
            {
                var queue = hud.GetComponentsInChildren<Button>(true).Single(b => b.name == "Queue" + i);
                var preview = images.Single(item => item.name == "Preview" + i * 3);
                var currentRect = (RectTransform)queue.transform;
                Assert.That(preview.GetComponent<Button>(), Is.Null);
                Assert.That(preview.raycastTarget, Is.False);
                Assert.That(currentRect.anchoredPosition.y - currentRect.sizeDelta.y * .5f,
                    Is.GreaterThan(preview.rectTransform.anchoredPosition.y + preview.rectTransform.sizeDelta.y * .5f), "上下两排不能重叠");
                Assert.That(preview.rectTransform.anchoredPosition.y - preview.rectTransform.sizeDelta.y * .5f,
                    Is.GreaterThan(-540), "底部留出安全边距");
            }
            foreach (var preview in images.Where(item => item.name.StartsWith("Preview"))) Assert.That(preview.raycastTarget, Is.False);
            foreach (var transform in hud.GetComponentsInChildren<Transform>(true))
                Assert.That(transform.GetComponents<Component>().All(component => component != null), Is.True, transform.name);
            var font = hud.GetComponentInChildren<TextMeshProUGUI>(true).font;
            foreach (char character in "抬坑堵筒") Assert.That(font.characterLookupTable.ContainsKey(character), Is.True, character.ToString());
        }

        [TestCase("BlockPortersHudView", typeof(TMPro.TextMeshProUGUI))]
        [TestCase("BlockPortersSettingsView", typeof(TMPro.TextMeshProUGUI))]
        [TestCase("BlockPortersResultView", typeof(TMPro.TextMeshProUGUI))]
        public void ViewBindingsMatchSavedComponents(string viewName, System.Type boundType)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/UI/{viewName}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var imageName = viewName == "BlockPortersSettingsView" ? "SettingsCard" :
                viewName == "BlockPortersResultView" ? "ResultCard" : "ProgressTrack";
            var image = prefab.GetComponentsInChildren<Image>(true).Single(item => item.name == imageName);
            Assert.That(image.sprite, Is.Not.Null, imageName + " 必须引用 Sprite 而非原始 Texture");
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(transform.GetComponents<Component>().All(component => component != null), Is.True, transform.name);
            var index = prefab.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Components, Is.Not.Empty);
            Assert.That(index.ComponentTypes.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingKeys.Length, Is.EqualTo(index.Components.Length));
            Assert.That(index.BindingMethods.Length, Is.EqualTo(index.Components.Length));
            for (int i = 0; i < index.Components.Length; i++)
            {
                var component = index.Components[i];
                Assert.That(component, Is.Not.Null);
                Assert.That(component.transform.IsChildOf(prefab.transform), Is.True);
                Assert.That(index.ComponentTypes[i], Is.EqualTo(component.GetType().FullName));
                var path = component.transform.name;
                for (var parent = component.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                Assert.That(index.BindingKeys[i], Is.EqualTo(path + "|" + component.GetType().FullName));
                Assert.That(index.Get<Component>(i), Is.SameAs(component));
            }
            Assert.That(index.Components.Any(component => component.GetType() == boundType), Is.True);
            var record = UIBindIndexDiscovery.BuildViewRecords("Assets/Scripts/Hotfix/Demos/BlockPorters", Root + "/Prefabs/UI")
                .Single(item => item.viewName == viewName);
            Assert.That(record.isValid, Is.True, record.validationMessage);
            Assert.That(record.moduleName, Is.EqualTo("BlockPorters"));
            Assert.That(record.usesCustomModuleOutputDirectory, Is.True);
            Assert.That(record.moduleOutputDirectory, Is.EqualTo("Assets/Scripts/Hotfix/Demos/BlockPorters/UI"));
            Assert.That(File.Exists(record.viewScriptPath), Is.True);
            Assert.That(File.Exists(record.componentScriptPath), Is.True);
        }

        [Test]
        public void AllTilesSharePrefabDimensionsAndThemeCatalogUsesAddresses()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UI/BlockPortersHudView.prefab");
            var style = AssetDatabase.LoadAssetAtPath<BlockPortersUiStyle>(Root + "/Data/UiStyle.asset");
            var socket = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/TileSocket.png");
            var face = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/TileFace.png");
            var tiles = hud.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("TaskSlot") ||
                i.name.StartsWith("Preview") || i.name.StartsWith("Queue") && i.name != "QueueDock").ToArray();
            Assert.That(tiles.Length, Is.EqualTo(27));
            foreach (var tile in tiles)
            {
                Assert.That(tile.rectTransform.sizeDelta, Is.EqualTo(style.TileDimensions), tile.name);
                Assert.That(tile.sprite, Is.SameAs(socket), tile.name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(tile.gameObject), Is.Not.Null, "必须保留共用方格 Prefab 链接");
                var content = tile.GetComponentsInChildren<Image>(true).Single(i => i.sprite == face);
                Assert.That(content.rectTransform.sizeDelta, Is.EqualTo(style.FaceDimensions), tile.name);
                if (tile.TryGetComponent<Button>(out var button)) Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
            }
            var catalog = AssetDatabase.LoadAssetAtPath<BlockPortersThemeCatalog>(Root + "/Data/ThemeCatalog.asset");
            Assert.That(catalog.Themes.Select(t => t.Id).Distinct().Count(), Is.EqualTo(4));
            foreach (var theme in catalog.Themes)
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + theme.BackgroundAddress + ".png"), Is.Not.Null);
            Assert.That(AssetDatabase.GetDependencies(Root + "/Data/ThemeCatalog.asset").Any(p => p.EndsWith(".png")), Is.False);
            Assert.That(AssetDatabase.GetDependencies(Root + "/Scenes/Main.unity").Count(p => p.Contains("/Backgrounds/")), Is.EqualTo(1));
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
