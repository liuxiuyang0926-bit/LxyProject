using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal static class TurnBasedSkillEditorValidator
    {
        public static void Validate() => Debug.Log(Run());

        public static string Run()
        {
            var checks = new List<string>();
            var logic = ScriptableObject.CreateInstance<TurnBasedSkillLogicAsset>();
            var expression = ScriptableObject.CreateInstance<TurnBasedSkillExpressionAsset>();
            try
            {
                logic.ResetToDamageSample("editor_validation", 1, 135, 0);
                TurnBasedSkillEditorUtility.BuildOutputOptions(logic, "missing_output", out string[] keys, out string[] labels);
                Require(Array.IndexOf(keys, "missing_output") >= 0 && labels[Array.IndexOf(keys, "missing_output")].Contains("保留原值"),
                    "失效逻辑绑定仍可见且原值保留", checks);
                TurnBasedSkillEditorUtility.BuildOutputOptions(null, "damage_result", out keys, out labels);
                Require(Array.IndexOf(keys, "damage_result") >= 0 && keys[0] == string.Empty,
                    "未选择逻辑源时保留旧绑定，并允许主动清空", checks);
                foreach (BattleEventType value in Enum.GetValues(typeof(BattleEventType)))
                    Require(!TurnBasedSkillEditorUtility.EventLabel(value).StartsWith("未知"), "事件中文覆盖：" + value, checks);
                var parameter = new BattleLogicDataAuthoring { kind = BattleLogicDataKind.Parameter };
                var variable = new BattleLogicDataAuthoring { kind = BattleLogicDataKind.RuntimeVariable, scope = BattleLogicVariableScope.Skill };
                Require(TurnBasedSkillEditorUtility.MatchesDataSource(parameter, ValueSourceType.LogicParameter) &&
                    !TurnBasedSkillEditorUtility.MatchesDataSource(parameter, ValueSourceType.SkillVariable) &&
                    TurnBasedSkillEditorUtility.MatchesDataSource(variable, ValueSourceType.SkillVariable) &&
                    !TurnBasedSkillEditorUtility.MatchesDataSource(variable, ValueSourceType.InvocationVariable),
                    "数据选择区分参数、技能变量与单次触发变量", checks);
                Require(TurnBasedSkillEditorUtility.MatchesSearch("关羽", "skill_1001", "关羽普通攻击") &&
                    TurnBasedSkillEditorUtility.MatchesSearch("SKILL_1001", "skill_1001"), "支持中文说明与标识搜索", checks);

                using var serialized = new SerializedObject(expression);
                var tracks = serialized.FindProperty("tracks");
                tracks.arraySize = 1;
                var operations = tracks.GetArrayElementAtIndex(0).FindPropertyRelative("operations");
                operations.arraySize = 2;
                var first = operations.GetArrayElementAtIndex(0);
                var second = operations.GetArrayElementAtIndex(1);
                TurnBasedSkillEditorUtility.SetTiming(first, 80, 20);
                TurnBasedSkillEditorUtility.SetTiming(second, 10, 15);
                Require(TurnBasedSkillEditorUtility.NextStartFrame(operations) == 100,
                    "乱序列表新增操作接在最晚结束帧后，避免重叠", checks);
                TurnBasedSkillEditorUtility.SetTiming(second, 40, second.FindPropertyRelative("durationFrames").intValue);
                Require(TurnBasedSkillEditorUtility.EndFrame(second) == 55 && second.FindPropertyRelative("durationFrames").intValue == 15,
                    "移动开始帧保持持续时长", checks);
                TurnBasedSkillEditorUtility.SetTiming(second, 40, 0);
                Require(TurnBasedSkillEditorUtility.EndFrame(second) == 40, "瞬时操作保持零时长", checks);
                TurnBasedSkillEditorUtility.SetTiming(second, int.MaxValue - 2, 10);
                Require(TurnBasedSkillEditorUtility.EndFrame(second) == int.MaxValue, "时间轴边界不产生整数溢出", checks);
                return "PASS " + checks.Count + " checks\n" + string.Join("\n", checks);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expression);
                UnityEngine.Object.DestroyImmediate(logic);
            }
        }

        private static void Require(bool condition, string message, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        }
    }
}
