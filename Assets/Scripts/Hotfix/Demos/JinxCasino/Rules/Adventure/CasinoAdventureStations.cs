namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoAdventureSession
    {
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
