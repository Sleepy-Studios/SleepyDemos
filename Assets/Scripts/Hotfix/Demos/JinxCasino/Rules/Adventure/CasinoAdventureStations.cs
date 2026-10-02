using System;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoAdventureSession
    {
        /// <summary>把旧存档未定位的活动局绑定到用户实际进入的同类机台，不开奖或结算。</summary>
        /// <param name="requestId">幂等请求ID，重试须保留机台及玩法。</param>
        /// <param name="stationId">场景保存的非空机台ID。</param>
        /// <param name="game">实际机台玩法，必须与活动局匹配。</param>
        /// <returns>绑定成功或已有同一绑定；其它机台不能接管。</returns>
        public CasinoAdventureResult BindActiveStation(string requestId, string stationId, CasinoGameKind game)
            => Execute(requestId, "station:" + stationId + ":" + (int)game, () =>
            {
                if (string.IsNullOrEmpty(stationId) || !IsValidStationId(stationId)) return Fail("InvalidStation", "机台标识非法。");
                if (round == null || state.ActiveGame != game) return Fail("WrongGame", "此机台不是存档中未完成的玩法。");
                if (!string.IsNullOrEmpty(state.ActiveStationId) && state.ActiveStationId != stationId)
                    return Fail("WrongStation", "已投入的局不能转移到另一张机台。");
                state.ActiveStationId = stationId;
                return Ok("已恢复此机台的原有局，未重新投入或开奖。");
            });

        private static string StationFingerprint(string stationId)
            => string.IsNullOrEmpty(stationId) ? string.Empty : ":station:" + stationId;

        private static bool IsValidStationId(string stationId)
        {
            if (stationId == null || stationId.Length == 0) return true;
            if (stationId.Length > 96) return false;
            foreach (char letter in stationId)
                if (!(letter >= 'a' && letter <= 'z' || letter >= 'A' && letter <= 'Z' ||
                      letter >= '0' && letter <= '9' || letter == '-' || letter == '_' || letter == '.')) return false;
            return true;
        }
    }
}
