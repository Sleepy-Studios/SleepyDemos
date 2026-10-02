using Hotfix.JinxCasino;
using Hotfix.JinxCasino.Interaction;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.UI
{
    /// 样板薄HUD与菜单；桌面主操作仍在实体物件，所有控件由Prefab保存。
    public sealed partial class JinxCasinoImmersionHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject fieldHud;
        [SerializeField] private TMP_Text wallet;
        [SerializeField] private TMP_Text objective;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private Button quitGameButton;
        private Vector2 mainMenuSize;
        private bool mainMenuSizeCaptured;
        private TMP_Text pauseLabel;
        private TMP_Text exitTableLabel;
        [SerializeField] private Button start;
        [SerializeField] private Button practice;
        [SerializeField] private Button resume;
        [SerializeField] private Button pause;
        [SerializeField] private Button leave;
        [SerializeField] private Button interact;
        [SerializeField] private Button exitTable;
        [SerializeField] private TouchInputPad movePad;
        [SerializeField] private TouchInputPad lookPad;
        private JinxCasinoController owner;
        private int menuState = -1;

        /// <summary>绑定当前样板宿主，退出后释放具体订阅和触屏指针。</summary>
        /// <param name="controller">所属本地场景。</param>
        public void Bind(JinxCasinoController controller)
        {
            Unbind(); owner = controller; menuState = -1;
            pauseLabel = pause.GetComponentInChildren<TMP_Text>(true);
            exitTableLabel = exitTable.GetComponentInChildren<TMP_Text>(true);
            if (!mainMenuSizeCaptured) { mainMenuSize = ((RectTransform)mainMenu.transform).sizeDelta; mainMenuSizeCaptured = true; }
            if (quitGameButton != null) quitGameButton.onClick.AddListener(QuitGame);
            owner.Changed += Refresh; owner.Player.Changed += Refresh;
            owner.Player.BindTouchPads(movePad, lookPad);
            start.onClick.AddListener(StartAdventure); practice.onClick.AddListener(StartPractice);
            resume.onClick.AddListener(Resume); pause.onClick.AddListener(Pause);
            leave.onClick.AddListener(Leave); interact.onClick.AddListener(Interact); exitTable.onClick.AddListener(ExitTable);
            BindTutorialControls();
            BindSaveControls();
            BindStandardEndingControls();
            BindSettingsControls();
            var mainButtons = new System.Collections.Generic.List<Button> { start, practice, tutorialStartButton, saveMainLoadButton, settingsMainButton };
            if (owner.IsStandalonePlayer) mainButtons.Add(quitGameButton);
            mainButtons.RemoveAll(button => button == null); SaveNavigation(mainButtons);
            Refresh();
        }

        /// 释放所有本实例事件，不清空其它控件的监听器。
        public void Unbind()
        {
            UnbindSettingsControls();
            if (owner == null) return;
            UnbindStandardEndingControls();
            UnbindSaveControls();
            UnbindTutorialControls();
            owner.Changed -= Refresh; owner.Player.Changed -= Refresh;
            owner.Player.BindTouchPads(null, null); owner.Player.SetMenuState(false, false, null);
            start.onClick.RemoveListener(StartAdventure); practice.onClick.RemoveListener(StartPractice);
            resume.onClick.RemoveListener(Resume); pause.onClick.RemoveListener(Pause);
            leave.onClick.RemoveListener(Leave); interact.onClick.RemoveListener(Interact); exitTable.onClick.RemoveListener(ExitTable);
            if (quitGameButton != null) quitGameButton.onClick.RemoveListener(QuitGame);
            ((RectTransform)mainMenu.transform).sizeDelta = mainMenuSize;
            owner = null;
        }
        private void QuitGame() => owner.QuitStandaloneApplication();
        private void StartAdventure() { owner.StartAdventure(CasinoAdventureMode.Standard); Refresh(); }
        private void StartPractice() { owner.StartAdventure(CasinoAdventureMode.Practice); Refresh(); }
        private void Pause() { owner.Player.Pause(); Refresh(); }
        private void Resume() { owner.Player.Resume(); Refresh(); }
        private void Leave() => owner.RequestExit();
        private void Interact() { owner.Player.Interact(); Refresh(); }
        private void ExitTable() { owner.Player.CloseTable(true); Refresh(); }
        private void Update() { if (owner != null) Refresh(); }

        private void Refresh()
        {
            if (owner == null) return;
            int state = ResolveSettingsHudState(ResolveSaveHudState(ResolveEndingHudState(ResolveTutorialHudState())));
            if (menuState != state)
            {
                menuState = state;
                mainMenu.SetActive(state == 0); pauseMenu.SetActive(state == 1); fieldHud.SetActive(state == 2);
                RefreshSettingsControls(state);
                RefreshTutorialControls(state);
                RefreshSaveControls(state);
                RefreshStandardEndingControls(state);
                owner.Player.SetMenuState(state != 2, state == 9, SettingsFirstSelection(state), HasSettingsUi || HasStandardEndingUi || HasSaveUi || HasTutorialUi ? CancelImmersionHudWindow : null);
            }
            if (quitGameButton != null)
            {
                quitGameButton.gameObject.SetActive(state == 0 && owner.IsStandalonePlayer);
                ((RectTransform)mainMenu.transform).sizeDelta = new Vector2(mainMenuSize.x, owner.IsStandalonePlayer ? mainMenuSize.y + 84 : mainMenuSize.y);
            }
            var leaveLabel = leave.GetComponentInChildren<TMP_Text>(true);
            if (leaveLabel != null) leaveLabel.text = owner.IsStandalonePlayer ? "返回主菜单" : "返回大厅";
            RefreshSettingsControls(state);
            RefreshTutorialControls(state);
            RefreshSaveControls(state);
            RefreshStandardEndingControls(state);
            var adventure = owner.Game.State;
            wallet.text = "筹码  " + (owner.Game.State?.Coins ?? 0);
            objective.text = AdventureObjective(adventure);
            var table = owner.Player.TableView;
            bool atDesk = table != null || owner.Player.HasShopFocus;
            bool touching = owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Touch;
            bool usingGamepad = owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Gamepad;
            movePad.gameObject.SetActive(state == 2 && !atDesk && touching);
            lookPad.gameObject.SetActive(state == 2 && !atDesk && touching);
            interact.gameObject.SetActive(state == 2 && !atDesk && touching);
            exitTable.gameObject.SetActive(state == 2 && atDesk);
            if (pauseLabel != null) pauseLabel.text = usingGamepad ? "Menu 暂停" : touching || atDesk ? "暂停" : "Esc 暂停";
            if (exitTableLabel != null) exitTableLabel.text = usingGamepad ? "B 离开桌面" : touching ? "离开桌面" : "Esc 离开桌面";
            string action = owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Gamepad ? "A" : touching ? "交互" : "E";
            var nearby = owner.Player.FindNearbyStation();
            prompt.text = atDesk ? owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Gamepad
                ? "方向选择 · A 操作 · X 次要 · Y 规则 · B 离开" : touching ? "点选桌面物件 · 轻触返回离开" : "点击物件 · 方向键 / Enter · H 规则 · Esc 离开"
                : owner.Player.Exit.IsNearby ? action + " " + owner.Player.Exit.Prompt : owner.Player.IsShopNearby ? action + " 查看附近机台 / 补给柜台" : nearby != null ? action + " 进入机台" : "走近一张机台，试试今天的运气";
            feedback.text = InteractionFeedback(table);
        }
        private string AdventureObjective(CasinoAdventureState state)
        {
            if (state == null) return string.Empty;
            if (state.Mode == CasinoAdventureMode.Practice) return "自由练习";
            if (state.Phase == CasinoAdventurePhase.Closing) return "时间结束 · 完成当前机台";
            if (state.Phase == CasinoAdventurePhase.Finale) return "核验通过 · 前往离场口";
            if (state.Phase == CasinoAdventurePhase.Failed) return "本次未达标 · 前往离场口";
            return "目标 " + owner.Game.Target + "   ·   " + Mathf.CeilToInt(state.RemainingMilliseconds / 1000f) + " 秒";
        }

        private string InteractionFeedback(JinxCasinoTableView table)
        {
            if (owner.Player.HasShopFocus) return owner.Player.ShopFeedback ?? "选择实物查看报价；购买按钮确认付款。";
            if (table == null)
            {
                if (owner.Player.Exit.IsNearby) return owner.Player.Exit.Feedback ?? string.Empty;
                var state = owner.Game.State;
                if (state?.Mode != CasinoAdventureMode.Standard) return string.Empty;
                if (state.Phase == CasinoAdventurePhase.Closing) return "时间到了，先回到原机台完成这一局。";
                if (state.Phase == CasinoAdventurePhase.Finale) return "验票通过，前往离场口领取离场券。";
                if (state.Phase == CasinoAdventurePhase.Failed) return "本次未达标，前往离场口结束旅程。";
                if (state.Phase == CasinoAdventurePhase.Playing && state.Coins >= owner.Game.Target) return "筹码已达标，前往验票口核验。";
                return string.Empty;
            }
            if (owner.Player.HasTableFeedbackError) return owner.Player.TableFeedback;
            return owner.Player.IsTableAnimating ? "等待机台完成动作。" : TableOperationHint(table);
        }

        private static string TableOperationHint(JinxCasinoTableView table)
        {
            if (table.IsSlotsPrepared) return "拉动右侧拉杆，开始这次投入。";
            if (table.HasOwnActiveRound) return table.Game == CasinoGameKind.Blackjack ? "桌边按钮：要牌或停牌。" : "绿灯亮起时拉动你的拉杆。";
            if (table.DraftStake > 0) return "检查桌面筹码与规则，再确认投入。";
            return table.Presentation?.IsComplete == true ? "结果已显示在机台上，可继续投入或离开。" : "选择筹码，确认后开始游玩。";
        }
        private void OnDestroy() => Unbind();
    }
}
