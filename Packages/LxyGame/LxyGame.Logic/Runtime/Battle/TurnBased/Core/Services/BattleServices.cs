using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Rules;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core.Services
{
    public readonly struct DamageRequest
    {
        public DamageRequest(
            UnitId attacker,
            UnitId target,
            long value,
            CompiledActionFlags flags)
        {
            Attacker = attacker;
            Target = target;
            Value = value;
            Flags = flags;
        }

        public UnitId Attacker { get; }
        public UnitId Target { get; }
        public long Value { get; }
        public CompiledActionFlags Flags { get; }
    }

    public sealed class BattleServices
    {
        public BattleServices()
        {
            Damage = new DamageService();
            Heal = new HealService();
            Buff = new BuffService();
            Target = new TargetService();
            Turn = new TurnService();
            Resource = new ResourceService();
            Summon = new SummonService();
            Position = new PositionService();
            Death = new DeathService();
        }

        public DamageService Damage { get; }
        public HealService Heal { get; }
        public BuffService Buff { get; }
        public TargetService Target { get; }
        public TurnService Turn { get; }
        public ResourceService Resource { get; }
        public SummonService Summon { get; }
        public PositionService Position { get; }
        public DeathService Death { get; }
    }

    public sealed class DamageService
    {
        public void Apply(BattleContext context, in DamageRequest request)
        {
            if (request.Value <= 0 ||
                !context.World.TryGetUnit(request.Attacker, out BattleUnit attacker) ||
                !context.World.TryGetUnit(request.Target, out BattleUnit target) ||
                attacker.IsDead || target.IsDead)
            {
                return;
            }

            bool miss = false;
            if ((request.Flags & CompiledActionFlags.CanMiss) != 0)
            {
                long hitChance = BattleNumeric.BasisPointOne +
                    attacker.Attributes.Get(AttributeType.HitRate) -
                    target.Attributes.Get(AttributeType.DodgeRate);
                hitChance = System.Math.Max(0, System.Math.Min(BattleNumeric.BasisPointOne, hitChance));
                miss = context.Random.RollBasisPoint() >= hitChance;
            }

            if (miss)
            {
                context.EnqueueEvent(new DamageResolvedEvent(
                    request.Attacker,
                    request.Target,
                    0,
                    false,
                    true,
                    false));
                return;
            }

            long damage = request.Value;
            if ((request.Flags & CompiledActionFlags.IgnoreDefense) == 0)
            {
                damage = System.Math.Max(1, damage - target.Attributes.Get(AttributeType.Defense));
            }

            bool critical = false;
            if ((request.Flags & CompiledActionFlags.CanCritical) != 0)
            {
                long criticalRate = System.Math.Max(
                    0,
                    System.Math.Min(
                        BattleNumeric.BasisPointOne,
                        attacker.Attributes.Get(AttributeType.CritRate)));
                critical = context.Random.RollBasisPoint() < criticalRate;
                if (critical)
                {
                    long scale = attacker.Attributes.Get(AttributeType.CritDamage);
                    if (scale <= 0)
                    {
                        scale = BattleNumeric.DefaultCritDamage;
                    }

                    damage = Scale(damage, scale);
                }
            }

            long shield = target.Attributes.Get(AttributeType.Shield);
            long absorbed = System.Math.Min(shield, damage);
            if (absorbed > 0)
            {
                target.Attributes.Set(AttributeType.Shield, shield - absorbed);
                damage -= absorbed;
            }

            long previousHp = target.Attributes.Get(AttributeType.Hp);
            long currentHp = System.Math.Max(0, previousHp - damage);
            target.Attributes.Set(AttributeType.Hp, currentHp);

            context.EnqueueEvent(new DamageResolvedEvent(
                request.Attacker,
                request.Target,
                damage,
                critical,
                false,
                currentHp == 0));
            if (currentHp != previousHp)
            {
                context.EnqueueEvent(new HpChangedEvent(
                    request.Attacker,
                    request.Target,
                    previousHp,
                    currentHp));
            }

            if (currentHp == 0)
            {
                context.Services.Death.Kill(context, request.Attacker, request.Target);
            }
        }

        private static long Scale(long value, long scaleBasisPoint)
        {
            FP result = FP.FromLong(value) * FP.FromRaw(scaleBasisPoint);
            return result.RawValue / FP.Precision;
        }
    }

    public sealed class HealService
    {
        public void Apply(BattleContext context, UnitId source, UnitId targetId, long value)
        {
            if (value <= 0 ||
                !context.World.TryGetUnit(targetId, out BattleUnit target) ||
                target.IsDead)
            {
                return;
            }

            long previous = target.Attributes.Get(AttributeType.Hp);
            long maximum = target.Attributes.Get(AttributeType.MaxHp);
            long current = System.Math.Min(maximum, checked(previous + value));
            long healed = current - previous;
            if (healed <= 0)
            {
                return;
            }

            target.Attributes.Set(AttributeType.Hp, current);
            context.EnqueueEvent(new HealResolvedEvent(source, targetId, healed));
            context.EnqueueEvent(new HpChangedEvent(source, targetId, previous, current));
        }
    }

    public sealed class ResourceService
    {
        public bool CanSpend(BattleUnit unit, AttributeType attribute, long value)
        {
            return value >= 0 && unit.Attributes.Get(attribute) >= value;
        }

        public void Spend(
            BattleContext context,
            UnitId unitId,
            AttributeType attribute,
            long value)
        {
            BattleUnit unit = context.World.GetUnit(unitId);
            if (!CanSpend(unit, attribute, value))
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidCommand,
                    $"单位 {unitId} 的 {attribute} 不足。");
            }

            if (value > 0)
            {
                unit.Attributes.Add(attribute, -value);
            }
        }

        public void Modify(
            BattleContext context,
            UnitId source,
            UnitId targetId,
            AttributeType attribute,
            long delta)
        {
            if (attribute == AttributeType.Hp)
            {
                if (delta >= 0)
                {
                    context.Services.Heal.Apply(context, source, targetId, delta);
                }
                else
                {
                    context.Services.Damage.Apply(
                        context,
                        new DamageRequest(
                            source,
                            targetId,
                            checked(-delta),
                            CompiledActionFlags.IgnoreDefense));
                }

                return;
            }

            BattleUnit target = context.World.GetUnit(targetId);
            long value = target.Attributes.Add(attribute, delta);
            if (attribute == AttributeType.Shield || attribute == AttributeType.Energy ||
                attribute == AttributeType.Anger)
            {
                target.Attributes.Set(attribute, System.Math.Max(0, value));
            }
        }
    }

    public sealed class DeathService
    {
        public void Kill(BattleContext context, UnitId source, UnitId targetId)
        {
            if (!context.World.TryGetUnit(targetId, out BattleUnit target) || target.IsDead)
            {
                return;
            }

            target.Attributes.Set(AttributeType.Hp, 0);
            target.IsDead = true;
            context.World.Grid.Remove(targetId);
            context.EnqueueEvent(new UnitLifeEvent(false, source, targetId));
        }

        public void Revive(BattleContext context, UnitId source, UnitId targetId, long hp)
        {
            BattleUnit target = context.World.GetUnit(targetId);
            if (!target.IsDead || hp <= 0)
            {
                return;
            }

            target.IsDead = false;
            target.Attributes.Set(
                AttributeType.Hp,
                System.Math.Min(hp, target.Attributes.Get(AttributeType.MaxHp)));
            context.World.Grid.Place(targetId, target.Position);
            context.World.TurnTimeline.Add(target);
            context.EnqueueEvent(new UnitLifeEvent(true, source, targetId));
        }
    }

    public sealed class PositionService
    {
        public void Move(BattleContext context, UnitId targetId, int position)
        {
            BattleUnit target = context.World.GetUnit(targetId);
            context.World.Grid.Place(targetId, position);
            target.Position = position;
        }
    }

    public sealed class SummonService
    {
        public void Summon(BattleContext context, UnitId source, BattleUnit unit)
        {
            context.World.AddUnit(unit);
            context.World.TurnTimeline.Add(unit);
            context.Rules.RegisterUnitSkills(context, unit);
            context.EnqueueEvent(new UnitSummonedEvent(source, unit.Id));
        }
    }

    public sealed class TurnService
    {
        public void End(BattleContext context, UnitId actor) => context.Flow.CompleteTurn(context, actor);
    }

    public sealed class BuffService
    {
        public void Add(BattleContext context, UnitId source, UnitId targetId, BuffId buffId)
        {
            if (!context.Database.TryGetBuff(buffId, out CompiledBuff definition))
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidRuntimeData,
                    $"Buff 数据不存在：{buffId}");
            }

            BattleUnit target = context.World.GetUnit(targetId);
            BuffInstance current = target.Buffs.Find(buffId);
            if (current != null && definition.StackRule != BuffStackRule.Independent)
            {
                if (definition.StackRule == BuffStackRule.Replace)
                {
                    RemoveInstance(context, source, target, current);
                    current = null;
                }
                else
                {
                    if (definition.StackRule == BuffStackRule.AddStack)
                    {
                        current.Stack = System.Math.Min(definition.MaxStack, current.Stack + 1);
                    }

                    current.RemainingRounds = definition.DurationRounds;
                    context.EnqueueEvent(new BuffChangedEvent(
                        true,
                        source,
                        targetId,
                        buffId,
                        current.InstanceId,
                        current.Stack));
                    return;
                }
            }

            var instance = new BuffInstance
            {
                InstanceId = context.NextRuntimeInstanceId(),
                BuffId = buffId,
                Owner = targetId,
                Source = source,
                Stack = 1,
                RemainingRounds = definition.DurationRounds,
            };
            target.Buffs.Add(instance);
            context.Rules.RegisterBuff(context, instance, definition);
            context.EnqueueEvent(new BuffChangedEvent(
                true,
                source,
                targetId,
                buffId,
                instance.InstanceId,
                instance.Stack));
        }

        public void Remove(BattleContext context, UnitId source, UnitId targetId, BuffId buffId)
        {
            BattleUnit target = context.World.GetUnit(targetId);
            BuffInstance instance = target.Buffs.Find(buffId);
            if (instance != null)
            {
                RemoveInstance(context, source, target, instance);
            }
        }

        internal void AdvanceRound(BattleContext context)
        {
            IReadOnlyList<UnitId> unitIds = context.World.UnitIds;
            var expired = new List<long>();
            for (int unitIndex = 0; unitIndex < unitIds.Count; unitIndex++)
            {
                BattleUnit unit = context.World.GetUnit(unitIds[unitIndex]);
                IReadOnlyList<BuffInstance> items = unit.Buffs.Items;
                expired.Clear();
                for (int buffIndex = 0; buffIndex < items.Count; buffIndex++)
                {
                    BuffInstance instance = items[buffIndex];
                    instance.RemainingRounds--;
                    if (instance.RemainingRounds <= 0)
                    {
                        expired.Add(instance.InstanceId);
                    }
                }

                for (int index = 0; index < expired.Count; index++)
                {
                    RemoveByInstanceId(context, unit, expired[index]);
                }
            }
        }

        private static void RemoveByInstanceId(BattleContext context, BattleUnit owner, long instanceId)
        {
            if (owner.Buffs.Remove(instanceId, out BuffInstance removed))
            {
                context.Rules.UnregisterRuntimeInstance(instanceId);
                context.EnqueueEvent(new BuffChangedEvent(
                    false,
                    removed.Source,
                    owner.Id,
                    removed.BuffId,
                    removed.InstanceId,
                    removed.Stack));
            }
        }

        private static void RemoveInstance(
            BattleContext context,
            UnitId source,
            BattleUnit owner,
            BuffInstance instance)
        {
            if (owner.Buffs.Remove(instance.InstanceId, out BuffInstance removed))
            {
                context.Rules.UnregisterRuntimeInstance(instance.InstanceId);
                context.EnqueueEvent(new BuffChangedEvent(
                    false,
                    source,
                    owner.Id,
                    removed.BuffId,
                    removed.InstanceId,
                    removed.Stack));
            }
        }
    }

    public sealed class TargetService
    {
        public void Resolve(
            BattleContext context,
            in RuleContext rule,
            in CompiledTargetSelector selector,
            List<UnitId> output)
        {
            output.Clear();
            switch (selector.Type)
            {
                case TargetSelectorType.Self:
                    TryAdd(context, rule.Owner, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.Attacker:
                    TryAdd(context, rule.Attacker, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.EventTarget:
                    TryAdd(context, rule.Target, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.SelectedTargets:
                    AddSelected(context, rule.Event as SkillCastEvent, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.AllAllies:
                    AddByCamp(context, rule.Owner, true, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.AllEnemies:
                    AddByCamp(context, rule.Owner, false, selector.IncludeDead, output);
                    break;
                case TargetSelectorType.RandomEnemy:
                    AddByCamp(context, rule.Owner, false, selector.IncludeDead, output);
                    ApplyConfigFilter(context, selector.ConfigIdFilter, output);
                    if (output.Count > 1)
                    {
                        UnitId selected = output[context.Random.NextInt(0, output.Count)];
                        output.Clear();
                        output.Add(selected);
                    }
                    break;
                case TargetSelectorType.LowestHp:
                    AddByCamp(context, rule.Owner, false, selector.IncludeDead, output);
                    ApplyConfigFilter(context, selector.ConfigIdFilter, output);
                    SelectLowestHp(context, output);
                    break;
                case TargetSelectorType.HighestAttack:
                    AddByCamp(context, rule.Owner, false, selector.IncludeDead, output);
                    ApplyConfigFilter(context, selector.ConfigIdFilter, output);
                    SelectHighestAttack(context, output);
                    break;
                case TargetSelectorType.FrontRowEnemy:
                    AddByCamp(context, rule.Owner, false, selector.IncludeDead, output);
                    ApplyConfigFilter(context, selector.ConfigIdFilter, output);
                    SelectFront(context, output);
                    break;
                default:
                    throw new BattleExecutionException(
                        BattleAbortReason.InvalidRuntimeData,
                        $"未知目标选择器：{selector.Type}");
            }

            int count = selector.Count;
            ApplyConfigFilter(context, selector.ConfigIdFilter, output);

            if (count > 0 && output.Count > count)
            {
                output.RemoveRange(count, output.Count - count);
            }
        }

        private static void ApplyConfigFilter(
            BattleContext context,
            int configIdFilter,
            List<UnitId> output)
        {
            if (configIdFilter <= 0)
            {
                return;
            }

            for (int index = output.Count - 1; index >= 0; index--)
            {
                if (context.World.GetUnit(output[index]).ConfigId != configIdFilter)
                {
                    output.RemoveAt(index);
                }
            }
        }

        private static void AddSelected(
            BattleContext context,
            SkillCastEvent castEvent,
            bool includeDead,
            List<UnitId> output)
        {
            if (castEvent == null)
            {
                return;
            }

            for (int index = 0; index < castEvent.SelectedTargets.Length; index++)
            {
                TryAdd(context, castEvent.SelectedTargets[index], includeDead, output);
            }
        }

        private static void AddByCamp(
            BattleContext context,
            UnitId ownerId,
            bool allies,
            bool includeDead,
            List<UnitId> output)
        {
            if (!context.World.TryGetUnit(ownerId, out BattleUnit owner))
            {
                return;
            }

            IReadOnlyList<UnitId> ids = context.World.UnitIds;
            for (int index = 0; index < ids.Count; index++)
            {
                BattleUnit candidate = context.World.GetUnit(ids[index]);
                bool campMatches = allies
                    ? candidate.Camp == owner.Camp
                    : candidate.Camp != owner.Camp && candidate.Camp != BattleCamp.Neutral;
                if (campMatches && (includeDead || !candidate.IsDead))
                {
                    output.Add(candidate.Id);
                }
            }
        }

        private static void TryAdd(
            BattleContext context,
            UnitId id,
            bool includeDead,
            List<UnitId> output)
        {
            if (id.IsValid && context.World.TryGetUnit(id, out BattleUnit unit) &&
                (includeDead || !unit.IsDead))
            {
                output.Add(id);
            }
        }

        private static void SelectLowestHp(BattleContext context, List<UnitId> output)
        {
            if (output.Count <= 1)
            {
                return;
            }

            UnitId selected = output[0];
            FP selectedRatio = GetHpRatio(context.World.GetUnit(selected));
            for (int index = 1; index < output.Count; index++)
            {
                UnitId candidate = output[index];
                FP ratio = GetHpRatio(context.World.GetUnit(candidate));
                if (ratio < selectedRatio || ratio == selectedRatio && candidate.CompareTo(selected) < 0)
                {
                    selected = candidate;
                    selectedRatio = ratio;
                }
            }

            output.Clear();
            output.Add(selected);
        }

        private static void SelectHighestAttack(BattleContext context, List<UnitId> output)
        {
            if (output.Count <= 1)
            {
                return;
            }

            UnitId selected = output[0];
            long selectedValue = context.World.GetUnit(selected).Attributes.Get(AttributeType.Attack);
            for (int index = 1; index < output.Count; index++)
            {
                UnitId candidate = output[index];
                long value = context.World.GetUnit(candidate).Attributes.Get(AttributeType.Attack);
                if (value > selectedValue || value == selectedValue && candidate.CompareTo(selected) < 0)
                {
                    selected = candidate;
                    selectedValue = value;
                }
            }

            output.Clear();
            output.Add(selected);
        }

        private static void SelectFront(BattleContext context, List<UnitId> output)
        {
            if (output.Count <= 1)
            {
                return;
            }

            UnitId selected = output[0];
            int position = context.World.GetUnit(selected).Position;
            for (int index = 1; index < output.Count; index++)
            {
                UnitId candidate = output[index];
                int candidatePosition = context.World.GetUnit(candidate).Position;
                if (candidatePosition < position ||
                    candidatePosition == position && candidate.CompareTo(selected) < 0)
                {
                    selected = candidate;
                    position = candidatePosition;
                }
            }

            output.Clear();
            output.Add(selected);
        }

        private static FP GetHpRatio(BattleUnit unit)
        {
            long maximum = unit.Attributes.Get(AttributeType.MaxHp);
            return maximum <= 0
                ? FP.Zero
                : FP.FromRatio(unit.Attributes.Get(AttributeType.Hp), maximum);
        }
    }
}
