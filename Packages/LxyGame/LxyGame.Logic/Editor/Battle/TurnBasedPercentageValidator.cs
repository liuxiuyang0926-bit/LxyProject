using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Application;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Random;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class TurnBasedPercentageValidator
    {
        public static void Validate()
        {
            Require(BattlePercentageAuthoring.Encode(12.55, true) == 1255, "小数百分比");
            Require(BattlePercentageAuthoring.Encode(-12.5, true) == -1250, "负向百分比偏移");
            Reject(() => BattlePercentageAuthoring.Encode(double.NaN, true), "非有限数");
            Reject(() => BattlePercentageAuthoring.Encode(double.PositiveInfinity, true), "无穷大");
            Reject(() => BattlePercentageAuthoring.Encode(12.555, true), "超出定点精度");
            Reject(() => BattlePercentageAuthoring.Encode(1.5), "整数类型小数");
            var logic = ScriptableObject.CreateInstance<TurnBasedSkillLogicAsset>();
            try
            {
                logic.ResetToDamageSample("percentage_test", 1, 135, 0);
                logic.Rules[0].actions[1].flags = CompiledActionFlags.None;
                Run(logic, 1, 865); // 攻击100 × 135%。
                logic.DataDefinitions[0].defaultValue = 12.5;
                Run(logic, 1, 988); // 最终整数伤害按既有规则截断。
                var damage = Damage();
                logic.Rules[0].actions = new List<BattleActionAuthoring> { damage };
                damage.value.scalePercent = 135;
                Run(logic, 1, 865);
                damage.value.scalePercent = 12.5;
                Run(logic, 1, 988);
                damage.value.scalePercent = 100;
                damage.value.source = ValueSourceType.Constant;
                damage.value.constant = 135;
                damage.value.percentage = true;
                Reject(() => logic.Compile(), "百分比误写入整数伤害");
                damage.value.percentage = false;
                damage.value.source = ValueSourceType.OwnerAttribute;
                damage.value.constant = 0;

                var randomCondition = new BattleConditionAuthoring
                {
                    type = CompiledConditionType.LogicValueCompare,
                    comparison = ComparisonOperator.Less,
                    leftValue = new BattleValueAuthoring { source = ValueSourceType.RandomBasisPoint },
                    rightValue = new BattleValueAuthoring { constant = 30 },
                };
                logic.Rules[0].conditions.Add(randomCondition);
                Require(logic.Compile()[0].Conditions[0].RightValue.Constant == 3000, "随机百分比比较单位");
                for (int seed = 1; seed <= 32; seed++)
                {
                    long expectedHp = new DeterministicBattleRandom(seed).RollBasisPoint() < 3000 ? 900 : 1000;
                    ulong first = Run(logic, seed, expectedHp);
                    Require(first == Run(logic, seed, expectedHp), "双世界状态哈希");
                }
                randomCondition.rightValue.constant = 0;
                Run(logic, 1, 1000);
                randomCondition.rightValue.constant = 100;
                Run(logic, 1, 900);
                randomCondition.rightValue.constant = 12.55;
                Require(logic.Compile()[0].Conditions[0].RightValue.Constant == 1255, "小数概率阈值");

                logic.Rules[0].conditions.Clear();
                logic.Rules[0].conditions.Add(new BattleConditionAuthoring
                {
                    type = CompiledConditionType.HpPercentCompare,
                    target = new BattleTargetAuthoring { type = TargetSelectorType.Self },
                    comparison = ComparisonOperator.Less,
                    value = 50,
                });
                Run(logic, 1, 900, 499);
                Run(logic, 1, 1000, 500);
                logic.Rules[0].conditions.Clear();
                logic.AddDataDefinition(new BattleLogicDataAuthoring
                {
                    key = 2002, name = "percentage_variable", kind = BattleLogicDataKind.RuntimeVariable,
                    scope = BattleLogicVariableScope.Skill, valueType = BattleLogicDataValueType.BasisPoint,
                });
                logic.Rules[0].actions.Insert(0, new BattleActionAuthoring
                {
                    type = CompiledActionType.SetSkillVariable, referenceId = 2002,
                    value = new BattleValueAuthoring { constant = 12.5 },
                });
                logic.Rules[0].actions.Insert(1, new BattleActionAuthoring
                {
                    type = CompiledActionType.AddSkillVariable, referenceId = 2002,
                    value = new BattleValueAuthoring { constant = 7.5 },
                });
                damage.value.useDynamicScale = true;
                damage.value.dynamicScale = new BattleValueOperandAuthoring
                { source = ValueSourceType.SkillVariable, referenceId = 2002 };
                Run(logic, 1, 980);
                damage.value.dynamicScale = new BattleValueOperandAuthoring
                { source = ValueSourceType.Constant, constant = 135 };
                Run(logic, 1, 865);

                // 构造旧格式，验证转换一次后不会在重载/重复运行时继续除以100。
                logic.ResetToDamageSample("legacy_percentage_test", 1, 13500, 0);
                foreach (var action in logic.Rules[0].actions)
                    foreach (var value in new[] { action.value, action.referenceValue })
                    { value.scalePercent = 10000; value.dynamicScale.constant = 10000; }
                using (var serialized = new SerializedObject(logic))
                {
                    serialized.FindProperty("percentageVersion").intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Require(logic.UpgradeLegacyPercentages(), "首次迁移");
                Require(logic.DataDefinitions[0].defaultValue == 135, "旧参数转换");
                Require(!logic.UpgradeLegacyPercentages(), "迁移幂等");
                logic.Rules[0].actions[1].flags = CompiledActionFlags.None;
                Run(logic, 1, 865);
                string json = JsonUtility.ToJson(logic);
                JsonUtility.FromJsonOverwrite(json, logic);
                Require(!logic.UpgradeLegacyPercentages(), "序列化后迁移幂等");
                Run(logic, 1, 865);

                string source = TurnBasedLogicDataAuthoringUtility.BuildSource(logic, "PercentageGeneratedTest");
                Require(source.Contains("配置百分比：135%"), "代码生成实际默认值说明");
                Require(source.Contains("13500L"), "代码生成定点编译值");
            }
            finally { UnityEngine.Object.DestroyImmediate(logic); }
            Debug.Log("[TurnBasedBattle] 百分比自检通过：小数倍率、动态倍率、概率0/100及32种子、血量阈值、百分比变量、迁移幂等、代码生成和双世界哈希。");
        }

        private static BattleActionAuthoring Damage() => new BattleActionAuthoring
        {
            type = CompiledActionType.Damage, flags = CompiledActionFlags.None,
            value = new BattleValueAuthoring { source = ValueSourceType.OwnerAttribute, attribute = AttributeType.Attack },
        };

        private static ulong Run(TurnBasedSkillLogicAsset logic, int seed, long expectedHp, long casterHp = 1000)
        {
            var compiledRules = logic.Compile();
            var ids = new RuleId[compiledRules.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = compiledRules[i].Id;
            var skill = new CompiledSkill(new SkillId(1001), AttributeType.Energy, 0, ids, "",
                logic.LogicId, logic.CompileDataDefinitions());
            var database = new CompiledBattleDatabase("percentage-test", "1", 1, new[] { skill }, compiledRules);
            var attacker = Unit(1, BattleCamp.Attacker, 1, 100);
            var defender = Unit(2, BattleCamp.Defender, 10, 50);
            attacker.InitializeHealth(1000, casterHp);
            using (var session = new BattleApplication(database).CreateSession(new[] { attacker, defender }, seed))
            {
                var start = session.Step(new StartBattleCommand(1));
                Require(start.Success, start.Error);
                var cast = session.Step(new UseSkillCommand(2, attacker.Id, skill.Id, new[] { defender.Id }));
                Require(cast.Success, cast.Error);
                Require(defender.Attributes.Get(AttributeType.Hp) == expectedHp,
                    $"期望目标生命 {expectedHp}，实际 {defender.Attributes.Get(AttributeType.Hp)}");
                return cast.StateHash;
            }
        }

        private static BattleUnit Unit(int id, BattleCamp camp, int position, long speed)
        {
            var unit = new BattleUnit(new UnitId(id), id, camp, position);
            unit.InitializeHealth(1000);
            unit.InitializeAttribute(AttributeType.Attack, 100);
            unit.InitializeAttribute(AttributeType.Speed, speed);
            unit.Skills.Add(new SkillInstance(new SkillId(1001), unit.Id));
            return unit;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("百分比自检失败：" + message); }

        private static void Reject(Action action, string name)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("未拒绝非法输入：" + name);
        }
    }
}
