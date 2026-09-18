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
        /// <summary>
        /// 公开的帧数据。
        /// </summary>
        public int Frame;
        /// <summary>
        /// 公开的类型数据。
        /// </summary>
        public BattleEventType Type;
        /// <summary>
        /// 公开的源数据实体标识数据。
        /// </summary>
        public int SourceEntityId;
        /// <summary>
        /// 公开的目标实体标识数据。
        /// </summary>
        public int TargetEntityId;
        /// <summary>
        /// 公开的IntValue1数据。
        /// </summary>
        public int IntValue1;
        /// <summary>
        /// 公开的IntValue2数据。
        /// </summary>
        public int IntValue2;
        /// <summary>
        /// 公开的位置数据。
        /// </summary>
        public FPVector2 Position;
    }

    public sealed class BattleEventCollector
    {
        private readonly List<BattleEvent> events =
            new List<BattleEvent>(32);
        private int currentFrame;

        /// <summary>
        /// 向调用方提供事件。
        /// </summary>
        public IReadOnlyList<BattleEvent> Events => events;

        /// <summary>
        /// 执行Begin帧相关逻辑。
        /// </summary>
        public void BeginFrame(int frame)
        {
            currentFrame = frame;
            events.Clear();
        }

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(BattleEvent battleEvent)
        {
            battleEvent.Frame = currentFrame;
            events.Add(battleEvent);
        }
    }

    public interface IBattleEventSink
    {
        /// <summary>
        /// 执行Process事件相关逻辑。
        /// </summary>
        void ProcessEvents(IReadOnlyList<BattleEvent> battleEvents);
    }
}
