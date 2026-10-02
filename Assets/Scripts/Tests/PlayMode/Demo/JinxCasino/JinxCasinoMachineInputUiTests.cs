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
using Hotfix.JinxCasino.Persistence;
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
    /// 从正式保存HUD操作真实下拉和输入框；不引用TMP测试程序集或模拟UI事件订阅。
    public sealed class JinxCasinoMachineInputUiTests
    {
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter presenter;
        private JinxCasinoGameSettings runtimeSettings;
        private GameViewResolution resolution;
        private string saveDirectory;

        [UnityTest, Timeout(180000)]
        public IEnumerator SemanticBetChoicesSurviveTimerRefreshAndReachRules()
        {
            yield return EnterPractice();
            presenter.ShowStation(CasinoGameKind.Roulette); yield return null;
            var groups = Field<object>(presenter, "choiceGroupDropdown"); var options = Field<object>(presenter, "choiceOptionDropdown");
            SetValue(groups, 1); SetValue(options, 2); yield return null;
            Assert.That(Text(Field<object>(presenter, "choiceInput")), Is.EqualTo("39"));
            Assert.That(Labels(options)[2], Is.EqualTo("单数"));
            Assert.That(Text(Field<object>(presenter, "machineRules")), Does.Contain("18/37"));
            Assert.That(Text(Field<object>(presenter, "machineRules")), Does.Not.Contain("39单"));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Value(groups), Is.EqualTo(1)); Assert.That(Value(options), Is.EqualTo(2));
            Assert.That(((Component)Field<object>(presenter, "choiceInput")).gameObject.activeSelf, Is.False);
            Click((Component)options); yield return null;
            Assert.That(((Component)options).GetComponentsInChildren<Toggle>().Length, Is.GreaterThan(0), "保存模板必须可以实际展开供触屏选择");
            options.GetType().GetMethod("Hide").Invoke(options, null);
            // TMP默认0.15秒淡出期间列表仍接收射线；等待真实列表清理后，再验证确认按钮无任何遮挡。
            yield return Wait(() => ((Component)options).GetComponentsInChildren<Toggle>().Length == 0, "下拉列表完成淡出并销毁", 2);
            SetText(Field<object>(presenter, "stakeInput"), "10");
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            Assert.That(controller.Game.State.ProcessedRequests.Last().Result.Success, Is.True);
            Assert.That(controller.Game.GetPresentation().Choice, Is.EqualTo(39));
            presenter.ShowStation(CasinoGameKind.SicBo); yield return null;
            Assert.That(Labels(groups).Count, Is.EqualTo(5)); SetValue(groups, 4); SetValue(options, 5); yield return null;
            Assert.That(Text(Field<object>(presenter, "choiceInput")), Is.EqualTo("305"));
            Assert.That(Text(Field<object>(presenter, "machineRules")), Does.Contain("0.46%"));
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            Assert.That(controller.Game.GetPresentation().Choice, Is.EqualTo(305));
            presenter.ShowStation(CasinoGameKind.CoinFlip); yield return null; SetValue(options, 0);
            Assert.That(Text(Field<object>(presenter, "choiceInput")), Is.EqualTo("0"));
            presenter.ShowStation(CasinoGameKind.Slots); yield return null;
            Assert.That(((Component)options).gameObject.activeSelf, Is.False);
            presenter.ShowStation(CasinoGameKind.CoinFlip); yield return null;
            Assert.That(Value(options), Is.EqualTo(0), "关闭重开同一玩法保留自己的选择");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RiskAndPrizePickersSubmitActualValuesWithoutNumericUi()
        {
            yield return EnterPractice(); presenter.ShowStation(CasinoGameKind.LuckyDraw); yield return null;
            var groups = Field<object>(presenter, "choiceGroupDropdown"); var options = Field<object>(presenter, "choiceOptionDropdown");
            SetValue(groups, 2); SetValue(options, 5); SetText(Field<object>(presenter, "stakeInput"), "10");
            Assert.That(Text(Field<object>(presenter, "choiceInput")), Is.EqualTo("25"));
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            var actionPicker = Field<object>(presenter, "actionOptionDropdown");
            Assert.That(Value(actionPicker), Is.EqualTo(5)); SetValue(actionPicker, 1);
            Assert.That(((Component)Field<object>(presenter, "actionInput")).gameObject.activeSelf, Is.False);
            Assert.That(((Component)Field<object>(presenter, "numberInput")).gameObject.activeSelf, Is.False);
            Submit(CasinoMiniGameAction.PickPrize); yield return null;
            Assert.That(controller.Game.HasActiveRound, Is.False); Assert.That(controller.Game.State.LastRoundCost, Is.EqualTo(10));
            Assert.That(controller.Game.GetPresentation().Choice, Is.EqualTo(25));
            Assert.That(controller.Game.State.LastRoundDescription, Does.Contain("签筒 1 抽到"), "下拉第二签筒必须提交规则值一");
            presenter.ShowStation(CasinoGameKind.CooperativeLevers); yield return null;
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            Assert.That(Labels(actionPicker).Count, Is.EqualTo(1)); Assert.That(Labels(actionPicker)[0], Does.Contain("你的杠杆"));
            yield return Wait(() => LeverWindowOpen(), "真实杠杆窗口", 12);
            Submit(CasinoMiniGameAction.PullLever); yield return null;
            yield return Wait(() => !controller.Game.HasActiveRound, "助手完成另一根杠杆", 3);
            Assert.That(controller.Game.State.LastRoundPayout, Is.EqualTo(40));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator VaultCluePickerAndPasswordRemainIndependentAndOpenRealVault()
        {
            yield return EnterPractice(); presenter.ShowStation(CasinoGameKind.CooperativeVault); yield return null;
            SetText(Field<object>(presenter, "stakeInput"), "10"); Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            var picker = Field<object>(presenter, "actionOptionDropdown"); var password = Field<object>(presenter, "numberInput");
            while (VisibleClues().Count(character => character != '?') < 2)
            {
                SetValue(picker, 0); Submit(CasinoMiniGameAction.InspectClue); yield return null;
            }
            Assert.That(((Component)picker).gameObject.activeInHierarchy && ((Component)password).gameObject.activeInHierarchy, Is.True);
            SetText(password, "123");
            if (VisibleClues().Contains("?")) { SetValue(picker, 0); Submit(CasinoMiniGameAction.InspectClue); yield return null; }
            Assert.That(Text(password), Is.EqualTo("123"), "查看线索和计时刷新不能覆写已输入密码");
            string code = VisibleClues(); Assert.That(Regex.IsMatch(code, "^[1-9]{3}$"), Is.True);
            SetText(password, code); Submit(CasinoMiniGameAction.EnterCode); yield return null;
            Assert.That(controller.Game.HasActiveRound, Is.False); Assert.That(controller.Game.State.LastRoundPayout, Is.EqualTo(30));
        }

        private void Submit(CasinoMiniGameAction action)
        {
            int index = Array.FindIndex(controller.Game.GetActions(), descriptor => descriptor.Kind == action);
            Assert.That(index, Is.GreaterThanOrEqualTo(0)); int count = controller.Game.State.ProcessedRequests.Count;
            Click(Field<Button[]>(presenter, "actionButtons")[index]);
            Assert.That(controller.Game.State.ProcessedRequests.Count, Is.EqualTo(count + 1));
            Assert.That(controller.Game.State.ProcessedRequests.Last().Result.Success, Is.True, controller.Game.Status);
        }
        private string VisibleClues()
        {
            var match = Regex.Match(Text(Field<object>(presenter, "machineResult")), "互补线索：([1-9?]{3})");
            Assert.That(match.Success, Is.True); return match.Groups[1].Value;
        }
        private bool LeverWindowOpen()
        {
            var match = Regex.Match(Text(Field<object>(presenter, "machineResult")), "周期位置 (\\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out int value) && value >= 200 && value <= 400;
        }

        private IEnumerator EnterPractice()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single); yield return Wait(() => startup.isDone, "正式启动", 90);
            }
            yield return Wait(IsStableHub, "正式Hub", 90);
            resolution = new GameViewResolution(1280, 720); yield return Wait(() => Screen.width == 1280 && Screen.height == 720, "720p GameView", 15);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask(); yield return Wait(() => travel.IsCompleted, "正式场景", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "正式保存HUD", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>();
            presenter = UIManager.Instance.Get<JinxCasinoHudView>().gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true);
            runtimeSettings = Object.Instantiate(Field<JinxCasinoGameSettings>(controller, "gameSettings"));
            var config = runtimeSettings.CreateConfig(); config.AllowedGames = Array.Empty<CasinoGameKind>(); config.EventIntervalMilliseconds = 0;
            SetField(runtimeSettings, "adventure", config);
            controller.ConfigureAdventure(runtimeSettings, Field<JinxCasinoWorldArea[]>(controller, "areas"), Field<JinxCasinoSceneEffects>(controller, "sceneEffects"));
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "MachineUi-" + Guid.NewGuid().ToString("N")));
            controller.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); controller.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            yield return null; Click(Field<Button>(presenter, "practiceButton")); yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (controller != null) { controller.RequestExit(); yield return Wait(() => controller == null && IsStableHub(), "UI回归退出", 45); }
            }
            finally
            {
                resolution?.Dispose(); if (runtimeSettings != null) Object.Destroy(runtimeSettings);
                if (controller == null && !string.IsNullOrEmpty(saveDirectory))
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    Assert.That(saveDirectory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(saveDirectory).StartsWith("MachineUi-", StringComparison.Ordinal), Is.True);
                    if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
                }
            }
        }
        private static void Click(Component component)
        {
            Assert.That(component != null && component.gameObject.activeInHierarchy, Is.True);
            if (component is Button button) Assert.That(button.interactable, Is.True);
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)component.transform; var canvas = component.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(component.transform), Is.True, "真实控件中心不能被遮挡");
            ExecuteEvents.Execute(component.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static string Text(object component) => (string)component.GetType().GetProperty("text").GetValue(component);
        private static void SetText(object component, string value) => component.GetType().GetProperty("text").SetValue(component, value);
        private static int Value(object component) => (int)component.GetType().GetProperty("value").GetValue(component);
        private static void SetValue(object component, int value) => component.GetType().GetProperty("value").SetValue(component, value);
        private static List<string> Labels(object dropdown) => ((IEnumerable)dropdown.GetType().GetProperty("options").GetValue(dropdown)).Cast<object>().Select(option => (string)option.GetType().GetProperty("text").GetValue(option)).ToList();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static IEnumerator Wait(Func<bool> predicate, string reason, float seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + "超时");
        }
    }
}
#endif
