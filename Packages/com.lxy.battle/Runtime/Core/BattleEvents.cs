using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public enum BattleEventType
    {
        None = 0,
        EntitySpawned = 1,
        EntityDied = 2,
        SkillStarted = 3,
        SkillHit = 4,
        SkillFinished = 5,
        Damage = 6,
        Heal = 7,
        BuffApplied = 8,
        BuffTicked = 9,
        BuffRemoved = 10,
    }

    public struct BattleEvent
    {
        public int Frame;
        public BattleEventType Type;
        public int SourceEntityId;
        public int TargetEntityId;
        public int IntValue1;
        public int IntValue2;
        public FPVector2 Position;
    }

    public sealed class BattleEventCollector
    {
        private readonly List<BattleEvent> events =
            new List<BattleEvent>(32);
        private int currentFrame;

        public IReadOnlyList<BattleEvent> Events => events;

        public void BeginFrame(int frame)
        {
            currentFrame = frame;
            events.Clear();
        }

        public void Add(BattleEvent battleEvent)
        {
            battleEvent.Frame = currentFrame;
            events.Add(battleEvent);
        }
    }

    public interface IBattleEventSink
    {
        void ProcessEvents(IReadOnlyList<BattleEvent> battleEvents);
    }
}
