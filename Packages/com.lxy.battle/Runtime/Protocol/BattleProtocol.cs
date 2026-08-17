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
        public int PlayerId;
        public int Sequence;
        public BattleCommandType CommandType;
        public long MoveX;
        public long MoveY;
        public int SkillId;
        public int TargetId;
        public long TargetX;
        public long TargetY;

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
        public int Frame;
        public readonly List<FrameCommand> Commands =
            new List<FrameCommand>();

        public FrameData()
        {
        }

        public FrameData(int frame)
        {
            Frame = frame;
        }

        public FrameData Clone()
        {
            var clone = new FrameData(Frame);
            clone.Commands.AddRange(Commands);
            return clone;
        }

        public void SortCommands()
        {
            Commands.Sort(FrameCommand.Compare);
        }
    }
}
