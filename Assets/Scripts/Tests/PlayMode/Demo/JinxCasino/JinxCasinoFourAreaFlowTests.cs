#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 四区正式入口回归；仅复制运行时配置，专有存档/档案目录不接触用户进度。
    public sealed class JinxCasinoFourAreaFlowTests
    {
        private readonly List<JinxCasinoGameSettings> configurations = new List<JinxCasinoGameSettings>();
        private GameViewResolution resolution;
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter presenter;
        private JinxCasinoHudView hud;
        private JinxCasinoWorldArea[] areas;
        private JinxCasinoStation[] fixedStations;
        private CharacterController body;
        private CasinoLocalSaveStore store;
        private CasinoProfileStore profileStore;
        private string saveDirectory;

        [UnityTest, Timeout(240000)]
        public IEnumerator FourAreasPhysicalGatesShoppingCheckpointsAndDignifiedEnding()
        {
            yield return EnterP2();
            yield return Screenshot("P2Menu720p");
            Click(Field<Button>(presenter, "standardButton")); yield return null;
            Assert.That(controller.AdventureState.Config.StageCount, Is.EqualTo(4));
            Assert.That(controller.AdventureState.Config.StageDurationMilliseconds, Is.EqualTo(240000));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(1000));
            var firstGate = VerifyLockedGate(areas[1]);

            yield return CompleteCurrentStage();
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(1000), "区域额度只是门槛，不收费");
            yield return Screenshot("P2FirstShopping720p");
            yield return ContinueAfterShopping();
            Assert.That(Vector3.Distance(body.transform.position, areas[1].SafePosition), Is.LessThan(0.15f));
            VerifyOpenGate(firstGate);

            yield return OpenFixedStation(CasinoGameKind.CoinFlip);
            Assert.That(controller.BeginAdventureGame("checkpoint-bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            yield return null;
            long checkpointCoins = controller.AdventureState.Coins;
            Assert.That(controller.SaveAdventure(1), Is.True);
            Assert.That(store.Load(1).State.StageIndex, Is.EqualTo(1));
            Assert.That(controller.BeginAdventureGame("after-save", CasinoGameKind.CoinFlip, 50, 0).Success, Is.True);
            yield return null;
            Assert.That(controller.AdventureState.Coins, Is.Not.EqualTo(checkpointCoins));
            Assert.That(controller.LoadAdventure(1), Is.True); yield return null;
            Assert.That(controller.AdventureState.StageIndex, Is.EqualTo(1));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(checkpointCoins));
            Assert.That(controller.BeginAdventureGame("checkpoint-bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            yield return null;
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(checkpointCoins), "阶段恢复后重发已支付请求不能重新开奖入账");
            Click(Field<Button>(presenter, "machineCloseButton")); yield return null;

            while (controller.AdventureState.Phase != CasinoAdventurePhase.Finale)
            {
                int stage = controller.AdventureState.StageIndex;
                var nextGate = stage < 3 ? VerifyLockedGate(areas[stage + 1]) : null;
                yield return CompleteCurrentStage();
                if (controller.AdventureState.Phase == CasinoAdventurePhase.Shopping)
                {
                    yield return ContinueAfterShopping();
                    VerifyOpenGate(nextGate);
                }
            }
            Assert.That(controller.AdventureState.CompletedStages, Is.EqualTo(4));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(checkpointCoins));
            yield return Resize(1600, 720);
            yield return Screenshot("P2Finale20x9");
            yield return OpenProfileAndReturnToEnding(CasinoAdventurePhase.Finale);
            Click(Field<Button>(presenter, "leaveEndingButton")); yield return null;
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.LeaveWithDignity));
            Assert.That(controller.ProfileData.DignifiedExits, Is.EqualTo(1));
            Assert.That(profileStore.LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
            yield return OpenProfileAndReturnToEnding(CasinoAdventurePhase.Ended);
            yield return AssertSelectedEndingArtwork(CasinoAdventureEnding.LeaveWithDignity);
            yield return Screenshot("P4EndingDignity20x9");
            yield return ExitThroughSavedPanel();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TakeOverNeedsFourStagesAndThreePlayerVisibleVaultClues()
        {
            yield return EnterP2();
            Click(Field<Button>(presenter, "standardButton")); yield return null;
            yield return AdvanceAllFourStages();
            Assert.That(controller.AdventureState.TakeOverUnlocked, Is.False);
            Assert.That(Field<Button>(presenter, "takeoverButton").interactable, Is.False);
            Click(Field<Button>(presenter, "vaultChallengeButton")); yield return null;
            SetText(Field<object>(presenter, "stakeInput"), "10");
            SetText(Field<object>(presenter, "choiceInput"), "0");
            yield return null;
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            Assert.That(controller.HasActiveAdventureRound, Is.True);
            yield return RevealVisibleVaultClues();
            yield return Screenshot("P2VisibleVaultClues720p");
            int code = int.Parse(VisibleVaultClues());
            yield return SubmitAction(CasinoMiniGameAction.EnterCode, code);
            Assert.That(controller.HasActiveAdventureRound, Is.False);
            Assert.That(controller.AdventureState.TakeOverUnlocked, Is.True);
            Assert.That(controller.AdventureState.LastRoundPayout, Is.GreaterThan(controller.AdventureState.LastRoundCost));
            Click(Field<Button>(presenter, "machineCloseButton")); yield return null;
            Assert.That(Field<Button>(presenter, "takeoverButton").interactable, Is.True);
            yield return Resize(1600, 720); yield return Screenshot("P2TakeoverChoice20x9");
            Click(Field<Button>(presenter, "takeoverButton")); yield return null;
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.TakeOver));
            Assert.That(controller.ProfileData.Takeovers, Is.EqualTo(1));
            Assert.That(profileStore.LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
            yield return AssertSelectedEndingArtwork(CasinoAdventureEnding.TakeOver);
            yield return Screenshot("P4EndingTakeover20x9");
            yield return ExitThroughSavedPanel();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedFourAreaRunWithdrawsWithoutUnlockingLaterStages()
        {
            yield return EnterP2(config => { config.Targets = new long[] { 10000, 10000, 10000, 10000 }; config.StageDurationMilliseconds = 2500; });
            Click(Field<Button>(presenter, "standardButton")); yield return null;
            yield return Wait(() => controller.AdventureState.Phase == CasinoAdventurePhase.Failed, "专有短计时副本触发真实失败", 10);
            Assert.That(controller.AdventureState.CompletedStages, Is.Zero);
            Assert.That(Field<GameObject>(areas[1], "lockedGate").activeSelf, Is.True);
            Assert.That(Field<GameObject>(areas[3], "contents").activeInHierarchy, Is.False);
            yield return Resize(1600, 720); yield return Screenshot("P2Failed20x9");
            Click(Field<Button>(presenter, "withdrawButton")); yield return null;
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(controller.ProfileData.Withdrawals, Is.EqualTo(1));
            Assert.That(profileStore.LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
            yield return AssertSelectedEndingArtwork(CasinoAdventureEnding.Withdraw);
            yield return Screenshot("P4EndingWithdraw20x9");
            yield return ExitThroughSavedPanel();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PracticeOpensAllPhysicalAreasAndExercisesSeventeenDistinctGameRules()
        {
            yield return EnterP2();
            Click(Field<Button>(presenter, "practiceButton")); yield return null;
            Assert.That(controller.AdventureState.Mode, Is.EqualTo(CasinoAdventureMode.Practice));
            foreach (var area in areas)
            {
                Assert.That(Field<GameObject>(area, "contents").activeInHierarchy, Is.True);
                var gate = Field<GameObject>(area, "lockedGate"); if (gate != null) Assert.That(gate.activeSelf, Is.False);
            }
            Assert.That(controller.GetAvailableAdventureGames(), Has.Length.EqualTo(17));
            var played = new HashSet<CasinoGameKind>();
            foreach (CasinoGameKind game in Enum.GetValues(typeof(CasinoGameKind)))
            {
                yield return null;
                Assert.That(controller.RefillPractice().Success, Is.True);
                yield return null;
                yield return OpenFixedStation(game);
                SetText(Field<object>(presenter, "stakeInput"), "10");
                SetText(Field<object>(presenter, "choiceInput"), game == CasinoGameKind.CoinFlip ? "2" : "0");
                yield return null;
                Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
                Assert.That(controller.AdventureState.ActiveGame, Is.EqualTo(game));
                yield return CompletePracticeGame(game);
                Assert.That(controller.HasActiveAdventureRound, Is.False, game.ToString());
                Assert.That(controller.AdventureState.LockedCoins, Is.Zero);
                Assert.That(controller.AdventureState.LastRoundCost, Is.InRange(0, 10));
                Assert.That(controller.AdventureState.Coins, Is.GreaterThanOrEqualTo(0));
                played.Add(game);
                if (game == CasinoGameKind.Blackjack || game == CasinoGameKind.CooperativeVault) yield return Screenshot("P2Practice" + game + "720p");
                Click(Field<Button>(presenter, "machineCloseButton")); yield return null;
            }
            Assert.That(played.Count, Is.EqualTo(17));
            Assert.That(controller.ProfileData.FinishedRuns, Is.Zero, "练习不能写正式成长战绩");
            yield return Resize(1600, 720); yield return Screenshot("P2PracticeAllAreas20x9");
            Click(Field<Button>(presenter, "slotsButton")); yield return null;
            yield return ExitThroughSavedPanel();
        }

        private IEnumerator OpenProfileAndReturnToEnding(CasinoAdventurePhase expectedPhase)
        {
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(expectedPhase));
            Assert.That(Field<GameObject>(presenter, "endingPanel").activeInHierarchy, Is.True);
            long coins = controller.AdventureState.Coins;
            long finished = controller.ProfileData.FinishedRuns;
            long fame = controller.ProfileData.Fame;
            var profile = Field<JinxCasinoProfilePresenter>(presenter, "profilePresenter");
            Click(Field<Button>(presenter, "profileButton")); yield return null;
            Assert.That(profile.gameObject.activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(presenter, "endingPanel").activeInHierarchy, Is.False);
            Click(Field<Button>(profile, "closeButton")); yield return null;
            Assert.That(profile.gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>(presenter, "endingPanel").activeInHierarchy, Is.True, "档案关闭必须按原阶段返回结局，不落到场地");
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(expectedPhase));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(coins));
            Assert.That(controller.ProfileData.FinishedRuns, Is.EqualTo(finished)); Assert.That(controller.ProfileData.Fame, Is.EqualTo(fame));
            if (expectedPhase == CasinoAdventurePhase.Finale)
            {
                Assert.That(Field<Button>(presenter, "leaveEndingButton").isActiveAndEnabled, Is.True);
                Assert.That(Field<Button>(presenter, "takeoverButton").interactable, Is.EqualTo(controller.AdventureState.TakeOverUnlocked));
            }
            else
            {
                Assert.That(Text(Field<object>(presenter, "endingText")), Does.Contain("已登记声望"));
                Assert.That(Field<Button>(presenter, "leaveEndingButton").gameObject.activeSelf, Is.False, "结束后显示报告，不能再次选结局领奖");
            }
        }

        private IEnumerator AssertSelectedEndingArtwork(CasinoAdventureEnding ending)
        {
            var image = Field<Image>(presenter, "endingArtwork");
            Assert.That(image, Is.Not.Null, "P4三个结局必须装配保存图片，不能以文字代替图片验收");
            var bindings = Field<CasinoEndingArtworkBinding[]>(presenter, "endingArtworks");
            Assert.That(bindings.Length, Is.EqualTo(3));
            var expected = bindings.Single(binding => binding.Ending == ending).Sprite;
            Assert.That(expected, Is.Not.Null);
            yield return Wait(() => image.isActiveAndEnabled && image.gameObject.activeInHierarchy && image.sprite == expected && !image.canvasRenderer.cull,
                "实际P4结局图片可见绘制", 2);
            Assert.That(image.color.a, Is.GreaterThanOrEqualTo(0.99f));
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(ending));
            var rect = image.rectTransform; var canvas = image.GetComponentInParent<Canvas>();
            var center = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(center.x, Is.InRange(0, Screen.width)); Assert.That(center.y, Is.InRange(0, Screen.height));
            Assert.That(rect.rect.width > 0 && rect.rect.height > 0, Is.True);
        }

        private IEnumerator CompletePracticeGame(CasinoGameKind game)
        {
            if (!controller.HasActiveAdventureRound)
            {
                Assert.That(game == CasinoGameKind.Slots || game == CasinoGameKind.Roulette || game == CasinoGameKind.SicBo || game == CasinoGameKind.DragonTiger || game == CasinoGameKind.Blackjack, Is.True,
                    "只有原本即时或天然21点的玩法可以创建后直接完成");
                yield break;
            }
            switch (game)
            {
                case CasinoGameKind.CoinFlip:
                    yield return SubmitAction(CasinoMiniGameAction.GuessHeads);
                    if (controller.HasActiveAdventureRound) yield return SubmitAction(CasinoMiniGameAction.CashOut);
                    break;
                case CasinoGameKind.Blackjack: yield return SubmitAction(CasinoMiniGameAction.Stand); break;
                case CasinoGameKind.HighLow:
                    yield return SubmitAction(CasinoMiniGameAction.GuessHigher);
                    if (controller.HasActiveAdventureRound) yield return SubmitAction(CasinoMiniGameAction.CashOut);
                    break;
                case CasinoGameKind.LuckyDraw: yield return SubmitAction(CasinoMiniGameAction.PickPrize, 2); break;
                case CasinoGameKind.Bingo:
                    foreach (int number in new[] { 3, 14, 27 }) yield return SubmitAction(CasinoMiniGameAction.SelectNumber, number);
                    for (int draw = 0; draw < 10 && controller.HasActiveAdventureRound; draw++) yield return SubmitAction(CasinoMiniGameAction.DrawNumber);
                    break;
                case CasinoGameKind.Plinko:
                    yield return SubmitAction(CasinoMiniGameAction.DropBall, 3);
                    yield return Wait(() => !controller.HasActiveAdventureRound, "弹珠真实八层落盘", 5); break;
                case CasinoGameKind.CooperativeLevers:
                    yield return Wait(() => LeverWindowOpen(), "公开周期提示进入0号协作窗口", 8);
                    yield return SubmitAction(CasinoMiniGameAction.PullLever, 0);
                    yield return Wait(() => !controller.HasActiveAdventureRound, "单人NPC协作拉杆结算", 5); break;
                case CasinoGameKind.PushYourLuckDice:
                    yield return SubmitAction(CasinoMiniGameAction.RollDice);
                    if (controller.HasActiveAdventureRound) yield return SubmitAction(CasinoMiniGameAction.CashOut);
                    break;
                case CasinoGameKind.PassingBag:
                    yield return SubmitAction(CasinoMiniGameAction.PassBag);
                    yield return Wait(() => !controller.HasActiveAdventureRound, "福袋真实保险丝与传递结算", 10); break;
                case CasinoGameKind.BlindAuction:
                    yield return SubmitAction(CasinoMiniGameAction.RevealClue);
                    yield return SubmitAction(CasinoMiniGameAction.Bid, 9);
                    yield return Wait(() => !controller.HasActiveAdventureRound, "盲拍真实截止成交", 12);
                    Assert.That(controller.AdventureState.LastRoundCost, Is.EqualTo(9)); break;
                case CasinoGameKind.MechanicalRace:
                    for (int boost = 0; boost < 3 && controller.HasActiveAdventureRound; boost++) yield return SubmitAction(CasinoMiniGameAction.Boost);
                    if (controller.HasActiveAdventureRound) yield return SubmitAction(CasinoMiniGameAction.Dodge);
                    yield return Wait(() => !controller.HasActiveAdventureRound, "机械赛跑真实固定tick结算", 35); break;
                case CasinoGameKind.CooperativeVault:
                    yield return RevealVisibleVaultClues(); yield return SubmitAction(CasinoMiniGameAction.EnterCode, int.Parse(VisibleVaultClues())); break;
                case CasinoGameKind.ChickenElevator:
                    yield return SubmitAction(CasinoMiniGameAction.Climb, 0);
                    if (controller.HasActiveAdventureRound) yield return SubmitAction(CasinoMiniGameAction.CashOut);
                    break;
                default: Assert.Fail("缺少该游戏的真实操作流程：" + game); break;
            }
        }

        private bool LeverWindowOpen()
        {
            if (!controller.HasActiveAdventureRound) return false;
            var match = Regex.Match(Text(Field<object>(presenter, "machineResult")), "周期位置 (\\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out int position) && position >= 200 && position <= 400;
        }

        private IEnumerator RevealVisibleVaultClues()
        {
            for (int step = 0; step < 3; step++)
            {
                string visible = VisibleVaultClues(); int hidden = visible.IndexOf('?');
                if (hidden < 0) break;
                yield return SubmitAction(CasinoMiniGameAction.InspectClue, hidden);
            }
            Assert.That(Regex.IsMatch(VisibleVaultClues(), "^[1-9]{3}$"), Is.True, "必须从已展示的三条线索得到密码，不读取隐藏Target或Round JSON");
        }
        private string VisibleVaultClues()
        {
            var visible = Text(Field<object>(presenter, "machineResult"));
            var match = Regex.Match(visible, "互补线索：([1-9?]{3})");
            Assert.That(match.Success, Is.True, visible); return match.Groups[1].Value;
        }
        private IEnumerator SubmitAction(CasinoMiniGameAction kind, int value = 0)
        {
            if (kind == CasinoMiniGameAction.InspectClue)
            {
                // NPC可能在父协程让帧后补充线索，执行前再读当前可见问号，避免对已揭示位重复操作。
                value = VisibleVaultClues().IndexOf('?');
                if (value < 0) yield break;
            }
            int index = Array.FindIndex(controller.GetAdventureActions(), action => action.Kind == kind);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "当前真实机台应提供" + kind);
            if (controller.GetAdventureActions()[index].RequiresValue) SetText(Field<object>(presenter, "actionInput"), value.ToString());
            int receipts = controller.AdventureState.ProcessedRequests.Count;
            Click(Field<Button[]>(presenter, "actionButtons")[index]); yield return null;
            Assert.That(controller.AdventureState.ProcessedRequests.Count, Is.EqualTo(receipts + 1), "必须实际提交机台动作而非只等待自动结算");
            Assert.That(controller.AdventureState.ProcessedRequests.Last().Result.Success, Is.True, controller.AdventureStatus);
        }

        private IEnumerator AdvanceAllFourStages()
        {
            for (int stage = 0; stage < 4; stage++)
            {
                Assert.That(controller.AdventureState.StageIndex, Is.EqualTo(stage));
                yield return CompleteCurrentStage();
                if (stage < 3) yield return ContinueAfterShopping();
            }
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Finale));
            Assert.That(controller.AdventureState.CompletedStages, Is.EqualTo(4));
        }
        private IEnumerator CompleteCurrentStage()
        {
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            Assert.That(controller.AdventureState.Coins, Is.GreaterThanOrEqualTo(controller.AdventureTarget));
            Click(Field<Button>(presenter, "finishStageButton")); yield return null;
            var expected = controller.AdventureState.StageIndex == 3 ? CasinoAdventurePhase.Finale : CasinoAdventurePhase.Shopping;
            yield return Wait(() => controller.AdventureState.Phase == expected, "真实额度按钮完成当前区域", 5);
        }
        private IEnumerator ContinueAfterShopping()
        {
            int next = controller.AdventureState.StageIndex + 1;
            Assert.That(Field<GameObject>(presenter, "shopPanel").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(areas[next], "lockedGate").activeSelf, Is.True);
            Click(Field<Button>(presenter, "shopCloseButton")); yield return null;
            Click(Field<Button>(presenter, "nextStageButton")); yield return null;
            yield return Wait(() => controller.AdventureState.StageIndex == next && controller.AdventureState.Phase == CasinoAdventurePhase.Playing, "明确购物后进入下一站", 5);
            Assert.That(Field<GameObject>(areas[next], "lockedGate").activeSelf, Is.False);
            Assert.That(Field<GameObject>(areas[next], "contents").activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(body.transform.position, areas[next].SafePosition), Is.LessThan(0.15f));
        }
        private IEnumerator OpenFixedStation(CasinoGameKind game)
        {
            var station = fixedStations.Single(value => value.Game == game);
            Assert.That(station.isActiveAndEnabled, Is.True);
            PlaceBody(station.InteractionPosition + Vector3.up * 0.03f); yield return null;
            Click(Field<Button>(presenter, "interactButton")); yield return null;
            Assert.That(Field<GameObject>(presenter, "machinePanel").activeInHierarchy, Is.True);
            Assert.That(Text(Field<object>(presenter, "machineTitle")), Is.EqualTo(CasinoContentCatalog.Games.Single(definition => definition.Kind == game).Name));
        }
        private void PlaceBody(Vector3 position)
        { body.enabled = false; body.transform.position = position; body.enabled = true; Physics.SyncTransforms(); }

        private GateProbe VerifyLockedGate(JinxCasinoWorldArea nextArea)
        {
            var gate = Field<GameObject>(nextArea, "lockedGate");
            Assert.That(gate.activeInHierarchy && gate.GetComponent<Collider>().enabled, Is.True);
            var probe = new GateProbe { Gate = gate, Plane = gate.transform.position, Normal = gate.transform.right };
            probe.Outside = probe.Plane - probe.Normal * 1.3f; probe.Outside.y = nextArea.SafePosition.y;
            PlaceBody(probe.Outside);
            var flags = body.Move(probe.Normal * 4); Physics.SyncTransforms();
            Assert.That((flags & CollisionFlags.Sides) != 0, Is.True, "未开放区域" + nextArea.Index + "的门必须真实阻挡CC");
            Assert.That(Vector3.Dot(body.transform.position - probe.Plane, probe.Normal), Is.LessThan(0));
            return probe;
        }
        private void VerifyOpenGate(GateProbe probe)
        {
            Assert.That(probe.Gate.activeSelf, Is.False);
            PlaceBody(probe.Outside); body.Move(probe.Normal * 4); Physics.SyncTransforms();
            Assert.That(Vector3.Dot(body.transform.position - probe.Plane, probe.Normal), Is.GreaterThan(0), "解锁后同一路径必须能穿过");
        }
        private sealed class GateProbe { public GameObject Gate; public Vector3 Plane; public Vector3 Normal; public Vector3 Outside; }

        private IEnumerator EnterP2(Action<CasinoAdventureConfig> change = null)
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null); yield return Wait(() => startup.isDone, "唯一正式启动入口", 90);
            }
            yield return Wait(IsStableHub, "正式Hub稳定", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False);
            yield return Resize(1280, 720);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "正式导航四区场景", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "保存HUD初始化", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>(); hud = UIManager.Instance.Get<JinxCasinoHudView>();
            presenter = hud.gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true); body = Field<CharacterController>(controller, "body");
            Assert.That(controller != null && presenter != null && body != null, Is.True);
            areas = Field<JinxCasinoWorldArea[]>(controller, "areas").OrderBy(area => area.Index).ToArray();
            Assert.That(areas.Select(area => area.Index), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            var sceneRoots = controller.gameObject.scene.GetRootGameObjects();
            fixedStations = sceneRoots.SelectMany(root => root.GetComponentsInChildren<JinxCasinoStation>(true)).Where(station => station.GetComponent<JinxCasinoRotationStand>() == null).ToArray();
            Assert.That(fixedStations.Length, Is.EqualTo(17));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(CasinoGameKind)).Cast<CasinoGameKind>(), fixedStations.Select(station => station.Game));
            Assert.That(sceneRoots.Sum(root => root.GetComponentsInChildren<JinxCasinoRotationStand>(true).Length), Is.EqualTo(4));
            Assert.That(sceneRoots.Sum(root => root.GetComponentsInChildren<Camera>(true).Length), Is.EqualTo(1));
            Assert.That(sceneRoots.Sum(root => root.GetComponentsInChildren<AudioListener>(true).Length), Is.EqualTo(1));
            AssertSingleListener();

            var saved = Field<JinxCasinoGameSettings>(controller, "gameSettings"); var defaults = saved.CreateConfig();
            Assert.That(defaults.StageCount, Is.EqualTo(4));
            Assert.That(defaults.StageDurationMilliseconds, Is.EqualTo(240000));
            Assert.That(defaults.Targets, Is.EqualTo(new long[] { 1200, 2000, 3500, 5000 }));
            var clone = Object.Instantiate(saved); configurations.Add(clone);
            var config = clone.CreateConfig(); config.StageCount = 4; config.Targets = new long[] { 900, 900, 900, 900 };
            config.EventIntervalMilliseconds = 0; config.AllowedGames = Array.Empty<CasinoGameKind>(); config.ShopItemIds = Array.Empty<string>();
            change?.Invoke(config); SetField(clone, "adventure", config);
            controller.ConfigureAdventure(clone, areas, Field<JinxCasinoSceneEffects>(controller, "sceneEffects"));
            Assert.That(saved.CreateConfig().StageDurationMilliseconds, Is.EqualTo(240000));
            Assert.That(saved.CreateConfig().Targets, Is.EqualTo(new long[] { 1200, 2000, 3500, 5000 }));
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "P2-" + Guid.NewGuid().ToString("N")));
            store = new CasinoLocalSaveStore(saveDirectory); controller.SetLocalSaveStore(store);
            profileStore = new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")); controller.SetLocalProfileStore(profileStore);
            yield return null; yield return null;
        }

        private IEnumerator Resize(int width, int height)
        {
            resolution?.Dispose(); resolution = new GameViewResolution(width, height);
            yield return Wait(() => Screen.width == width && Screen.height == height, "实际GameView分辨率" + width + "x" + height, 15);
            yield return null; yield return null;
        }
        private IEnumerator ExitThroughSavedPanel()
        {
            var back = Field<Button[]>(presenter, "panelBackButtons").Single(button => button.gameObject.activeInHierarchy);
            var previousHud = hud; var previousController = controller;
            Click(back);
            yield return Wait(() => previousController == null && IsStableHub(), "保存面板返回Hub并卸载四区", 45);
            Assert.That(previousHud.State, Is.EqualTo(ViewState.Destroyed)); Assert.That(previousHud.gameObject == null, Is.True);
            Assert.That(UIManager.Instance.Get<JinxCasinoHudView>(), Is.Null);
            Assert.That(Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<JinxCasinoAreaFacilities>(FindObjectsSortMode.None), Is.Empty);
            AssertSingleListener();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null)
                {
                    yield return Wait(() => live == null || !live.IsBusy, "清理四区在途操作", 20);
                    if (live != null) live.RequestExit();
                    yield return Wait(() => live == null && IsStableHub(), "清理四区并回Hub", 45);
                }
            }
            finally
            {
                resolution?.Dispose(); resolution = null;
                foreach (var settings in configurations) if (settings != null) Object.Destroy(settings); configurations.Clear();
                if (Object.FindFirstObjectByType<JinxCasinoController>() == null && !string.IsNullOrEmpty(saveDirectory))
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    string resolved = Path.GetFullPath(saveDirectory);
                    Assert.That(resolved.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("P2-", StringComparison.Ordinal), Is.True);
                    if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
                }
            }
        }

        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True, "只能点击保存的可用按钮");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            point = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "中心射线不能被模态层或效果遮挡");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static string Text(object component) => (string)component.GetType().GetProperty("text").GetValue(component);
        private static void SetText(object component, string value) => component.GetType().GetProperty("text").SetValue(component, value);
        private static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "保存字段不存在：" + name); return (T)field.GetValue(owner);
        }
        private static void SetField(object owner, string name, object value)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(owner, value);
        }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
            !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + "超时");
        }
        private static IEnumerator Screenshot(string name)
        {
            var liveHud = UIManager.Instance.Get<JinxCasinoHudView>();
            Assert.That(liveHud, Is.Not.Null);
            var groups = liveHud.gameObject.GetComponentsInParent<CanvasGroup>(true);
            Assert.That(groups.Length, Is.GreaterThan(0), "正式UI必须经过Core的保存View过渡");
            yield return Wait(() => groups.All(group => group != null && group.alpha >= 0.99f), "截图前Core入场淡出实际结束", 2);
            Canvas.ForceUpdateCanvases();
            yield return null; yield return null; yield return null;
            int width = Screen.width, height = Screen.height;
            string directory = Path.GetFullPath("Library/JinxCasino/Verification"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
            ScreenCapture.CaptureScreenshot(path); yield return null; yield return null;
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 24, "实际P2截图落盘", 15);
            byte[] bytes = File.ReadAllBytes(path); Assert.That(bytes[0], Is.EqualTo(137)); Assert.That(bytes[1], Is.EqualTo((byte)'P'));
            int pngWidth = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
            int pngHeight = bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23];
            Assert.That(pngWidth, Is.EqualTo(width)); Assert.That(pngHeight, Is.EqualTo(height));
            Debug.Log("[JinxCasinoFourAreaFlowTests] " + width + "x" + height + "四区正式入口证据：" + path);
        }
    }
}
#endif
