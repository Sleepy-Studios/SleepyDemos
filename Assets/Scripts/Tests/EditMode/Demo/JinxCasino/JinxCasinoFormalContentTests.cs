using System;
using System.IO;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tests.Demo
{
    /// 正式资源装配回归；临时Additive场景只读检查，始终还原原活动场景。
    public sealed class JinxCasinoFormalContentTests
    {
        private const string Root = "Assets/LoadResources/Demos/jinx_casino/";

        [Test]
        public void SavedFourAreaSceneBindsEveryFormalMachineAndKeepsOneLocalCamera()
        {
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(Root + "Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots.SelectMany(value => value.GetComponentsInChildren<JinxCasinoController>(true)).Single();
                var areas = roots.SelectMany(value => value.GetComponentsInChildren<JinxCasinoWorldArea>(true)).ToArray();
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, areas.Select(value => value.Index));
                foreach (var area in areas)
                {
                    var architecture = area.GetComponentsInChildren<Transform>(true).Single(value => value.name == "FormalArchitecture");
                    Assert.That(architecture.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(4));
                    Assert.That(architecture.GetComponentsInChildren<Collider>(true), Is.Empty, "美术内衬不能改变跨区与任务物理");
                    foreach (var surface in architecture.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.That(surface.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                        Assert.That(EditorUtility.IsPersistent(surface.sharedMaterial), Is.True);
                        Assert.That(EditorUtility.IsPersistent(surface.GetComponent<MeshFilter>().sharedMesh), Is.True);
                    }
                }
                var cameras = roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).ToArray();
                Assert.That(cameras.Length, Is.EqualTo(1));
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
                var body = cameras[0].GetComponentInParent<CharacterController>(); Assert.That(body, Is.Not.Null);
                var avatars = roots.SelectMany(value => value.GetComponentsInChildren<JinxCasinoAvatarPresentation>(true)).ToArray();
                Assert.That(avatars.Length, Is.EqualTo(5)); Assert.That(avatars.Count(value => value.ActorRoot == body.transform), Is.EqualTo(1));
                Assert.That(body.height, Is.EqualTo(1.8f)); Assert.That(body.radius, Is.EqualTo(0.3f));
                var stations = roots.SelectMany(value => value.GetComponentsInChildren<JinxCasinoStation>(true)).ToArray();
                Assert.That(stations.Length, Is.EqualTo(21));
                var fixedStations = stations.Where(value => value.GetComponent<JinxCasinoRotationStand>() == null).ToArray();
                CollectionAssert.AreEquivalent(CasinoContentCatalog.Games.Select(value => value.Kind), fixedStations.Select(value => value.Game));
                foreach (var station in stations)
                {
                    Assert.That(station.GetComponent<JinxCasinoStationPresentation>(), Is.Not.Null, station.name);
                    Assert.That(station.transform.Find("FormalVisual"), Is.Not.Null, station.name);
                    Assert.That(station.GetComponentsInChildren<Collider>(true), Is.Not.Empty, "视觉换皮不能丢掉原机台碰撞");
                }
                Assert.That(controller.GetComponent<JinxCasinoAudioDirector>(), Is.Not.Null);
                var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(Root + "Data/AdventureSettings.asset").CreateConfig();
                Assert.That(settings.StageCount, Is.EqualTo(4)); Assert.That(settings.StageDurationMilliseconds, Is.EqualTo(240000));
                Assert.That(settings.AllowedGames, Is.Empty); Assert.That(settings.ShopItemIds, Is.Empty);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SavedHudBindsItemIconsSettingsAndAllThreeEndingArtworksWithoutExtraCanvas()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/UI/JinxCasinoHudView.prefab"); Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<CanvasScaler>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
            var presenter = root.GetComponentInChildren<JinxCasinoAdventurePresenter>(true); Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            foreach (string field in new[] { "choiceGroupDropdown", "choiceOptionDropdown", "actionOptionDropdown", "numberInput", "localSettingsPresenter", "settingsButton", "emoteButton", "endingArtwork" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var endings = serialized.FindProperty("endingArtworks"); Assert.That(endings.arraySize, Is.EqualTo(3));
            for (int index = 0; index < endings.arraySize; index++) Assert.That(endings.GetArrayElementAtIndex(index).FindPropertyRelative("Sprite").objectReferenceValue, Is.Not.Null);
            var template = root.GetComponentInChildren<JinxCasinoItemCard>(true); Assert.That(template, Is.Not.Null);
            var card = new SerializedObject(template); Assert.That(card.FindProperty("iconImage").objectReferenceValue, Is.Not.Null);
            Assert.That(card.FindProperty("itemIcons").arraySize, Is.EqualTo(24));
            for (int index = 0; index < 24; index++) Assert.That(card.FindProperty("itemIcons").GetArrayElementAtIndex(index).FindPropertyRelative("Sprite").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void OriginalAudioAndSpritesAreImportedForSavedRuntimeResources()
        {
            var icons = Directory.GetFiles(Root + "Art/Icons", "*.png"); Assert.That(icons.Length, Is.EqualTo(41));
            foreach (string path in icons)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512, 512)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); Assert.That(importer.mipmapEnabled, Is.False);
            }
            var audio = Directory.GetFiles(Root + "Audio", "*.wav"); Assert.That(audio.Length, Is.EqualTo(14));
            foreach (string path in audio)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.channels, Is.EqualTo(1)); Assert.That(clip.length, Is.GreaterThan(0));
            }
        }
    }
}
