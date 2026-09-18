using System;

namespace Game.Battle.Core
{
    public sealed class BattleRandom
    {
        private uint state;

        /// <summary>
        /// 创建战斗随机数实例。
        /// </summary>
        public BattleRandom(int seed)
        {
            state = unchecked((uint)seed);
            if (state == 0)
            {
                state = 1;
            }
        }

        /// <summary>
        /// 向调用方提供状态。
        /// </summary>
        public uint State => state;

        /// <summary>
        /// 执行Next相关逻辑。
        /// </summary>
        public uint Next()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        /// <summary>
        /// 执行Range相关逻辑。
        /// </summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive));
            }

            return minInclusive +
                   (int)(Next() %
                         (uint)(maxExclusive - minInclusive));
        }
    }
}
