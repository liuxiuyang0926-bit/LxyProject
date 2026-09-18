using System;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.Core.Commands
{
    public enum BattleCommandType
    {
        StartBattle = 0,
        UseSkill = 1,
        EndTurn = 2,
        ExitBattle = 3,
    }

    public interface IBattleCommand
    {
        long CommandId { get; }
        BattleCommandType Type { get; }
    }

    public sealed class StartBattleCommand : IBattleCommand
    {
        public StartBattleCommand(long commandId) => CommandId = commandId;

        public long CommandId { get; }
        public BattleCommandType Type => BattleCommandType.StartBattle;
    }

    public sealed class UseSkillCommand : IBattleCommand
    {
        public UseSkillCommand(
            long commandId,
            UnitId caster,
            SkillId skill,
            UnitId[] targets)
        {
            CommandId = commandId;
            Caster = caster;
            Skill = skill;
            Targets = targets == null ? Array.Empty<UnitId>() : (UnitId[])targets.Clone();
        }

        public long CommandId { get; }
        public BattleCommandType Type => BattleCommandType.UseSkill;
        public UnitId Caster { get; }
        public SkillId Skill { get; }
        public UnitId[] Targets { get; }
    }

    public sealed class EndTurnCommand : IBattleCommand
    {
        public EndTurnCommand(long commandId, UnitId actor)
        {
            CommandId = commandId;
            Actor = actor;
        }

        public long CommandId { get; }
        public BattleCommandType Type => BattleCommandType.EndTurn;
        public UnitId Actor { get; }
    }

    public sealed class ExitBattleCommand : IBattleCommand
    {
        public ExitBattleCommand(long commandId) => CommandId = commandId;

        public long CommandId { get; }
        public BattleCommandType Type => BattleCommandType.ExitBattle;
    }
}
