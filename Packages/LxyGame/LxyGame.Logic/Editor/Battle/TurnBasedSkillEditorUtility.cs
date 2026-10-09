using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal static class TurnBasedSkillEditorUtility
    {
        public static string FirstLine(string text) => string.IsNullOrWhiteSpace(text)
            ? string.Empty : text.Trim().Split('\n')[0].TrimEnd('\r');

        public static GUIContent AssetLabel(string id, string description)
        {
            string title = FirstLine(description);
            if (title.Length > 30) title = title.Substring(0, 30) + "…";
            return new GUIContent(string.IsNullOrEmpty(title) ? id : title + "\n" + id,
                id + "\n" + description);
        }

        public static bool MatchesSearch(string search, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            foreach (string value in values)
                if ((value ?? string.Empty).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        public static string EventLabel(BattleEventType value)
        {
            switch (value)
            {
                case BattleEventType.None: return "未选择事件";
                case BattleEventType.BattleStarted: return "战斗开始";
                case BattleEventType.RoundStarted: return "回合开始";
                case BattleEventType.TurnStarted: return "行动开始";
                case BattleEventType.SkillCast: return "技能释放";
                case BattleEventType.BeforeDamage: return "伤害结算前";
                case BattleEventType.DamageResolved: return "伤害结算完成";
                case BattleEventType.HpChanged: return "生命变化";
                case BattleEventType.HealResolved: return "治疗完成";
                case BattleEventType.BuffAdded: return "获得增益或减益";
                case BattleEventType.BuffRemoved: return "移除增益或减益";
                case BattleEventType.UnitSummoned: return "单位召唤";
                case BattleEventType.UnitDead: return "单位死亡";
                case BattleEventType.UnitRevived: return "单位复活";
                case BattleEventType.TurnEnded: return "行动结束";
                case BattleEventType.BattleEnded: return "战斗结束";
                default: return "未知事件（" + (int)value + "）";
            }
        }

        public static string PhaseLabel(BattleEventPhase value) => value == BattleEventPhase.Before
            ? "前置阶段" : value == BattleEventPhase.After ? "后置阶段" : "主阶段";

        // Keep unresolved references visible. Merely opening a dropdown must not erase data.
        public static void BuildOutputOptions(TurnBasedSkillLogicAsset source, string currentKey,
            out string[] keys, out string[] labels)
        {
            var keyList = new List<string> { string.Empty };
            var labelList = new List<string> { "不读取逻辑结果" };
            if (source != null)
                foreach (BattleLogicOutputAuthoring output in source.Outputs)
                {
                    if (output == null || string.IsNullOrWhiteSpace(output.key)) continue;
                    keyList.Add(output.key);
                    labelList.Add(EventLabel(output.eventType) + " · " + output.key);
                }
            if (!string.IsNullOrEmpty(currentKey) && !keyList.Contains(currentKey))
            {
                keyList.Add(currentKey);
                labelList.Add("未找到绑定（保留原值）：" + currentKey);
            }
            keys = keyList.ToArray();
            labels = labelList.ToArray();
        }

        public static bool MatchesDataSource(BattleLogicDataAuthoring data, ValueSourceType source)
        {
            if (data == null) return false;
            if (source == ValueSourceType.LogicParameter) return data.kind == BattleLogicDataKind.Parameter;
            return data.kind == BattleLogicDataKind.RuntimeVariable &&
                ((source == ValueSourceType.InvocationVariable && data.scope == BattleLogicVariableScope.Invocation) ||
                 (source == ValueSourceType.SkillVariable && data.scope == BattleLogicVariableScope.Skill));
        }

        public static bool IsLocalData(ValueSourceType source) => source == ValueSourceType.LogicParameter ||
            source == ValueSourceType.InvocationVariable || source == ValueSourceType.SkillVariable;

        public static int NextStartFrame(SerializedProperty operations)
        {
            int end = 0;
            for (int i = 0; i < operations.arraySize; i++)
            {
                SerializedProperty operation = operations.GetArrayElementAtIndex(i);
                end = Math.Max(end, EndFrame(operation));
            }
            return end;
        }

        public static int EndFrame(SerializedProperty operation) => (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, operation.FindPropertyRelative("startFrame").intValue) +
            Math.Max(0, operation.FindPropertyRelative("durationFrames").intValue));

        public static void SetTiming(SerializedProperty operation, int start, int duration)
        {
            start = Math.Max(0, start);
            operation.FindPropertyRelative("startFrame").intValue = start;
            operation.FindPropertyRelative("durationFrames").intValue = Math.Min(Math.Max(0, duration), int.MaxValue - start);
        }
    }
}
