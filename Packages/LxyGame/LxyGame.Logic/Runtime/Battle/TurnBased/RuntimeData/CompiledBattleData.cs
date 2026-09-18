using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.RuntimeData
{
    public static class BattleNumeric
    {
        public const int BasisPointOne = 10000;
        public const int DefaultCritDamage = 15000;
    }

    public enum BattleEventType
    {
        None = 0,
        BattleStarted = 1,
        RoundStarted = 2,
        TurnStarted = 3,
        SkillCast = 4,
        BeforeDamage = 5,
        DamageResolved = 6,
        HpChanged = 7,
        HealResolved = 8,
        BuffAdded = 9,
        BuffRemoved = 10,
        UnitSummoned = 11,
        UnitDead = 12,
        UnitRevived = 13,
        TurnEnded = 14,
        BattleEnded = 15,
    }

    public enum BattleEventPhase
    {
        Before = 0,
        Main = 1,
        After = 2,
    }

    public enum TargetSelectorType
    {
        Self = 0,
        Attacker = 1,
        EventTarget = 2,
        SelectedTargets = 3,
        AllAllies = 4,
        AllEnemies = 5,
        RandomEnemy = 6,
        LowestHp = 7,
        HighestAttack = 8,
        FrontRowEnemy = 9,
    }

    public readonly struct CompiledTargetSelector
    {
        public CompiledTargetSelector(
            TargetSelectorType type,
            int count = 1,
            bool includeDead = false,
            int configIdFilter = 0)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Type = type;
            Count = count;
            IncludeDead = includeDead;
            ConfigIdFilter = configIdFilter;
        }

        public TargetSelectorType Type { get; }
        public int Count { get; }
        public bool IncludeDead { get; }
        public int ConfigIdFilter { get; }
    }

    public enum ValueSourceType
    {
        Constant = 0,
        OwnerAttribute = 1,
        AttackerAttribute = 2,
        TargetAttribute = 3,
        CurrentHp = 4,
        MaxHp = 5,
        LostHp = 6,
        HpPercent = 7,
        BuffStack = 8,
        EventValue = 9,
        CurrentRound = 10,
        BattleCounter = 11,
        LogicParameter = 12,
        InvocationVariable = 13,
        SkillVariable = 14,
        RandomBasisPoint = 15,
    }

    public enum BattleLogicDataKind
    {
        Parameter = 0,
        RuntimeVariable = 1,
    }

    public enum BattleLogicDataValueType
    {
        Integer = 0,
        BasisPoint = 1,
        Boolean = 2,
        Identifier = 3,
    }

    public enum BattleLogicVariableScope
    {
        Invocation = 0,
        Skill = 1,
    }

    public readonly struct CompiledBattleLogicData
    {
        public CompiledBattleLogicData(
            BattleVariableKey key,
            string name,
            BattleLogicDataKind kind,
            BattleLogicDataValueType valueType,
            BattleLogicVariableScope scope,
            long defaultValue)
        {
            if (!key.IsValid || string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("战斗逻辑数据定义无效。");
            }

            Key = key;
            Name = name;
            Kind = kind;
            ValueType = valueType;
            Scope = scope;
            DefaultValue = defaultValue;
        }

        public BattleVariableKey Key { get; }
        public string Name { get; }
        public BattleLogicDataKind Kind { get; }
        public BattleLogicDataValueType ValueType { get; }
        public BattleLogicVariableScope Scope { get; }
        public long DefaultValue { get; }
    }

    public readonly struct CompiledValue
    {
        public CompiledValue(
            ValueSourceType source,
            long constant = 0,
            AttributeType attribute = AttributeType.Hp,
            int referenceId = 0,
            int scaleBasisPoint = BattleNumeric.BasisPointOne,
            long offset = 0,
            bool hasMinimum = false,
            long minimum = 0,
            bool hasMaximum = false,
            long maximum = 0,
            bool useDynamicScale = false,
            ValueSourceType dynamicScaleSource = ValueSourceType.Constant,
            long dynamicScaleConstant = BattleNumeric.BasisPointOne,
            AttributeType dynamicScaleAttribute = AttributeType.Hp,
            int dynamicScaleReferenceId = 0)
        {
            if (scaleBasisPoint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scaleBasisPoint));
            }

            if (hasMinimum && hasMaximum && minimum > maximum)
            {
                throw new ArgumentException("数值下限不能大于上限。");
            }

            Source = source;
            Constant = constant;
            Attribute = attribute;
            ReferenceId = referenceId;
            ScaleBasisPoint = scaleBasisPoint;
            Offset = offset;
            HasMinimum = hasMinimum;
            Minimum = minimum;
            HasMaximum = hasMaximum;
            Maximum = maximum;
            UseDynamicScale = useDynamicScale;
            DynamicScaleSource = dynamicScaleSource;
            DynamicScaleConstant = dynamicScaleConstant;
            DynamicScaleAttribute = dynamicScaleAttribute;
            DynamicScaleReferenceId = dynamicScaleReferenceId;
        }

        public ValueSourceType Source { get; }
        public long Constant { get; }
        public AttributeType Attribute { get; }
        public int ReferenceId { get; }
        public int ScaleBasisPoint { get; }
        public long Offset { get; }
        public bool HasMinimum { get; }
        public long Minimum { get; }
        public bool HasMaximum { get; }
        public long Maximum { get; }
        public bool UseDynamicScale { get; }
        public ValueSourceType DynamicScaleSource { get; }
        public long DynamicScaleConstant { get; }
        public AttributeType DynamicScaleAttribute { get; }
        public int DynamicScaleReferenceId { get; }
    }

    public enum ComparisonOperator
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5,
    }

    public enum CompiledConditionType
    {
        Always = 0,
        IsSelf = 1,
        IsAlive = 2,
        IsDead = 3,
        IsEnemy = 4,
        IsAlly = 5,
        ConfigIdCompare = 6,
        HasBuff = 7,
        AttributeCompare = 8,
        HpPercentCompare = 9,
        EventValueCompare = 10,
        CounterCompare = 11,
        RoundCompare = 12,
        TargetCountCompare = 13,
        CounterCompareCurrentRound = 14,
        LogicValueCompare = 15,
    }

    public readonly struct CompiledCondition
    {
        public CompiledCondition(
            CompiledConditionType type,
            CompiledTargetSelector target,
            ComparisonOperator comparison = ComparisonOperator.Equal,
            long value = 0,
            AttributeType attribute = AttributeType.Hp,
            int referenceId = 0,
            bool negate = false,
            CompiledValue leftValue = default,
            CompiledValue rightValue = default)
        {
            Type = type;
            Target = target;
            Comparison = comparison;
            Value = value;
            Attribute = attribute;
            ReferenceId = referenceId;
            Negate = negate;
            LeftValue = leftValue;
            RightValue = rightValue;
        }

        public CompiledConditionType Type { get; }
        public CompiledTargetSelector Target { get; }
        public ComparisonOperator Comparison { get; }
        public long Value { get; }
        public AttributeType Attribute { get; }
        public int ReferenceId { get; }
        public bool Negate { get; }
        public CompiledValue LeftValue { get; }
        public CompiledValue RightValue { get; }
    }

    [Flags]
    public enum CompiledActionFlags
    {
        None = 0,
        CanMiss = 1 << 0,
        CanCritical = 1 << 1,
        IgnoreDefense = 1 << 2,
    }

    public enum CompiledActionType
    {
        Damage = 0,
        Heal = 1,
        AddBuff = 2,
        RemoveBuff = 3,
        ModifyAttribute = 4,
        ModifyResource = 5,
        Kill = 6,
        Revive = 7,
        MovePosition = 8,
        AddCounter = 9,
        SetVariable = 10,
        EndTurn = 11,
        SetInvocationVariable = 12,
        AddInvocationVariable = 13,
        SetSkillVariable = 14,
        AddSkillVariable = 15,
    }

    public readonly struct CompiledAction
    {
        public CompiledAction(
            CompiledActionType type,
            CompiledTargetSelector target,
            CompiledValue value = default,
            AttributeType attribute = AttributeType.Hp,
            int referenceId = 0,
            CompiledActionFlags flags = CompiledActionFlags.None,
            bool useReferenceValue = false,
            CompiledValue referenceValue = default)
        {
            Type = type;
            Target = target;
            Value = value;
            Attribute = attribute;
            ReferenceId = referenceId;
            Flags = flags;
            UseReferenceValue = useReferenceValue;
            ReferenceValue = referenceValue;
        }

        public CompiledActionType Type { get; }
        public CompiledTargetSelector Target { get; }
        public CompiledValue Value { get; }
        public AttributeType Attribute { get; }
        public int ReferenceId { get; }
        public CompiledActionFlags Flags { get; }
        public bool UseReferenceValue { get; }
        public CompiledValue ReferenceValue { get; }
    }

    public sealed class CompiledRule
    {
        public CompiledRule(
            RuleId id,
            BattleEventType trigger,
            BattleEventPhase phase,
            int priority,
            CompiledCondition[] conditions,
            CompiledAction[] actions,
            bool ownerMustBeAlive = true)
        {
            if (!id.IsValid || trigger == BattleEventType.None)
            {
                throw new ArgumentException("规则 ID 或触发事件无效。");
            }

            if (actions == null || actions.Length == 0)
            {
                throw new ArgumentException("规则至少需要一个 Action。", nameof(actions));
            }

            Id = id;
            Trigger = trigger;
            Phase = phase;
            Priority = priority;
            Conditions = conditions == null
                ? Array.Empty<CompiledCondition>()
                : (CompiledCondition[])conditions.Clone();
            Actions = (CompiledAction[])actions.Clone();
            OwnerMustBeAlive = ownerMustBeAlive;
        }

        public RuleId Id { get; }
        public BattleEventType Trigger { get; }
        public BattleEventPhase Phase { get; }
        public int Priority { get; }
        public CompiledCondition[] Conditions { get; }
        public CompiledAction[] Actions { get; }
        public bool OwnerMustBeAlive { get; }
    }

    public sealed class CompiledSkill
    {
        public CompiledSkill(
            SkillId id,
            AttributeType costAttribute,
            long cost,
            RuleId[] ruleIds,
            string expressionKey,
            string logicId = "",
            CompiledBattleLogicData[] logicData = null)
        {
            if (!id.IsValid || cost < 0)
            {
                throw new ArgumentException("技能运行时数据无效。");
            }

            Id = id;
            CostAttribute = costAttribute;
            Cost = cost;
            RuleIds = ruleIds == null ? Array.Empty<RuleId>() : (RuleId[])ruleIds.Clone();
            ExpressionKey = expressionKey ?? string.Empty;
            LogicId = logicId ?? string.Empty;
            LogicData = logicData == null
                ? Array.Empty<CompiledBattleLogicData>()
                : (CompiledBattleLogicData[])logicData.Clone();
            Array.Sort(LogicData, CompareLogicData);
            if (LogicData.Length > 0 && string.IsNullOrWhiteSpace(LogicId))
            {
                throw new ArgumentException("包含逻辑数据的技能必须提供 LogicId。", nameof(logicId));
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < LogicData.Length; index++)
            {
                CompiledBattleLogicData item = LogicData[index];
                if (index > 0 && LogicData[index - 1].Key == item.Key)
                {
                    throw new ArgumentException(
                        $"技能 {id} 的逻辑数据 Key 重复：{item.Key}。",
                        nameof(logicData));
                }
                if (!names.Add(item.Name))
                {
                    throw new ArgumentException(
                        $"技能 {id} 的逻辑数据名称重复：{item.Name}。",
                        nameof(logicData));
                }
                if (item.ValueType == BattleLogicDataValueType.Boolean &&
                    item.DefaultValue != 0 && item.DefaultValue != 1)
                {
                    throw new ArgumentException(
                        $"技能 {id} 的布尔数据 {item.Name} 默认值只能是 0 或 1。",
                        nameof(logicData));
                }
            }
        }

        public SkillId Id { get; }
        public AttributeType CostAttribute { get; }
        public long Cost { get; }
        public RuleId[] RuleIds { get; }
        public string ExpressionKey { get; }
        public string LogicId { get; }
        public CompiledBattleLogicData[] LogicData { get; }

        public bool TryGetLogicData(
            BattleVariableKey key,
            out CompiledBattleLogicData definition)
        {
            int low = 0;
            int high = LogicData.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int comparison = LogicData[middle].Key.CompareTo(key);
                if (comparison == 0)
                {
                    definition = LogicData[middle];
                    return true;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            definition = default;
            return false;
        }

        private static int CompareLogicData(
            CompiledBattleLogicData left,
            CompiledBattleLogicData right) => left.Key.CompareTo(right.Key);
    }

    public enum BuffStackRule
    {
        RefreshDuration = 0,
        AddStack = 1,
        Replace = 2,
        Independent = 3,
    }

    public sealed class CompiledBuff
    {
        public CompiledBuff(
            BuffId id,
            int durationRounds,
            int maxStack,
            BuffStackRule stackRule,
            RuleId[] ruleIds,
            int tags = 0)
        {
            if (!id.IsValid || durationRounds <= 0 || maxStack <= 0)
            {
                throw new ArgumentException("Buff 运行时数据无效。");
            }

            Id = id;
            DurationRounds = durationRounds;
            MaxStack = maxStack;
            StackRule = stackRule;
            RuleIds = ruleIds == null ? Array.Empty<RuleId>() : (RuleId[])ruleIds.Clone();
            Tags = tags;
        }

        public BuffId Id { get; }
        public int DurationRounds { get; }
        public int MaxStack { get; }
        public BuffStackRule StackRule { get; }
        public RuleId[] RuleIds { get; }
        public int Tags { get; }
    }
}
