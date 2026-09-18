using System;

namespace Game.Battle.TurnBased.Core.Random
{
    public interface IBattleRandom
    {
        uint State { get; }
        long CallCount { get; }
        uint NextUInt();
        int NextInt(int minInclusive, int maxExclusive);
        int RollBasisPoint();
    }

    public sealed class DeterministicBattleRandom : IBattleRandom
    {
        private readonly Game.Battle.Core.BattleRandom random;

        public DeterministicBattleRandom(int seed)
        {
            random = new Game.Battle.Core.BattleRandom(seed);
        }

        public uint State => random.State;
        public long CallCount { get; private set; }

        public uint NextUInt()
        {
            CallCount++;
            return random.Next();
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            CallCount++;
            return random.Range(minInclusive, maxExclusive);
        }

        public int RollBasisPoint() => NextInt(0, 10000);
    }
}
