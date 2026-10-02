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
        public void SavedCounterAllowsOldInventoryWithoutSellingTheItemAgain()
        {
            var scene = EditorSceneManager.OpenPreviewScene(Root + "/Scenes/Immersion.unity");
            try
            {
                var counter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<JinxCasinoShopCounter>(true)).Single();
                Assert.That(counter.CounterId, Is.EqualTo("s1.supply"));
                Assert.That(counter.ProductCount, Is.EqualTo(3));
                Assert.That(counter.Targets.Select(target => target.TargetId).Distinct().Count(), Is.EqualTo(6));
                var state = new CasinoAdventureState { Coins = 1000, Config = new CasinoAdventureConfig() };
                state.Config.ShopItemIds = new[] { "redraw_card" };
                state.Inventory.Add(new CasinoInventoryEntry { ItemId = "duo_wrench", Count = 1 });
                counter.Present(state, 0, null);
                Assert.That(counter.Targets.Single(target => target.Action == JinxCasinoTableAction.PurchaseProduct).IsAvailable, Is.False);
                var use = counter.Targets.Single(target => target.Action == JinxCasinoTableAction.UseProduct);
                Assert.That(use.IsAvailable, Is.True, "已有库存不受本局售卖列表限制，实体按钮与次要动作应一致。");
                state.Inventory.Clear(); counter.Present(state, 0, null);
                Assert.That(use.IsAvailable, Is.False);
                Assert.That(state.Coins, Is.EqualTo(1000), "展示报价不能修改资金。");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

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
        public void AllRulesBoardsFitInsideTheirSavedTableView()
        {
            var scene = EditorSceneManager.OpenPreviewScene(Root + "/Scenes/Immersion.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
                camera.aspect = 16f / 9;
                foreach (var station in roots.SelectMany(root => root.GetComponentsInChildren<JinxCasinoStation>(true)))
                {
                    camera.transform.SetPositionAndRotation(station.FocusPose.position, station.FocusPose.rotation);
                    camera.fieldOfView = station.FocusFieldOfView;
                    foreach (var target in station.Targets)
                    {
                        Vector3 point = camera.WorldToViewportPoint(target.transform.position);
                        Assert.That(point.z, Is.GreaterThan(0), target.TargetId);
                        Assert.That(point.x, Is.InRange(.02f, .98f), target.TargetId + "操作目标横向越界");
                        Assert.That(point.y, Is.InRange(.08f, .92f), target.TargetId + "操作目标被边缘HUD遮挡");
                    }
                    var frame = station.transform.Find("RulesPlacard/Frame");
                    Assert.That(frame, Is.Not.Null, station.name);
                    foreach (float x in new[] { -.5f, .5f }) foreach (float y in new[] { -.5f, .5f })
                    {
                        Vector3 point = camera.WorldToViewportPoint(frame.TransformPoint(new Vector3(x, y, .5f)));
                        Assert.That(point.z, Is.GreaterThan(0), station.name);
                        Assert.That(point.x, Is.InRange(.02f, .98f), station.name + "规则牌横向越界");
                        Assert.That(point.y, Is.InRange(.08f, .9f), station.name + "规则牌被边缘HUD遮挡");
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
