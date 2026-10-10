using System;
using System.Collections.Generic;

namespace Hotfix.KagitKart
{
    /// 原作标识与中文显示名，显示名不改变内部存档标识。
    public static class KagitKartCatalog
    {
        public const string SourceCommit = "da100b21ae1c5332d2843d21a9bb7b68b97e9c2a";
        private static readonly string[] TrackIds = { "meadow", "bosphorus", "glacier", "desk" };
        /// 原作四章顺序，供章节菜单使用。
        public static IReadOnlyList<string> Tracks { get; } = Array.AsReadOnly(TrackIds);
        private static readonly string[] TrackNames = { "雏菊草原", "博斯普鲁斯夜景", "冰川通道", "书写桌面" };
        private static readonly string[] TrackDescriptions = { "春日清晨、风车与溪流", "从蓝调时刻驶入夜色，跨越两座桥", "粉红晨曦中的山间通道", "驶出书本，来到读者的书桌" };
        private static readonly string[] TrackFiles = { "Meadow", "Bosphorus", "Glacier", "Desk" };
        private static readonly string[] CharacterIds = { "tilki", "kurbaga", "penguen", "ayi", "kedi", "baykus", "tavsan", "ahtapot" };
        private static readonly string[] CharacterNames = { "肉桂狐", "睡莲蛙", "摇摆企鹅", "蓬蓬熊", "煤球猫", "风车猫头鹰", "胡萝卜兔", "墨水章鱼" };
        private static readonly string[] CharacterFiles = { "Fox", "Frog", "Penguin", "Bear", "Cat", "Owl", "Rabbit", "Octopus" };
        private static readonly string[] ItemIds = { "rocket", "rocket3", "gum", "plane", "homing", "ink", "foil", "scissors" };
        private static readonly string[] ItemNames = { "钢笔火箭", "三连钢笔火箭", "泡泡糖", "纸飞机", "追踪纸飞机", "墨水瓶", "玻璃纸护盾", "剪刀" };

        // 每行仍按原作道具顺序抽取，不能仅按 ItemIds 顺序累加权重。
        private static readonly int[][] Odds =
        {
            new[] { 2, 50, 3, 40, 0, 10 },
            new[] { 2, 30, 3, 35, 0, 20, 4, 10, 5, 5 },
            new[] { 2, 15, 3, 25, 0, 25, 4, 20, 5, 10, 1, 5 },
            new[] { 3, 15, 0, 25, 4, 25, 5, 15, 1, 15, 6, 5 },
            new[] { 3, 10, 0, 20, 4, 25, 5, 15, 1, 20, 6, 7, 7, 3 },
            new[] { 0, 15, 4, 25, 5, 15, 1, 30, 6, 12, 7, 8 },
            new[] { 4, 15, 5, 10, 1, 40, 6, 25, 7, 10 },
            new[] { 4, 10, 5, 5, 1, 45, 6, 30, 7, 10 }
        };

        /// <summary>检查稳定赛道标识。</summary>
        /// <param name="id">原作赛道标识。</param>
        /// <returns>是否属于原作杯赛。</returns>
        public static bool IsTrack(string id) => Array.IndexOf(TrackIds, id) >= 0;

        /// <summary>获取赛道中文名。</summary>
        /// <param name="id">原作赛道标识。</param>
        public static string TrackName(string id) => Lookup(TrackIds, TrackNames, id);

        /// <summary>获取原作章节副标题的统一中文译文。</summary>
        /// <param name="id">原作赛道标识。</param>
        public static string TrackDescription(string id) => Lookup(TrackIds, TrackDescriptions, id);

        /// <summary>读取符合项目资源命名规则的赛道文件名，不改变内部标识。</summary>
        /// <param name="id">原作赛道标识。</param>
        public static string TrackAssetName(string id) => Lookup(TrackIds, TrackFiles, id);

        /// <summary>获取角色中文名。</summary>
        /// <param name="id">原作角色标识。</param>
        public static string CharacterName(string id) => Lookup(CharacterIds, CharacterNames, id);

        /// <summary>获取保存的角色资产名，不使用原作动物显示文字拼接资源地址。</summary>
        /// <param name="id">原作稳定角色标识。</param>
        /// <returns>对应的原生骨骼 Prefab 文件名。</returns>
        public static string CharacterAssetName(string id) => Lookup(CharacterIds, CharacterFiles, id);

        /// <summary>获取道具中文名。</summary>
        /// <param name="id">原作道具标识。</param>
        public static string ItemName(string id) => Lookup(ItemIds, ItemNames, id);

        /// <summary>迁移原作按名次的加权抽奖，剪刀未解锁时重新归一化。</summary>
        /// <param name="place">名次，范围 1 到 8。</param>
        /// <param name="uniform">来自道具随机流的 [0,1) 数值。</param>
        /// <param name="scissorsAllowed">是否通过剪刀时间与并发条件。</param>
        /// <returns>原作道具标识。</returns>
        public static string PickItem(int place, double uniform, bool scissorsAllowed)
        {
            if (place < 1 || place > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(place));
            }
            if (double.IsNaN(uniform) || uniform < 0 || uniform >= 1)
                throw new ArgumentOutOfRangeException(nameof(uniform));
            int[] row = Odds[place - 1];
            int total = 0;
            for (int i = 0; i < row.Length; i += 2)
                if (scissorsAllowed || row[i] != 7)
                {
                    total += row[i + 1];
                }
            double value = uniform * total;
            for (int i = 0; i < row.Length; i += 2)
            {
                if (!scissorsAllowed && row[i] == 7)
                {
                    continue;
                }
                value -= row[i + 1];
                if (value < 0)
                {
                    return ItemIds[row[i]];
                }
            }
            return ItemIds[row[0]];
        }

        private static string Lookup(string[] ids, string[] names, string id)
        {
            int index = Array.IndexOf(ids, id);
            if (index < 0)
            {
                throw new ArgumentException("未知的纸艺卡丁车内容标识。", nameof(id));
            }
            return names[index];
        }
    }
}
