using System;
using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino.Adapters
{
    /// Controller与具体保存机台的薄适配；不修改现有Host刷新/效果/存档流程。
    public sealed class JinxCasinoControllerTableOperations : IJinxCasinoTableOperations
    {
        private readonly JinxCasinoController owner;
        private readonly JinxCasinoStation station;

        /// <summary>绑定实际Controller与机台，WorldGame改变后旧Session会拒绝草稿投入。</summary>
        /// <param name="controller">当前场景宿主。</param>
        /// <param name="table">具体保存的机台实例。</param>
        public JinxCasinoControllerTableOperations(JinxCasinoController controller, JinxCasinoStation table)
        { owner = controller ?? throw new ArgumentNullException(nameof(controller)); station = table != null ? table : throw new ArgumentNullException(nameof(table)); }

        public CasinoAdventureState AdventureState => owner.AdventureState;
        public string StationId => station != null ? station.StationId : null;
        public CasinoGameKind StationGame => station != null ? station.Game : CasinoGameKind.Slots;
        public bool IsStationAvailable => station != null && station.isActiveAndEnabled && station.HasTableInteraction;
        public string AdventureBetRules => owner.AdventureBetRules;
        public string ActiveRoundDescription => owner.ActiveRoundDescription;
        public CasinoMiniGamePresentation GetAdventurePresentation() => owner.GetAdventurePresentation();
        public CasinoMiniGameActionDescriptor[] GetAdventureActions() => owner.GetAdventureActions();
        public CasinoGameDefinition[] GetAvailableAdventureGames() => owner.GetAvailableAdventureGames();

        /// <summary>通过现有Host投入；效果与资金刷新仍只有Host一条链路。</summary>
        /// <param name="requestId">稳定请求编号。</param>
        /// <param name="game">具体机台玩法。</param>
        /// <param name="stake">已确认投入。</param>
        /// <param name="choice">本阶段三款为0。</param>
        /// <param name="stationId">具体机台ID。</param>
        public CasinoAdventureResult BeginAdventureGame(string requestId, CasinoGameKind game, long stake, int choice, string stationId)
            => owner.BeginAdventureGame(requestId, game, stake, choice, stationId);

        /// <summary>通过现有Host操作已投入局。</summary>
        /// <param name="requestId">稳定动作编号。</param>
        /// <param name="action">真实小游戏动作。</param>
        /// <param name="value">动作参数。</param>
        /// <param name="stationId">原机台ID。</param>
        public CasinoAdventureResult ActInAdventure(string requestId, CasinoMiniGameAction action, int value, string stationId)
            => owner.ActInAdventure(requestId, action, value, stationId);

    }
}
