using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.UI
{
    /// 常驻HUD只维护场地提示、触控区域与教学提示条。
    public sealed class JinxCasinoImmersionHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject fieldHud;
        [SerializeField] private TMP_Text wallet;
        [SerializeField] private TMP_Text objective;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private Button pause;
        [SerializeField] private Button interact;
        [SerializeField] private Button exitTable;
        [SerializeField] private TouchInputPad movePad;
        [SerializeField] private TouchInputPad lookPad;
        [SerializeField] private GameObject tutorialStrip;
        [SerializeField] private TMP_Text tutorialHintText;
        [SerializeField] private TMP_Text tutorialDirectionText;
        [SerializeField] private TMP_Text tutorialFeedbackText;
        private JinxCasinoController owner;
        private TMP_Text pauseLabel, exitTableLabel;
        private string tableInputLabel;
        private bool positionsCaptured;
        private PadLayout originalMove, originalLook;
        private struct PadLayout
        {
            internal Vector2 Minimum, Maximum, Pivot, Position, Size;
            internal static PadLayout Capture(RectTransform rect) => new PadLayout { Minimum = rect.anchorMin, Maximum = rect.anchorMax,
                Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta };
            internal void Apply(RectTransform rect)
            { rect.anchorMin = Minimum; rect.anchorMax = Maximum; rect.pivot = Pivot; rect.sizeDelta = Size; rect.anchoredPosition = Position; }
        }

        /// <summary>显示前绑定场地与触控输入。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void Bind(JinxCasinoController controller)
        {
            Unbind(); owner = controller; tableInputLabel = null;
            pauseLabel = pause.GetComponentInChildren<TMP_Text>(true); exitTableLabel = exitTable.GetComponentInChildren<TMP_Text>(true);
            if (!positionsCaptured)
            { originalMove = PadLayout.Capture((RectTransform)movePad.transform); originalLook = PadLayout.Capture((RectTransform)lookPad.transform); positionsCaptured = true; }
            owner.Changed += Refresh; owner.Player.Changed += OnPlayerChanged;
            owner.Settings.Changed += ApplyPadLayout; ApplyPadLayout();
            owner.Player.BindTouchPads(movePad, lookPad);
            pause.onClick.AddListener(Pause); interact.onClick.AddListener(Interact); exitTable.onClick.AddListener(ExitTable);
            Refresh();
        }
        /// 隐藏时释放订阅、触控指针与布局引用。
        public void Unbind()
        {
            if (owner == null) return;
            owner.Changed -= Refresh; owner.Player.Changed -= OnPlayerChanged; owner.Settings.Changed -= ApplyPadLayout;
            owner.Player.BindTouchPads(null, null);
            pause.onClick.RemoveListener(Pause); interact.onClick.RemoveListener(Interact); exitTable.onClick.RemoveListener(ExitTable);
            if (positionsCaptured) { originalMove.Apply((RectTransform)movePad.transform); originalLook.Apply((RectTransform)lookPad.transform); }
            owner = null;
        }
        private void Pause() => owner?.Player.Pause();
        private void Interact() => owner?.Player.Interact();
        private void ExitTable() => owner?.Player.CloseTable(true);
        private void Update() { if (owner != null) Refresh(); }
        private void Refresh()
        {
            if (owner == null) return;
            int state = owner.UI.State;
            fieldHud.SetActive(state == 2);
            var adventure = owner.Game.State;
            wallet.text = "筹码  " + (owner.Game.State?.Coins ?? 0);
            objective.text = AdventureObjective(adventure);
            var table = owner.Player.TableView;
            // 离桌会立即清会话，但相机仍在返回；这期间不能提前开放探索触区。
            bool atDesk = owner.Player.HasFocus;
            bool touching = owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Touch;
            bool exploring = state == 2 && owner.Player.IsExplorationInputReady;
            movePad.gameObject.SetActive(exploring && touching);
            lookPad.gameObject.SetActive(exploring && touching);
            interact.gameObject.SetActive(exploring && touching);
            exitTable.gameObject.SetActive(state == 2 && atDesk);
            exitTable.interactable = table != null || owner.Player.HasShopFocus;
            if (pauseLabel != null) pauseLabel.text = owner.Player.InputLabel("Menu/Pause", "暂停");
            if (exitTableLabel != null) exitTableLabel.text = owner.Player.InputLabel("Table/Back", "离开桌面");
            string action = owner.Player.InputLabel("Exploration/Interact", "");
            var nearby = owner.Player.FindNearbyStation();
            tableInputLabel ??= "方向选择 · " + owner.Player.InputLabel("Table/Confirm", "操作")
                + " · " + owner.Player.InputLabel("Table/Secondary", "次要") + " · "
                + owner.Player.InputLabel("Table/Help", "规则") + " · " + owner.Player.InputLabel("Table/Back", "离开");
            prompt.text = atDesk ? tableInputLabel
                : owner.Player.Exit.IsNearby ? action + " " + owner.Player.Exit.Prompt
                : owner.Player.IsShopNearby ? action + " 查看附近机台 / 补给柜台"
                : nearby != null ? action + " 进入机台" : "走近一张机台，试试今天的运气";
            if (state == 2 && !exploring && table == null && !owner.Player.HasShopFocus) prompt.text = "正在回到探索视角…";
            feedback.text = InteractionFeedback(table);
            tutorialStrip.SetActive(state == 2 && owner.Player.Tutorial.Status == Hotfix.JinxCasino.Rules.CasinoTutorialStatus.Active);
            tutorialHintText.text = owner.Player.Tutorial.Hint;
            tutorialDirectionText.text = owner.Player.Tutorial.Direction;
            tutorialFeedbackText.text = !string.IsNullOrEmpty(owner.Player.Tutorial.Feedback) ? owner.Player.Tutorial.Feedback : owner.UI.Tutorial.Feedback ?? string.Empty;
            var stripRect = (RectTransform)tutorialStrip.transform;
            float stripHeight = !string.IsNullOrEmpty(tutorialFeedbackText.text) ? 156 : !string.IsNullOrEmpty(tutorialDirectionText.text) ? 112 : 82;
            stripRect.sizeDelta = new Vector2(stripRect.sizeDelta.x, stripHeight);
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
        private void ApplyPadLayout()
        {
            if (!positionsCaptured || owner == null || movePad == null || lookPad == null) return;
            movePad.ResetInput(); lookPad.ResetInput();
            bool left = owner.Settings.Value.LeftHanded;
            (left ? originalLook : originalMove).Apply((RectTransform)movePad.transform);
            (left ? originalMove : originalLook).Apply((RectTransform)lookPad.transform);
        }
        private void OnPlayerChanged() { tableInputLabel = null; Refresh(); }
        private void OnDestroy() => Unbind();
    }
}
