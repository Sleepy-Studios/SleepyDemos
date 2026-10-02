using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        [SerializeField] private JinxCasinoExitTerminal[] exitTerminals = Array.Empty<JinxCasinoExitTerminal>();
        private JinxCasinoExitTerminal armedExitTerminal;
        private string armedExitRun;
        private float exitArmedUntil;
        private int lastExitActionFrame = -1;
        private JinxCasinoExitTerminal feedbackExitTerminal;
        private CasinoAdventurePhase feedbackExitPhase;
        /// 最近现场离场反馈；没有额外钱包或结局状态。
        public string ExitFeedback { get; private set; }
        /// 当前已有真正Ended的单区标准结果，供必要结局卡显示。
        public bool HasStandardEnding => Game.State?.Mode == CasinoAdventureMode.Standard && Game.State.Phase == CasinoAdventurePhase.Ended && Game.State.Config.StageCount == 1;
        /// 当前能实际接近且看到的检票/离场物件。
        public bool IsExitTerminalNearby => PreferNearbyExit(FindNearbyStation());
        /// 与实际最近物件及原领域阶段一致，不把返回Hub误称为通关。
        public string ExitInteractionPrompt
        {
            get
            {
                var terminal = FindNearbyExitTerminal();
                if (terminal == null || !IsExitTerminalNearby) return string.Empty;
                if (HasStandardEnding) return "返回大厅";
                if (Game.HasActiveRound) return "回原机台完成这一局";
                if (terminal.Action == JinxCasinoExitAction.Verify) return Game.State.Phase == CasinoAdventurePhase.Finale ? "核验已通过，前往离场口" : "核验本次筹码";
                return IsExitWithdrawalArmed(terminal) ? "再次交互确认撤离" : Game.State.Phase == CasinoAdventurePhase.Finale ? "领取离场券" : "提前离场";
            }
        }

        /// <summary>绑定两件已保存的现场物件，不替换已有机台、柜台、出生位或碰撞。</summary>
        /// <param name="terminals">唯一ID的单区检票/离场物件。</param>
        public void ConfigureExitTerminals(JinxCasinoExitTerminal[] terminals)
        {
            if (terminals == null || terminals.Length != 2 || Array.Exists(terminals, terminal => terminal == null ||
                string.IsNullOrWhiteSpace(terminal.TerminalId) || !Enum.IsDefined(typeof(JinxCasinoExitAction), terminal.Action)) ||
                terminals[0].TerminalId == terminals[1].TerminalId || terminals[0].Action == terminals[1].Action)
                throw new ArgumentException("单区离场需要唯一ID的检票与离场两件物件。", nameof(terminals));
            exitTerminals = (JinxCasinoExitTerminal[])terminals.Clone();
        }
        /// <summary>查询该物件的短时撤离确认，仅是输入意图，不是已支付结局。</summary>
        /// <param name="terminal">实际保存的离场物件。</param>
        public bool IsExitWithdrawalArmed(JinxCasinoExitTerminal terminal) => terminal != null && armedExitTerminal == terminal &&
            armedExitRun == Game.State?.RunId && (PresentationClock?.TimeSeconds ?? 0) < exitArmedUntil;

        private JinxCasinoExitTerminal FindNearbyExitTerminal()
        {
            if (!UsesImmersion || body == null || worldCamera == null || Game.State?.Mode != CasinoAdventureMode.Standard || Game.State.Config.StageCount != 1) return null;
            JinxCasinoExitTerminal nearest = null; float best = 1.6f * 1.6f;
            foreach (var terminal in exitTerminals)
            {
                if (terminal == null || !terminal.isActiveAndEnabled) continue;
                float distance = (terminal.InteractionPosition - body.transform.position).sqrMagnitude;
                if (distance > best) continue;
                Vector3 view = worldCamera.WorldToViewportPoint(terminal.ControlPosition);
                if (view.z <= 0 || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) continue;
                if (!Physics.Linecast(worldCamera.transform.position, terminal.ControlPosition, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    hit.collider.GetComponentInParent<JinxCasinoExitTerminal>() != terminal) continue;
                nearest = terminal; best = distance;
            }
            return nearest;
        }

        private bool PreferNearbyExit(JinxCasinoStation station)
        {
            var terminal = FindNearbyExitTerminal();
            if (terminal == null) return false;
            float distance = (terminal.InteractionPosition - body.transform.position).sqrMagnitude;
            return (station == null || distance < (station.InteractionPosition - body.transform.position).sqrMagnitude) &&
                (!IsShopNearby || distance < (shopCounter.InteractionPosition - body.transform.position).sqrMagnitude);
        }
        private void InteractWithExitTerminal()
        {
            if (IsBusy || IsImmersionPaused || IsAdventureInputBlocked || HasImmersionFocus || lastExitActionFrame == Time.frameCount) return;
            var terminal = FindNearbyExitTerminal();
            if (terminal == null) return;
            lastExitActionFrame = Time.frameCount;
            feedbackExitTerminal = terminal; feedbackExitPhase = Game.State.Phase;
            if (HasStandardEnding) { RequestExit(); return; }
            if (Game.HasActiveRound)
            { ExitFeedback = "先回到投入筹码的机台，完成这一局后再来。"; ImmersionInputChanged?.Invoke(); return; }
            CasinoAdventureResult result;
            if (terminal.Action == JinxCasinoExitAction.Verify)
            {
                if (Game.State.Phase == CasinoAdventurePhase.Finale)
                { ExitFeedback = "核验已通过，请去另一侧领取离场券。"; ImmersionInputChanged?.Invoke(); return; }
                if (Game.State.Phase != CasinoAdventurePhase.Playing)
                { ExitFeedback = "时间已到，请前往另一侧离场口。"; ImmersionInputChanged?.Invoke(); return; }
                result = Game.CompleteStage(Time.frameCount);
            }
            else if (Game.State.Phase == CasinoAdventurePhase.Finale)
                result = Game.ChooseEnding(CasinoAdventureEnding.LeaveWithDignity, Time.frameCount);
            else if (!IsExitWithdrawalArmed(terminal))
            {
                armedExitTerminal = terminal; armedExitRun = Game.State.RunId;
                exitArmedUntil = (PresentationClock?.TimeSeconds ?? 0) + 5;
                ExitFeedback = "再次交互确认撤离：会结束本次旅程，并保留剩余筹码。";
                ImmersionInputChanged?.Invoke(); return;
            }
            else result = Game.ChooseEnding(CasinoAdventureEnding.Withdraw, Time.frameCount);
            ExitFeedback = result.Success && terminal.Action == JinxCasinoExitAction.Verify ? "验票通过，请到离场口领取离场券。" : result.Description ?? result.Error;
            feedbackExitPhase = Game.State.Phase;
            if (result.Success) ClearExitIntent();
            ImmersionInputChanged?.Invoke();
        }

        private void UpdateExitIntent()
        {
            var nearby = FindNearbyExitTerminal();
            bool changed = false;
            if (ExitFeedback != null && (nearby != feedbackExitTerminal || Game.State?.Phase != feedbackExitPhase))
            { ExitFeedback = null; feedbackExitTerminal = null; changed = true; }
            if (armedExitTerminal != null && (armedExitRun != Game.State?.RunId ||
                (PresentationClock?.TimeSeconds ?? 0) >= exitArmedUntil || nearby != armedExitTerminal))
            { ClearExitIntent(); ExitFeedback = null; feedbackExitTerminal = null; changed = true; }
            if (changed) ImmersionInputChanged?.Invoke();
        }
        private void ResetExitInteraction()
        { ClearExitIntent(); ExitFeedback = null; feedbackExitTerminal = null; lastExitActionFrame = -1; }
        private void ClearExitIntent() { armedExitTerminal = null; armedExitRun = null; exitArmedUntil = 0; }
    }
}
