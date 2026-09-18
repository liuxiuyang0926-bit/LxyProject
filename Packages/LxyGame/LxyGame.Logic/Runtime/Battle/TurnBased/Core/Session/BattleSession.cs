using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Core.Actions;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Trace;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.Core.Session
{
    public sealed class BattleStepResult
    {
        internal BattleStepResult(
            bool success,
            BattleAbortReason abortReason,
            string error,
            BattleEventBase[] events,
            ulong stateHash,
            BattlePhase phase,
            BattleResult battleResult)
        {
            Success = success;
            AbortReason = abortReason;
            Error = error ?? string.Empty;
            Events = events ?? Array.Empty<BattleEventBase>();
            StateHash = stateHash;
            Phase = phase;
            BattleResult = battleResult;
        }

        public bool Success { get; }
        public BattleAbortReason AbortReason { get; }
        public string Error { get; }
        public IReadOnlyList<BattleEventBase> Events { get; }
        public ulong StateHash { get; }
        public BattlePhase Phase { get; }
        public BattleResult BattleResult { get; }
    }

    public sealed class BattleSession : IDisposable
    {
        private readonly CommandProcessor commandProcessor = new CommandProcessor();
        private readonly HashSet<long> commandIds = new HashSet<long>();
        private readonly List<BattleEventBase> stepEvents = new List<BattleEventBase>(32);
        private bool disposed;

        public BattleSession(BattleContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public BattleContext Context { get; }
        public bool IsFinished => Context.Flow.IsFinished;
        public bool IsDisposed => disposed;

        public BattleStepResult Step(IBattleCommand command)
        {
            stepEvents.Clear();
            if (disposed || Context.IsDisposed)
            {
                return Failure(BattleAbortReason.Disposed, "BattleSession 已释放。");
            }

            if (IsFinished)
            {
                return Failure(BattleAbortReason.InvalidCommand, "战斗已经结束。");
            }

            if (command == null || command.CommandId <= 0 || !commandIds.Add(command.CommandId))
            {
                Context.Abort(BattleAbortReason.InvalidCommand);
                return Failure(BattleAbortReason.InvalidCommand, "Command 为空、ID 无效或重复。");
            }

            Context.BeginStep();
            try
            {
                commandProcessor.Execute(Context, command);
                DrainUntilStable();
                Context.Flow.CompleteResolution();
                ulong hash = Context.CalculateStateHash();
                Context.Trace.Add(
                    new BattleTraceEntry(
                        Context.StepIndex,
                        0,
                        BattleTraceKind.State,
                        Context.Flow.Phase,
                        UnitId.None,
                        UnitId.None,
                        0,
                        0,
                        hash),
                    BattleTraceLevel.Summary);
                return new BattleStepResult(
                    true,
                    BattleAbortReason.None,
                    string.Empty,
                    stepEvents.ToArray(),
                    hash,
                    Context.Flow.Phase,
                    Context.Flow.Result);
            }
            catch (BattleExecutionException exception)
            {
                Context.Abort(exception.Reason);
                return Failure(exception.Reason, exception.Message);
            }
            catch (OverflowException exception)
            {
                Context.Abort(BattleAbortReason.DeterminismViolation);
                return Failure(BattleAbortReason.DeterminismViolation, exception.Message);
            }
            catch (Exception exception)
            {
                Context.Abort(BattleAbortReason.InternalError);
                return Failure(BattleAbortReason.InternalError, exception.Message);
            }
        }

        private void DrainUntilStable()
        {
            while (true)
            {
                if (Context.Actions.TryDequeue(out IBattleAction action))
                {
                    action.Execute(Context);
                    continue;
                }

                if (Context.Events.TryDequeue(out BattleEventBase battleEvent))
                {
                    stepEvents.Add(battleEvent);
                    Context.TraceEvent(battleEvent, 0);
                    Context.Rules.Dispatch(Context, battleEvent);
                    continue;
                }

                if (Context.Flow.EvaluateBattleEnd(Context))
                {
                    continue;
                }

                break;
            }
        }

        private BattleStepResult Failure(BattleAbortReason reason, string error)
        {
            ulong hash = Context.IsDisposed ? 0 : Context.CalculateStateHash();
            return new BattleStepResult(
                false,
                reason,
                error,
                stepEvents.ToArray(),
                hash,
                Context.Flow.Phase,
                Context.Flow.Result);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            commandIds.Clear();
            stepEvents.Clear();
            Context.Dispose();
        }
    }
}
