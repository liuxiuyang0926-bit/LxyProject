using System.Collections.Generic;
using Game.Battle.Core.Math;
using Game.Battle.Protocol;

namespace Game.Battle.Client
{
    /// <summary>
    /// 无服务器本地帧源。接口形态与未来服务器广播 FrameData 保持一致，
    /// 替换网络层时 BattleWorld 和表现层无需改动。
    /// </summary>
    public sealed class LocalFrameSession
    {
        private readonly Dictionary<int, List<FrameCommand>> pending =
            new Dictionary<int, List<FrameCommand>>();
        private int nextSequence;

        /// <summary>
        /// 排队提交移动。
        /// </summary>
        public void QueueMove(
            int frame,
            int playerId,
            FPVector2 direction)
        {
            Add(
                frame,
                new FrameCommand
                {
                    PlayerId = playerId,
                    Sequence = NextSequence(),
                    CommandType = BattleCommandType.Move,
                    MoveX = direction.X.RawValue,
                    MoveY = direction.Y.RawValue,
                });
        }

        /// <summary>
        /// 排队提交停止移动。
        /// </summary>
        public void QueueStopMove(int frame, int playerId)
        {
            Add(
                frame,
                new FrameCommand
                {
                    PlayerId = playerId,
                    Sequence = NextSequence(),
                    CommandType = BattleCommandType.StopMove,
                });
        }

        /// <summary>
        /// 排队提交技能。
        /// </summary>
        public void QueueSkill(
            int frame,
            int playerId,
            int skillId,
            int targetEntityId,
            FPVector2 targetPosition)
        {
            Add(
                frame,
                new FrameCommand
                {
                    PlayerId = playerId,
                    Sequence = NextSequence(),
                    CommandType = BattleCommandType.CastSkill,
                    SkillId = skillId,
                    TargetId = targetEntityId,
                    TargetX = targetPosition.X.RawValue,
                    TargetY = targetPosition.Y.RawValue,
                });
        }

        /// <summary>
        /// 构建帧。
        /// </summary>
        public FrameData BuildFrame(int frame)
        {
            var frameData = new FrameData(frame);
            if (pending.TryGetValue(
                    frame,
                    out List<FrameCommand> commands))
            {
                frameData.Commands.AddRange(commands);
                pending.Remove(frame);
            }

            frameData.SortCommands();
            return frameData;
        }

        /// <summary>
        /// 执行NextSequence相关逻辑。
        /// </summary>
        private int NextSequence()
        {
            nextSequence = checked(nextSequence + 1);
            return nextSequence;
        }

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        private void Add(int frame, FrameCommand command)
        {
            if (!pending.TryGetValue(
                    frame,
                    out List<FrameCommand> commands))
            {
                commands = new List<FrameCommand>();
                pending.Add(frame, commands);
            }

            commands.Add(command);
        }
    }
}
