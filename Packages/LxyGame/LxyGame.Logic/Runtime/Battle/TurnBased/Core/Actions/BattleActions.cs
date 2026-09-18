using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Services;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Actions
{
    public interface IBattleAction
    {
        void Execute(BattleContext context);
    }

    internal sealed class StartBattleAction : IBattleAction
    {
        public void Execute(BattleContext context) => context.Flow.Start(context);
    }

    internal sealed class CastSkillAction : IBattleAction
    {
        private readonly UnitId caster;
        private readonly CompiledSkill skill;
        private readonly UnitId[] targets;

        public CastSkillAction(UnitId caster, CompiledSkill skill, UnitId[] targets)
        {
            this.caster = caster;
            this.skill = skill;
            this.targets = targets;
        }

        public void Execute(BattleContext context)
        {
            context.Services.Resource.Spend(context, caster, skill.CostAttribute, skill.Cost);
            BattleUnit unit = context.World.GetUnit(caster);
            if (unit.Skills.TryGet(skill.Id, out SkillInstance instance))
            {
                instance.TriggerCount++;
            }

            context.EnqueueEvent(new SkillCastEvent(caster, skill.Id, targets));
        }
    }

    internal sealed class DamageAction : IBattleAction
    {
        private readonly DamageRequest request;

        public DamageAction(DamageRequest request) => this.request = request;

        public void Execute(BattleContext context) => context.Services.Damage.Apply(context, request);
    }

    internal sealed class HealAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;
        private readonly long value;

        public HealAction(UnitId source, UnitId target, long value)
        {
            this.source = source;
            this.target = target;
            this.value = value;
        }

        public void Execute(BattleContext context) => context.Services.Heal.Apply(context, source, target, value);
    }

    internal sealed class AddBuffAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;
        private readonly BuffId buff;

        public AddBuffAction(UnitId source, UnitId target, BuffId buff)
        {
            this.source = source;
            this.target = target;
            this.buff = buff;
        }

        public void Execute(BattleContext context) => context.Services.Buff.Add(context, source, target, buff);
    }

    internal sealed class RemoveBuffAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;
        private readonly BuffId buff;

        public RemoveBuffAction(UnitId source, UnitId target, BuffId buff)
        {
            this.source = source;
            this.target = target;
            this.buff = buff;
        }

        public void Execute(BattleContext context) => context.Services.Buff.Remove(context, source, target, buff);
    }

    internal sealed class ModifyAttributeAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;
        private readonly AttributeType attribute;
        private readonly long delta;

        public ModifyAttributeAction(UnitId source, UnitId target, AttributeType attribute, long delta)
        {
            this.source = source;
            this.target = target;
            this.attribute = attribute;
            this.delta = delta;
        }

        public void Execute(BattleContext context) =>
            context.Services.Resource.Modify(context, source, target, attribute, delta);
    }

    internal sealed class KillAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;

        public KillAction(UnitId source, UnitId target)
        {
            this.source = source;
            this.target = target;
        }

        public void Execute(BattleContext context) => context.Services.Death.Kill(context, source, target);
    }

    internal sealed class ReviveAction : IBattleAction
    {
        private readonly UnitId source;
        private readonly UnitId target;
        private readonly long hp;

        public ReviveAction(UnitId source, UnitId target, long hp)
        {
            this.source = source;
            this.target = target;
            this.hp = hp;
        }

        public void Execute(BattleContext context) => context.Services.Death.Revive(context, source, target, hp);
    }

    internal sealed class MovePositionAction : IBattleAction
    {
        private readonly UnitId target;
        private readonly int position;

        public MovePositionAction(UnitId target, int position)
        {
            this.target = target;
            this.position = position;
        }

        public void Execute(BattleContext context) => context.Services.Position.Move(context, target, position);
    }

    internal sealed class ModifyCounterAction : IBattleAction
    {
        private readonly BattleVariableKey key;
        private readonly long value;
        private readonly bool additive;

        public ModifyCounterAction(BattleVariableKey key, long value, bool additive)
        {
            this.key = key;
            this.value = value;
            this.additive = additive;
        }

        public void Execute(BattleContext context)
        {
            if (additive)
            {
                context.World.Counters.Add(key, value);
            }
            else
            {
                context.World.Counters.Set(key, value);
            }
        }
    }

    internal sealed class EndTurnAction : IBattleAction
    {
        private readonly UnitId actor;

        public EndTurnAction(UnitId actor) => this.actor = actor;

        public void Execute(BattleContext context) => context.Services.Turn.End(context, actor);
    }

    internal sealed class EndBattleAction : IBattleAction
    {
        private readonly BattleResult result;

        public EndBattleAction(BattleResult result) => this.result = result;

        public void Execute(BattleContext context) => context.Flow.End(context, result);
    }
}
