using System;
using System.Collections.Generic;

namespace Game.Battle.Protocol
{
    public enum BattleCommandType
    {
        None = 0,
        Move = 1,
        StopMove = 2,
        CastSkill = 3,
    }

    [Serializable]
    public struct FrameCommand
    {
        /// <summary>
        /// 公开的玩家标识数据。
        /// </summary>
        public int PlayerId;
        /// <summary>
        /// 公开的Sequence数据。
        /// </summary>
        public int Sequence;
        /// <summary>
        /// 公开的Command类型数据。
        /// </summary>
        public BattleCommandType CommandType;
        /// <summary>
        /// 公开的MoveX数据。
        /// </summary>
        public long MoveX;
        /// <summary>
        /// 公开的MoveY数据。
        /// </summary>
        public long MoveY;
        /// <summary>
        /// 公开的技能标识数据。
        /// </summary>
        public int SkillId;
        /// <summary>
        /// 公开的目标标识数据。
        /// </summary>
        public int TargetId;
        /// <summary>
        /// 公开的目标X数据。
        /// </summary>
        public long TargetX;
        /// <summary>
        /// 公开的目标Y数据。
        /// </summary>
        public long TargetY;

        /// <summary>
        /// 执行比较相关逻辑。
        /// </summary>
        public static int Compare(
            FrameCommand left,
            FrameCommand right)
        {
            int playerResult =
                left.PlayerId.CompareTo(right.PlayerId);
            return playerResult != 0
                ? playerResult
                : left.Sequence.CompareTo(right.Sequence);
        }
    }

    [Serializable]
    public sealed class FrameData
    {
        /// <summary>
        /// 公开的帧数据。
        /// </summary>
        public int Frame;
        /// <summary>
        /// 公开的Commands数据。
        /// </summary>
        public readonly List<FrameCommand> Commands =
            new List<FrameCommand>();

        /// <summary>
        /// 创建帧Data实例。
        /// </summary>
        public FrameData()
        {
        }

        /// <summary>
        /// 创建帧Data实例。
        /// </summary>
        public FrameData(int frame)
        {
            Frame = frame;
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public FrameData Clone()
        {
            var clone = new FrameData(Frame);
            clone.Commands.AddRange(Commands);
            return clone;
        }

        /// <summary>
        /// 执行SortCommands相关逻辑。
        /// </summary>
        public void SortCommands()
        {
            Commands.Sort(FrameCommand.Compare);
        }
    }
}
