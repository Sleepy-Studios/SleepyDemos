namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoMiniGameRound
    {
        /// 当前协作局是否已接受一次真实扳手帮助；参与快照，重复帮助不会再次放宽窗口。
        public bool CooperationHelpUsed => state.CooperationHelpUsed;

        /// <summary>协作金库揭示一条未查看线索，协作杠杆每窗口前后各扩一百毫秒；每局最多一次。</summary>
        /// <returns>确实改变合法协作局时为true；宿主据此消费库存或预存次数。</returns>
        public bool TryApplyCooperationHelp()
        {
            if (state.Complete || state.CooperationHelpUsed) return false;
            if (state.Game == CasinoGameKind.CooperativeVault)
            {
                int unrevealed = -1;
                for (int index = 0; index < state.Selected.Length; index++) if (state.Selected[index] == 0) { unrevealed = index; break; }
                if (unrevealed < 0 || !TryAct(CasinoMiniGameAction.InspectClue, unrevealed)) return false;
            }
            else if (state.Game == CasinoGameKind.CooperativeLevers) state.OperationCount++;
            else return false;
            state.CooperationHelpUsed = true;
            DescribeParty(); return true;
        }
    }
}
