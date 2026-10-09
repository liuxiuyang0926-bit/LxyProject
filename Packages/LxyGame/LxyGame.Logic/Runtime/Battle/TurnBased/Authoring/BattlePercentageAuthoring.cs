using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Authoring
{
    /// <summary>配置使用实际百分数；仅在编译边界转换为定点比例的内部表示。</summary>
    public static class BattlePercentageAuthoring
    {
        public const double OneHundred = 100;

        public static long Encode(double value, bool percentage = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("数值必须是有限数。");
            decimal scaled = (decimal)value * (percentage ? FP.Precision / 100m : 1m);
            if (scaled != decimal.Truncate(scaled))
                throw new ArgumentException(percentage
                    ? "百分比最多支持两位小数，例如 12.55 表示 12.55%。"
                    : "整数、数量和配置编号不能填写小数。");
            return checked((long)scaled);
        }

        public static int Scale(double percentage) => checked((int)Encode(percentage, true));

        public static bool IsPercentage(AttributeType attribute) =>
            attribute == AttributeType.CritRate || attribute == AttributeType.CritDamage ||
            attribute == AttributeType.HitRate || attribute == AttributeType.DodgeRate;

        public static bool IsPercentage(int key, IReadOnlyList<BattleLogicDataAuthoring> data)
        {
            if (data != null)
                for (int i = 0; i < data.Count; i++)
                    if (data[i] != null && data[i].key == key)
                        return data[i].valueType == BattleLogicDataValueType.BasisPoint;
            return false;
        }

        public static bool IsPercentage(ValueSourceType source, AttributeType attribute,
            int referenceId, IReadOnlyList<BattleLogicDataAuthoring> data) =>
            source == ValueSourceType.HpPercent || source == ValueSourceType.RandomBasisPoint ||
            ((source == ValueSourceType.OwnerAttribute || source == ValueSourceType.AttackerAttribute ||
              source == ValueSourceType.TargetAttribute) && IsPercentage(attribute)) ||
            ((source == ValueSourceType.LogicParameter || source == ValueSourceType.InvocationVariable ||
              source == ValueSourceType.SkillVariable) && IsPercentage(referenceId, data));

        public static bool IsPercentage(BattleValueAuthoring value,
            IReadOnlyList<BattleLogicDataAuthoring> data) => value != null &&
            ((value.source == ValueSourceType.Constant && value.percentage) ||
             IsPercentage(value.source, value.attribute, value.referenceId, data));

        public static bool IsPercentage(BattleConditionAuthoring condition) =>
            condition.type == CompiledConditionType.HpPercentCompare ||
            (condition.type == CompiledConditionType.AttributeCompare && IsPercentage(condition.attribute));

        public static bool IsPercentage(BattleActionAuthoring action,
            IReadOnlyList<BattleLogicDataAuthoring> data) =>
            ((action.type == CompiledActionType.ModifyAttribute || action.type == CompiledActionType.ModifyResource) &&
             IsPercentage(action.attribute)) ||
            ((action.type == CompiledActionType.SetInvocationVariable || action.type == CompiledActionType.AddInvocationVariable ||
              action.type == CompiledActionType.SetSkillVariable || action.type == CompiledActionType.AddSkillVariable) &&
             IsPercentage(action.referenceId, data));

        // 每个根资产只迁移一次。保留枚举编号和编译后的定点表示，旧回放与伤害结果不变。
        internal static void MigrateRules(IEnumerable<BattleRuleAuthoring> rules,
            IReadOnlyList<BattleLogicDataAuthoring> data, HashSet<BattleValueAuthoring> visited)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                if (rule.conditions != null) foreach (var condition in rule.conditions)
                {
                    if (condition == null) continue;
                    if (IsPercentage(condition)) condition.value /= 100;
                    bool percentage = IsPercentage(condition.leftValue, data) || IsPercentage(condition.rightValue, data);
                    MigrateValue(condition.leftValue, data, percentage, visited);
                    MigrateValue(condition.rightValue, data, percentage, visited);
                }
                if (rule.actions != null) foreach (var action in rule.actions)
                {
                    if (action == null) continue;
                    MigrateValue(action.value, data, IsPercentage(action, data), visited);
                    MigrateValue(action.referenceValue, data, false, visited);
                }
            }
        }

        private static void MigrateValue(BattleValueAuthoring value,
            IReadOnlyList<BattleLogicDataAuthoring> data, bool percentage, HashSet<BattleValueAuthoring> visited)
        {
            if (value == null || !visited.Add(value)) return;
            value.scalePercent /= 100;
            if (value.dynamicScale != null) value.dynamicScale.constant /= 100;
            if (percentage || IsPercentage(value, data))
            {
                value.constant /= 100;
                value.offset /= 100;
                value.minimum /= 100;
                value.maximum /= 100;
            }
        }
    }
}
