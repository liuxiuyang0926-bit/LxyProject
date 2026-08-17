using System;
using System.Collections.Generic;
using Game.Battle.Core;
using Game.Battle.Protocol;

namespace Game.Battle.Client
{
    public readonly struct BattleFrameSnapshot
    {
        public BattleFrameSnapshot(int frame, ulong stateHash)
        {
            Frame = frame;
            StateHash = stateHash;
        }

        public int Frame { get; }
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

        public IReadOnlyList<FrameData> ReplayFrames => replayFrames;
        public IReadOnlyList<BattleFrameSnapshot> StateSnapshots =>
            snapshots;

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
