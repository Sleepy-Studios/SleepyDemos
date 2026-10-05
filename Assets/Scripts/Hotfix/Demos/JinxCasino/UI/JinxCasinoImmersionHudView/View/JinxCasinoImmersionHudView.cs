using System;
using Hotfix.JinxCasino;
using Hotfix.JinxCasino.UI;
using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// 常驻HUD只维护场地提示、触控区域与教学提示条。
    [Module("JinxCasino")]
    [UIBind("JinxCasinoImmersionHudView")]
    public sealed partial class JinxCasinoImmersionHudView : View
    {
        private GameObject fieldHud;

        private TMP_Text wallet;

        private TMP_Text objective;

        private TMP_Text prompt;

        private TMP_Text feedback;

        private Button pause;

        private Button interact;

        private Button exitTable;

        private TouchInputPad movePad;

        private TouchInputPad lookPad;

        private GameObject tutorialStrip;

        private TMP_Text tutorialHintText;

        private TMP_Text tutorialDirectionText;

        private TMP_Text tutorialFeedbackText;

        private JinxCasinoController owner;

        protected override void OnGameObjectInitialize()
        {
            fieldHud = RectTransform_FieldHud.gameObject;
            wallet = TextMeshProUGUI_Wallet;
            objective = TextMeshProUGUI_Objective;
            prompt = TextMeshProUGUI_Prompt;
            feedback = TextMeshProUGUI_Feedback;
            pause = Button_Pause;
            interact = Button_Interact;
            exitTable = Button_ExitTable;
            movePad = TouchInputPad_MovePad;
            lookPad = TouchInputPad_LookPad;
            tutorialStrip = RectTransform_TutorialStrip.gameObject;
            tutorialHintText = TextMeshProUGUI_Hint;
            tutorialDirectionText = TextMeshProUGUI_Direction;
            tutorialFeedbackText = TextMeshProUGUI_Feedback1;
            pauseLabel = pause.GetComponentInChildren<TMP_Text>(true);
            exitTableLabel = exitTable.GetComponentInChildren<TMP_Text>(true);
            originalMove = PadLayout.Capture((RectTransform)movePad.transform);
            originalLook = PadLayout.Capture((RectTransform)lookPad.transform);
            positionsCaptured = true;
            pause.onClick.AddListener(Pause);
            interact.onClick.AddListener(Interact);
            exitTable.onClick.AddListener(ExitTable);
            BindData<JinxCasinoData>(OnData);
        }

        private bool? appliedLeftHanded;

        private void OnData(JinxCasinoData value)
        {
            owner = value.Scene;
            tableInputLabel = null;
            bool leftHanded = value.Settings.Value.LeftHanded;
            if (appliedLeftHanded != leftHanded)
            {
                appliedLeftHanded = leftHanded;
                ApplyPadLayout();
            }

            Refresh();
        }

        protected override void OnHide()
        {
            if (owner != null)
                owner.Data.Player.BindTouchPads(null, null);
            if (positionsCaptured)
            {
                originalMove.Apply((RectTransform)movePad.transform);
                originalLook.Apply((RectTransform)lookPad.transform);
            }

            appliedLeftHanded = null;
            owner = null;
            base.OnHide();
        }

        private TMP_Text pauseLabel, exitTableLabel;

        private string tableInputLabel;

        private bool positionsCaptured;

        private PadLayout originalMove, originalLook;

        private struct PadLayout
        {
            internal Vector2 Minimum, Maximum, Pivot, Position, Size;

            internal static PadLayout Capture(RectTransform rect) => new PadLayout
            {
                Minimum = rect.anchorMin,
                Maximum = rect.anchorMax,
                Pivot = rect.pivot,
                Position = rect.anchoredPosition,
                Size = rect.sizeDelta
            };

            internal void Apply(RectTransform rect)
            {
                rect.anchorMin = Minimum;
                rect.anchorMax = Maximum;
                rect.pivot = Pivot;
                rect.sizeDelta = Size;
                rect.anchoredPosition = Position;
            }
        }

        /// <summary>显示前绑定场地与触控输入。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void SetData(JinxCasinoController controller)
        {
            owner = controller;
            tableInputLabel = null;
            owner.Data.Player.BindTouchPads(movePad, lookPad);
        }

        /// 隐藏时释放订阅、触控指针与布局引用。
        private void Pause() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.Pause));

        private void Interact() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.Interact));

        private void ExitTable() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ExitTable));

        private void Refresh()
        {
            if (owner == null)
                return;
            JinxCasinoPage state = owner.Data.Page;
            fieldHud.SetActive(state == JinxCasinoPage.Field);
            var adventure = owner.Data.Game.State;
            wallet.text = "筹码  " + (owner.Data.Game.State?.Coins ?? 0);
            objective.text = AdventureObjective(adventure);
            var table = owner.Data.Player.TableView;
            // 离桌会立即清会话，但相机仍在返回；这期间不能提前开放探索触区。
            bool atDesk = owner.Data.Player.HasFocus;
            bool touching = owner.Data.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Touch;
            bool exploring = state == JinxCasinoPage.Field && owner.Data.Player.IsExplorationInputReady;
            movePad.gameObject.SetActive(exploring && touching);
            lookPad.gameObject.SetActive(exploring && touching);
            interact.gameObject.SetActive(exploring && touching);
            exitTable.gameObject.SetActive(state == JinxCasinoPage.Field && atDesk);
            exitTable.interactable = table != null || owner.Data.Player.HasShopFocus;
            if (pauseLabel != null)
                pauseLabel.text = owner.Data.Player.InputLabel("Menu/Pause", "暂停");
            if (exitTableLabel != null)
                exitTableLabel.text = owner.Data.Player.InputLabel("Table/Back", "离开桌面");
            string action = owner.Data.Player.InputLabel("Exploration/Interact", "");
            var nearby = owner.Data.Player.FindNearbyStation();
            tableInputLabel ??= "方向选择 · " + owner.Data.Player.InputLabel("Table/Confirm", "操作") + " · " + owner.Data.Player.InputLabel("Table/Secondary", "次要") + " · " + owner.Data.Player.InputLabel("Table/Help", "规则") + " · " + owner.Data.Player.InputLabel("Table/Back", "离开");
            prompt.text = atDesk ? tableInputLabel : owner.Data.Player.Exit.IsNearby ? action + " " + owner.Data.Player.Exit.Prompt : owner.Data.Player.IsShopNearby ? action + " 查看附近机台 / 补给柜台" : nearby != null ? action + " 进入机台" : "走近一张机台，试试今天的运气";
            if (state == JinxCasinoPage.Field && !exploring && table == null && !owner.Data.Player.HasShopFocus)
                prompt.text = "正在回到探索视角…";
            feedback.text = InteractionFeedback(table);
            tutorialStrip.SetActive(state == JinxCasinoPage.Field && owner.Data.Player.Tutorial.Status == Hotfix.JinxCasino.Rules.CasinoTutorialStatus.Active);
            tutorialHintText.text = owner.Data.Player.Tutorial.Hint;
            tutorialDirectionText.text = owner.Data.Player.Tutorial.Direction;
            tutorialFeedbackText.text = !string.IsNullOrEmpty(owner.Data.Player.Tutorial.Feedback) ? owner.Data.Player.Tutorial.Feedback : owner.Data.Tutorial.Feedback ?? string.Empty;
            var stripRect = (RectTransform)tutorialStrip.transform;
            float stripHeight = !string.IsNullOrEmpty(tutorialFeedbackText.text) ? 156 : !string.IsNullOrEmpty(tutorialDirectionText.text) ? 112 : 82;
            stripRect.sizeDelta = new Vector2(stripRect.sizeDelta.x, stripHeight);
        }

        private string AdventureObjective(CasinoAdventureState state)
        {
            if (state == null)
                return string.Empty;
            if (state.Mode == CasinoAdventureMode.Practice)
                return "自由练习";
            if (state.Phase == CasinoAdventurePhase.Closing)
                return "时间结束 · 完成当前机台";
            if (state.Phase == CasinoAdventurePhase.Finale)
                return "核验通过 · 前往离场口";
            if (state.Phase == CasinoAdventurePhase.Failed)
                return "本次未达标 · 前往离场口";
            return "目标 " + owner.Data.Game.Target + "   ·   " + Mathf.CeilToInt(state.RemainingMilliseconds / 1000f) + " 秒";
        }

        private string InteractionFeedback(JinxCasinoTableView table)
        {
            if (owner.Data.Player.HasShopFocus)
                return owner.Data.Player.ShopFeedback ?? "选择实物查看报价；购买按钮确认付款。";
            if (table == null)
            {
                if (owner.Data.Player.Exit.IsNearby)
                    return owner.Data.Player.Exit.Feedback ?? string.Empty;
                var state = owner.Data.Game.State;
                if (state?.Mode != CasinoAdventureMode.Standard)
                    return string.Empty;
                if (state.Phase == CasinoAdventurePhase.Closing)
                    return "时间到了，先回到原机台完成这一局。";
                if (state.Phase == CasinoAdventurePhase.Finale)
                    return "验票通过，前往离场口领取离场券。";
                if (state.Phase == CasinoAdventurePhase.Failed)
                    return "本次未达标，前往离场口结束旅程。";
                if (state.Phase == CasinoAdventurePhase.Playing && state.Coins >= owner.Data.Game.Target)
                    return "筹码已达标，前往验票口核验。";
                return string.Empty;
            }

            if (owner.Data.Player.HasTableFeedbackError)
                return owner.Data.Player.TableFeedback;
            return owner.Data.Player.IsTableAnimating ? "等待机台完成动作。" : TableOperationHint(table);
        }

        private static string TableOperationHint(JinxCasinoTableView table)
        {
            if (table.IsSlotsPrepared)
                return "拉动右侧拉杆，开始这次投入。";
            if (table.HasOwnActiveRound)
                return table.Game == CasinoGameKind.Blackjack ? "桌边按钮：要牌或停牌。" : "绿灯亮起时拉动你的拉杆。";
            if (table.DraftStake > 0)
                return "检查桌面筹码与规则，再确认投入。";
            return table.Presentation?.IsComplete == true ? "结果已显示在机台上，可继续投入或离开。" : "选择筹码，确认后开始游玩。";
        }

        private void ApplyPadLayout()
        {
            if (!positionsCaptured || owner == null || movePad == null || lookPad == null)
                return;
            movePad.ResetInput();
            lookPad.ResetInput();
            bool left = owner.Data.Settings.Value.LeftHanded;
            (left ? originalLook : originalMove).Apply((RectTransform)movePad.transform);
            (left ? originalMove : originalLook).Apply((RectTransform)lookPad.transform);
        }
    }
}
