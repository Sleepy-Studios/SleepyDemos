#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    // 合入现有类的内容；复用其SetUp/TearDown、Wait和Capture，勿新建测试程序集。
    public sealed partial class KagitKartScenePlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator NativeHudUsesRealCountdownAndInventory()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "HUD场景入口");
            try
            {
                yield return world.StartRaceAsync(KagitKartRaceMode.TimeTrial, "meadow", "tilki", 120).ToCoroutine();
                world.SendMessage("OnApplicationFocus", true);
                world.Dispatch(KagitKartCommand.Resume);
                world.Dispatch(KagitKartCommand.SkipIntro);
                yield return null;
                var hud = UIManager.Instance.Get<KagitKartHudView>();
                var slot = hud.gameObject.GetComponentInChildren<KagitKartItemSlot>(true);
                var notice = hud.gameObject.GetComponentInChildren<KagitKartHudNotice>(true);
                Assert.That(slot, Is.Not.Null);
                Assert.That(notice, Is.Not.Null);
                var player = world.Session.Player;
                Assert.That(player.Item, Is.EqualTo("rocket3"));
                Assert.That(player.ItemCount, Is.EqualTo(3));
                AssertHudInventory(slot, player);
                Assert.That(HudField<TMP_Text>(slot, "count").text, Is.EqualTo("×3"));
                Assert.That(HudField<TMP_Text>(notice, "totalText").gameObject.activeSelf, Is.False, "单人不写/1。");
                var countdown = HudField<RectTransform>(notice, "countdown");
                Assert.That(countdown.gameObject.activeSelf, Is.True);
                Assert.That(countdown.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("3"));
                yield return WaitForPresentation(0.55f);
                yield return Wait(() => world.Session.Race.Countdown == 2, "原作第二声倒计时");
                yield return WaitForPresentation(0.55f);
                yield return Wait(() => world.Session.Race.Countdown == 1, "原作第三声倒计时");
                yield return WaitForPresentation(0.55f);
                yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "真实倒计时发出Go");
                world.Dispatch(KagitKartCommand.Pause);
                yield return Wait(() => world.Session.Pause.IsPaused, "Go期间真实暂停");
                double frozen = world.Session.RaceTime;
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(countdown.gameObject.activeSelf, Is.True, "Go的0.9秒JS寿命在暂停时冻结。");
                Assert.That(world.Session.RaceTime, Is.EqualTo(frozen));
                world.Dispatch(KagitKartCommand.Resume);
                yield return Wait(() => !world.Session.Pause.IsPaused && !countdown.gameObject.activeSelf, "恢复后Go按寿命消失");

                // 控制宿主帧以避免测试固定步和正式帧重复推进；下列仍走真实Items.Advance规则。
                world.enabled = false;
                double itemTime = world.Session.RaceTime;
                player.Controls = new KagitKartControls { Item = true };
                world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                slot.Advance(0, world.ReducedMotion);
                Assert.That(player.ItemCount, Is.EqualTo(2));
                AssertHudInventory(slot, player);

                // 库存gum是表现前置样本；持有/释放由正式Use/StepHeld完成，不宣称随机抽中gum。
                HudSet(player, nameof(KagitKartVehicle.Item), "gum");
                HudSet(player, nameof(KagitKartVehicle.ItemCount), 1);
                player.Controls = default;
                world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                player.Controls = new KagitKartControls { Item = true };
                world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                slot.Advance(0, world.ReducedMotion);
                Assert.That(player.ItemHeld, Is.True);
                AssertHudInventory(slot, player);
                Assert.That(HudField<RectTransform>(slot, "heldBorder").gameObject.activeSelf, Is.True);
                Assert.That(HudField<TMP_Text>(slot, "count").gameObject.activeInHierarchy, Is.False);
                yield return WaitForPresentation(0);
                player.Controls = default;
                world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                slot.Advance(0, world.ReducedMotion);
                Assert.That(player.ItemHeld, Is.False);
                Assert.That(HudField<RectTransform>(slot, "heldBorder").gameObject.activeSelf, Is.False);
                Assert.That(HudField<Image>(slot, "icon").enabled, Is.False);

                world.enabled = true;
                yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
                world.Dispatch(KagitKartCommand.Resume);
                world.Dispatch(KagitKartCommand.SkipIntro);
                yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "真实八车倒计时");
                world.enabled = false;
                hud = UIManager.Instance.Get<KagitKartHudView>();
                slot = hud.gameObject.GetComponentInChildren<KagitKartItemSlot>(true);
                player = world.Session.Player;
                var box = world.Session.Items.Boxes.First(value => value.Active);
                player.PlaceAt(box.Position, 0);
                itemTime = world.Session.RaceTime;
                world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                slot.Advance((float)KagitKartSimulationClock.Step, world.ReducedMotion);
                Assert.That(box.TakenBy, Is.SameAs(player));
                Assert.That(player.Roulette, Is.GreaterThan(0));
                var reel = HudField<RectTransform>(slot, "reel");
                Assert.That(reel.gameObject.activeSelf, Is.True);
                Vector2 frozenReel = reel.anchoredPosition;
                slot.Advance(0, world.ReducedMotion);
                Assert.That(reel.anchoredPosition, Is.EqualTo(frozenReel), "暂停感知dt=0不推进轮盘。");
                yield return WaitForPresentation(0);
                for (int step = 0; step < 240 && player.Roulette > 0; step++)
                {
                    world.Session.Items.Advance(KagitKartSimulationClock.Step, itemTime += KagitKartSimulationClock.Step, true);
                    slot.Advance((float)KagitKartSimulationClock.Step, world.ReducedMotion);
                }
                Assert.That(player.Item, Is.Not.Null);
                Assert.That(reel.gameObject.activeSelf, Is.False);
                AssertHudInventory(slot, player);
                yield return WaitForPresentation(0);
            }
            finally
            {
                if (world != null)
                {
                    // 直接Items.Advance已经领先Session钟；失败时也不恢复宿主，避免下一帧重复推进。
                    world.enabled = false;
                }
            }
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null
                && UIManager.Instance.Get<KagitKartHudView>() == null, "实际退出释放HUD");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NativeHudPresentationFixturesKeepSourceLifetimesAndReleaseSubscriptions()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "通知表现入口");
            yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
            world.SendMessage("OnApplicationFocus", true);
            world.Dispatch(KagitKartCommand.Resume);
            world.Dispatch(KagitKartCommand.SkipIntro);
            yield return Wait(() => world.Session.Race.State == KagitKartRaceState.Running, "真实场景和规则就绪");
            string reduced = world.Settings.ReducedMotion;
            world.enabled = false;
            world.Settings.ReducedMotion = "off"; // 仅此停止宿主的表现样本，不Dispatch，不写偏好。
            var session = world.Session;
            var notice = UIManager.Instance.Get<KagitKartHudView>().gameObject.GetComponentInChildren<KagitKartHudNotice>(true);
            try
            {
                notice.Bind(world);
                notice.Bind(world);
                AssertHudSubscriptions(session, notice, 1);
                // 以下显式构造HUD事件/显示状态，只验收表现，不证明驾驶或完赛规则。
                HudRaceFixture(notice, session.Player, "countdown", 2);
                notice.Advance(0, 0.04f);
                yield return WaitForPresentation(0);
                var text = HudField<KagitKartCountdownCard>(notice, "countdownCard");
                float earlyHeight = text.canvasRenderer.GetMesh().bounds.size.y;
                Assert.That(text.sprite, Is.Not.Null, "使用正式原字形Sprite");
                notice.Advance(0, 0.5f);
                yield return new WaitForEndOfFrame();
                Assert.That(Mathf.Abs(text.canvasRenderer.GetMesh().bounds.size.y - earlyHeight), Is.GreaterThan(1), "实际TMP网格应折卡投影。");
                HudRaceFixture(notice, session.Player, "countdown", 0);
                notice.Advance(0.89f, 0.5f);
                Assert.That(HudField<RectTransform>(notice, "countdown").gameObject.activeSelf, Is.True);
                notice.Advance(0.02f, 0);
                Assert.That(HudField<RectTransform>(notice, "countdown").gameObject.activeSelf, Is.False);

                HudSet(session.Race, nameof(KagitKartRace.StartGrade), KagitKartStartGrade.Good);
                HudSet(session.Player, nameof(KagitKartVehicle.Place), 2);
                HudRaceFixture(notice, session.Player, "raceStart");
                HudRaceFixture(notice, session.Player, "place", 2);
                HudRaceFixture(notice, session.Player, "checkpoint", split: -1.25);
                HudRaceFixture(notice, session.Player, "lap", 2);
                HudRaceFixture(notice, session.Player, "finalLap");
                HudRaceFixture(notice, session.Player, "wrongWay", 1);
                HudThreatFixture(notice, session.Player, "homing", 2);
                notice.Advance(0, 0.32f);
                Assert.That(HudField<TMP_Text>(notice, "placeText").text, Is.EqualTo("2."));
                Assert.That(HudField<TMP_Text>(notice, "totalText").text, Is.EqualTo("/8"));
                Assert.That(HudField<TMP_Text>(notice, "splitText").text, Is.EqualTo("−1.25"));
                ColorUtility.TryParseHtmlString("#5f7fa3", out var aheadColor);
                Assert.That(HudField<RectTransform>(notice, "split").GetComponent<Image>().color, Is.EqualTo(aheadColor));
                Assert.That(HudField<RectTransform>(notice, "wrong").gameObject.activeSelf, Is.True);
                Assert.That(HudTransient(notice, "Flip"), Is.Not.Null);
                var threat = HudField<RectTransform>(notice, "threat");
                Assert.That(threat.gameObject.activeSelf, Is.True);
                var slot = UIManager.Instance.Get<KagitKartHudView>().gameObject.GetComponentInChildren<KagitKartItemSlot>(true);
                Assert.That(HudField<Image>(notice, "threatIcon").sprite, Is.SameAs(slot.Icon("homing")));
                yield return WaitForPresentation(0);
                HudRaceFixture(notice, session.Player, "checkpoint", split: 0.25);
                Assert.That(HudField<TMP_Text>(notice, "splitText").text, Is.EqualTo("+0.25"));
                ColorUtility.TryParseHtmlString("#d9483b", out var behindColor);
                Assert.That(HudField<RectTransform>(notice, "split").GetComponent<Image>().color, Is.EqualTo(behindColor));
                // 目标方向是表现前置样本；不把这个弹体当成Items规则发射验收。
                var projectiles = HudField<IList>(session.Items, "projectiles");
                var projectile = new KagitKartProjectile
                {
                    Target = session.Player,
                    Kind = "homing",
                    Position = session.Player.Position + new Vector3(-10, 0, 5)
                };
                float heading = session.Player.Heading;
                HudSet(session.Player, nameof(KagitKartVehicle.Heading), 0f);
                projectiles.Insert(0, projectile);
                HudThreatFixture(notice, session.Player, "homing", 0.6f);
                notice.Advance(0, 0.1f);
                Assert.That(ScreenPoint(threat).x, Is.GreaterThan(Screen.width / 2f));
                Assert.That(ScreenPoint(threat).y, Is.GreaterThan(Screen.height / 2f));
                Assert.That(HudField<Image>(notice, "threatRing").fillAmount, Is.EqualTo(0.3f).Within(0.001f));
                yield return WaitForPresentation(0);
                projectiles.Remove(projectile);
                HudSet(session.Player, nameof(KagitKartVehicle.Heading), heading);
                var banner = HudTransient(notice, "Banner");
                var grade = HudTransient(notice, "Grade");
                float eta = HudField<Image>(notice, "threatRing").fillAmount;
                Quaternion wrongBefore = HudField<RectTransform>(notice, "wrong").localRotation;
                notice.Advance(0, 0.37f);
                Assert.That(HudField<Image>(notice, "threatRing").fillAmount, Is.EqualTo(eta), "JS时钟停止时ETA不走。");
                Assert.That(HudField<RectTransform>(notice, "wrong").localRotation, Is.Not.EqualTo(wrongBefore), "CSS墙钟仍摇摆。");
                Assert.That(banner.gameObject.activeSelf, Is.True);
                Assert.That(grade.gameObject.activeSelf, Is.True);
                Assert.That(HudField<CanvasGroup>(notice, "splitGroup").alpha, Is.GreaterThan(0.99f));

                notice.Advance(1.19f, 0);
                Assert.That(HudTransient(notice, "Flip"), Is.Not.Null);
                notice.Advance(0.02f, 0);
                Assert.That(HudTransient(notice, "Flip"), Is.Null, "换圈JS寿命1.2秒。");
                Assert.That(HudTransient(notice, "Grade"), Is.Not.Null);
                notice.Advance(0.5f, 0);
                Assert.That(HudTransient(notice, "Grade"), Is.Null, "评级JS寿命1.7秒。");
                notice.Advance(0.2f, 0.35f);
                Assert.That(banner.GetComponent<CanvasGroup>().alpha, Is.LessThan(0.001f), "1.9秒切away并按墙钟淡出。");
                notice.Advance(0.4f, 0);
                Assert.That(HudTransient(notice, "Banner"), Is.Null, "最后一圈2.3秒移除。");
                notice.Advance(0.3f, 0.21f);
                Assert.That(HudField<CanvasGroup>(notice, "splitGroup").alpha, Is.LessThan(0.001f), "分段差2.6秒后CSS淡出。");
                Assert.That(threat.gameObject.activeSelf, Is.False, "ETA低于-0.35删除。");

                HudSet(session.Player, nameof(KagitKartVehicle.Finished), true);
                HudSet(world.PlayerEntry, nameof(KagitKartRaceEntry.FinishTime), 12.34);
                HudSet(session.Race, nameof(KagitKartRace.State), KagitKartRaceState.Finishing);
                HudSet(session.Race, nameof(KagitKartRace.WaitLeft), 0.6);
                HudRaceFixture(notice, session.Player, "finish", 2);
                notice.Advance(0, 0.95f);
                Assert.That(HudField<RectTransform>(notice, "wrong").gameObject.activeSelf, Is.False);
                Assert.That(HudField<TMP_Text>(notice, "clockText").text, Is.EqualTo("0:12.34"));
                Assert.That(HudField<RectTransform>(notice, "finish").gameObject.activeSelf, Is.True);
                Assert.That(HudField<RectTransform>(notice, "waiting").gameObject.activeSelf, Is.True);
                StringAssert.Contains("1 秒", HudField<RectTransform>(notice, "waiting").GetComponentInChildren<TMP_Text>().text);
                yield return WaitForPresentation(0);
                HudSet(session.Race, nameof(KagitKartRace.WaitLeft), 0.0);
                notice.Advance(0, 0);
                Assert.That(HudField<RectTransform>(notice, "waiting").gameObject.activeSelf, Is.False);
                Assert.That(HudField<RectTransform>(notice, "finish").gameObject.activeSelf, Is.True, "冲线牌保持至HUD退出。");
                notice.Clear();
                AssertHudSubscriptions(session, notice, 0);
                Assert.That(HudField<object>(notice, "session"), Is.Null);
                Assert.That(HudField<RectTransform>(notice, "finish").gameObject.activeSelf, Is.False);
            }
            finally
            {
                notice.Clear();
                world.Settings.ReducedMotion = reduced;
                // 样本修改了显示状态；直接退出，不恢复这些状态参加比赛。
                world.enabled = false;
            }
            world.Dispatch(KagitKartCommand.Hub);
            yield return Wait(() => Object.FindFirstObjectByType<KagitKartWorld>() == null
                && UIManager.Instance.Get<KagitKartHudView>() == null, "通知样本退出收口");
        }

        private static T HudField<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void HudSet(object owner, string property, object value) =>
            owner.GetType().GetProperty(property).SetValue(owner, value);

        private static void AssertHudInventory(KagitKartItemSlot slot, KagitKartVehicle player)
        {
            var icon = HudField<Image>(slot, "icon");
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.SameAs(slot.Icon(player.Item)), "库存图标须使用实际得到的道具ID。");
        }

        private static void HudRaceFixture(KagitKartHudNotice notice, KagitKartVehicle player, string kind, int number = 0, double? split = null)
        {
            var signal = Activator.CreateInstance(typeof(KagitKartRaceEvent), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { kind, player, number, 0.0, split, false }, null);
            typeof(KagitKartHudNotice).GetMethod("OnRace", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(notice, new[] { signal });
        }

        private static void HudThreatFixture(KagitKartHudNotice notice, KagitKartVehicle player, string kind, float eta)
        {
            var signal = Activator.CreateInstance(typeof(KagitKartItemEvent), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { "threat", kind, Vector3.zero, player, null, null, eta, false }, null);
            typeof(KagitKartHudNotice).GetMethod("OnItem", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(notice, new[] { signal });
        }

        private static RectTransform HudTransient(KagitKartHudNotice notice, string kind)
        {
            foreach (object value in HudField<IEnumerable>(notice, "notices"))
            {
                var type = value.GetType();
                if (type.GetField("kind").GetValue(value).ToString() == kind)
                {
                    return (RectTransform)type.GetField("root").GetValue(value);
                }
            }
            return null;
        }

        private static void AssertHudSubscriptions(KagitKartSession session, KagitKartHudNotice notice, int count)
        {
            foreach (object source in new object[] { session.Race, session.Items, session.Player })
            {
                var callback = (Delegate)source.GetType().GetField("Signaled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
                int actual = callback?.GetInvocationList().Count(value => ReferenceEquals(value.Target, notice)) ?? 0;
                Assert.That(actual, Is.EqualTo(count), source.GetType().Name + "通知订阅数量");
            }
        }
    }
}
#endif
