using System;

namespace Game.Battle.Core
{
    public sealed class BattleRandom
    {
        private uint state;

        public BattleRandom(int seed)
        {
            state = unchecked((uint)seed);
            if (state == 0)
            {
                state = 1;
            }
        }

        public uint State => state;

        public uint Next()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

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
