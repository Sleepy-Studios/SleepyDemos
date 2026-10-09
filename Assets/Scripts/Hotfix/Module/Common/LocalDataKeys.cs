using System;

namespace Hotfix
{
    /// Demo 本机偏好和文件路径的统一定义；文件路径相对于各 Demo 的存储目录。
    public static class LocalDataKeys
    {
        public const string HowToFishPreferences = "HowToFish.LocalPreferences";
        public const string CasinoPreferences = "JinxCasino.LocalPreferences";
        public const string HowToFishDirectory = "HowToFish";
        public const string CasinoDirectory = "JinxCasino";
        public const string CasinoProfileDirectory = CasinoDirectory + "/Profile";
        public const string HowToFishSkins = "PlayerSkins.json";
        public const string CasinoProfile = "profile.json";
        /// <summary>钓鱼三槽文件名。</summary>
        /// <param name="slot">从零开始，范围 0–2。</param>
        public static string HowToFishSlot(int slot)
        {
            if ((uint)slot >= 3)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return "Slot" + (slot + 1) + ".json";
        }

        /// <summary>赌场三槽文件名。</summary>
        /// <param name="slot">范围 1–3。</param>
        public static string CasinoSlot(int slot)
        {
            if (slot < 1 || slot > 3)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return "save-" + slot + ".json";
        }
    }
}
