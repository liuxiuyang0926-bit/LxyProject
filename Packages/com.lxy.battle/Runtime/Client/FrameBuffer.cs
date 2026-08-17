using System;
using System.Collections.Generic;
using Game.Battle.Protocol;

namespace Game.Battle.Client
{
    public sealed class FrameBuffer
    {
        private readonly Dictionary<int, FrameData> frames =
            new Dictionary<int, FrameData>();

        public int Count => frames.Count;

        public void AddFrame(FrameData frameData)
        {
            if (frameData == null)
            {
                throw new ArgumentNullException(nameof(frameData));
            }

            frames[frameData.Frame] = frameData;
        }

        public bool TryGetFrame(int frame, out FrameData frameData)
        {
            if (!frames.TryGetValue(frame, out frameData))
            {
                return false;
            }

            frames.Remove(frame);
            return true;
        }

        public void Clear()
        {
            frames.Clear();
        }
    }
}
