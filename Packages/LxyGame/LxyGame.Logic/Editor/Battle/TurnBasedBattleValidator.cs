using System;
using Game.Battle.TurnBased.Application;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Session;
using Game.Battle.TurnBased.Core.Trace;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class TurnBasedBattleValidator
    {
        private static readonly SkillId NormalAttackId = new SkillId(1001);
        private static readonly SkillId PassiveId = new SkillId(1002);
        private static readonly SkillId PoisonSkillId = new SkillId(1003);
        private static readonly BuffId PoisonBuffId = new BuffId(2001);
        private static readonly BattleVariableKey KillCounter = new BattleVariableKey(3001);

        [MenuItem("工具/战斗/运行回合制架构自检")]
        public static void Validate()
        {
            CompiledBattleDatabase database = CreateDatabase();
            ValidateDamageAndDeath(database);
            ValidateBuffAndReplayDeterminism(database);
            Debug.Log("[TurnBasedBattle] 架构自检通过：Command/Action/Event/Rule/Buff/Replay 均确定性一致。");
        }

        private static void ValidateDamageAndDeath(CompiledBattleDatabase database)
        {
            using (BattleSession session = CreateSession(database, 30, 0x1234567))
            {
                RequireSuccess(session.Step(new StartBattleCommand(1)));
                BattleStepResult attack = session.Step(new UseSkillCommand(
                    2,
                    new UnitId(1),
                    NormalAttackId,
                    new[] { new UnitId(2) }));
                RequireSuccess(attack);

                if (!session.IsFinished ||
                    session.Context.Flow.Result != BattleResult.AttackerVictory)
                {
                    throw new InvalidOperationException("普通攻击致死后没有得到攻击方胜利结果。");
                }

                if (session.Context.World.Counters.Get(KillCounter) != 1)
                {
                    throw new InvalidOperationException("死亡被动规则没有稳定增加击杀计数。");
                }

                bool hasDamage = false;
                bool hasDeath = false;
                for (int index = 0; index < attack.Events.Count; index++)
                {
                    hasDamage |= attack.Events[index] is DamageResolvedEvent;
                    hasDeath |= attack.Events[index] is UnitLifeEvent life && !life.Revived;
                }

                if (!hasDamage || !hasDeath)
                {
                    throw new InvalidOperationException("普通攻击闭环缺少伤害或死亡领域事件。");
                }
            }
        }

        private static void ValidateBuffAndReplayDeterminism(CompiledBattleDatabase database)
        {
            using (BattleSession first = CreateSession(database, 200, 0x2468135))
            using (BattleSession second = CreateSession(database, 200, 0x2468135))
            {
                IBattleCommand[] commands =
                {
                    new StartBattleCommand(1),
                    new UseSkillCommand(
                        2,
                        new UnitId(1),
                        PoisonSkillId,
                        new[] { new UnitId(2) }),
                    new EndTurnCommand(3, new UnitId(1)),
                    new EndTurnCommand(4, new UnitId(2)),
                };

                for (int index = 0; index < commands.Length; index++)
                {
                    BattleStepResult left = first.Step(commands[index]);
                    BattleStepResult right = second.Step(Clone(commands[index]));
                    RequireSuccess(left);
                    RequireSuccess(right);
                    if (left.StateHash != right.StateHash || left.Events.Count != right.Events.Count)
                    {
                        throw new InvalidOperationException(
                            $"相同 Seed 与 Command 在第 {index} 步产生了不同结果。");
                    }

                    for (int eventIndex = 0; eventIndex < left.Events.Count; eventIndex++)
                    {
                        IBattleEvent leftEvent = left.Events[eventIndex];
                        IBattleEvent rightEvent = right.Events[eventIndex];
                        if (leftEvent.Type != rightEvent.Type ||
                            leftEvent.SequenceId != rightEvent.SequenceId ||
                            leftEvent.Source != rightEvent.Source ||
                            leftEvent.Target != rightEvent.Target ||
                            leftEvent.Value != rightEvent.Value)
                        {
                            throw new InvalidOperationException(
                                $"第 {index} 步的事件序列在 {eventIndex} 处不一致。");
                        }
                    }
                }

                BattleUnit defender = first.Context.World.GetUnit(new UnitId(2));
                if (defender.Attributes.Get(AttributeType.Hp) != 190 ||
                    !defender.Buffs.Has(PoisonBuffId))
                {
                    throw new InvalidOperationException("Poison Buff 的回合触发或持续时间错误。");
                }
            }
        }

        private static BattleSession CreateSession(
            CompiledBattleDatabase database,
            long defenderHp,
            int seed)
        {
            BattleUnit attacker = CreateUnit(
                1,
                101,
                BattleCamp.Attacker,
                0,
                100,
                40,
                5,
                100);
            attacker.Skills.Add(new SkillInstance(NormalAttackId, attacker.Id));
            attacker.Skills.Add(new SkillInstance(PassiveId, attacker.Id));
            attacker.Skills.Add(new SkillInstance(PoisonSkillId, attacker.Id));

            BattleUnit defender = CreateUnit(
                2,
                202,
                BattleCamp.Defender,
                1,
                defenderHp,
                20,
                10,
                50);
            defender.Skills.Add(new SkillInstance(NormalAttackId, defender.Id));

            return new BattleApplication(database).CreateSession(
                new[] { attacker, defender },
                seed,
                traceLevel: BattleTraceLevel.DebugDeterminism);
        }

        private static BattleUnit CreateUnit(
            int id,
            int configId,
            BattleCamp camp,
            int position,
            long hp,
            long attack,
            long defense,
            long speed)
        {
            var unit = new BattleUnit(new UnitId(id), configId, camp, position);
            unit.InitializeHealth(hp);
            unit.InitializeAttribute(AttributeType.Attack, attack);
            unit.InitializeAttribute(AttributeType.Defense, defense);
            unit.InitializeAttribute(AttributeType.Speed, speed);
            unit.InitializeAttribute(AttributeType.Energy, 100);
            return unit;
        }

        private static CompiledBattleDatabase CreateDatabase()
        {
            var self = new CompiledTargetSelector(TargetSelectorType.Self);
            var eventTarget = new CompiledTargetSelector(TargetSelectorType.EventTarget);
            var deadEventTarget = new CompiledTargetSelector(
                TargetSelectorType.EventTarget,
                1,
                true);
            var selectedTargets = new CompiledTargetSelector(TargetSelectorType.SelectedTargets, 0);

            var normalAttackRule = new CompiledRule(
                new RuleId(1),
                BattleEventType.SkillCast,
                BattleEventPhase.Main,
                0,
                null,
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.Damage,
                        selectedTargets,
                        new CompiledValue(
                            ValueSourceType.OwnerAttribute,
                            attribute: AttributeType.Attack)),
                });
            var deathPassiveRule = new CompiledRule(
                new RuleId(2),
                BattleEventType.UnitDead,
                BattleEventPhase.After,
                0,
                new[]
                {
                    new CompiledCondition(
                        CompiledConditionType.ConfigIdCompare,
                        deadEventTarget,
                        ComparisonOperator.Equal,
                        202),
                },
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.AddCounter,
                        self,
                        new CompiledValue(ValueSourceType.Constant, 1),
                        referenceId: KillCounter.Value),
                },
                false);
            var addPoisonRule = new CompiledRule(
                new RuleId(3),
                BattleEventType.SkillCast,
                BattleEventPhase.Main,
                0,
                null,
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.AddBuff,
                        selectedTargets,
                        referenceId: PoisonBuffId.Value),
                });
            var poisonTickRule = new CompiledRule(
                new RuleId(4),
                BattleEventType.TurnStarted,
                BattleEventPhase.Main,
                0,
                new[]
                {
                    new CompiledCondition(CompiledConditionType.IsSelf, eventTarget),
                },
                new[]
                {
                    new CompiledAction(
                        CompiledActionType.Damage,
                        self,
                        new CompiledValue(ValueSourceType.Constant, 10),
                        flags: CompiledActionFlags.IgnoreDefense),
                });

            return new CompiledBattleDatabase(
                "1.0.0",
                "1",
                0xC0DEC0DEUL,
                new[]
                {
                    new CompiledSkill(
                        NormalAttackId,
                        AttributeType.Energy,
                        0,
                        new[] { normalAttackRule.Id },
                        "NormalAttack"),
                    new CompiledSkill(
                        PassiveId,
                        AttributeType.Energy,
                        0,
                        new[] { deathPassiveRule.Id },
                        string.Empty),
                    new CompiledSkill(
                        PoisonSkillId,
                        AttributeType.Energy,
                        10,
                        new[] { addPoisonRule.Id },
                        "Poison"),
                },
                new[]
                {
                    normalAttackRule,
                    deathPassiveRule,
                    addPoisonRule,
                    poisonTickRule,
                },
                new[]
                {
                    new CompiledBuff(
                        PoisonBuffId,
                        2,
                        5,
                        BuffStackRule.AddStack,
                        new[] { poisonTickRule.Id }),
                });
        }

        private static void RequireSuccess(BattleStepResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"战斗步骤失败：{result.AbortReason}，{result.Error}");
            }
        }

        private static IBattleCommand Clone(IBattleCommand command)
        {
            switch (command)
            {
                case StartBattleCommand start:
                    return new StartBattleCommand(start.CommandId);
                case UseSkillCommand skill:
                    return new UseSkillCommand(
                        skill.CommandId,
                        skill.Caster,
                        skill.Skill,
                        (UnitId[])skill.Targets.Clone());
                case EndTurnCommand end:
                    return new EndTurnCommand(end.CommandId, end.Actor);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
