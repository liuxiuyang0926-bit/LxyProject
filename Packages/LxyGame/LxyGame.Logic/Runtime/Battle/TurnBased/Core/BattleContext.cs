using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Core.Actions;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Flow;
using Game.Battle.TurnBased.Core.Random;
using Game.Battle.TurnBased.Core.Rules;
using Game.Battle.TurnBased.Core.Services;
using Game.Battle.TurnBased.Core.Trace;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Core
{
    public sealed class BattleContext : IDisposable
    {
        private long sequence;
        private long runtimeInstanceSequence;
        private long registrationSequence;
        private int eventCount;
        private int actionCount;
        private int ruleExecutionCount;
        private bool disposed;

        public BattleContext(
            BattleWorld world,
            IBattleRuntimeDatabase database,
            int randomSeed,
            BattleExecutionLimits? limits = null,
            BattleTraceLevel traceLevel = BattleTraceLevel.Normal)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Database = database ?? throw new ArgumentNullException(nameof(database));
            Limits = limits ?? BattleExecutionLimits.Default;
            Config = new BattleConfigSnapshot(
                database.LogicVersion,
                database.RuntimeDataVersion,
                database.ConfigHash,
                randomSeed);
            Random = new DeterministicBattleRandom(randomSeed);
            Events = new BattleEventQueue();
            Actions = new BattleActionQueue();
            Rules = new RuleEngine();
            LogicVariables = new BattleLogicVariableStore();
            Services = new BattleServices();
            Flow = new BattleFlow();
            Trace = new BattleTrace(traceLevel);
            Rules.RegisterInitialRules(this);
        }

        public BattleWorld World { get; }
        public IBattleRuntimeDatabase Database { get; }
        public BattleConfigSnapshot Config { get; }
        public BattleExecutionLimits Limits { get; }
        public IBattleRandom Random { get; }
        public BattleEventQueue Events { get; }
        public BattleActionQueue Actions { get; }
        public RuleEngine Rules { get; }
        public BattleLogicVariableStore LogicVariables { get; }
        public BattleServices Services { get; }
        public BattleFlow Flow { get; }
        public BattleTrace Trace { get; }
        public long StepIndex { get; internal set; }
        public bool IsDisposed => disposed;

        internal void BeginStep()
        {
            eventCount = 0;
            actionCount = 0;
            ruleExecutionCount = 0;
            StepIndex++;
        }

        internal void EnqueueEvent(BattleEventBase battleEvent)
        {
            eventCount++;
            if (eventCount > Limits.MaxEventsPerStep)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.EventLimit,
                    $"单步事件数量超过上限 {Limits.MaxEventsPerStep}。");
            }

            battleEvent.AssignSequence(NextSequence());
            Events.Enqueue(battleEvent);
        }

        internal void EnqueueAction(IBattleAction action)
        {
            actionCount++;
            if (actionCount > Limits.MaxActionsPerStep)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.ActionLimit,
                    $"单步 Action 数量超过上限 {Limits.MaxActionsPerStep}。");
            }

            Actions.Enqueue(action);
        }

        internal void CountRuleExecution()
        {
            ruleExecutionCount++;
            if (ruleExecutionCount > Limits.MaxRuleExecutionsPerStep)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.RuleExecutionLimit,
                    $"单步规则执行次数超过上限 {Limits.MaxRuleExecutionsPerStep}。");
            }
        }

        internal long NextRuntimeInstanceId() => ++runtimeInstanceSequence;
        internal long NextRegistrationSequence() => ++registrationSequence;

        internal void TraceEvent(BattleEventBase battleEvent, ulong worldHash)
        {
            Trace.Add(
                new BattleTraceEntry(
                    StepIndex,
                    battleEvent.SequenceId,
                    BattleTraceKind.Event,
                    Flow.Phase,
                    battleEvent.Source,
                    battleEvent.Target,
                    (int)battleEvent.Type,
                    battleEvent.Value,
                    worldHash),
                BattleTraceLevel.Normal);
        }

        internal void TraceRule(CompiledRule rule, in RuleContext ruleContext)
        {
            Trace.Add(
                new BattleTraceEntry(
                    StepIndex,
                    ruleContext.Event.SequenceId,
                    BattleTraceKind.Rule,
                    Flow.Phase,
                    ruleContext.Owner,
                    ruleContext.Target,
                    rule.Id.Value,
                    rule.Priority,
                    0),
                BattleTraceLevel.Verbose);
        }

        internal ulong CalculateStateHash()
        {
            var hash = new Game.Battle.Core.BattleHash();
            hash.Add(Config.ConfigHash);
            hash.Add(Config.Seed);
            hash.Add(sequence);
            hash.Add(runtimeInstanceSequence);
            hash.Add(registrationSequence);
            hash.Add((int)Flow.Phase);
            hash.Add((int)Flow.Result);
            hash.Add(Flow.RoundIndex);
            hash.Add(Flow.TurnIndex);
            hash.Add(Flow.CurrentActor.Value);
            hash.Add(Random.State);
            hash.Add(Random.CallCount);

            IReadOnlyList<UnitId> ids = World.UnitIds;
            hash.Add(ids.Count);
            for (int unitIndex = 0; unitIndex < ids.Count; unitIndex++)
            {
                BattleUnit unit = World.GetUnit(ids[unitIndex]);
                hash.Add(unit.Id.Value);
                hash.Add(unit.ConfigId);
                hash.Add((int)unit.Camp);
                hash.Add(unit.Position);
                hash.Add(unit.IsDead);
                for (int attributeIndex = 0;
                     attributeIndex < (int)AttributeType.Count;
                     attributeIndex++)
                {
                    hash.Add(unit.Attributes.Get((AttributeType)attributeIndex));
                }

                IReadOnlyList<SkillInstance> skills = unit.Skills.Items;
                hash.Add(skills.Count);
                for (int skillIndex = 0; skillIndex < skills.Count; skillIndex++)
                {
                    SkillInstance skill = skills[skillIndex];
                    hash.Add(skill.SkillId.Value);
                    hash.Add(skill.RuntimeInstanceId);
                    hash.Add(skill.TriggerCount);
                    hash.Add(skill.Cooldown);
                    hash.Add(skill.Disabled);
                }

                IReadOnlyList<BuffInstance> buffs = unit.Buffs.Items;
                hash.Add(buffs.Count);
                for (int buffIndex = 0; buffIndex < buffs.Count; buffIndex++)
                {
                    BuffInstance buff = buffs[buffIndex];
                    hash.Add(buff.InstanceId);
                    hash.Add(buff.BuffId.Value);
                    hash.Add(buff.Owner.Value);
                    hash.Add(buff.Source.Value);
                    hash.Add(buff.Stack);
                    hash.Add(buff.RemainingRounds);
                }
            }

            foreach (KeyValuePair<BattleVariableKey, long> pair in World.Counters.Items)
            {
                hash.Add(pair.Key.Value);
                hash.Add(pair.Value);
            }

            LogicVariables.AddToHash(ref hash);

            IReadOnlyList<TurnItem> turns = World.TurnTimeline.Items;
            hash.Add(turns.Count);
            for (int index = 0; index < turns.Count; index++)
            {
                hash.Add(turns[index].Unit.Value);
                hash.Add(turns[index].TimelineValue);
                hash.Add(turns[index].Speed);
            }

            return hash.Value;
        }

        internal void Abort(BattleAbortReason reason)
        {
            Events.Clear();
            Actions.Clear();
            Flow.Abort(reason);
        }

        private long NextSequence() => ++sequence;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Events.Clear();
            Actions.Clear();
            Rules.Clear();
            LogicVariables.Clear();
            Trace.Clear();
            Flow.Dispose();
        }
    }
}
