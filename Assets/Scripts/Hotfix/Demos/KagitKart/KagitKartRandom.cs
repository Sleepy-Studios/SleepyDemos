using System;

namespace Hotfix.KagitKart
{
    /// 原作 Mulberry32 随机流；玩法与表现使用独立分支。
    public sealed class KagitKartRandom
    {
        private readonly uint seed;
        private uint state;

        /// <summary>使用原作无符号 32 位种子。</summary>
        /// <param name="seed">比赛种子。</param>
        public KagitKartRandom(uint seed)
        {
            this.seed = seed;
            state = seed;
        }

        /// 读取下一个 [0,1) 随机数。
        public double Next()
        {
            unchecked
            {
                state += 0x6d2b79f5;
                uint value = (state ^ (state >> 15)) * (state | 1);
                value ^= value + (value ^ (value >> 7)) * (value | 61);
                return (value ^ (value >> 14)) / 4294967296.0;
            }
        }

        /// <summary>创建不消耗当前流的命名分支，与原作 UTF-16 FNV-1a 一致。</summary>
        /// <param name="name">固定分支名，例如 ai、items、grid。</param>
        /// <returns>独立的确定性随机流。</returns>
        public KagitKartRandom Fork(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in name)
                {
                    hash = (hash ^ character) * 16777619;
                }
                return new KagitKartRandom(hash ^ seed * 2654435761);
            }
        }
    }
}
