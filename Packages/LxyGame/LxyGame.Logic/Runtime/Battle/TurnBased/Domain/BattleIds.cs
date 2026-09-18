using System;

namespace Game.Battle.TurnBased.Domain
{
    public readonly struct UnitId : IEquatable<UnitId>, IComparable<UnitId>
    {
        public static readonly UnitId None = default;

        public UnitId(int value) => Value = value;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(UnitId other) => Value.CompareTo(other.Value);
        public bool Equals(UnitId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is UnitId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(UnitId left, UnitId right) => left.Equals(right);
        public static bool operator !=(UnitId left, UnitId right) => !left.Equals(right);
    }

    public readonly struct SkillId : IEquatable<SkillId>, IComparable<SkillId>
    {
        public static readonly SkillId None = default;

        public SkillId(int value) => Value = value;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(SkillId other) => Value.CompareTo(other.Value);
        public bool Equals(SkillId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(SkillId left, SkillId right) => left.Equals(right);
        public static bool operator !=(SkillId left, SkillId right) => !left.Equals(right);
    }

    public readonly struct BuffId : IEquatable<BuffId>, IComparable<BuffId>
    {
        public static readonly BuffId None = default;

        public BuffId(int value) => Value = value;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(BuffId other) => Value.CompareTo(other.Value);
        public bool Equals(BuffId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BuffId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(BuffId left, BuffId right) => left.Equals(right);
        public static bool operator !=(BuffId left, BuffId right) => !left.Equals(right);
    }

    public readonly struct RuleId : IEquatable<RuleId>, IComparable<RuleId>
    {
        public static readonly RuleId None = default;

        public RuleId(int value) => Value = value;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(RuleId other) => Value.CompareTo(other.Value);
        public bool Equals(RuleId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RuleId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(RuleId left, RuleId right) => left.Equals(right);
        public static bool operator !=(RuleId left, RuleId right) => !left.Equals(right);
    }

    public readonly struct BattleVariableKey : IEquatable<BattleVariableKey>, IComparable<BattleVariableKey>
    {
        public BattleVariableKey(int value) => Value = value;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(BattleVariableKey other) => Value.CompareTo(other.Value);
        public bool Equals(BattleVariableKey other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BattleVariableKey other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(BattleVariableKey left, BattleVariableKey right) => left.Equals(right);
        public static bool operator !=(BattleVariableKey left, BattleVariableKey right) => !left.Equals(right);
    }

    public enum BattleCamp
    {
        Neutral = 0,
        Attacker = 1,
        Defender = 2,
    }

    public enum AttributeType
    {
        Hp = 0,
        MaxHp = 1,
        Attack = 2,
        Defense = 3,
        Speed = 4,
        CritRate = 5,
        CritDamage = 6,
        HitRate = 7,
        DodgeRate = 8,
        Anger = 9,
        Energy = 10,
        Shield = 11,
        Count = 12,
    }

    public enum BattlePhase
    {
        Created = 0,
        BattleBegin = 1,
        RoundBegin = 2,
        TurnBegin = 3,
        WaitingCommand = 4,
        Resolving = 5,
        TurnEnd = 6,
        BattleEnd = 7,
        Aborted = 8,
        Disposed = 9,
    }

    public enum BattleResult
    {
        None = 0,
        AttackerVictory = 1,
        DefenderVictory = 2,
        Draw = 3,
        Exited = 4,
        Aborted = 5,
    }

    public enum BattleAbortReason
    {
        None = 0,
        InvalidCommand = 1,
        RuleExecutionLimit = 2,
        EventLimit = 3,
        ActionLimit = 4,
        InvalidRuntimeData = 5,
        DeterminismViolation = 6,
        InternalError = 7,
        Disposed = 8,
    }
}
