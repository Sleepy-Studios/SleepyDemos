using Core.Runtime;
using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Interaction
{
    /// 检票口与离场口的可见性、距离和撤离意图；资金与结局仍由Game提交。
    public sealed class JinxCasinoExitInteraction
    {
        private readonly JinxCasinoGame game;

        private readonly JinxCasinoPlayerInteraction player;

        private readonly Action leaveScene;

        internal JinxCasinoExitInteraction(JinxCasinoGame game, JinxCasinoPlayerInteraction player, JinxCasinoExitTerminal[] terminals, Action leaveScene)
        {
            this.game = game;
            this.player = player;
            this.leaveScene = leaveScene;
            if (terminals != null && terminals.Length > 0)
                Configure(terminals);
            else
                exitTerminals = Array.Empty<JinxCasinoExitTerminal>();
        }

        private JinxCasinoExitTerminal[] exitTerminals;

        private JinxCasinoExitTerminal armedExitTerminal;

        private string armedExitRun;

        private float exitArmedUntil;

        private int lastExitActionFrame = -1;

        private JinxCasinoExitTerminal feedbackExitTerminal;

        private CasinoAdventurePhase feedbackExitPhase;

        /// 最近现场离场反馈；没有额外钱包或结局状态。
        public string Feedback { get; private set; }

        /// 当前已有真正Ended的单区标准结果，供必要结局卡显示。
        public bool HasEnding => game.State?.Mode == CasinoAdventureMode.Standard && game.State.Phase == CasinoAdventurePhase.Ended && game.State.Config.StageCount == 1;

        /// 当前能实际接近且看到的检票/离场物件。
        public bool IsNearby => PreferNearby(player.FindNearbyStation());

        /// 与实际最近物件及原领域阶段一致，不把返回Hub误称为通关。
        public string Prompt
        {
            get
            {
                var terminal = FindNearbyExitTerminal();
                if (terminal == null || !IsNearby)
                    return string.Empty;
                if (HasEnding)
                    return "返回大厅";
                if (game.HasActiveRound)
                    return "回原机台完成这一局";
                if (terminal.Action == JinxCasinoExitAction.Verify)
                    return game.State.Phase == CasinoAdventurePhase.Finale ? "核验已通过，前往离场口" : "核验本次筹码";
                return IsWithdrawalArmed(terminal) ? "再次交互确认撤离" : game.State.Phase == CasinoAdventurePhase.Finale ? "领取离场券" : "提前离场";
            }
        }

        /// <summary>绑定两件已保存的现场物件，不替换已有机台、柜台、出生位或碰撞。</summary>
        /// <param name="terminals">唯一ID的单区检票/离场物件。</param>
        private void Configure(JinxCasinoExitTerminal[] terminals)
        {
            if (terminals == null || terminals.Length != 2 || Array.Exists(terminals, terminal => terminal == null || string.IsNullOrWhiteSpace(terminal.TerminalId) || !Enum.IsDefined(typeof(JinxCasinoExitAction), terminal.Action)) || terminals[0].TerminalId == terminals[1].TerminalId || terminals[0].Action == terminals[1].Action)
                throw new ArgumentException("单区离场需要唯一ID的检票与离场两件物件。", nameof(terminals));
            exitTerminals = (JinxCasinoExitTerminal[])terminals.Clone();
        }

        /// <summary>查询该物件的短时撤离确认，仅是输入意图，不是已支付结局。</summary>
        /// <param name="terminal">实际保存的离场物件。</param>
        public bool IsWithdrawalArmed(JinxCasinoExitTerminal terminal) => terminal != null && armedExitTerminal == terminal && armedExitRun == game.State?.RunId && (player.Clock?.TimeSeconds ?? 0) < exitArmedUntil;

        private JinxCasinoExitTerminal FindNearbyExitTerminal()
        {
            if (player.Body == null || player.Camera == null || game.State?.Mode != CasinoAdventureMode.Standard || game.State.Config.StageCount != 1)
                return null;
            JinxCasinoExitTerminal nearest = null;
            float best = 1.6f * 1.6f;
            foreach (var terminal in exitTerminals)
            {
                if (terminal == null || !terminal.isActiveAndEnabled)
                    continue;
                float distance = (terminal.InteractionPosition - player.Body.transform.position).sqrMagnitude;
                if (distance > best)
                    continue;
                Vector3 view = player.Camera.WorldToViewportPoint(terminal.ControlPosition);
                if (view.z <= 0 || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1)
                    continue;
                if (!Physics.Linecast(player.Camera.transform.position, terminal.ControlPosition, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<JinxCasinoExitTerminal>() != terminal)
                    continue;
                nearest = terminal;
                best = distance;
            }

            return nearest;
        }

        internal bool PreferNearby(JinxCasinoStation station)
        {
            var terminal = FindNearbyExitTerminal();
            if (terminal == null)
                return false;
            float distance = (terminal.InteractionPosition - player.Body.transform.position).sqrMagnitude;
            return (station == null || distance < (station.InteractionPosition - player.Body.transform.position).sqrMagnitude) && (!player.IsShopNearby || distance < (player.ShopCounter.InteractionPosition - player.Body.transform.position).sqrMagnitude);
        }

        internal void Interact()
        {
            if (!player.AcceptsCommands || player.IsPaused || player.IsMenuOpen || player.HasFocus || lastExitActionFrame == Time.frameCount)
                return;
            var terminal = FindNearbyExitTerminal();
            if (terminal == null)
                return;
            lastExitActionFrame = Time.frameCount;
            feedbackExitTerminal = terminal;
            feedbackExitPhase = game.State.Phase;
            if (HasEnding)
            {
                leaveScene();
                return;
            }

            if (game.HasActiveRound)
            {
                Feedback = "先回到投入筹码的机台，完成这一局后再来。";
                player.NotifyChanged();
                return;
            }

            CasinoAdventureResult result;
            if (terminal.Action == JinxCasinoExitAction.Verify)
            {
                if (game.State.Phase == CasinoAdventurePhase.Finale)
                {
                    Feedback = "核验已通过，请去另一侧领取离场券。";
                    player.NotifyChanged();
                    return;
                }

                if (game.State.Phase != CasinoAdventurePhase.Playing)
                {
                    Feedback = "时间已到，请前往另一侧离场口。";
                    player.NotifyChanged();
                    return;
                }

                var completion = new JinxCasinoCompleteStageAction(game, Time.frameCount);
                GlobalData.Dispatch(completion);
                result = completion.Result;
            }
            else if (game.State.Phase == CasinoAdventurePhase.Finale)
            {
                var ending = new JinxCasinoChooseEndingAction(game, CasinoAdventureEnding.LeaveWithDignity, Time.frameCount);
                GlobalData.Dispatch(ending);
                result = ending.Result;
            }
            else if (!IsWithdrawalArmed(terminal))
            {
                armedExitTerminal = terminal;
                armedExitRun = game.State.RunId;
                exitArmedUntil = (player.Clock?.TimeSeconds ?? 0) + 5;
                Feedback = "再次交互确认撤离：会结束本次旅程，并保留剩余筹码。";
                player.NotifyChanged();
                return;
            }
            else
            {
                var withdrawal = new JinxCasinoChooseEndingAction(game, CasinoAdventureEnding.Withdraw, Time.frameCount);
                GlobalData.Dispatch(withdrawal);
                result = withdrawal.Result;
            }

            Feedback = result.Success && terminal.Action == JinxCasinoExitAction.Verify ? "验票通过，请到离场口领取离场券。" : result.Description ?? result.Error;
            feedbackExitPhase = game.State.Phase;
            if (result.Success)
                ClearExitIntent();
            player.NotifyChanged();
        }

        internal void Tick()
        {
            var nearby = FindNearbyExitTerminal();
            bool changed = false;
            if (Feedback != null && (nearby != feedbackExitTerminal || game.State?.Phase != feedbackExitPhase))
            {
                Feedback = null;
                feedbackExitTerminal = null;
                changed = true;
            }

            if (armedExitTerminal != null && (armedExitRun != game.State?.RunId || (player.Clock?.TimeSeconds ?? 0) >= exitArmedUntil || nearby != armedExitTerminal))
            {
                ClearExitIntent();
                Feedback = null;
                feedbackExitTerminal = null;
                changed = true;
            }

            if (changed)
                player.NotifyChanged();
        }

        internal void Reset()
        {
            ClearExitIntent();
            Feedback = null;
            feedbackExitTerminal = null;
            lastExitActionFrame = -1;
        }

        private void ClearExitIntent()
        {
            armedExitTerminal = null;
            armedExitRun = null;
            exitArmedUntil = 0;
        }
    }
}
