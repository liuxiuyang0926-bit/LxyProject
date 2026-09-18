using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Core;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Session;

namespace Game.Battle.TurnBased.Adapters.Replay
{
    public sealed class BattleReplay
    {
        private readonly List<IBattleCommand> commands = new List<IBattleCommand>();

        public BattleReplay(BattleConfigSnapshot config)
        {
            Config = config;
        }

        public BattleConfigSnapshot Config { get; }
        public IReadOnlyList<IBattleCommand> Commands => commands;

        public void Record(IBattleCommand command)
        {
            commands.Add(Clone(command));
        }

        public BattleStepResult[] Play(BattleSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            BattleConfigSnapshot target = session.Context.Config;
            if (target.LogicVersion != Config.LogicVersion ||
                target.RuntimeDataVersion != Config.RuntimeDataVersion ||
                target.ConfigHash != Config.ConfigHash ||
                target.Seed != Config.Seed)
            {
                throw new InvalidOperationException("Replay 与目标 BattleSession 的版本、配置或 Seed 不一致。");
            }

            var results = new BattleStepResult[commands.Count];
            for (int index = 0; index < commands.Count; index++)
            {
                results[index] = session.Step(Clone(commands[index]));
                if (!results[index].Success)
                {
                    break;
                }
            }

            return results;
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
                        (Game.Battle.TurnBased.Domain.UnitId[])skill.Targets.Clone());
                case EndTurnCommand end:
                    return new EndTurnCommand(end.CommandId, end.Actor);
                case ExitBattleCommand exit:
                    return new ExitBattleCommand(exit.CommandId);
                default:
                    throw new NotSupportedException($"Replay 不支持 Command：{command?.Type}");
            }
        }
    }
}
