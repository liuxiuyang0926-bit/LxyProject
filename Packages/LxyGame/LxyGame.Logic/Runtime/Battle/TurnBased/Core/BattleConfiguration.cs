using System;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.Core
{
    public readonly struct BattleConfigSnapshot
    {
        public BattleConfigSnapshot(
            string logicVersion,
            string runtimeDataVersion,
            ulong configHash,
            int seed)
        {
            LogicVersion = logicVersion ?? throw new ArgumentNullException(nameof(logicVersion));
            RuntimeDataVersion = runtimeDataVersion ?? throw new ArgumentNullException(nameof(runtimeDataVersion));
            ConfigHash = configHash;
            Seed = seed;
        }

        public string LogicVersion { get; }
        public string RuntimeDataVersion { get; }
        public ulong ConfigHash { get; }
        public int Seed { get; }
    }

    public readonly struct BattleExecutionLimits
    {
        public BattleExecutionLimits(
            int maxEventsPerStep = 1024,
            int maxActionsPerStep = 1024,
            int maxRuleExecutionsPerStep = 1024)
        {
            if (maxEventsPerStep <= 0 || maxActionsPerStep <= 0 || maxRuleExecutionsPerStep <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEventsPerStep));
            }

            MaxEventsPerStep = maxEventsPerStep;
            MaxActionsPerStep = maxActionsPerStep;
            MaxRuleExecutionsPerStep = maxRuleExecutionsPerStep;
        }

        public int MaxEventsPerStep { get; }
        public int MaxActionsPerStep { get; }
        public int MaxRuleExecutionsPerStep { get; }

        public static BattleExecutionLimits Default =>
            new BattleExecutionLimits(1024, 1024, 1024);
    }

    internal sealed class BattleExecutionException : Exception
    {
        public BattleExecutionException(BattleAbortReason reason, string message)
            : base(message)
        {
            Reason = reason;
        }

        public BattleAbortReason Reason { get; }
    }
}
