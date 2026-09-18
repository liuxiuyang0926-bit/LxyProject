using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal abstract class LocalizedEnumOdinDrawer<T> : OdinValueDrawer<T>
        where T : struct, Enum
    {
        protected abstract T[] Values { get; }
        protected abstract string[] Labels { get; }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            T[] values = Values;
            string[] labels = Labels;
            int current = IndexOf(values, ValueEntry.SmartValue);
            int shown = current < 0 ? 0 : current;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(label ?? GUIContent.none, shown, labels);
            if (EditorGUI.EndChangeCheck())
            {
                ValueEntry.SmartValue = values[Mathf.Clamp(next, 0, values.Length - 1)];
            }
        }

        private static int IndexOf(IReadOnlyList<T> values, T value)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int index = 0; index < values.Count; index++)
            {
                if (comparer.Equals(values[index], value))
                {
                    return index;
                }
            }
            return -1;
        }
    }

    [OdinDrawer]
    internal sealed class BattleExecutionModeOdinDrawer :
        LocalizedEnumOdinDrawer<BattleAuthoringExecutionMode>
    {
        private static readonly BattleAuthoringExecutionMode[] EnumValues =
        {
            BattleAuthoringExecutionMode.Sequence,
            BattleAuthoringExecutionMode.Parallel,
        };
        private static readonly string[] DisplayLabels =
        {
            "顺序",
            "并行",
        };
        protected override BattleAuthoringExecutionMode[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleSkillTargetModeOdinDrawer :
        LocalizedEnumOdinDrawer<BattleSkillTargetMode>
    {
        private static readonly BattleSkillTargetMode[] EnumValues =
        {
            BattleSkillTargetMode.SingleEnemy,
            BattleSkillTargetMode.AllEnemies,
            BattleSkillTargetMode.Self,
            BattleSkillTargetMode.LowestHpAlly,
        };
        private static readonly string[] DisplayLabels =
        {
            "单个敌人",
            "全部敌人",
            "自身",
            "生命最低友军",
        };
        protected override BattleSkillTargetMode[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleUnitControlOdinDrawer :
        LocalizedEnumOdinDrawer<BattleUnitControl>
    {
        private static readonly BattleUnitControl[] EnumValues =
        {
            BattleUnitControl.Player,
            BattleUnitControl.Ai,
        };
        private static readonly string[] DisplayLabels = { "玩家", "AI" };
        protected override BattleUnitControl[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleEventTypeOdinDrawer :
        LocalizedEnumOdinDrawer<BattleEventType>
    {
        private static readonly BattleEventType[] EnumValues =
            (BattleEventType[])Enum.GetValues(typeof(BattleEventType));
        private static readonly string[] DisplayLabels =
        {
            "无（不可用于规则）",
            "战斗开始",
            "回合开始",
            "行动开始",
            "技能释放",
            "伤害结算前",
            "伤害结算完成",
            "生命变化",
            "治疗完成",
            "获得 Buff",
            "移除 Buff",
            "单位召唤",
            "单位死亡",
            "单位复活",
            "行动结束",
            "战斗结束",
        };
        protected override BattleEventType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleEventPhaseOdinDrawer :
        LocalizedEnumOdinDrawer<BattleEventPhase>
    {
        private static readonly BattleEventPhase[] EnumValues =
            (BattleEventPhase[])Enum.GetValues(typeof(BattleEventPhase));
        private static readonly string[] DisplayLabels = { "前置", "主阶段", "后置" };
        protected override BattleEventPhase[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class TargetSelectorTypeOdinDrawer :
        LocalizedEnumOdinDrawer<TargetSelectorType>
    {
        private static readonly TargetSelectorType[] EnumValues =
            (TargetSelectorType[])Enum.GetValues(typeof(TargetSelectorType));
        private static readonly string[] DisplayLabels =
        {
            "规则拥有者",
            "事件攻击者",
            "事件目标",
            "技能选中目标",
            "全部友军",
            "全部敌人",
            "随机敌人",
            "生命最低敌人",
            "攻击最高敌人",
            "前排敌人",
        };
        protected override TargetSelectorType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class ValueSourceTypeOdinDrawer :
        LocalizedEnumOdinDrawer<ValueSourceType>
    {
        private static readonly ValueSourceType[] EnumValues =
            (ValueSourceType[])Enum.GetValues(typeof(ValueSourceType));
        private static readonly string[] DisplayLabels =
        {
            "固定值",
            "拥有者属性",
            "事件攻击者属性",
            "当前目标属性",
            "当前生命",
            "最大生命",
            "已损失生命",
            "生命百分比",
            "Buff 层数",
            "事件数值",
            "当前回合",
            "战斗计数器",
            "策划参数",
            "单次触发临时变量",
            "技能实例变量",
            "确定性随机值（0~9999）",
        };
        protected override ValueSourceType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class ComparisonOperatorOdinDrawer :
        LocalizedEnumOdinDrawer<ComparisonOperator>
    {
        private static readonly ComparisonOperator[] EnumValues =
            (ComparisonOperator[])Enum.GetValues(typeof(ComparisonOperator));
        private static readonly string[] DisplayLabels =
        {
            "等于",
            "不等于",
            "小于",
            "小于等于",
            "大于",
            "大于等于",
        };
        protected override ComparisonOperator[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class ConditionTypeOdinDrawer :
        LocalizedEnumOdinDrawer<CompiledConditionType>
    {
        private static readonly CompiledConditionType[] EnumValues =
            (CompiledConditionType[])Enum.GetValues(typeof(CompiledConditionType));
        private static readonly string[] DisplayLabels =
        {
            "始终满足",
            "目标是自身",
            "目标存活",
            "目标死亡",
            "目标是敌人",
            "目标是友军",
            "配置 ID 比较",
            "拥有 Buff",
            "属性比较",
            "生命百分比比较",
            "事件数值比较",
            "计数器比较",
            "回合数比较",
            "目标数量比较",
            "计数器与当前回合比较",
            "两个动态数值比较",
        };
        protected override CompiledConditionType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class ActionTypeOdinDrawer :
        LocalizedEnumOdinDrawer<CompiledActionType>
    {
        private static readonly CompiledActionType[] EnumValues =
            (CompiledActionType[])Enum.GetValues(typeof(CompiledActionType));
        private static readonly string[] DisplayLabels =
        {
            "造成伤害",
            "恢复生命",
            "添加 Buff",
            "移除 Buff",
            "修改属性",
            "修改资源",
            "直接击杀",
            "复活",
            "修改逻辑站位",
            "累加计数器",
            "设置变量",
            "结束当前行动",
            "设置单次触发变量",
            "累加单次触发变量",
            "设置技能实例变量",
            "累加技能实例变量",
        };
        protected override CompiledActionType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleLogicDataKindOdinDrawer :
        LocalizedEnumOdinDrawer<BattleLogicDataKind>
    {
        private static readonly BattleLogicDataKind[] EnumValues =
            (BattleLogicDataKind[])Enum.GetValues(typeof(BattleLogicDataKind));
        private static readonly string[] DisplayLabels =
        {
            "策划参数（只读）",
            "运行时变量",
        };
        protected override BattleLogicDataKind[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleLogicDataValueTypeOdinDrawer :
        LocalizedEnumOdinDrawer<BattleLogicDataValueType>
    {
        private static readonly BattleLogicDataValueType[] EnumValues =
            (BattleLogicDataValueType[])Enum.GetValues(typeof(BattleLogicDataValueType));
        private static readonly string[] DisplayLabels =
        {
            "整数",
            "万分比",
            "布尔值（0/1）",
            "配置标识符",
        };
        protected override BattleLogicDataValueType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleLogicVariableScopeOdinDrawer :
        LocalizedEnumOdinDrawer<BattleLogicVariableScope>
    {
        private static readonly BattleLogicVariableScope[] EnumValues =
            (BattleLogicVariableScope[])Enum.GetValues(typeof(BattleLogicVariableScope));
        private static readonly string[] DisplayLabels =
        {
            "单次规则触发",
            "技能实例整场",
        };
        protected override BattleLogicVariableScope[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class AttributeTypeOdinDrawer :
        LocalizedEnumOdinDrawer<AttributeType>
    {
        private static readonly AttributeType[] EnumValues =
            (AttributeType[])Enum.GetValues(typeof(AttributeType));
        private static readonly string[] DisplayLabels =
        {
            "生命",
            "最大生命",
            "攻击",
            "防御",
            "速度",
            "暴击率",
            "暴击伤害",
            "命中率",
            "闪避率",
            "怒气",
            "能量",
            "护盾",
            "Count（内部保留）",
        };
        protected override AttributeType[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class BattleCampOdinDrawer :
        LocalizedEnumOdinDrawer<BattleCamp>
    {
        private static readonly BattleCamp[] EnumValues =
            (BattleCamp[])Enum.GetValues(typeof(BattleCamp));
        private static readonly string[] DisplayLabels = { "中立", "攻击方", "防守方" };
        protected override BattleCamp[] Values => EnumValues;
        protected override string[] Labels => DisplayLabels;
    }

    [OdinDrawer]
    internal sealed class ActionFlagsOdinDrawer : OdinValueDrawer<CompiledActionFlags>
    {
        private static readonly string[] FlagLabels =
        {
            "可以闪避",
            "可以暴击",
            "无视防御",
        };

        protected override void DrawPropertyLayout(GUIContent label)
        {
            int current = (int)ValueEntry.SmartValue;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.MaskField(
                label ?? GUIContent.none,
                current,
                FlagLabels);
            if (EditorGUI.EndChangeCheck())
            {
                ValueEntry.SmartValue = (CompiledActionFlags)next;
            }
        }
    }
}
