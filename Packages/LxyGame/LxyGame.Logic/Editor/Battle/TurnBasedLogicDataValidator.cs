using System;
using Game.Battle.TurnBased.Application;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Session;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class TurnBasedLogicDataValidator
    {
        private static readonly SkillId TestSkill = new SkillId(810001);
        private static readonly BattleVariableKey DamageRate =
            new BattleVariableKey(1001);
        private static readonly BattleVariableKey InvocationValue =
            new BattleVariableKey(3001);
        private static readonly BattleVariableKey BuffParameter =
            new BattleVariableKey(1002);
        private static readonly BattleVariableKey CastCount =
            new BattleVariableKey(2001);
        private static readonly BattleVariableKey ResultCounter =
            new BattleVariableKey(9810001);
        private static readonly BuffId TestBuff = new BuffId(820001);

        [MenuItem("工具/战斗/运行逻辑临时数据自检", false, 6)]
        public static void Validate()
        {
            CompiledBattleDatabase database = CreateDatabase();
            ulong first = RunScenario(database, 0x1357246);
            ulong second = RunScenario(database, 0x1357246);
            if (first != second)
            {
                throw new InvalidOperationException(
                    "相同 Seed 与指令产生了不同状态哈希，逻辑变量不满足确定性要求。");
            }

            Debug.Log(
                "[TurnBasedBattle] 逻辑临时数据自检通过：策划参数、单次触发变量、" +
                "技能实例变量、动态倍率/引用、动态比较和状态哈希均正常。");
        }

        private static ulong RunScenario(CompiledBattleDatabase database, int seed)
        {
            BattleUnit attacker = CreateUnit(1, 101, BattleCamp.Attacker, 1, 100, 100);
            attacker.InitializeAttribute(AttributeType.Attack, 50);
            var skillInstance = new SkillInstance(TestSkill, attacker.Id);
            attacker.Skills.Add(skillInstance);

            BattleUnit defender = CreateUnit(2, 201, BattleCamp.Defender, 10, 100, 50);
            defender.Skills.Add(new SkillInstance(TestSkill, defender.Id));

            using (BattleSession session = new BattleApplication(database).CreateSession(
                       new[] { attacker, defender },
                       seed))
            {
                RequireSuccess(session.Step(new StartBattleCommand(1)));
                BattleStepResult cast = session.Step(new UseSkillCommand(
                    2,
                    attacker.Id,
                    TestSkill,
                    new[] { defender.Id }));
                RequireSuccess(cast);

                long hp = defender.Attributes.Get(AttributeType.Hp);
                if (hp != 75)
                {
                    throw new InvalidOperationException(
                        $"临时变量传递伤害失败，期望生命 75，实际 {hp}。");
                }
                if (session.Context.World.Counters.Get(ResultCounter) != 1)
                {
                    throw new InvalidOperationException("技能实例变量动态比较没有通过。");
                }
                if (!defender.Buffs.Has(TestBuff))
                {
                    throw new InvalidOperationException("动态 Buff ID 没有正确解析并添加 Buff。");
                }
                if (skillInstance.RuntimeInstanceId <= 0 ||
                    session.Context.LogicVariables.GetSkill(
                        skillInstance.RuntimeInstanceId,
                        CastCount) != 1)
                {
                    throw new InvalidOperationException("技能实例变量没有跨规则持久保存。");
                }

                return cast.StateHash;
            }
        }

        private static BattleUnit CreateUnit(
            int id,
            int configId,
            BattleCamp camp,
            int position,
            long hp,
            long speed)
        {
            var unit = new BattleUnit(new UnitId(id), configId, camp, position);
            unit.InitializeHealth(hp);
            unit.InitializeAttribute(AttributeType.Attack, 1);
            unit.InitializeAttribute(AttributeType.Defense, 0);
            unit.InitializeAttribute(AttributeType.Speed, speed);
            unit.InitializeAttribute(AttributeType.Energy, 100);
            return unit;
        }

        private static CompiledBattleDatabase CreateDatabase()
        {
            var self = new CompiledTargetSelector(TargetSelectorType.Self);
            var selected = new CompiledTargetSelector(TargetSelectorType.SelectedTargets, 0);
            var writeAndDamage = new CompiledRule(
                new RuleId(810001),
                BattleEventType.SkillCast,
                BattleEventPhase.Main,
                0,
                null,
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.SetInvocationVariable,
                        self,
                        new CompiledValue(
                            ValueSourceType.OwnerAttribute,
                            attribute: AttributeType.Attack,
                            useDynamicScale: true,
                            dynamicScaleSource: ValueSourceType.LogicParameter,
                            dynamicScaleReferenceId: DamageRate.Value),
                        referenceId: InvocationValue.Value),
                    new CompiledAction(
                        CompiledActionType.Damage,
                        selected,
                        new CompiledValue(
                            ValueSourceType.InvocationVariable,
                            referenceId: InvocationValue.Value),
                        flags: CompiledActionFlags.IgnoreDefense),
                    new CompiledAction(
                        CompiledActionType.AddSkillVariable,
                        self,
                        new CompiledValue(ValueSourceType.Constant, 1),
                        referenceId: CastCount.Value),
                    new CompiledAction(
                        CompiledActionType.AddBuff,
                        selected,
                        useReferenceValue: true,
                        referenceValue: new CompiledValue(
                            ValueSourceType.LogicParameter,
                            referenceId: BuffParameter.Value)),
                });
            var observeSkillVariable = new CompiledRule(
                new RuleId(810002),
                BattleEventType.SkillCast,
                BattleEventPhase.Main,
                10,
                new[]
                {
                    new CompiledCondition(
                        CompiledConditionType.LogicValueCompare,
                        self,
                        ComparisonOperator.Equal,
                        leftValue: new CompiledValue(
                            ValueSourceType.SkillVariable,
                            referenceId: CastCount.Value),
                        rightValue: new CompiledValue(ValueSourceType.Constant, 1)),
                },
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.SetVariable,
                        self,
                        new CompiledValue(ValueSourceType.Constant, 1),
                        referenceId: ResultCounter.Value),
                });
            var skill = new CompiledSkill(
                TestSkill,
                AttributeType.Energy,
                0,
                new[] { writeAndDamage.Id, observeSkillVariable.Id },
                string.Empty,
                "skill_logic_variable_self_test",
                new[]
                {
                    new CompiledBattleLogicData(
                        DamageRate,
                        "damage_rate",
                        BattleLogicDataKind.Parameter,
                        BattleLogicDataValueType.Integer,
                        BattleLogicVariableScope.Invocation,
                        5000),
                    new CompiledBattleLogicData(
                        BuffParameter,
                        "buff_id",
                        BattleLogicDataKind.Parameter,
                        BattleLogicDataValueType.Identifier,
                        BattleLogicVariableScope.Invocation,
                        TestBuff.Value),
                    new CompiledBattleLogicData(
                        InvocationValue,
                        "temp_damage",
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicDataValueType.Integer,
                        BattleLogicVariableScope.Invocation,
                        0),
                    new CompiledBattleLogicData(
                        CastCount,
                        "cast_count",
                        BattleLogicDataKind.RuntimeVariable,
                        BattleLogicDataValueType.Integer,
                        BattleLogicVariableScope.Skill,
                        0),
                });

            return new CompiledBattleDatabase(
                "logic-data-test-1",
                "1",
                0xBADDADAUL,
                new[] { skill },
                new[] { writeAndDamage, observeSkillVariable },
                new[]
                {
                    new CompiledBuff(
                        TestBuff,
                        1,
                        1,
                        BuffStackRule.RefreshDuration,
                        null),
                });
        }

        private static void RequireSuccess(BattleStepResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"战斗步骤失败：{result.AbortReason} / {result.Error}");
            }
        }
    }
}
