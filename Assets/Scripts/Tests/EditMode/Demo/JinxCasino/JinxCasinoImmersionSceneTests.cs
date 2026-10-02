#if UNITY_EDITOR
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Demo
{
    /// 保存资源的接线检查；不代替真实指针、手柄导航或视觉验收。
    public sealed class JinxCasinoImmersionSceneTests
    {
        private const string Root = "Assets/LoadResources/Demos/jinx_casino";

        [Test]
        public void SavedSampleConfigOpensExactlyThreeGamesInOneArea()
        {
            var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(Root + "/Data/ImmersionSettings.asset");
            Assert.That(settings, Is.Not.Null);
            var config = settings.CreateConfig();
            var games = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack, CasinoGameKind.CooperativeLevers };
            Assert.That(config.StageCount, Is.EqualTo(1));
            Assert.That(config.Targets, Is.EqualTo(new long[] { 1200 }));
            Assert.That(config.EventIntervalMilliseconds, Is.Zero, "尚无事件设施的样板不能开启全量事件。");
            Assert.That(config.AllowedGames, Is.EquivalentTo(games));
            Assert.That(config.InitiallyAvailableGames, Is.EquivalentTo(games));
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(games));
        }

        [Test]
        public void SavedSceneHasUniqueCameraAndMatchingTablePresentations()
        {
            var scene = EditorSceneManager.OpenPreviewScene(Root + "/Scenes/Immersion.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
                var owner = roots.SelectMany(root => root.GetComponentsInChildren<JinxCasinoController>(true)).Single();
                Assert.That(owner.UsesImmersion, Is.True);
                var player = owner.GetComponentInChildren<CharacterController>(true);
                Assert.That(player.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")),
                    "不可见本地胶囊不能挡住聚焦视角的实体点击。");
                var stations = roots.SelectMany(root => root.GetComponentsInChildren<JinxCasinoStation>(true)).ToArray();
                Assert.That(stations.Length, Is.EqualTo(3));
                Assert.That(stations.Select(station => station.StationId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(stations.Sum(station => station.Targets.Count), Is.EqualTo(22));
                foreach (var station in stations)
                {
                    Assert.That(station.HasTableInteraction, Is.True, station.name);
                    var presentation = station.GetComponent<JinxCasinoS1Presentation>();
                    Assert.That(presentation, Is.Not.Null, station.name);
                    Assert.That(presentation.StationId, Is.EqualTo(station.StationId));
                    foreach (var target in station.Targets)
                    {
                        Assert.That(target.transform.IsChildOf(station.transform), Is.True);
                        Assert.That(target.GetComponent<Collider>(), Is.Not.Null, target.name);
                    }
                }
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<JinxCasinoS1PresentationCoordinator>(true)).Count(), Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedPlaquesRenderTheirWholeAmountInsteadOfTruncatingFirstLine()
        {
            var scene = EditorSceneManager.OpenPreviewScene(Root + "/Scenes/Immersion.unity");
            try
            {
                foreach (var visual in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoS1Presentation>(true)))
                {
                    var saved = new SerializedObject(visual);
                    foreach (string field in new[] { "amountText", "resultText" })
                    {
                        var label = saved.FindProperty(field).objectReferenceValue;
                        Assert.That(label, Is.Not.Null);
                        // Tests.EditMode不增加TMP引用；检查真实排版结果，不仅断言字号/矩形常量。
                        const string text = "筹码1000000";
                        label.GetType().GetProperty("text").SetValue(label, text);
                        label.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(label, new object[] { true, true });
                        var info = label.GetType().GetProperty("textInfo").GetValue(label);
                        int count = (int)info.GetType().GetField("characterCount").GetValue(info);
                        var characters = (System.Array)info.GetType().GetField("characterInfo").GetValue(info);
                        int visible = characters.Cast<object>().Take(count).Count(character => (bool)character.GetType().GetField("isVisible").GetValue(character));
                        Assert.That(visible, Is.EqualTo(text.Length), visual.name + "/" + field);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedHudHasAllRequiredMenuAndTouchReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UI/JinxCasinoImmersionHudView.prefab");
            Assert.That(prefab, Is.Not.Null);
            var presenter = prefab.GetComponent<JinxCasinoImmersionHudPresenter>();
            Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            foreach (string field in new[] { "mainMenu", "pauseMenu", "fieldHud", "wallet", "objective", "prompt", "feedback",
                "start", "practice", "resume", "pause", "leave", "interact", "exitTable", "movePad", "lookPad" })
                Assert.That(serialized.FindProperty(field)?.objectReferenceValue, Is.Not.Null, field);
        }
    }
}
#endif
