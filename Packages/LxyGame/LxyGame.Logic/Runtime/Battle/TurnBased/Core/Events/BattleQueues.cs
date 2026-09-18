using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Core.Actions;

namespace Game.Battle.TurnBased.Core.Events
{
    public sealed class BattleEventQueue
    {
        private readonly Queue<BattleEventBase> queue = new Queue<BattleEventBase>();

        public int Count => queue.Count;

        internal void Enqueue(BattleEventBase battleEvent)
        {
            queue.Enqueue(battleEvent ?? throw new ArgumentNullException(nameof(battleEvent)));
        }

        internal bool TryDequeue(out BattleEventBase battleEvent)
        {
            if (queue.Count > 0)
            {
                battleEvent = queue.Dequeue();
                return true;
            }

            battleEvent = null;
            return false;
        }

        internal void Clear() => queue.Clear();
    }

    public sealed class BattleActionQueue
    {
        private readonly Queue<IBattleAction> queue = new Queue<IBattleAction>();

        public int Count => queue.Count;

        internal void Enqueue(IBattleAction action)
        {
            queue.Enqueue(action ?? throw new ArgumentNullException(nameof(action)));
        }

        internal bool TryDequeue(out IBattleAction action)
        {
            if (queue.Count > 0)
            {
                action = queue.Dequeue();
                return true;
            }

            action = null;
            return false;
        }

        internal void Clear() => queue.Clear();
    }
}
