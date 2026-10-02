using System;

namespace Hotfix.JinxCasino.Rules
{
    // 使用显式 uint 溢出语义，不依赖 UnityEngine.Random、时间、浮点或平台运行库算法。
    public struct CasinoRandom
    {
        private uint state;

        /// 供快照保存的完整生成器状态。
        public uint State => state;

        /// <summary>创建跨平台固定序列；零种子映射到固定非零状态。</summary>
        /// <param name="seed">本局固定种子；相同种子和调用顺序产生相同结果。</param>
        public CasinoRandom(uint seed)
        {
            state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        /// 产生下一个 32 位随机值并推进状态。
        public uint NextUInt()
        {
            unchecked
            {
                uint value = state == 0 ? 0x6D2B79F5u : state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }
        }

        /// <summary>按拒绝采样生成整数，避免简单取模产生的区间偏差。</summary>
        /// <param name="exclusiveMaximum">不包含的正整数上界；非正数抛出异常。</param>
        /// <returns>0 到 exclusiveMaximum - 1 的整数。</returns>
        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            uint bound = (uint)exclusiveMaximum;
            // xorshift32 的非零状态循环排除 0，故在 1..uint.MaxValue 上取样。
            uint limit = uint.MaxValue - uint.MaxValue % bound;
            uint value;
            do value = NextUInt(); while (value > limit);
            return (int)((value - 1) % bound);
        }
    }
}
