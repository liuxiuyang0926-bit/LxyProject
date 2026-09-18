using System;
using Game.Battle.TurnBased.Core.Actions;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Commands
{
    public sealed class CommandProcessor
    {
        internal void Execute(BattleContext context, IBattleCommand command)
        {
            switch (command)
            {
                case StartBattleCommand _:
                    RequirePhase(context, BattlePhase.Created);
                    context.EnqueueAction(new StartBattleAction());
                    return;
                case UseSkillCommand useSkill:
                    ExecuteUseSkill(context, useSkill);
                    return;
                case EndTurnCommand endTurn:
                    ExecuteEndTurn(context, endTurn);
                    return;
                case ExitBattleCommand _:
                    if (context.Flow.IsFinished)
                    {
                        throw Invalid("战斗已经结束。");
                    }

                    context.Flow.BeginResolution();
                    context.EnqueueAction(new EndBattleAction(BattleResult.Exited));
                    return;
                default:
                    throw Invalid($"不支持的 Command：{command?.Type}");
            }
        }

        private static void ExecuteUseSkill(BattleContext context, UseSkillCommand command)
        {
            RequirePhase(context, BattlePhase.WaitingCommand);
            if (command.Caster != context.Flow.CurrentActor)
            {
                throw Invalid(
                    $"当前行动者是 {context.Flow.CurrentActor}，不能由 {command.Caster} 释放技能。");
            }

            BattleUnit caster = context.World.GetUnit(command.Caster);
            if (caster.IsDead ||
                !caster.Skills.TryGet(command.Skill, out SkillInstance instance) ||
                instance.Disabled || instance.Cooldown > 0)
            {
                throw Invalid($"技能不可用：Caster={command.Caster}，Skill={command.Skill}。");
            }

            if (!context.Database.TryGetSkill(command.Skill, out CompiledSkill skill))
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidRuntimeData,
                    $"技能运行时数据不存在：{command.Skill}");
            }

            if (!context.Services.Resource.CanSpend(caster, skill.CostAttribute, skill.Cost))
            {
                throw Invalid($"技能资源不足：Skill={command.Skill}。");
            }

            ValidateTargets(context, command.Targets);
            context.Flow.BeginResolution();
            context.EnqueueAction(new CastSkillAction(
                command.Caster,
                skill,
                (UnitId[])command.Targets.Clone()));
        }

        private static void ExecuteEndTurn(BattleContext context, EndTurnCommand command)
        {
            RequirePhase(context, BattlePhase.WaitingCommand);
            if (command.Actor != context.Flow.CurrentActor)
            {
                throw Invalid(
                    $"当前行动者是 {context.Flow.CurrentActor}，不能结束 {command.Actor} 的回合。");
            }

            context.Flow.BeginResolution();
            context.EnqueueAction(new EndTurnAction(command.Actor));
        }

        private static void ValidateTargets(BattleContext context, UnitId[] targets)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                UnitId target = targets[index];
                if (!target.IsValid || !context.World.TryGetUnit(target, out BattleUnit unit) || unit.IsDead)
                {
                    throw Invalid($"技能目标无效：{target}");
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (targets[previous] == target)
                    {
                        throw Invalid($"技能目标重复：{target}");
                    }
                }
            }
        }

        private static void RequirePhase(BattleContext context, BattlePhase expected)
        {
            if (context.Flow.Phase != expected)
            {
                throw Invalid(
                    $"Command 阶段错误：Expected={expected}，Current={context.Flow.Phase}。");
            }
        }

        private static BattleExecutionException Invalid(string message) =>
            new BattleExecutionException(BattleAbortReason.InvalidCommand, message);
    }
}
