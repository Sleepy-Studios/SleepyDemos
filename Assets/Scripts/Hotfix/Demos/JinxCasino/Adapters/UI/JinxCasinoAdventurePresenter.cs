using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    [Serializable]
    public sealed class CasinoEndingArtworkBinding
    {
        public CasinoAdventureEnding Ending;
        public Sprite Sprite;
    }

    /// 离线冒险界面：使用保存的按钮和列表模板，规则始终由场景宿主提交。
    public sealed class JinxCasinoAdventurePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject machinePanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject slotsPanel;
        [SerializeField] private GameObject endingPanel;
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private TMP_Text hudText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text eventText;
        [SerializeField] private TMP_Text machineTitle;
        [SerializeField] private TMP_Text machineRules;
        [SerializeField] private TMP_Text machineResult;
        [SerializeField] private TMP_Text endingText;
        [SerializeField] private TMP_Text overwriteText;
        [SerializeField] private TMP_Text[] slotTexts;
        [SerializeField] private TMP_InputField stakeInput;
        [SerializeField] private TMP_InputField choiceInput;
        [SerializeField] private TMP_InputField actionInput;
        [SerializeField] private TMP_Dropdown choiceGroupDropdown;
        [SerializeField] private TMP_Dropdown choiceOptionDropdown;
        [SerializeField] private TMP_Dropdown actionOptionDropdown;
        [SerializeField] private TMP_Text choiceHeading;
        [SerializeField] private TMP_Text actionHeading;
        [SerializeField] private TMP_Text numberHeading;
        [SerializeField] private TMP_InputField numberInput;
        [SerializeField] private Button standardButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button endlessButton;
        [SerializeField] private Button interactButton;
        [SerializeField] private Button resumeRoundButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button slotsButton;
        [SerializeField] private Button finishStageButton;
        [SerializeField] private Button nextStageButton;
        [SerializeField] private Button confirmBetButton;
        [SerializeField] private Button machineCloseButton;
        [SerializeField] private Button shopCloseButton;
        [SerializeField] private Button slotsCloseButton;
        [SerializeField] private Button withdrawButton;
        [SerializeField] private Button leaveEndingButton;
        [SerializeField] private Button takeoverButton;
        [SerializeField] private Button vaultChallengeButton;
        [SerializeField] private Button refillButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button eventButton;
        [SerializeField] private Button eventCloseButton;
        [SerializeField] private Button[] panelBackButtons = Array.Empty<Button>();
        [SerializeField] private Button[] eventButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] eventButtonTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Dropdown exchangeDropdown;
        [SerializeField] private Button[] actionButtons;
        [SerializeField] private TMP_Text[] actionButtonTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Button[] saveButtons;
        [SerializeField] private Button[] loadButtons;
        [SerializeField] private Transform itemContent;
        [SerializeField] private JinxCasinoItemCard itemTemplate;
        [SerializeField] private Image[] inkEdges = Array.Empty<Image>();
        [SerializeField] private GameObject radarPanel;
        [SerializeField] private TMP_Text radarText;
        [SerializeField] private JinxCasinoProfilePresenter profilePresenter;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button profileMenuButton;
        [SerializeField] private JinxCasinoLocalSettingsPresenter localSettingsPresenter;
        [SerializeField] private GameObject localShortcutPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button emoteButton;
        [SerializeField] private JinxCasinoSocialPresenter socialPresenter;
        [SerializeField] private Button socialButton;
        [SerializeField] private Image endingArtwork;
        [SerializeField] private CasinoEndingArtworkBinding[] endingArtworks = Array.Empty<CasinoEndingArtworkBinding>();
        private CasinoFinishedRunRecord endingRecord;
        private readonly List<JinxCasinoItemCard> cards = new List<JinxCasinoItemCard>();
        private readonly List<Action> removeListeners = new List<Action>();
        private JinxCasinoController controller;
        private CasinoGameKind selectedGame;
        private CasinoMiniGameActionDescriptor[] actions = Array.Empty<CasinoMiniGameActionDescriptor>();
        private CasinoAdventurePhase? previousPhase;
        private Window window;
        private string beginRequest;
        private int overwriteSlot;
        private int inventoryRevision = -1;
        private string displayedRun;
        private string cardFilter;
        private string machineFeedback;
        private string eventFeedback;
        private string displayedEvent;
        private bool previousChoicePending;
        private int exchangeRevision = -1;
        private CasinoEventActionDescriptor[] eventActions = Array.Empty<CasinoEventActionDescriptor>();
        private readonly List<string> exchangeItemIds = new List<string>();
        private readonly CasinoSaveSlotInfo[] slotInfo = new CasinoSaveSlotInfo[3];
        private CasinoMachineUiOptions.Group[] choiceGroups = Array.Empty<CasinoMachineUiOptions.Group>();
        private CasinoMachineUiOptions.Option[] actionOptions = Array.Empty<CasinoMachineUiOptions.Option>();
        private CasinoGameKind? pickerGame;
        private CasinoMiniGameAction? pickerAction;
        private CasinoMiniGameAction? numericAction;
        private string actionOptionsKey;
        private string numericRound;
        private bool writingActionWire;
        private bool legacyActionOverride;
        private readonly Dictionary<CasinoGameKind, int> rememberedChoices = new Dictionary<CasinoGameKind, int>();

        private enum Window { None, Menu, Machine, Shop, Slots, Ending, Event, Profile, Settings, Emotes, Social }

        private void LateUpdate()
        {
            float intensity = controller != null ? controller.AdventureInkIntensity : 0;
            foreach (var edge in inkEdges)
            {
                if (edge == null) continue;
                var color = edge.color; color.a = intensity * 0.72f; edge.color = color;
            }
        }

        /// <summary>绑定具体内容宿主；重新显示时不保留旧订阅或操作编号。</summary>
        /// <param name="owner">当前Demo的Controller。</param>
        public void Bind(JinxCasinoController owner)
        {
            Unbind(); controller = owner; previousPhase = null;
            controller.Changed += Refresh;
            Listen(standardButton, () => StartMode(CasinoAdventureMode.Standard));
            Listen(practiceButton, () => StartMode(CasinoAdventureMode.Practice));
            Listen(endlessButton, () => StartMode(CasinoAdventureMode.Endless));
            Listen(interactButton, controller.InteractWithNearbyStation);
            Listen(resumeRoundButton, () => ShowStation(controller.Game.State.ActiveGame));
            Listen(shopButton, () => Open(Window.Shop));
            Listen(slotsButton, OpenSlots);
            Listen(finishStageButton, () => controller.Game.CompleteStage(Time.frameCount));
            Listen(nextStageButton, () => { controller.Game.BeginNextStage(Time.frameCount); Open(Window.None); });
            Listen(confirmBetButton, ConfirmBet);
            Listen(machineCloseButton, ReturnToField);
            Listen(shopCloseButton, ReturnToField);
            Listen(slotsCloseButton, ReturnToField);
            Listen(withdrawButton, () => controller.Game.ChooseEnding(CasinoAdventureEnding.Withdraw, Time.frameCount));
            Listen(leaveEndingButton, () => controller.Game.ChooseEnding(CasinoAdventureEnding.LeaveWithDignity, Time.frameCount));
            Listen(takeoverButton, () => controller.Game.ChooseEnding(CasinoAdventureEnding.TakeOver, Time.frameCount));
            Listen(vaultChallengeButton, () => ShowStation(CasinoGameKind.CooperativeVault));
            Listen(refillButton, () => controller.Game.RefillPractice(Time.frameCount));
            Listen(backButton, controller.RequestExit);
            foreach (var button in panelBackButtons) Listen(button, controller.RequestExit);
            Listen(eventButton, () => Open(Window.Event));
            Listen(eventCloseButton, () => Open(Window.None));
            Listen(profileButton, () => Open(Window.Profile));
            Listen(profileMenuButton, () => Open(Window.Profile));
            profilePresenter?.Bind(owner, ReturnToField);
            localSettingsPresenter?.Bind(owner, ReturnToField);
            Listen(settingsButton, () => Open(Window.Settings)); Listen(emoteButton, () => Open(Window.Emotes));
            socialPresenter?.Bind(owner, ReturnToField); Listen(socialButton, () => Open(Window.Social));
            Listen(stakeInput, OnBeginParameterChanged);
            Listen(choiceInput, OnBeginParameterChanged);
            Listen(actionInput, _ => { if (!writingActionWire) legacyActionOverride = true; machineFeedback = null; if (window == Window.Machine) RefreshMachine(); });
            Listen(choiceGroupDropdown, _ => SelectChoiceGroup());
            Listen(choiceOptionDropdown, _ => SelectChoiceOption());
            Listen(actionOptionDropdown, _ => SelectActionOption());
            Listen(numberInput, _ => SetActionWire(numberInput.text));
            for (int index = 0; index < actionButtons.Length; index++)
            {
                int captured = index;
                Listen(actionButtons[index], () => Act(captured));
            }
            for (int index = 0; index < eventButtons.Length; index++)
            {
                int captured = index; Listen(eventButtons[index], () => ResolveEvent(captured));
            }
            for (int index = 0; index < saveButtons.Length; index++)
            {
                int slot = index + 1;
                Listen(saveButtons[index], () => SaveSlot(slot));
                Listen(loadButtons[index], () =>
                {
                    if (controller.LoadAdventure(slot)) ReturnToField();
                    if (window == Window.Slots) { ReadSlotInfo(); RefreshSlots(); }
                });
            }
            Open(controller.Game.HasAdventure ? Window.None : Window.Menu);
        }

        /// 解绑界面，释放本界面的输入阻挡；关闭机台面板不撤销已投入的规则局。
        public void Unbind()
        {
            if (controller != null)
            {
                controller.Changed -= Refresh; controller.BindAdventurePresenter(null, false);
            }
            foreach (var remove in removeListeners) remove();
            removeListeners.Clear();
            profilePresenter?.Unbind();
            localSettingsPresenter?.Unbind();
            socialPresenter?.Unbind();
            foreach (var card in cards) if (card != null) Destroy(card.gameObject);
            cards.Clear(); controller = null; inventoryRevision = -1; cardFilter = null;
            displayedRun = null; previousChoicePending = false; displayedEvent = null;
            machineFeedback = eventFeedback = null; exchangeRevision = -1;
            pickerGame = null; pickerAction = numericAction = null; actionOptionsKey = numericRound = null;
            rememberedChoices.Clear(); legacyActionOverride = false;
            Array.Clear(slotInfo, 0, slotInfo.Length);
        }

        /// <summary>开启保存的机台操作面板，投入前展示规则并等待确认。</summary>
        /// <param name="game">已开放、已接近或正在恢复的机台。</param>
        public void ShowStation(CasinoGameKind game)
        {
            if (controller == null || !controller.Game.HasAdventure) return;
            if (controller.Game.HasActiveRound) game = controller.Game.State.ActiveGame;
            if (!controller.Game.HasActiveRound && !IsGameAllowed(controller.Game.State.Config, game)) return;
            selectedGame = game; beginRequest = Guid.NewGuid().ToString("N");
            machineFeedback = null; legacyActionOverride = false;
            int choice = rememberedChoices.TryGetValue(game, out int previous) ? previous : game == CasinoGameKind.CoinFlip ? 2 : 0;
            choiceInput.SetTextWithoutNotify(choice.ToString());
            Open(Window.Machine);
        }

        private void StartMode(CasinoAdventureMode mode)
        {
            controller.StartAdventure(mode);
            if (controller.Game.HasAdventure) Open(Window.None);
        }

        private void Refresh()
        {
            if (controller == null) return;
            var state = controller.Game.State;
            if (emoteButton != null) emoteButton.gameObject.SetActive(state != null && window == Window.None);
            if (socialButton != null) socialButton.gameObject.SetActive(state != null && window == Window.None);
            statusText.text = !string.IsNullOrEmpty(controller.AdventureSceneAnnouncement) ? controller.AdventureSceneAnnouncement :
                !string.IsNullOrEmpty(controller.LocalSocialMessage) ? controller.LocalSocialMessage : controller.Game.Status;
            if (state == null)
            {
                hudText.text = "倒霉蛋俱乐部  ·  选择你的旅程";
                interactButton.interactable = shopButton.interactable = finishStageButton.interactable = false;
                resumeRoundButton.gameObject.SetActive(false); nextStageButton.gameObject.SetActive(false);
                refillButton.gameObject.SetActive(false); eventButton.gameObject.SetActive(false); missionText.text = string.Empty;
                return;
            }
            if (displayedRun != state.RunId)
            {
                endingRecord = null;
                displayedRun = state.RunId; cardFilter = null; machineFeedback = eventFeedback = null;
                previousChoicePending = false; displayedEvent = null; exchangeRevision = -1;
                beginRequest = Guid.NewGuid().ToString("N");
            }
            int areaIndex = Mathf.Clamp(state.StageIndex % 4, 0, 3);
            var area = CasinoContentCatalog.Areas[areaIndex];
            string clock = state.Mode == CasinoAdventureMode.Practice ? "自由练习" : TimeText(state.RemainingMilliseconds);
            hudText.text = area.Name + "  ·  " + clock + "\n团队筹码 " + state.Coins + "  /  额度 " + controller.Game.Target
                + (state.LockedCoins > 0 ? "  ·  机台预留 " + state.LockedCoins : string.Empty);
            RefreshMission(state);
            if (radarPanel != null)
            {
                string hint = controller.AdventureRadarHint;
                radarPanel.SetActive(window == Window.None && !string.IsNullOrEmpty(hint));
                if (radarText != null) radarText.text = hint;
            }
            if (previousPhase != state.Phase)
            {
                previousPhase = state.Phase;
                if (state.Phase == CasinoAdventurePhase.Shopping) Open(Window.Shop);
                if (state.Phase == CasinoAdventurePhase.Finale || state.Phase == CasinoAdventurePhase.Failed || state.Phase == CasinoAdventurePhase.Ended) Open(Window.Ending);
            }
            interactButton.interactable = state.Phase == CasinoAdventurePhase.Playing && window == Window.None;
            shopButton.interactable = state.Phase != CasinoAdventurePhase.Ended;
            finishStageButton.interactable = state.Mode != CasinoAdventureMode.Practice && state.Phase == CasinoAdventurePhase.Playing && !controller.Game.HasActiveRound && state.Coins >= controller.Game.Target;
            resumeRoundButton.gameObject.SetActive(controller.Game.HasActiveRound);
            nextStageButton.gameObject.SetActive(state.Phase == CasinoAdventurePhase.Shopping);
            refillButton.gameObject.SetActive(state.Mode == CasinoAdventureMode.Practice);
            eventButton.gameObject.SetActive(state.EventChoicePending);
            bool newlyPending = state.EventChoicePending && (!previousChoicePending || displayedEvent != state.CurrentEventId);
            previousChoicePending = state.EventChoicePending; displayedEvent = state.CurrentEventId;
            if (newlyPending && state.Phase == CasinoAdventurePhase.Playing)
            { eventFeedback = null; Open(Window.Event); return; }
            if (window == Window.Event)
            {
                if (!state.EventChoicePending) { Open(Window.None); return; }
                RefreshEvent(state);
            }
            if (window == Window.Machine) RefreshMachine();
            if (window == Window.Shop && inventoryRevision != state.Revision) RefreshCards(state);
            if (window == Window.Ending) RefreshEnding(state);
        }

        private void Open(Window target)
        {
            window = target;
            menuPanel.SetActive(target == Window.Menu); machinePanel.SetActive(target == Window.Machine);
            shopPanel.SetActive(target == Window.Shop); slotsPanel.SetActive(target == Window.Slots); endingPanel.SetActive(target == Window.Ending);
            eventPanel.SetActive(target == Window.Event);
            if (profilePresenter != null) profilePresenter.gameObject.SetActive(target == Window.Profile);
            if (socialPresenter != null) socialPresenter.gameObject.SetActive(target == Window.Social);
            if (localShortcutPanel != null) localShortcutPanel.SetActive(target == Window.None || target == Window.Menu);
            if (localSettingsPresenter != null)
            {
                localSettingsPresenter.gameObject.SetActive(target == Window.Settings || target == Window.Emotes);
                if (target == Window.Settings) localSettingsPresenter.ShowSettings();
                if (target == Window.Emotes) localSettingsPresenter.ShowEmotes();
            }
            controller?.BindAdventurePresenter(this, target != Window.None, target == Window.Settings);
            if (target == Window.Shop && controller?.Game.State != null) RefreshCards(controller.Game.State);
            if (target == Window.Machine) RefreshMachine();
            Refresh();
        }

        private void RefreshMachine()
        {
            if (controller == null || controller.Game.State == null) return;
            var state = controller.Game.State;
            var definition = Array.Find(CasinoContentCatalog.Games, game => game.Kind == selectedGame);
            machineTitle.text = definition?.Name ?? "机台";
            bool validChoice = int.TryParse(choiceInput.text, out int choice);
            bool cooperationHelp = (selectedGame == CasinoGameKind.CooperativeLevers || selectedGame == CasinoGameKind.CooperativeVault) &&
                (controller.Game.HasActiveRound ? controller.Game.GetPresentation()?.CooperationHelpUsed == true : state.CooperationHelpCharges > 0);
            string rules;
            try { rules = validChoice ? CasinoMachineUiOptions.Describe(selectedGame, choice, state.NextDiceBias, cooperationHelp) : "请选择下注区域或玩法。"; }
            catch (ArgumentOutOfRangeException) { validChoice = false; rules = CasinoMachineUiOptions.Describe(selectedGame, 0, state.NextDiceBias, cooperationHelp) + "\n当前选择不可用，请重新选择。"; }
            machineRules.text = rules + "\n" + HumanRules(controller.Game.BetRules);
            string roundText = HumanRoundText(controller.Game.HasActiveRound ? controller.Game.ActiveRoundDescription : state.LastRoundDescription ?? "先选择投入与玩法，再确认投入。");
            // 计时刷新保留最近的失败提示，同时继续展示当前机台状态，避免错误被下一帧结果文本冲掉。
            machineResult.text = string.IsNullOrEmpty(machineFeedback) ? roundText : machineFeedback + "\n\n" + roundText;
            confirmBetButton.gameObject.SetActive(!controller.Game.HasActiveRound && (state.Phase == CasinoAdventurePhase.Playing || state.Phase == CasinoAdventurePhase.Finale));
            stakeInput.interactable = choiceInput.interactable = !controller.Game.HasActiveRound;
            RefreshChoicePickers(choice, !controller.Game.HasActiveRound);
            confirmBetButton.interactable = validChoice && long.TryParse(stakeInput.text, out long stake) && stake > 0;
            actions = controller.Game.GetActions();
            for (int index = 0; index < actionButtons.Length; index++)
            {
                bool available = index < actions.Length;
                actionButtons[index].gameObject.SetActive(available);
                if (!available) continue;
                actionButtonTexts[index].text = ActionLabel(actions[index]);
            }
            RefreshActionPickers(state);
            RefreshLeverWindowCue();
            // 兼容旧场景和测试的协议字段仍参与监听；新面板只显示保存的语义控件。
            if (choiceGroupDropdown != null) choiceInput.gameObject.SetActive(false);
            actionInput.gameObject.SetActive(actionOptionDropdown == null && actions.Any(action => action.RequiresValue));
        }

        private void RefreshLeverWindowCue()
        {
            var view = controller.Game.GetPresentation();
            if (selectedGame != CasinoGameKind.CooperativeLevers || view == null || view.IsComplete) return;
            int.TryParse(actionInput.text, out int target);
            int margin = view.CooperationHelpUsed ? 100 : 0;
            long cycle = view.ElapsedMilliseconds % 2000;
            int start = 200 + target * 250;
            bool completed = target >= 0 && target < view.SelectedValues.Length && view.SelectedValues[target] != 0;
            bool open = cycle >= start - margin && cycle <= start + 200 + margin;
            for (int index = 0; index < actions.Length && index < actionButtons.Length; index++)
            {
                if (actions[index].Kind != CasinoMiniGameAction.PullLever) continue;
                actionButtonTexts[index].text = completed ? "本周期已拉下" : open ? "窗口开放 · 现在拉！" : "等待窗口 · 可提前尝试";
                var image = actionButtons[index].targetGraphic as Image;
                if (image != null) image.color = open && !completed ? new Color(0.1f, 0.58f, 0.48f, 1) : new Color(0.24f, 0.1f, 0.19f, 1);
            }
        }

        private void ConfirmBet()
        {
            if (!long.TryParse(stakeInput.text, out long stake) || !int.TryParse(choiceInput.text, out int choice))
            { machineFeedback = "请输入整数投入，并选择玩法。"; RefreshMachine(); return; }
            machineFeedback = null;
            var result = controller.Game.BeginGame(beginRequest, selectedGame, stake, choice, null, Time.frameCount);
            if (result.Success) beginRequest = Guid.NewGuid().ToString("N");
            else machineFeedback = result.Description ?? result.Error;
            RefreshMachine();
        }

        private void RefreshChoicePickers(int choice, bool canChoose)
        {
            if (choiceGroupDropdown == null || choiceOptionDropdown == null) return;
            if (pickerGame != selectedGame)
            {
                pickerGame = selectedGame; choiceGroups = CasinoMachineUiOptions.Choices(selectedGame);
                SetOptions(choiceGroupDropdown, choiceGroups.Select(group => group.Label));
            }
            bool visible = canChoose && choiceGroups.Length > 0;
            choiceHeading.gameObject.SetActive(visible);
            choiceGroupDropdown.gameObject.SetActive(visible && choiceGroups.Length > 1);
            choiceOptionDropdown.gameObject.SetActive(visible);
            if (choiceGroups.Length == 0) return;
            int groupIndex = Array.FindIndex(choiceGroups, group => Array.Exists(group.Options, option => option.Value == choice));
            if (groupIndex < 0) return; // 不把无效兼容输入静默变成有效下注。
            choiceGroupDropdown.SetValueWithoutNotify(groupIndex);
            var options = choiceGroups[groupIndex].Options;
            if (choiceOptionDropdown.options.Count != options.Length || choiceOptionDropdown.options[0].text != options[0].Label)
                SetOptions(choiceOptionDropdown, options.Select(option => option.Label));
            choiceOptionDropdown.SetValueWithoutNotify(Array.FindIndex(options, option => option.Value == choice));
            choiceHeading.text = selectedGame == CasinoGameKind.LuckyDraw ? "选择风险与签筒" : selectedGame == CasinoGameKind.SicBo || selectedGame == CasinoGameKind.Roulette ? "选择下注区域" : "选择玩法";
        }

        private void SelectChoiceGroup()
        {
            if (choiceGroups.Length == 0) return;
            var options = choiceGroups[choiceGroupDropdown.value].Options;
            SetOptions(choiceOptionDropdown, options.Select(option => option.Label));
            choiceOptionDropdown.SetValueWithoutNotify(0);
            choiceInput.text = options[0].Value.ToString();
        }

        private void SelectChoiceOption()
        {
            if (choiceGroups.Length == 0) return;
            var options = choiceGroups[choiceGroupDropdown.value].Options;
            if (choiceOptionDropdown.value >= 0 && choiceOptionDropdown.value < options.Length) choiceInput.text = options[choiceOptionDropdown.value].Value.ToString();
        }

        private void RefreshActionPickers(CasinoAdventureState state)
        {
            if (actionOptionDropdown == null || numberInput == null) return;
            var selected = actions.FirstOrDefault(action => action.RequiresValue && action.Kind != CasinoMiniGameAction.Bid && action.Kind != CasinoMiniGameAction.EnterCode);
            var numeric = actions.FirstOrDefault(action => action.Kind == CasinoMiniGameAction.Bid || action.Kind == CasinoMiniGameAction.EnterCode);
            actionHeading.gameObject.SetActive(selected != null); actionOptionDropdown.gameObject.SetActive(selected != null);
            numberHeading.gameObject.SetActive(numeric != null); numberInput.gameObject.SetActive(numeric != null);
            var previousPicker = pickerAction;
            pickerAction = selected?.Kind; numericAction = numeric?.Kind;
            if (selected != null)
            {
                string key = selectedGame + ":" + selected.Kind + ":" + selected.Minimum + ":" + selected.Maximum + ":" + state.PlayerCount;
                var view = controller.Game.GetPresentation();
                key += ":" + state.RunId + ":" + state.SettledRoundSequence + ":" + string.Join(",", view?.SelectedValues ?? Array.Empty<int>());
                if (key != actionOptionsKey)
                {
                    int prior = previousPicker == selected.Kind && actionOptionDropdown.value >= 0 && actionOptionDropdown.value < actionOptions.Length ? actionOptions[actionOptionDropdown.value].Value : selected.Minimum;
                    if (selected.Kind == CasinoMiniGameAction.PickPrize && int.TryParse(choiceInput.text, out int initial)) prior = initial % 10;
                    actionOptionsKey = key; actionOptions = CasinoMachineUiOptions.ActionOptions(selected, state.PlayerCount)
                        .Where(option => IsActionTargetAvailable(selected.Kind, option.Value, view)).ToArray();
                    SetOptions(actionOptionDropdown, actionOptions.Select(option => option.Label));
                    int index = Array.FindIndex(actionOptions, option => option.Value == prior);
                    actionOptionDropdown.SetValueWithoutNotify(Math.Max(0, index));
                }
                actionOptionDropdown.interactable = actionOptions.Length > 0;
                actionHeading.text = selected.Kind == CasinoMiniGameAction.InspectClue ? "查看哪一位线索" : selected.Kind == CasinoMiniGameAction.PullLever ? "你负责的杠杆" : "选择操作目标";
                if (legacyActionOverride && int.TryParse(actionInput.text, out int wire))
                {
                    int index = Array.FindIndex(actionOptions, option => option.Value == wire);
                    if (index >= 0) actionOptionDropdown.SetValueWithoutNotify(index);
                }
            }
            if (numeric != null)
            {
                string key = state.RunId + ":" + state.SettledRoundSequence + ":" + numeric.Kind;
                if (numericRound != key)
                {
                    numericRound = key;
                    numberInput.SetTextWithoutNotify(numeric.Kind == CasinoMiniGameAction.Bid ? numeric.Minimum.ToString() : string.Empty);
                }
                numberHeading.text = numeric.Kind == CasinoMiniGameAction.EnterCode ? "三位密码" : "出价 · " + numeric.Minimum + " 至 " + numeric.Maximum + " 筹码";
                numberInput.characterLimit = numeric.Kind == CasinoMiniGameAction.EnterCode ? 3 : 7;
                if (legacyActionOverride) numberInput.SetTextWithoutNotify(actionInput.text);
            }
            for (int index = 0; index < actions.Length && index < actionButtons.Length; index++)
                actionButtons[index].interactable = actions[index].Kind != pickerAction || actionOptions.Length > 0;
        }

        private static bool IsActionTargetAvailable(CasinoMiniGameAction kind, int value, CasinoMiniGamePresentation view)
        {
            if (view == null) return true;
            if (kind == CasinoMiniGameAction.SelectNumber) return Array.IndexOf(view.SelectedValues, value) < 0;
            if (kind == CasinoMiniGameAction.InspectClue || kind == CasinoMiniGameAction.PullLever)
                return value < view.SelectedValues.Length && view.SelectedValues[value] == 0;
            return true;
        }

        private string ActionLabel(CasinoMiniGameActionDescriptor action)
        {
            switch (action.Kind)
            {
                case CasinoMiniGameAction.SelectNumber: return "选入这个号码";
                case CasinoMiniGameAction.PickPrize: return "打开这个签筒";
                case CasinoMiniGameAction.DropBall: return "从这里投下";
                case CasinoMiniGameAction.PullLever: return controller.Game.State.PlayerCount == 1 ? "拉下自己的杠杆" : "拉下选定杠杆";
                case CasinoMiniGameAction.InspectClue: return "查看这条线索";
                case CasinoMiniGameAction.EnterCode: return "输入密码开锁";
                case CasinoMiniGameAction.Bid: return "提交出价";
                case CasinoMiniGameAction.Climb: return "乘这台电梯上楼";
                default: return action.Label;
            }
        }

        private string HumanRoundText(string text)
        {
            text = text.Replace("每人可查看0/1/2号线索", "可查看百位、十位、个位线索")
                .Replace("选择0或1号梯", "选择左侧或右侧电梯")
                .Replace("选择落点0..6", "在右侧选择落点后投下弹珠");
            if (selectedGame == CasinoGameKind.LuckyDraw && controller.Game.HasActiveRound)
            {
                string revealed = text.IndexOf("透视筒值：", StringComparison.Ordinal) >= 0 ? text.Substring(text.IndexOf("透视筒值：", StringComparison.Ordinal)) : string.Empty;
                return "选择你要打开的签筒。\n" + revealed;
            }
            if (selectedGame == CasinoGameKind.LuckyDraw)
                for (int index = 0; index < 6; index++)
                    if (text.Contains("签筒 " + index + " 抽到")) return text.Replace("签筒 " + index + " 抽到", "第 " + (index + 1) + " 签筒抽到");
            if (selectedGame == CasinoGameKind.CooperativeLevers && controller.Game.State.PlayerCount == 1)
                text = text.Replace("单人：NPC在500毫秒拉1号，你负责0号。", "助手会在每周期第五百毫秒拉下另一根。你负责自己的杠杆。")
                    .Replace("杠杆i窗口[200+i*250,400+i*250]毫秒", "你的窗口为每周期第200至400毫秒")
                    .Replace("杠杆i窗口[100+i*250,500+i*250]毫秒", "你的窗口为每周期第100至500毫秒");
            if (selectedGame == CasinoGameKind.MechanicalRace && controller.Game.HasActiveRound)
                for (int index = 0; index < 4; index++)
                    if (text.Contains("下注跑者 " + index + "，")) return text.Replace("下注跑者 " + index + "，", "支持跑者 " + (index + 1) + "，");
            if (selectedGame == CasinoGameKind.MechanicalRace)
                for (int index = 0; index < 4; index++)
                    if (text.Contains("机械赛跑赢家 " + index + "，")) return text.Replace("机械赛跑赢家 " + index + "，", "机械赛跑赢家 · 跑者 " + (index + 1) + "，");
            if (selectedGame == CasinoGameKind.PassingBag && controller.Game.HasActiveRound)
            {
                text = text.Replace("你是0号", "你是当前玩家").Replace("1号NPC收到后700毫秒自动传回", "助手收到后七百毫秒自动传回");
                if (controller.Game.State.PlayerCount == 1) text = text.Replace("持有人 0，", "当前在你手中，").Replace("持有人 1，", "当前在助手手中，");
            }
            return text;
        }

        private void SelectActionOption()
        {
            if (actionOptionDropdown.value >= 0 && actionOptionDropdown.value < actionOptions.Length) SetActionWire(actionOptions[actionOptionDropdown.value].Value.ToString());
        }

        private void SetActionWire(string value)
        {
            // 可见控件直接写回旧协议字段，但不把程序自己的同步当成测试/旧场景的外部覆写。
            writingActionWire = true;
            try { legacyActionOverride = false; actionInput.text = value; }
            finally { writingActionWire = false; }
        }

        private static void SetOptions(TMP_Dropdown dropdown, IEnumerable<string> labels)
        {
            var options = labels.ToList();
            if (options.Count == 0) options.Add("等待可操作目标");
            dropdown.ClearOptions(); dropdown.AddOptions(options);
        }

        private void Act(int index)
        {
            if (index < 0 || index >= actions.Length) return;
            var action = actions[index];
            int value = 0;
            string input = actionInput.text;
            if (!legacyActionOverride && action.RequiresValue)
            {
                if (numericAction == action.Kind && numberInput != null) input = numberInput.text;
                else if (pickerAction == action.Kind && actionOptionDropdown != null && actionOptionDropdown.value >= 0 && actionOptionDropdown.value < actionOptions.Length)
                    input = actionOptions[actionOptionDropdown.value].Value.ToString();
            }
            if (action.RequiresValue && (!int.TryParse(input, out value) || value < action.Minimum || value > action.Maximum))
            { machineFeedback = action.Kind == CasinoMiniGameAction.EnterCode ? "请填写三位密码。" : action.Kind == CasinoMiniGameAction.Bid ? "请填写预算内的有效出价。" : "请先选择本次操作的目标。"; RefreshMachine(); return; }
            machineFeedback = null;
            var result = controller.Game.Act(Guid.NewGuid().ToString("N"), action.Kind, value, null, Time.frameCount);
            legacyActionOverride = false;
            if (!result.Success) machineFeedback = result.Description ?? result.Error;
            RefreshMachine();
        }

        private void RefreshCards(CasinoAdventureState state)
        {
            EnsureCards(state.Config);
            inventoryRevision = state.Revision;
            foreach (var card in cards)
            {
                int count = state.Inventory.Find(entry => entry.ItemId == card.ItemId)?.Count ?? 0;
                bool prepared = state.PreparedItems.Contains(card.ItemId);
                bool canPurchase = (state.Phase == CasinoAdventurePhase.Playing || state.Phase == CasinoAdventurePhase.Shopping) && state.Coins - state.LockedCoins >= card.Price;
                card.Refresh(count, prepared, canPurchase, (count > 0 || prepared) && state.Phase != CasinoAdventurePhase.Ended);
            }
        }

        private void UseItem(string id)
        {
            if (controller.Game.State.PreparedItems.Contains(id)) controller.Game.CancelPreparedItem(id, Time.frameCount);
            else controller.UseAdventureItem(id);
        }

        private void RefreshEnding(CasinoAdventureState state)
        {
            bool finale = state.Phase == CasinoAdventurePhase.Finale;
            bool ended = state.Phase == CasinoAdventurePhase.Ended;
            bool hasVault = IsGameAllowed(state.Config, CasinoGameKind.CooperativeVault);
            bool canRescue = state.Inventory.Any(item => item.ItemId == "rescue_whistle" && item.Count > 0);
            leaveEndingButton.gameObject.SetActive(finale); takeoverButton.gameObject.SetActive(finale && hasVault);
            takeoverButton.interactable = state.TakeOverUnlocked;
            vaultChallengeButton.gameObject.SetActive(finale && hasVault && !state.TakeOverUnlocked);
            withdrawButton.gameObject.SetActive(state.Phase == CasinoAdventurePhase.Failed);
            endingText.text = ended ? EndingName(state.Ending) + "\n团队筹码 " + state.Coins + "  ·  已完成 " + state.CompletedStages + " 个区域"
                : finale ? "最终额度已达成。\n体面离场：兑换离场奖励。" + (hasVault ? "\n接管狂欢城：先实际完成额外金库挑战，再选择接管。" : "\n当前阶段已完成，可以离场或返回园区入口。")
                : "本区未达额度。\n" + (canRescue ? "可以使用库存救场哨尝试救场，或选择狼狈撤离。" : "选择狼狈撤离，或返回园区入口开始新的旅程。");
            if (ended)
            {
                if (endingRecord == null) endingRecord = controller.Game.ProfileData?.FinishedRunRecords.FirstOrDefault(value => value.RunId == state.RunId);
                if (endingRecord != null)
                {
                    string title = endingRecord.Rescues > 0 ? "救场王" : endingRecord.Pranks >= 3 ? "整蛊大师" : state.Ending == CasinoAdventureEnding.Withdraw ? "倒霉蛋" : "俱乐部幸存者";
                    endingText.text += "\n本局称号 · " + title + "\n投入 " + endingRecord.SubmittedBets + " 局 · 任务 " + endingRecord.Tasks + " 次\n整蛊 " + endingRecord.Pranks + " 次 · 已登记声望 " + endingRecord.Fame;
                }
            }
            if (endingArtwork != null)
            {
                var size = endingText.rectTransform.sizeDelta; size.x = ended ? 550 : 1020; endingText.rectTransform.sizeDelta = size;
                var binding = Array.Find(endingArtworks, value => value != null && value.Ending == state.Ending);
                endingArtwork.sprite = ended ? binding?.Sprite : null;
                endingArtwork.gameObject.SetActive(endingArtwork.sprite != null);
            }
        }

        private void OpenSlots() { overwriteSlot = 0; overwriteText.text = "三个独立存档槽；保存完整进度与未完成机台。"; ReadSlotInfo(); RefreshSlots(); Open(Window.Slots); }
        private void RefreshSlots()
        {
            for (int index = 0; index < slotTexts.Length; index++)
            {
                var info = slotInfo[index];
                if (info == null) continue;
                slotTexts[index].text = "存档 " + (index + 1) + "\n" + (info.IsEmpty ? "空槽" : info.Error ?? ("第" + (info.StageIndex + 1) + "区 · " + info.Coins + "筹码" + (info.UsesBackup ? "（备份可恢复）" : string.Empty)));
                saveButtons[index].interactable = controller.Game.HasAdventure;
                loadButtons[index].interactable = !info.IsEmpty && string.IsNullOrEmpty(info.Error);
            }
        }
        private void SaveSlot(int slot)
        {
            var info = slotInfo[slot - 1];
            if (!info.IsEmpty && overwriteSlot != slot) { overwriteSlot = slot; overwriteText.text = "存档" + slot + "已有内容，再次点保存确认覆盖。"; return; }
            if (controller.Game.SaveAdventure(slot)) { overwriteSlot = 0; overwriteText.text = "保存完成。阶段结束和离场会继续保存此槽。"; }
            else overwriteText.text = controller.Game.Status;
            ReadSlotInfo();
            RefreshSlots();
        }

        private void ReadSlotInfo()
        {
            // 磁盘校验只在用户打开存档及其保存/读取操作后执行，常规100毫秒HUD刷新仅使用缓存。
            for (int index = 0; index < slotInfo.Length; index++) slotInfo[index] = controller.Game.GetSaveSlotInfo(index + 1);
        }

        private void OnBeginParameterChanged(string text)
        {
            if (int.TryParse(choiceInput.text, out int choice)) rememberedChoices[selectedGame] = choice;
            beginRequest = Guid.NewGuid().ToString("N"); machineFeedback = null;
            if (window == Window.Machine) RefreshMachine();
        }

        private void ReturnToField()
        {
            var phase = controller.Game.State?.Phase;
            Open(!controller.Game.HasAdventure ? Window.Menu : phase == CasinoAdventurePhase.Finale || phase == CasinoAdventurePhase.Failed || phase == CasinoAdventurePhase.Ended ? Window.Ending :
                controller.Game.State.EventChoicePending ? Window.Event : Window.None);
        }

        private void RefreshMission(CasinoAdventureState state)
        {
            var mission = state.ActiveMission;
            missionText.text = mission == null ? "场地任务：留意突发事件，接近目标完成交互。" :
                mission.Description + "\n进度 " + mission.Progress + "/" + mission.TargetCount + "  ·  " +
                (mission.Completed ? "已完成" : mission.Failed ? "任务已结束" : "剩余 " + TimeText(Math.Max(0, mission.DeadlineMilliseconds - state.ElapsedMilliseconds))) +
                (mission.Carrying ? "  ·  搬运中，前往交付点" : string.Empty);
        }

        private void RefreshEvent(CasinoAdventureState state)
        {
            eventActions = controller.Game.GetEventActions();
            var definition = CasinoContentCatalog.FindEvent(state.CurrentEventId);
            eventText.text = (definition?.Name ?? "团队事件") + "\n" + (definition?.Description ?? "请选择如何回应事件。") +
                (string.IsNullOrEmpty(eventFeedback) ? string.Empty : "\n" + eventFeedback);
            for (int index = 0; index < eventButtons.Length; index++)
            {
                bool available = index < eventActions.Length;
                eventButtons[index].gameObject.SetActive(available);
                if (available) eventButtonTexts[index].text = eventActions[index].Label;
            }
            bool exchange = state.CurrentEventId == "item_exchange";
            exchangeDropdown.gameObject.SetActive(exchange);
            if (!exchange || exchangeRevision == state.Revision) return;
            string previous = exchangeDropdown.value < exchangeItemIds.Count ? exchangeItemIds[exchangeDropdown.value] : null;
            exchangeRevision = state.Revision; exchangeItemIds.Clear();
            var labels = new List<string>();
            foreach (var item in CasinoContentCatalog.Items)
            {
                int count = state.Inventory.Find(entry => entry.ItemId == item.Id)?.Count ?? 0;
                if (count <= 0 || state.PreparedItems.Contains(item.Id)) continue;
                exchangeItemIds.Add(item.Id); labels.Add(item.Name + " · 库存" + count);
            }
            if (labels.Count == 0) labels.Add("没有可交换的库存道具");
            exchangeDropdown.ClearOptions(); exchangeDropdown.AddOptions(labels);
            exchangeDropdown.SetValueWithoutNotify(Math.Max(0, exchangeItemIds.IndexOf(previous)));
            exchangeDropdown.interactable = exchangeItemIds.Count > 0;
        }

        private void ResolveEvent(int index)
        {
            if (index < 0 || index >= eventActions.Length) return;
            string itemId = exchangeDropdown.gameObject.activeSelf && exchangeDropdown.value < exchangeItemIds.Count ? exchangeItemIds[exchangeDropdown.value] : null;
            eventFeedback = null;
            var result = controller.Game.ResolveEvent(eventActions[index].Choice, itemId, Time.frameCount);
            if (!result.Success) eventFeedback = result.Description ?? result.Error;
            if (controller.Game.State.EventChoicePending) RefreshEvent(controller.Game.State);
            else ReturnToField();
        }

        private void EnsureCards(CasinoAdventureConfig config)
        {
            string filter = string.Join("|", config.ShopItemIds) + ":" + string.Join("|", config.AllowedGames.Select(game => ((int)game).ToString()));
            if (filter == cardFilter) return;
            cardFilter = filter;
            foreach (var card in cards) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            cards.Clear();
            foreach (var item in CasinoContentCatalog.Items)
            {
                if (config.ShopItemIds.Length > 0 && Array.IndexOf(config.ShopItemIds, item.Id) < 0 || !HasCompatibleGame(config, item.Id)) continue;
                var card = Instantiate(itemTemplate, itemContent); card.gameObject.SetActive(true);
                card.Bind(item, () => controller.Game.PurchaseItem(item.Id, Time.frameCount), () => UseItem(item.Id)); cards.Add(card);
            }
        }

        private static bool IsGameAllowed(CasinoAdventureConfig config, CasinoGameKind game)
            => config.AllowedGames.Length == 0 || Array.IndexOf(config.AllowedGames, game) >= 0;

        private static bool HasCompatibleGame(CasinoAdventureConfig config, string itemId)
        {
            if (config.AllowedGames.Length == 0) return true;
            switch (itemId)
            {
                case "lock_reel": return IsGameAllowed(config, CasinoGameKind.Slots);
                case "reroll_dice": return IsGameAllowed(config, CasinoGameKind.SicBo) || IsGameAllowed(config, CasinoGameKind.PushYourLuckDice);
                case "redraw_card": return IsGameAllowed(config, CasinoGameKind.Blackjack) || IsGameAllowed(config, CasinoGameKind.HighLow);
                case "xray": return IsGameAllowed(config, CasinoGameKind.Blackjack) || IsGameAllowed(config, CasinoGameKind.HighLow) ||
                    IsGameAllowed(config, CasinoGameKind.LuckyDraw) || IsGameAllowed(config, CasinoGameKind.BlindAuction) || IsGameAllowed(config, CasinoGameKind.CooperativeVault);
                default: return true;
            }
        }

        private void Listen(TMP_InputField input, UnityEngine.Events.UnityAction<string> callback)
        {
            if (input == null) return;
            input.onValueChanged.AddListener(callback);
            removeListeners.Add(() => { if (input != null) input.onValueChanged.RemoveListener(callback); });
        }
        private void Listen(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> callback)
        {
            if (dropdown == null) return;
            dropdown.onValueChanged.AddListener(callback);
            removeListeners.Add(() => { if (dropdown != null) dropdown.onValueChanged.RemoveListener(callback); });
        }
        private void Listen(Button button, Action callback)
        {
            if (button == null) return;
            UnityEngine.Events.UnityAction listener = () => { if (controller != null) controller.GetComponent<JinxCasinoAudioDirector>()?.PlayUiClick(); callback(); };
            button.onClick.AddListener(listener); removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
        }
        private static string HumanRules(string text)
        {
            foreach (var item in CasinoContentCatalog.Items) text = text.Replace(item.Id, item.Name);
            return text;
        }
        private static string TimeText(long milliseconds) => Math.Max(0, milliseconds / 60000).ToString("00") + ":" + Math.Max(0, milliseconds / 1000 % 60).ToString("00");
        private static string EndingName(CasinoAdventureEnding ending)
            => ending == CasinoAdventureEnding.TakeOver ? "接管狂欢城" : ending == CasinoAdventureEnding.LeaveWithDignity ? "体面离场" : "狼狈撤离";
        private void OnDestroy() => Unbind();
    }
}
