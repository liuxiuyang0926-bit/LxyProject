using System;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Events
{
    public interface IBattleEvent
    {
        long SequenceId { get; }
        BattleEventType Type { get; }
        BattleEventPhase Phase { get; }
        UnitId Source { get; }
        UnitId Target { get; }
        long Value { get; }
    }

    public abstract class BattleEventBase : IBattleEvent
    {
        protected BattleEventBase(
            BattleEventType type,
            BattleEventPhase phase,
            UnitId source,
            UnitId target,
            long value)
        {
            Type = type;
            Phase = phase;
            Source = source;
            Target = target;
            Value = value;
        }

        public long SequenceId { get; private set; }
        public BattleEventType Type { get; }
        public BattleEventPhase Phase { get; }
        public UnitId Source { get; }
        public UnitId Target { get; }
        public long Value { get; }

        internal void AssignSequence(long sequenceId)
        {
            if (SequenceId != 0 || sequenceId <= 0)
            {
                throw new InvalidOperationException("战斗事件序号只能分配一次且必须大于零。");
            }

            SequenceId = sequenceId;
        }
    }

    public sealed class BattleStartedEvent : BattleEventBase
    {
        public BattleStartedEvent()
            : base(BattleEventType.BattleStarted, BattleEventPhase.Main, UnitId.None, UnitId.None, 0)
        {
        }
    }

    public sealed class RoundStartedEvent : BattleEventBase
    {
        public RoundStartedEvent(int round)
            : base(BattleEventType.RoundStarted, BattleEventPhase.Main, UnitId.None, UnitId.None, round)
        {
            Round = round;
        }

        public int Round { get; }
    }

    public sealed class TurnStartedEvent : BattleEventBase
    {
        public TurnStartedEvent(UnitId actor, int round)
            : base(BattleEventType.TurnStarted, BattleEventPhase.Main, actor, actor, round)
        {
            Actor = actor;
            Round = round;
        }

        public UnitId Actor { get; }
        public int Round { get; }
    }

    public sealed class SkillCastEvent : BattleEventBase
    {
        public SkillCastEvent(UnitId caster, SkillId skill, UnitId[] selectedTargets)
            : base(
                BattleEventType.SkillCast,
                BattleEventPhase.Main,
                caster,
                selectedTargets != null && selectedTargets.Length > 0
                    ? selectedTargets[0]
                    : UnitId.None,
                0)
        {
            Caster = caster;
            Skill = skill;
            SelectedTargets = selectedTargets == null
                ? Array.Empty<UnitId>()
                : (UnitId[])selectedTargets.Clone();
        }

        public UnitId Caster { get; }
        public SkillId Skill { get; }
        public UnitId[] SelectedTargets { get; }
    }

    public sealed class DamageResolvedEvent : BattleEventBase
    {
        public DamageResolvedEvent(
            UnitId attacker,
            UnitId target,
            long damage,
            bool critical,
            bool miss,
            bool targetDead)
            : base(BattleEventType.DamageResolved, BattleEventPhase.After, attacker, target, damage)
        {
            Damage = damage;
            Critical = critical;
            Miss = miss;
            TargetDead = targetDead;
        }

        public long Damage { get; }
        public bool Critical { get; }
        public bool Miss { get; }
        public bool TargetDead { get; }
    }

    public sealed class HpChangedEvent : BattleEventBase
    {
        public HpChangedEvent(UnitId source, UnitId target, long previousHp, long currentHp)
            : base(BattleEventType.HpChanged, BattleEventPhase.After, source, target, currentHp - previousHp)
        {
            PreviousHp = previousHp;
            CurrentHp = currentHp;
        }

        public long PreviousHp { get; }
        public long CurrentHp { get; }
    }

    public sealed class HealResolvedEvent : BattleEventBase
    {
        public HealResolvedEvent(UnitId source, UnitId target, long healed)
            : base(BattleEventType.HealResolved, BattleEventPhase.After, source, target, healed)
        {
            Healed = healed;
        }

        public long Healed { get; }
    }

    public sealed class BuffChangedEvent : BattleEventBase
    {
        public BuffChangedEvent(
            bool added,
            UnitId source,
            UnitId target,
            BuffId buff,
            long instanceId,
            int stack)
            : base(
                added ? BattleEventType.BuffAdded : BattleEventType.BuffRemoved,
                BattleEventPhase.After,
                source,
                target,
                stack)
        {
            Added = added;
            Buff = buff;
            InstanceId = instanceId;
            Stack = stack;
        }

        public bool Added { get; }
        public BuffId Buff { get; }
        public long InstanceId { get; }
        public int Stack { get; }
    }

    public sealed class UnitLifeEvent : BattleEventBase
    {
        public UnitLifeEvent(bool revived, UnitId source, UnitId target)
            : base(
                revived ? BattleEventType.UnitRevived : BattleEventType.UnitDead,
                BattleEventPhase.After,
                source,
                target,
                0)
        {
            Revived = revived;
        }

        public bool Revived { get; }
    }

    public sealed class UnitSummonedEvent : BattleEventBase
    {
        public UnitSummonedEvent(UnitId source, UnitId summoned)
            : base(BattleEventType.UnitSummoned, BattleEventPhase.After, source, summoned, 0)
        {
        }
    }

    public sealed class TurnEndedEvent : BattleEventBase
    {
        public TurnEndedEvent(UnitId actor, int round)
            : base(BattleEventType.TurnEnded, BattleEventPhase.After, actor, actor, round)
        {
            Actor = actor;
            Round = round;
        }

        public UnitId Actor { get; }
        public int Round { get; }
    }

    public sealed class BattleEndedEvent : BattleEventBase
    {
        public BattleEndedEvent(BattleResult result)
            : base(BattleEventType.BattleEnded, BattleEventPhase.Main, UnitId.None, UnitId.None, (long)result)
        {
            Result = result;
        }

        public BattleResult Result { get; }
    }
}
