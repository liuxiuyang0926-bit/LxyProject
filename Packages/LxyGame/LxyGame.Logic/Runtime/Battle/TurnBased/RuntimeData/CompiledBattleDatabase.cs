using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.RuntimeData
{
    public interface IBattleRuntimeDatabase
    {
        string LogicVersion { get; }
        string RuntimeDataVersion { get; }
        ulong ConfigHash { get; }
        bool TryGetSkill(SkillId id, out CompiledSkill skill);
        bool TryGetRule(RuleId id, out CompiledRule rule);
        bool TryGetBuff(BuffId id, out CompiledBuff buff);
        bool TryGetExpression(string id, out CompiledBattleExpression expression);
    }

    public sealed class CompiledBattleDatabase : IBattleRuntimeDatabase
    {
        private readonly Dictionary<SkillId, CompiledSkill> skills =
            new Dictionary<SkillId, CompiledSkill>();
        private readonly Dictionary<RuleId, CompiledRule> rules =
            new Dictionary<RuleId, CompiledRule>();
        private readonly Dictionary<BuffId, CompiledBuff> buffs =
            new Dictionary<BuffId, CompiledBuff>();
        private readonly Dictionary<string, CompiledBattleExpression> expressions =
            new Dictionary<string, CompiledBattleExpression>(StringComparer.Ordinal);

        public CompiledBattleDatabase(
            string logicVersion,
            string runtimeDataVersion,
            ulong configHash,
            CompiledSkill[] skillDefinitions,
            CompiledRule[] ruleDefinitions,
            CompiledBuff[] buffDefinitions = null,
            CompiledBattleExpression[] expressionDefinitions = null)
        {
            LogicVersion = RequireVersion(logicVersion, nameof(logicVersion));
            RuntimeDataVersion = RequireVersion(runtimeDataVersion, nameof(runtimeDataVersion));
            ConfigHash = configHash;

            AddUnique(skillDefinitions, skills, item => item.Id, "Skill");
            AddUnique(ruleDefinitions, rules, item => item.Id, "Rule");
            AddUnique(
                buffDefinitions ?? Array.Empty<CompiledBuff>(),
                buffs,
                item => item.Id,
                "Buff");
            AddUnique(
                expressionDefinitions ?? Array.Empty<CompiledBattleExpression>(),
                expressions,
                item => item.Id,
                "Expression");
            ValidateReferences();
        }

        public string LogicVersion { get; }
        public string RuntimeDataVersion { get; }
        public ulong ConfigHash { get; }

        public bool TryGetSkill(SkillId id, out CompiledSkill skill) =>
            skills.TryGetValue(id, out skill);

        public bool TryGetRule(RuleId id, out CompiledRule rule) =>
            rules.TryGetValue(id, out rule);

        public bool TryGetBuff(BuffId id, out CompiledBuff buff) =>
            buffs.TryGetValue(id, out buff);

        public bool TryGetExpression(
            string id,
            out CompiledBattleExpression expression)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                expression = null;
                return false;
            }
            return expressions.TryGetValue(id, out expression);
        }

        private void ValidateReferences()
        {
            foreach (KeyValuePair<SkillId, CompiledSkill> pair in skills)
            {
                ValidateRuleIds(pair.Value.RuleIds, $"Skill {pair.Key}");
                for (int index = 0; index < pair.Value.RuleIds.Length; index++)
                {
                    ValidateRuleLogicData(
                        pair.Value,
                        rules[pair.Value.RuleIds[index]],
                        $"Skill {pair.Key} / Rule {pair.Value.RuleIds[index]}");
                }
            }

            foreach (KeyValuePair<BuffId, CompiledBuff> pair in buffs)
            {
                ValidateRuleIds(pair.Value.RuleIds, $"Buff {pair.Key}");
                for (int index = 0; index < pair.Value.RuleIds.Length; index++)
                {
                    ValidateRuleLogicData(
                        null,
                        rules[pair.Value.RuleIds[index]],
                        $"Buff {pair.Key} / Rule {pair.Value.RuleIds[index]}");
                }
            }
        }

        private static void ValidateRuleLogicData(
            CompiledSkill skill,
            CompiledRule rule,
            string owner)
        {
            for (int index = 0; index < rule.Conditions.Length; index++)
            {
                CompiledCondition condition = rule.Conditions[index];
                if (condition.Type == CompiledConditionType.LogicValueCompare)
                {
                    ValidateLogicValue(skill, condition.LeftValue, owner + " Condition 左值");
                    ValidateLogicValue(skill, condition.RightValue, owner + " Condition 右值");
                }
            }

            for (int index = 0; index < rule.Actions.Length; index++)
            {
                CompiledAction action = rule.Actions[index];
                ValidateLogicValue(skill, action.Value, owner + " Action 数值");
                if (action.UseReferenceValue)
                {
                    ValidateLogicValue(skill, action.ReferenceValue, owner + " Action 动态引用");
                }

                BattleLogicVariableScope scope;
                switch (action.Type)
                {
                    case CompiledActionType.SetInvocationVariable:
                    case CompiledActionType.AddInvocationVariable:
                        scope = BattleLogicVariableScope.Invocation;
                        break;
                    case CompiledActionType.SetSkillVariable:
                    case CompiledActionType.AddSkillVariable:
                        scope = BattleLogicVariableScope.Skill;
                        break;
                    default:
                        continue;
                }

                ValidateLogicDataDefinition(
                    skill,
                    action.ReferenceId,
                    BattleLogicDataKind.RuntimeVariable,
                    scope,
                    owner + " Action 目标变量");
            }
        }

        private static void ValidateLogicValue(
            CompiledSkill skill,
            in CompiledValue value,
            string owner)
        {
            ValidateLogicSource(skill, value.Source, value.ReferenceId, owner);
            if (value.UseDynamicScale)
            {
                ValidateLogicSource(
                    skill,
                    value.DynamicScaleSource,
                    value.DynamicScaleReferenceId,
                    owner + " 动态倍率");
            }
        }

        private static void ValidateLogicSource(
            CompiledSkill skill,
            ValueSourceType source,
            int key,
            string owner)
        {
            switch (source)
            {
                case ValueSourceType.LogicParameter:
                    ValidateLogicDataDefinition(
                        skill,
                        key,
                        BattleLogicDataKind.Parameter,
                        null,
                        owner);
                    break;
                case ValueSourceType.InvocationVariable:
                    ValidateLogicDataDefinition(
                        skill,
                        key,
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicVariableScope.Invocation,
                        owner);
                    break;
                case ValueSourceType.SkillVariable:
                    ValidateLogicDataDefinition(
                        skill,
                        key,
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicVariableScope.Skill,
                        owner);
                    break;
            }
        }

        private static void ValidateLogicDataDefinition(
            CompiledSkill skill,
            int key,
            BattleLogicDataKind kind,
            BattleLogicVariableScope? scope,
            string owner)
        {
            if (skill == null ||
                !skill.TryGetLogicData(
                    new BattleVariableKey(key),
                    out CompiledBattleLogicData definition))
            {
                throw new InvalidOperationException(
                    $"{owner} 引用了未声明的技能逻辑数据 Key {key}。");
            }

            if (definition.Kind != kind ||
                scope.HasValue && definition.Scope != scope.Value)
            {
                throw new InvalidOperationException(
                    $"{owner} 引用的数据 {definition.Name} 类型或作用域不匹配。");
            }
        }

        private void ValidateRuleIds(RuleId[] ruleIds, string owner)
        {
            for (int index = 0; index < ruleIds.Length; index++)
            {
                if (!rules.ContainsKey(ruleIds[index]))
                {
                    throw new InvalidOperationException(
                        $"{owner} 引用了不存在的 Rule {ruleIds[index]}。");
                }
            }
        }

        private static void AddUnique<TId, TValue>(
            TValue[] source,
            Dictionary<TId, TValue> target,
            Func<TValue, TId> keySelector,
            string label)
            where TValue : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            for (int index = 0; index < source.Length; index++)
            {
                TValue item = source[index] ??
                    throw new InvalidOperationException($"{label} 数据包含空项。");
                TId key = keySelector(item);
                if (target.ContainsKey(key))
                {
                    throw new InvalidOperationException($"{label} ID 重复：{key}");
                }

                target.Add(key, item);
            }
        }

        private static string RequireVersion(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("版本不能为空。", parameterName);
            }

            return value;
        }
    }
}
