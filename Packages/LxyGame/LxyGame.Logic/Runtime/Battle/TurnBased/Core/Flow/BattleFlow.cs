using System;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.Core.Flow
{
    public sealed class BattleFlow
    {
        private int completedTurnsInRound;

        public BattlePhase Phase { get; private set; } = BattlePhase.Created;
        public BattleResult Result { get; private set; }
        public int RoundIndex { get; private set; }
        public int TurnIndex { get; private set; }
        public UnitId CurrentActor { get; private set; }
        public bool IsFinished => Phase == BattlePhase.BattleEnd || Phase == BattlePhase.Aborted;

        internal void Start(BattleContext context)
        {
            if (Phase != BattlePhase.Created)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidCommand,
                    "战斗已经启动。");
            }

            if (context.World.CountAlive(BattleCamp.Attacker) == 0 ||
                context.World.CountAlive(BattleCamp.Defender) == 0)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidRuntimeData,
                    "战斗双方至少需要一个存活单位。");
            }

            context.World.TurnTimeline.Rebuild(context.World);
            RoundIndex = 1;
            TurnIndex = 1;
            completedTurnsInRound = 0;
            CurrentActor = context.World.TurnTimeline.CurrentActor;
            Phase = BattlePhase.Resolving;

            context.EnqueueEvent(new BattleStartedEvent());
            context.EnqueueEvent(new RoundStartedEvent(RoundIndex));
            context.EnqueueEvent(new TurnStartedEvent(CurrentActor, RoundIndex));
        }

        internal void BeginResolution()
        {
            if (!IsFinished)
            {
                Phase = BattlePhase.Resolving;
            }
        }

        internal void CompleteResolution()
        {
            if (!IsFinished)
            {
                Phase = BattlePhase.WaitingCommand;
            }
        }

        internal void CompleteTurn(BattleContext context, UnitId actor)
        {
            if (actor != CurrentActor)
            {
                throw new BattleExecutionException(
                    BattleAbortReason.InvalidCommand,
                    $"只能结束当前行动单位的回合：Current={CurrentActor}，Receive={actor}。");
            }

            Phase = BattlePhase.TurnEnd;
            context.EnqueueEvent(new TurnEndedEvent(actor, RoundIndex));
            context.World.TurnTimeline.Advance(context.World);
            completedTurnsInRound++;

            int aliveCount = context.World.CountAlive();
            if (aliveCount <= 0)
            {
                return;
            }

            if (completedTurnsInRound >= aliveCount)
            {
                context.Services.Buff.AdvanceRound(context);
                completedTurnsInRound = 0;
                RoundIndex++;
                context.EnqueueEvent(new RoundStartedEvent(RoundIndex));
            }

            TurnIndex++;
            CurrentActor = context.World.TurnTimeline.CurrentActor;
            Phase = BattlePhase.Resolving;
            if (CurrentActor.IsValid)
            {
                context.EnqueueEvent(new TurnStartedEvent(CurrentActor, RoundIndex));
            }
        }

        internal bool EvaluateBattleEnd(BattleContext context)
        {
            if (IsFinished)
            {
                return false;
            }

            int attackerCount = context.World.CountAlive(BattleCamp.Attacker);
            int defenderCount = context.World.CountAlive(BattleCamp.Defender);
            if (attackerCount > 0 && defenderCount > 0)
            {
                return false;
            }

            BattleResult result = attackerCount == 0 && defenderCount == 0
                ? BattleResult.Draw
                : attackerCount > 0
                    ? BattleResult.AttackerVictory
                    : BattleResult.DefenderVictory;
            End(context, result);
            return true;
        }

        internal void End(BattleContext context, BattleResult result)
        {
            if (IsFinished)
            {
                return;
            }

            Result = result;
            Phase = BattlePhase.BattleEnd;
            context.EnqueueEvent(new BattleEndedEvent(result));
        }

        internal void Abort(BattleAbortReason reason)
        {
            Result = BattleResult.Aborted;
            Phase = BattlePhase.Aborted;
            AbortReason = reason;
        }

        public BattleAbortReason AbortReason { get; private set; }

        internal void Dispose()
        {
            Phase = BattlePhase.Disposed;
            CurrentActor = UnitId.None;
        }
    }
}
