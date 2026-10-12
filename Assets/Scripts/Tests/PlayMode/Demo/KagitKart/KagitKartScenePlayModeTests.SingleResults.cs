#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed partial class KagitKartScenePlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator SingleRaceResultsRestartActualRaceThenReturnToTitle()
        {
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ToCoroutine();
            KagitKartWorld world = null;
            yield return Wait(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null
                && !world.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "单场封面入口");
            yield return world.StartRaceAsync(KagitKartRaceMode.SingleRace, "meadow", "tilki", 120).ToCoroutine();
            for (int run = 0; run < 2; run++)
            {
                yield return Wait(() => world.Session != null && !world.Busy && world.IntroPlaying, "单场开场");
                world.SendMessage("OnApplicationFocus", true);
                world.Dispatch(KagitKartCommand.Resume);
                world.Dispatch(KagitKartCommand.SkipIntro);
                var session = world.Session;
                var hud = UIManager.Instance.Get<KagitKartHudView>();
                Assert.That(hud.transform.Find("Viewport").gameObject.activeInHierarchy, Is.True, "本场 HUD 正常显示");
                typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Autopilot)).SetValue(session.Player, true);
                // 沿现有 GP 场景测试，正式 AI 和固定步真正完成三圈，不合成结算数据。
                for (int step = 0; step < 120 * 480 && session.Race.State != KagitKartRaceState.Done; step++)
                {
                    session.AdvanceFrame(KagitKartSimulationClock.Step, default);
                    if (step % 240 == 0) yield return null;
                }
                Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Done));
                Assert.That(world.PlayerEntry.Progress.LapTimes.Count, Is.EqualTo(3));
                yield return Wait(() => !world.Busy && UIManager.Instance.Get<KagitKartResultsView>()?.State == ViewState.Visible, "单场真实成绩页");
                var results = UIManager.Instance.Get<KagitKartResultsView>();
                var rows = results.gameObject.GetComponentsInChildren<KagitKartResultRow>();
                Assert.That(rows.Length, Is.EqualTo(8), "单场八名车手全部显示");
                Assert.That(rows.All(row => row.transform.Find("Points").GetComponent<TMPro.TMP_Text>().text == ""
                    && row.transform.Find("Bonus").GetComponent<TMPro.TMP_Text>().text == ""), Is.True,
                    "单场没有 GP 总分或本场积分增量");
                Assert.That(results.transform.Find("Viewport/BoardFrame/SheetGroup/Sheet/Header/HeadingPoints").gameObject.activeInHierarchy, Is.False);
                Assert.That(results.transform.Find("Viewport/BoardFrame/SheetGroup/Sheet/Header/HeadingBest").gameObject.activeInHierarchy, Is.True,
                    "普通比赛保留最佳圈列");
                Assert.That(UIManager.Instance.Get<KagitKartHudView>(), Is.SameAs(hud), "结束仍长驻到 StopRound");
                Assert.That(hud.transform.Find("Viewport").gameObject.activeInHierarchy, Is.False, "成绩页隐藏比赛 HUD");
                Assert.That(hud.gameObject.GetComponentInChildren<KagitKartHudInk>(true).gameObject.activeInHierarchy, Is.False, "结束隐藏 Ink 独立层");
                Assert.That(results.gameObject.GetComponentsInChildren<Button>(true).Single(button => button.name == "Next").gameObject.activeInHierarchy,
                    Is.False, "单场不出现总榜或下一章入口");
                Assert.That(UIManager.Instance.Get<KagitKartStandingsView>(), Is.Null);
                var restart = Button(results, "Restart");
                Assert.That(restart.IsInteractable(), Is.True);
                Assert.That(restart.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("再赛一场"));
                if (run == 0)
                {
                    yield return Wait(() => rows.All(row => row.GetComponent<CanvasGroup>().alpha > 0.999f), "单场表行实际入场结束");
                    yield return KeyPress(Key.LeftArrow);
                    EventSystem.current.SetSelectedGameObject(restart.gameObject);
                    yield return null;
                    Assert.That(restart.GetComponent<UIState>().InteractionState, Is.EqualTo("Focused"));
                    Assert.That(restart.targetGraphic.color, Is.EqualTo((Color)new Color32(45, 42, 50, 255)), "选中仍为原作墨底");
                    Assert.That(restart.transform.Find("FocusInnerEdge").gameObject.activeSelf, Is.True);
                    Assert.That(((RectTransform)restart.transform).anchoredPosition.y, Is.GreaterThan(0));
                    EventSystem.current.SetSelectedGameObject(Button(results, "Hub").gameObject);
                    yield return null;
                    Assert.That(restart.transform.Find("FocusInnerEdge").gameObject.activeSelf, Is.False);
                    Assert.That(((RectTransform)restart.transform).anchoredPosition.y, Is.Zero);
                    EventSystem.current.SetSelectedGameObject(restart.gameObject);
                    yield return null;
                    yield return Capture("SingleRaceResults");
                    EventSystem.current.SetSelectedGameObject(restart.gameObject);
                    yield return KeyPress(Key.Enter);
                    yield return Wait(() => world.Session != null && !ReferenceEquals(world.Session, session)
                        && !world.Busy && world.IntroPlaying, "真实重赛按钮创建新场");
                    Assert.That(world.Mode, Is.EqualTo(KagitKartRaceMode.SingleRace));
                    Assert.That(world.TrackId, Is.EqualTo("meadow"));
                    Assert.That(world.CharacterId, Is.EqualTo("tilki"));
                    Assert.That(world.SpeedClass, Is.EqualTo(120));
                    Assert.That(world.GrandPrix, Is.Null);
                    Assert.That(UIManager.Instance.Get<KagitKartResultsView>(), Is.Null, "重赛释放旧成绩 Modal");
                    Assert.That(UIManager.Instance.Get<KagitKartHudView>(), Is.Not.SameAs(hud), "重赛绑定新 HUD 和新会话");
                }
                else
                {
                    var menu = Button(results, "Hub");
                    Assert.That(menu.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("返回菜单"));
                    EventSystem.current.SetSelectedGameObject(menu.gameObject);
                    yield return KeyPress(Key.Enter);
                }
            }
            yield return Wait(() => !world.Busy && world.Session == null
                && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "第二场结束后实际按钮返回封面");
            Assert.That(UIManager.Instance.Get<KagitKartResultsView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<KagitKartHudView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<KagitKartStandingsView>(), Is.Null);
            Assert.That(Object.FindObjectsByType<KagitKartSceneryWorld>(FindObjectsSortMode.None), Is.Empty);
        }
    }
}
#endif
