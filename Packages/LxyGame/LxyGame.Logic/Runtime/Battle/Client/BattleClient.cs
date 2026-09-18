using System;
using System.Collections.Generic;
using Game.Battle.Core;
using Game.Battle.Protocol;

namespace Game.Battle.Client
{
    public readonly struct BattleFrameSnapshot
    {
        /// <summary>
        /// 创建战斗帧快照实例。
        /// </summary>
        public BattleFrameSnapshot(int frame, ulong stateHash)
        {
            Frame = frame;
            StateHash = stateHash;
        }

        /// <summary>
        /// 向调用方提供帧。
        /// </summary>
        public int Frame { get; }
        /// <summary>
        /// 向调用方提供状态哈希。
        /// </summary>
        public ulong StateHash { get; }
    }

    public sealed class BattleClient
    {
        private readonly BattleWorld world;
        private readonly FrameBuffer frameBuffer;
        private readonly IBattleEventSink eventSink;
        private readonly List<FrameData> replayFrames =
            new List<FrameData>();
        private readonly List<BattleFrameSnapshot> snapshots =
            new List<BattleFrameSnapshot>();

        /// <summary>
        /// 创建战斗客户端实例。
        /// </summary>
        public BattleClient(
            BattleWorld world,
            FrameBuffer frameBuffer,
            IBattleEventSink eventSink = null)
        {
            this.world = world ??
                         throw new ArgumentNullException(nameof(world));
            this.frameBuffer = frameBuffer ??
                               throw new ArgumentNullException(
                                   nameof(frameBuffer));
            this.eventSink = eventSink;
        }

        /// <summary>
        /// 向调用方提供ReplayFrames。
        /// </summary>
        public IReadOnlyList<FrameData> ReplayFrames => replayFrames;
        /// <summary>
        /// 向调用方提供状态Snapshots。
        /// </summary>
        public IReadOnlyList<BattleFrameSnapshot> StateSnapshots =>
            snapshots;

        /// <summary>
        /// 尝试推进一帧，并返回是否成功。
        /// </summary>
        public bool TryAdvanceOneFrame()
        {
            if (!frameBuffer.TryGetFrame(
                    world.CurrentFrame,
                    out FrameData frameData))
            {
                return false;
            }

            replayFrames.Add(frameData.Clone());
            world.Tick(frameData);
            eventSink?.ProcessEvents(world.Events.Events);
            snapshots.Add(
                new BattleFrameSnapshot(
                    world.CurrentFrame,
                    world.CalculateStateHash()));
            return true;
        }

        /// <summary>
        /// 消费可用项帧。
        /// </summary>
        public int ConsumeAvailableFrames(int maxFrames = 8)
        {
            int consumed = 0;
            while (consumed < maxFrames && TryAdvanceOneFrame())
            {
                consumed++;
            }

            return consumed;
        }
    }
}
