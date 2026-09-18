using System;
using System.Collections.Generic;
using Game.Battle.Protocol;

namespace Game.Battle.Client
{
    public sealed class FrameBuffer
    {
        private readonly Dictionary<int, FrameData> frames =
            new Dictionary<int, FrameData>();

        /// <summary>
        /// 当前的数量。
        /// </summary>
        public int Count => frames.Count;

        /// <summary>
        /// 添加帧。
        /// </summary>
        public void AddFrame(FrameData frameData)
        {
            if (frameData == null)
            {
                throw new ArgumentNullException(nameof(frameData));
            }

            frames[frameData.Frame] = frameData;
        }

        /// <summary>
        /// 尝试获取帧，并返回是否成功。
        /// </summary>
        public bool TryGetFrame(int frame, out FrameData frameData)
        {
            if (!frames.TryGetValue(frame, out frameData))
            {
                return false;
            }

            frames.Remove(frame);
            return true;
        }

        /// <summary>
        /// 执行清空相关逻辑。
        /// </summary>
        public void Clear()
        {
            frames.Clear();
        }
    }
}
