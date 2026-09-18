using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;
using Game.Battle.TurnBased.Core.Actions;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Services;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Rules
{
    public readonly struct RuleContext
    {
        public RuleContext(
            BattleContext battle,
            IBattleEvent battleEvent,
            UnitId owner,
            UnitId attacker,
            UnitId target)
            : this(
                battle,
                battleEvent,
                owner,
                attacker,
                target,
                SkillId.None,
                0,
                null)
        {
        }

        internal RuleContext(
            BattleContext battle,
            IBattleEvent battleEvent,
            UnitId owner,
            UnitId attacker,
            UnitId target,
            SkillId skill,
            long sourceInstanceId,
            BattleInvocationVariableBuffer invocationVariables)
        {
            Battle = battle;
            Event = battleEvent;
            Owner = owner;
            Attacker = attacker;
            Target = target;
            Skill = skill;
            SourceInstanceId = sourceInstanceId;
            InvocationVariables = invocationVariables;
        }

        public BattleContext Battle { get; }
        public IBattleEvent Event { get; }
        public UnitId Owner { get; }
        public UnitId Attacker { get; }
        public UnitId Target { get; }
        public SkillId Skill { get; }
        public long SourceInstanceId { get; }
        internal BattleInvocationVariableBuffer InvocationVariables { get; }

        public RuleContext WithTarget(UnitId target) =>
            new RuleContext(
                Battle,
                Event,
                Owner,
                Attacker,
                target,
                Skill,
                SourceInstanceId,
                InvocationVariables);
    }

    internal enum RuleSourceType
    {
        Skill = 0,
        Buff = 1,
    }

    internal sealed class RuleBinding
    {
        public CompiledRule Rule;
        public UnitId Owner;
        public RuleSourceType SourceType;
        public SkillId Skill;
        public BuffId Buff;
        public long SourceInstanceId;
        public long RegistrationSequence;
    }

    public sealed class RuleEngine
    {
        private readonly Dictionary<BattleEventType, List<RuleBinding>> registry =
            new Dictionary<BattleEventType, List<RuleBinding>>();
        private readonly List<UnitId> targetBuffer = new List<UnitId>(16);
        private readonly List<UnitId> conditionTargetBuffer = new List<UnitId>(16);
        private readonly BattleInvocationVariableBuffer invocationVariables =
            new BattleInvocationVariableBuffer();

        internal void RegisterInitialRules(BattleContext context)
        {
            IReadOnlyList<UnitId> ids = context.World.UnitIds;
            for (int index = 0; index < ids.Count; index++)
            {
                RegisterUnitSkills(context, context.World.GetUnit(ids[index]));
            }
        }

        internal void Clear()
        {
            registry.Clear();
            targetBuffer.Clear();
            conditionTargetBuffer.Clear();
            invocationVariables.Clear();
        }

        public void RegisterUnitSkills(BattleContext context, BattleUnit unit)
        {
            IReadOnlyList<SkillInstance> skills = unit.Skills.Items;
            for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
            {
                SkillInstance instance = skills[skillIndex];
                if (!context.Database.TryGetSkill(instance.SkillId, out CompiledSkill skill))
                {
                    throw new BattleExecutionException(
                        BattleAbortReason.InvalidRuntimeData,
                        $"技能数据不存在：{instance.SkillId}");
                }

                if (instance.RuntimeInstanceId <= 0)
                {
                    instance.RuntimeInstanceId = context.NextRuntimeInstanceId();
                }

                for (int ruleIndex = 0; ruleIndex < skill.RuleIds.Length; ruleIndex++)
                {
                    Register(
                        context,
                        unit.Id,
                        RuleSourceType.Skill,
                        skill.Id,
                        BuffId.None,
                        instance.RuntimeInstanceId,
                        skill.RuleIds[ruleIndex]);
                }
            }
        }

        public void RegisterBuff(
            BattleContext context,
            BuffInstance instance,
            CompiledBuff definition)
        {
            for (int index = 0; index < definition.RuleIds.Length; index++)
            {
                Register(
                    context,
                    instance.Owner,
                    RuleSourceType.Buff,
                    SkillId.None,
                    instance.BuffId,
                    instance.InstanceId,
                    definition.RuleIds[index]);
            }
        }

        public void UnregisterRuntimeInstance(long sourceInstanceId)
        {
            foreach (KeyValuePair<BattleEventType, List<RuleBinding>> pair in registry)
            {
                List<RuleBinding> rules = pair.Value;
                for (int index = rules.Count - 1; index >= 0; index--)
                {
                    if (rules[index].SourceInstanceId == sourceInstanceId &&
                        rules[index].SourceType == RuleSourceType.Buff)
                    {
                        rules.RemoveAt(index);
                    }
                }
            }
        }

        internal void Dispatch(BattleContext context, BattleEventBase battleEvent)
        {
            if (!registry.TryGetValue(battleEvent.Type, out List<RuleBinding> bindings))
            {
                return;
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                RuleBinding binding = bindings[index];
                if (!CanTrigger(context, battleEvent, binding))
                {
                    continue;
                }

                context.CountRuleExecution();
                invocationVariables.Clear();
                var ruleContext = new RuleContext(
                    context,
                    battleEvent,
                    binding.Owner,
                    battleEvent.Source,
                    battleEvent.Target,
                    binding.Skill,
                    binding.SourceInstanceId,
                    invocationVariables);
                if (!EvaluateConditions(context, ruleContext, binding.Rule.Conditions))
                {
                    continue;
                }

                context.TraceRule(binding.Rule, ruleContext);
                ExecuteActions(context, ruleContext, binding.Rule.Actions);
            }
        }

        private void Register(
            BattleContext context,
            UnitId owner,
            RuleSourceType sourceType,
            SkillId skill,
            BuffId buff,
            long sourceInstanceId,
            RuleId ruleId)
        {
            if (!context.Database.TryGetRule(ruleId, out CompiledRule rule))
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidRuntimeData,
                    $"规则数据不存在：{ruleId}");
            }

            if (!registry.TryGetValue(rule.Trigger, out List<RuleBinding> bindings))
            {
                bindings = new List<RuleBinding>();
                registry.Add(rule.Trigger, bindings);
            }

            bindings.Add(new RuleBinding
            {
                Rule = rule,
                Owner = owner,
                SourceType = sourceType,
                Skill = skill,
                Buff = buff,
                SourceInstanceId = sourceInstanceId,
                RegistrationSequence = context.NextRegistrationSequence(),
            });
            bindings.Sort(CompareBindings);
        }

        private static int CompareBindings(RuleBinding left, RuleBinding right)
        {
            int result = left.Rule.Phase.CompareTo(right.Rule.Phase);
            if (result != 0)
            {
                return result;
            }

            result = left.Rule.Priority.CompareTo(right.Rule.Priority);
            if (result != 0)
            {
                return result;
            }

            result = left.Owner.CompareTo(right.Owner);
            if (result != 0)
            {
                return result;
            }

            result = left.Rule.Id.CompareTo(right.Rule.Id);
            return result != 0
                ? result
                : left.RegistrationSequence.CompareTo(right.RegistrationSequence);
        }

        private static bool CanTrigger(
            BattleContext context,
            BattleEventBase battleEvent,
            RuleBinding binding)
        {
            if (!context.World.TryGetUnit(binding.Owner, out BattleUnit owner))
            {
                return false;
            }

            if (binding.Rule.OwnerMustBeAlive && owner.IsDead)
            {
                return false;
            }

            if (binding.SourceType == RuleSourceType.Skill &&
                battleEvent is SkillCastEvent castEvent)
            {
                return castEvent.Caster == binding.Owner &&
                    castEvent.Skill == binding.Skill;
            }

            return true;
        }

        private bool EvaluateConditions(
            BattleContext context,
            in RuleContext rule,
            CompiledCondition[] conditions)
        {
            for (int index = 0; index < conditions.Length; index++)
            {
                bool result = EvaluateCondition(context, rule, conditions[index]);
                if (conditions[index].Negate)
                {
                    result = !result;
                }

                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        private bool EvaluateCondition(
            BattleContext context,
            in RuleContext rule,
            in CompiledCondition condition)
        {
            if (condition.Type == CompiledConditionType.Always)
            {
                return true;
            }

            if (condition.Type == CompiledConditionType.CounterCompareCurrentRound)
            {
                long counter = context.World.Counters.Get(
                    new BattleVariableKey(condition.ReferenceId));
                return Compare(
                    counter,
                    context.Flow.RoundIndex,
                    condition.Comparison);
            }

            if (condition.Type == CompiledConditionType.LogicValueCompare)
            {
                return Compare(
                    ResolveValue(context, rule, condition.LeftValue),
                    ResolveValue(context, rule, condition.RightValue),
                    condition.Comparison);
            }

            context.Services.Target.Resolve(context, rule, condition.Target, conditionTargetBuffer);
            if (condition.Type == CompiledConditionType.TargetCountCompare)
            {
                return Compare(
                    conditionTargetBuffer.Count,
                    condition.Value,
                    condition.Comparison);
            }

            if (conditionTargetBuffer.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < conditionTargetBuffer.Count; index++)
            {
                BattleUnit target = context.World.GetUnit(conditionTargetBuffer[index]);
                if (!EvaluateTargetCondition(context, rule, target, condition))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateTargetCondition(
            BattleContext context,
            in RuleContext rule,
            BattleUnit target,
            in CompiledCondition condition)
        {
            switch (condition.Type)
            {
                case CompiledConditionType.IsSelf:
                    return target.Id == rule.Owner;
                case CompiledConditionType.IsAlive:
                    return !target.IsDead;
                case CompiledConditionType.IsDead:
                    return target.IsDead;
                case CompiledConditionType.IsEnemy:
                    return context.World.GetUnit(rule.Owner).Camp != target.Camp;
                case CompiledConditionType.IsAlly:
                    return context.World.GetUnit(rule.Owner).Camp == target.Camp;
                case CompiledConditionType.ConfigIdCompare:
                    return Compare(target.ConfigId, condition.Value, condition.Comparison);
                case CompiledConditionType.HasBuff:
                    return target.Buffs.Has(new BuffId(condition.ReferenceId));
                case CompiledConditionType.AttributeCompare:
                    return Compare(
                        target.Attributes.Get(condition.Attribute),
                        condition.Value,
                        condition.Comparison);
                case CompiledConditionType.HpPercentCompare:
                    long maximum = target.Attributes.Get(AttributeType.MaxHp);
                    long ratio = maximum <= 0
                        ? 0
                        : FP.FromRatio(target.Attributes.Get(AttributeType.Hp), maximum).RawValue;
                    return Compare(ratio, condition.Value, condition.Comparison);
                case CompiledConditionType.EventValueCompare:
                    return Compare(rule.Event.Value, condition.Value, condition.Comparison);
                case CompiledConditionType.CounterCompare:
                    return Compare(
                        context.World.Counters.Get(new BattleVariableKey(condition.ReferenceId)),
                        condition.Value,
                        condition.Comparison);
                case CompiledConditionType.RoundCompare:
                    return Compare(context.Flow.RoundIndex, condition.Value, condition.Comparison);
                default:
                    throw new BattleExecutionException(
                        BattleAbortReason.InvalidRuntimeData,
                        $"未知 Condition：{condition.Type}");
            }
        }

        private void ExecuteActions(
            BattleContext context,
            in RuleContext rule,
            CompiledAction[] actions)
        {
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                CompiledAction action = actions[actionIndex];
                if (IsLogicVariableAction(action.Type))
                {
                    ExecuteLogicVariableAction(context, rule, action);
                    continue;
                }

                if (action.Type == CompiledActionType.AddCounter ||
                    action.Type == CompiledActionType.SetVariable ||
                    action.Type == CompiledActionType.EndTurn)
                {
                    EnqueueAction(context, rule, action, rule.Target);
                    continue;
                }

                context.Services.Target.Resolve(context, rule, action.Target, targetBuffer);
                for (int targetIndex = 0; targetIndex < targetBuffer.Count; targetIndex++)
                {
                    EnqueueAction(context, rule, action, targetBuffer[targetIndex]);
                }
            }
        }

        private static bool IsLogicVariableAction(CompiledActionType type) =>
            type == CompiledActionType.SetInvocationVariable ||
            type == CompiledActionType.AddInvocationVariable ||
            type == CompiledActionType.SetSkillVariable ||
            type == CompiledActionType.AddSkillVariable;

        private static void ExecuteLogicVariableAction(
            BattleContext context,
            in RuleContext rule,
            in CompiledAction action)
        {
            BattleVariableKey key = new BattleVariableKey(action.ReferenceId);
            bool invocation = action.Type == CompiledActionType.SetInvocationVariable ||
                action.Type == CompiledActionType.AddInvocationVariable;
            BattleLogicVariableScope scope = invocation
                ? BattleLogicVariableScope.Invocation
                : BattleLogicVariableScope.Skill;
            CompiledBattleLogicData definition = RequireLogicData(
                context,
                rule,
                key,
                BattleLogicDataKind.RuntimeVariable,
                scope);
            long value = ResolveValue(context, rule, action.Value);

            if (invocation)
            {
                if (rule.InvocationVariables == null)
                {
                    throw InvalidRuntimeData("当前规则没有单次触发变量上下文。");
                }

                if (action.Type == CompiledActionType.AddInvocationVariable)
                {
                    rule.InvocationVariables.Add(key, value, definition.DefaultValue);
                }
                else
                {
                    rule.InvocationVariables.Set(key, value);
                }
                return;
            }

            if (action.Type == CompiledActionType.AddSkillVariable)
            {
                context.LogicVariables.AddSkill(
                    rule.SourceInstanceId,
                    key,
                    value,
                    definition.DefaultValue);
            }
            else
            {
                context.LogicVariables.SetSkill(rule.SourceInstanceId, key, value);
            }
        }

        private static void EnqueueAction(
            BattleContext context,
            in RuleContext sourceRule,
            in CompiledAction action,
            UnitId target)
        {
            RuleContext rule = sourceRule.WithTarget(target);
            long value = ResolveValue(context, rule, action.Value);
            switch (action.Type)
            {
                case CompiledActionType.Damage:
                    context.EnqueueAction(new DamageAction(new DamageRequest(
                        rule.Owner,
                        target,
                        value,
                        action.Flags)));
                    break;
                case CompiledActionType.Heal:
                    context.EnqueueAction(new HealAction(rule.Owner, target, value));
                    break;
                case CompiledActionType.AddBuff:
                    context.EnqueueAction(new AddBuffAction(
                        rule.Owner,
                        target,
                        new BuffId(ResolveReferenceId(context, rule, action))));
                    break;
                case CompiledActionType.RemoveBuff:
                    context.EnqueueAction(new RemoveBuffAction(
                        rule.Owner,
                        target,
                        new BuffId(ResolveReferenceId(context, rule, action))));
                    break;
                case CompiledActionType.ModifyAttribute:
                case CompiledActionType.ModifyResource:
                    context.EnqueueAction(new ModifyAttributeAction(
                        rule.Owner,
                        target,
                        action.Attribute,
                        value));
                    break;
                case CompiledActionType.Kill:
                    context.EnqueueAction(new KillAction(rule.Owner, target));
                    break;
                case CompiledActionType.Revive:
                    context.EnqueueAction(new ReviveAction(rule.Owner, target, value));
                    break;
                case CompiledActionType.MovePosition:
                    context.EnqueueAction(new MovePositionAction(target, checked((int)value)));
                    break;
                case CompiledActionType.AddCounter:
                    context.EnqueueAction(new ModifyCounterAction(
                        new BattleVariableKey(action.ReferenceId),
                        value,
                        true));
                    break;
                case CompiledActionType.SetVariable:
                    context.EnqueueAction(new ModifyCounterAction(
                        new BattleVariableKey(action.ReferenceId),
                        value,
                        false));
                    break;
                case CompiledActionType.EndTurn:
                    context.EnqueueAction(new EndTurnAction(context.Flow.CurrentActor));
                    break;
                default:
                    throw new BattleExecutionException(
                        BattleAbortReason.InvalidRuntimeData,
                        $"未知 Action：{action.Type}");
            }
        }

        private static long ResolveValue(
            BattleContext context,
            in RuleContext rule,
            in CompiledValue definition)
        {
            long value = ResolveRawValue(
                context,
                rule,
                definition.Source,
                definition.Constant,
                definition.Attribute,
                definition.ReferenceId);
            long scale = definition.UseDynamicScale
                ? ResolveRawValue(
                    context,
                    rule,
                    definition.DynamicScaleSource,
                    definition.DynamicScaleConstant,
                    definition.DynamicScaleAttribute,
                    definition.DynamicScaleReferenceId)
                : definition.ScaleBasisPoint;
            if (scale < 0)
            {
                throw InvalidRuntimeData("数值表达式的动态倍率不能小于 0。");
            }

            FP scaled = FP.FromLong(value) * FP.FromRaw(scale);
            value = checked(scaled.RawValue / FP.Precision + definition.Offset);
            if (definition.HasMinimum)
            {
                value = System.Math.Max(value, definition.Minimum);
            }

            if (definition.HasMaximum)
            {
                value = System.Math.Min(value, definition.Maximum);
            }

            return value;
        }

        private static long ResolveRawValue(
            BattleContext context,
            in RuleContext rule,
            ValueSourceType source,
            long constant,
            AttributeType attribute,
            int referenceId)
        {
            switch (source)
            {
                case ValueSourceType.Constant:
                    return constant;
                case ValueSourceType.OwnerAttribute:
                    return GetAttribute(context, rule.Owner, attribute);
                case ValueSourceType.AttackerAttribute:
                    return GetAttribute(context, rule.Attacker, attribute);
                case ValueSourceType.TargetAttribute:
                    return GetAttribute(context, rule.Target, attribute);
                case ValueSourceType.CurrentHp:
                    return GetAttribute(context, rule.Target, AttributeType.Hp);
                case ValueSourceType.MaxHp:
                    return GetAttribute(context, rule.Target, AttributeType.MaxHp);
                case ValueSourceType.LostHp:
                    return GetAttribute(context, rule.Target, AttributeType.MaxHp) -
                        GetAttribute(context, rule.Target, AttributeType.Hp);
                case ValueSourceType.HpPercent:
                    long maximum = GetAttribute(context, rule.Target, AttributeType.MaxHp);
                    return maximum <= 0
                        ? 0
                        : FP.FromRatio(
                            GetAttribute(context, rule.Target, AttributeType.Hp),
                            maximum).RawValue;
                case ValueSourceType.BuffStack:
                    return GetBuffStack(context, rule.Target, new BuffId(referenceId));
                case ValueSourceType.EventValue:
                    return rule.Event.Value;
                case ValueSourceType.CurrentRound:
                    return context.Flow.RoundIndex;
                case ValueSourceType.BattleCounter:
                    return context.World.Counters.Get(new BattleVariableKey(referenceId));
                case ValueSourceType.LogicParameter:
                    return RequireLogicData(
                        context,
                        rule,
                        new BattleVariableKey(referenceId),
                        BattleLogicDataKind.Parameter,
                        null).DefaultValue;
                case ValueSourceType.InvocationVariable:
                    CompiledBattleLogicData invocationDefinition = RequireLogicData(
                        context,
                        rule,
                        new BattleVariableKey(referenceId),
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicVariableScope.Invocation);
                    if (rule.InvocationVariables == null)
                    {
                        throw InvalidRuntimeData("当前规则没有单次触发变量上下文。");
                    }
                    return rule.InvocationVariables.Get(
                        invocationDefinition.Key,
                        invocationDefinition.DefaultValue);
                case ValueSourceType.SkillVariable:
                    CompiledBattleLogicData skillDefinition = RequireLogicData(
                        context,
                        rule,
                        new BattleVariableKey(referenceId),
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicVariableScope.Skill);
                    return context.LogicVariables.GetSkill(
                        rule.SourceInstanceId,
                        skillDefinition.Key,
                        skillDefinition.DefaultValue);
                case ValueSourceType.RandomBasisPoint:
                    return context.Random.RollBasisPoint();
                default:
                    throw new BattleExecutionException(
                        BattleAbortReason.InvalidRuntimeData,
                        $"未知 ValueSource：{source}");
            }
        }

        private static CompiledBattleLogicData RequireLogicData(
            BattleContext context,
            in RuleContext rule,
            BattleVariableKey key,
            BattleLogicDataKind expectedKind,
            BattleLogicVariableScope? expectedScope)
        {
            if (!rule.Skill.IsValid || rule.SourceInstanceId <= 0 || !key.IsValid)
            {
                throw InvalidRuntimeData(
                    $"逻辑数据 {key.Value} 只能由有效的技能规则访问。");
            }

            if (!context.Database.TryGetSkill(rule.Skill, out CompiledSkill skill) ||
                !skill.TryGetLogicData(key, out CompiledBattleLogicData definition))
            {
                throw InvalidRuntimeData(
                    $"技能 {rule.Skill.Value} 未声明逻辑数据 Key {key.Value}。");
            }

            if (definition.Kind != expectedKind ||
                expectedScope.HasValue && definition.Scope != expectedScope.Value)
            {
                string scope = expectedScope.HasValue
                    ? $"/{expectedScope.Value}"
                    : string.Empty;
                throw InvalidRuntimeData(
                    $"技能 {rule.Skill.Value} 的逻辑数据 {definition.Name} 类型不匹配，" +
                    $"期望 {expectedKind}{scope}。");
            }

            return definition;
        }

        private static int ResolveReferenceId(
            BattleContext context,
            in RuleContext rule,
            in CompiledAction action)
        {
            int value = action.UseReferenceValue
                ? checked((int)ResolveValue(context, rule, action.ReferenceValue))
                : action.ReferenceId;
            if (value <= 0)
            {
                throw InvalidRuntimeData($"Action {action.Type} 的引用 ID 必须大于 0。");
            }
            return value;
        }

        private static BattleExecutionException InvalidRuntimeData(string message) =>
            new BattleExecutionException(BattleAbortReason.InvalidRuntimeData, message);

        private static long GetAttribute(
            BattleContext context,
            UnitId id,
            AttributeType attribute)
        {
            return id.IsValid && context.World.TryGetUnit(id, out BattleUnit unit)
                ? unit.Attributes.Get(attribute)
                : 0;
        }

        private static int GetBuffStack(BattleContext context, UnitId id, BuffId buff)
        {
            if (!id.IsValid || !context.World.TryGetUnit(id, out BattleUnit unit))
            {
                return 0;
            }

            BuffInstance instance = unit.Buffs.Find(buff);
            return instance?.Stack ?? 0;
        }

        private static bool Compare(long left, long right, ComparisonOperator comparison)
        {
            switch (comparison)
            {
                case ComparisonOperator.Equal:
                    return left == right;
                case ComparisonOperator.NotEqual:
                    return left != right;
                case ComparisonOperator.Less:
                    return left < right;
                case ComparisonOperator.LessOrEqual:
                    return left <= right;
                case ComparisonOperator.Greater:
                    return left > right;
                case ComparisonOperator.GreaterOrEqual:
                    return left >= right;
                default:
                    return false;
            }
        }
    }
}
