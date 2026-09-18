using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Trace
{
    public enum BattleTraceLevel
    {
        Off = 0,
        Summary = 1,
        Normal = 2,
        Verbose = 3,
        DebugDeterminism = 4,
    }

    public enum BattleTraceKind
    {
        Command = 0,
        Action = 1,
        Event = 2,
        Rule = 3,
        Random = 4,
        State = 5,
        Abort = 6,
    }

    public readonly struct BattleTraceEntry
    {
        public BattleTraceEntry(
            long step,
            long sequence,
            BattleTraceKind kind,
            BattlePhase phase,
            UnitId source,
            UnitId target,
            int typeId,
            long value,
            ulong worldHash)
        {
            Step = step;
            Sequence = sequence;
            Kind = kind;
            Phase = phase;
            Source = source;
            Target = target;
            TypeId = typeId;
            Value = value;
            WorldHash = worldHash;
        }

        public long Step { get; }
        public long Sequence { get; }
        public BattleTraceKind Kind { get; }
        public BattlePhase Phase { get; }
        public UnitId Source { get; }
        public UnitId Target { get; }
        public int TypeId { get; }
        public long Value { get; }
        public ulong WorldHash { get; }
    }

    public sealed class BattleTrace
    {
        private readonly List<BattleTraceEntry> entries;
        private readonly int capacity;

        public BattleTrace(BattleTraceLevel level, int capacity = 1024)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Level = level;
            this.capacity = capacity;
            entries = new List<BattleTraceEntry>(capacity);
        }

        public BattleTraceLevel Level { get; }
        public IReadOnlyList<BattleTraceEntry> Entries => entries;

        internal void Add(BattleTraceEntry entry, BattleTraceLevel requiredLevel)
        {
            if (Level < requiredLevel || Level == BattleTraceLevel.Off)
            {
                return;
            }

            if (entries.Count == capacity)
            {
                entries.RemoveAt(0);
            }

            entries.Add(entry);
        }

        internal void Clear() => entries.Clear();
    }
}
