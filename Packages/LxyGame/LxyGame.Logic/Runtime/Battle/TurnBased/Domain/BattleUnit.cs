using System;
using System.Collections.Generic;

namespace Game.Battle.TurnBased.Domain
{
    public sealed class AttributeSet
    {
        private readonly long[] values = new long[(int)AttributeType.Count];

        public long Get(AttributeType type)
        {
            ValidateType(type);
            return values[(int)type];
        }

        internal void Set(AttributeType type, long value)
        {
            ValidateType(type);
            values[(int)type] = value;
        }

        internal long Add(AttributeType type, long delta)
        {
            long value = checked(Get(type) + delta);
            Set(type, value);
            return value;
        }

        public long[] CreateSnapshot()
        {
            var snapshot = new long[values.Length];
            Array.Copy(values, snapshot, values.Length);
            return snapshot;
        }

        private static void ValidateType(AttributeType type)
        {
            if (type < 0 || type >= AttributeType.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }

    public sealed class SkillInstance
    {
        public SkillInstance(SkillId skillId, UnitId owner)
        {
            if (!skillId.IsValid || !owner.IsValid)
            {
                throw new ArgumentException("技能实例参数无效。");
            }

            SkillId = skillId;
            Owner = owner;
        }

        public SkillId SkillId { get; }
        public UnitId Owner { get; }
        public long RuntimeInstanceId { get; internal set; }
        public int TriggerCount { get; internal set; }
        public int Cooldown { get; internal set; }
        public bool Disabled { get; internal set; }
    }

    public sealed class SkillContainer
    {
        private readonly List<SkillInstance> skills = new List<SkillInstance>();

        public IReadOnlyList<SkillInstance> Items => skills;

        public void Add(SkillInstance skill)
        {
            if (skill == null)
            {
                throw new ArgumentNullException(nameof(skill));
            }

            if (TryGet(skill.SkillId, out _))
            {
                throw new InvalidOperationException($"技能重复：{skill.SkillId}");
            }

            skills.Add(skill);
            skills.Sort((left, right) => left.SkillId.CompareTo(right.SkillId));
        }

        public bool TryGet(SkillId id, out SkillInstance skill)
        {
            for (int index = 0; index < skills.Count; index++)
            {
                if (skills[index].SkillId == id)
                {
                    skill = skills[index];
                    return true;
                }
            }

            skill = null;
            return false;
        }
    }

    public sealed class BuffInstance
    {
        public long InstanceId { get; internal set; }
        public BuffId BuffId { get; internal set; }
        public UnitId Owner { get; internal set; }
        public UnitId Source { get; internal set; }
        public int Stack { get; internal set; }
        public int RemainingRounds { get; internal set; }
    }

    public sealed class BuffContainer
    {
        private readonly List<BuffInstance> buffs = new List<BuffInstance>();

        public IReadOnlyList<BuffInstance> Items => buffs;

        public bool Has(BuffId id)
        {
            for (int index = 0; index < buffs.Count; index++)
            {
                if (buffs[index].BuffId == id)
                {
                    return true;
                }
            }

            return false;
        }

        public BuffInstance Find(BuffId id)
        {
            for (int index = 0; index < buffs.Count; index++)
            {
                if (buffs[index].BuffId == id)
                {
                    return buffs[index];
                }
            }

            return null;
        }

        internal void Add(BuffInstance instance)
        {
            buffs.Add(instance ?? throw new ArgumentNullException(nameof(instance)));
            buffs.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
        }

        internal bool Remove(long instanceId, out BuffInstance removed)
        {
            for (int index = 0; index < buffs.Count; index++)
            {
                if (buffs[index].InstanceId != instanceId)
                {
                    continue;
                }

                removed = buffs[index];
                buffs.RemoveAt(index);
                return true;
            }

            removed = null;
            return false;
        }
    }

    public sealed class BattleUnit
    {
        public BattleUnit(UnitId id, int configId, BattleCamp camp, int position)
        {
            if (!id.IsValid || configId <= 0 || camp == BattleCamp.Neutral)
            {
                throw new ArgumentException("战斗单位初始参数无效。");
            }

            Id = id;
            ConfigId = configId;
            Camp = camp;
            Position = position;
            Attributes = new AttributeSet();
            Buffs = new BuffContainer();
            Skills = new SkillContainer();
        }

        public UnitId Id { get; }
        public int ConfigId { get; }
        public BattleCamp Camp { get; }
        public AttributeSet Attributes { get; }
        public BuffContainer Buffs { get; }
        public SkillContainer Skills { get; }
        public bool IsDead { get; internal set; }
        public int Position { get; internal set; }

        public void InitializeAttribute(AttributeType type, long value)
        {
            if (type == AttributeType.Hp || type == AttributeType.MaxHp)
            {
                throw new InvalidOperationException("生命值请使用 InitializeHealth 初始化。");
            }

            Attributes.Set(type, value);
        }

        public void InitializeHealth(long maximumHp, long currentHp = -1)
        {
            if (maximumHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHp));
            }

            long hp = currentHp < 0 ? maximumHp : currentHp;
            if (hp < 0 || hp > maximumHp)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHp));
            }

            Attributes.Set(AttributeType.MaxHp, maximumHp);
            Attributes.Set(AttributeType.Hp, hp);
            IsDead = hp == 0;
        }
    }
}
