using System;

namespace Hotfix.KagitKart
{
    /// Demo 本机偏好和文件路径的统一定义；文件路径相对于各 Demo 的存储目录。
    public static class LocalDataKeys
    {
        public const string KagitKartDirectory = "KagitKart";
        public const string KagitKartHintsPreferences = "KagitKart.UiHints";
        public const string KagitKartPreferences = "KagitKart.LocalPreferences";

        /// <summary>计时记录按赛道、速度组别和角色隔离，不改变原作记录粒度。</summary>
        /// <param name="track">原作赛道标识。</param>
        /// <param name="speedClass">80、120 或 200。</param>
        /// <param name="character">原作角色标识。</param>
        public static string KagitKartRecord(string track, int speedClass, string character)
        {
            if (!KagitKartCatalog.IsTrack(track))
            {
                throw new ArgumentException("计时赛道无效。", nameof(track));
            }
            if (speedClass != 80 && speedClass != 120 && speedClass != 200)
            {
                throw new ArgumentOutOfRangeException(nameof(speedClass));
            }
            KagitKartCatalog.CharacterName(character);
            return "Records/" + track + "-" + speedClass + "-" + character + ".json";
        }
    }
}
