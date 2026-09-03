using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public readonly struct BattleEntity
    {
        /// <summary>
        /// 创建战斗实体实例。
        /// </summary>
        public BattleEntity(int entityId, int playerId, int teamId)
        {
            EntityId = entityId;
            PlayerId = playerId;
            TeamId = teamId;
        }

        /// <summary>
        /// 向调用方提供实体标识。
        /// </summary>
        public int EntityId { get; }
        /// <summary>
        /// 向调用方提供Player标识。
        /// </summary>
        public int PlayerId { get; }
        /// <summary>
        /// 向调用方提供Team标识。
        /// </summary>
        public int TeamId { get; }
    }

    public struct TransformComponent
    {
        /// <summary>
        /// 公开的位置数据。
        /// </summary>
        public FPVector2 Position;
        /// <summary>
        /// 公开的Forward数据。
        /// </summary>
        public FPVector2 Forward;
    }

    public struct MovementComponent
    {
        /// <summary>
        /// 公开的Direction数据。
        /// </summary>
        public FPVector2 Direction;
        /// <summary>
        /// 公开的BaseSpeed数据。
        /// </summary>
        public FP BaseSpeed;
        /// <summary>
        /// 指示是否为Moving。
        /// </summary>
        public bool IsMoving;
    }

    public struct HealthComponent
    {
        /// <summary>
        /// 公开的当前数据。
        /// </summary>
        public int Current;
        /// <summary>
        /// 公开的最大数据。
        /// </summary>
        public int Maximum;
        /// <summary>
        /// 指示是否为Dead。
        /// </summary>
        public bool IsDead;
    }

    public struct CollisionComponent
    {
        /// <summary>
        /// 公开的形状数据。
        /// </summary>
        public BattleCollisionShape Shape;
        /// <summary>
        /// 公开的半范围数据。
        /// </summary>
        public FPVector2 HalfExtents;
        /// <summary>
        /// 公开的旋转角度数据。
        /// </summary>
        public FP RotationDegrees;
    }

    public struct ActiveSkillState
    {
        /// <summary>
        /// 公开的技能标识数据。
        /// </summary>
        public int SkillId;
        /// <summary>
        /// 公开的目标实体标识数据。
        /// </summary>
        public int TargetEntityId;
        /// <summary>
        /// 公开的目标位置数据。
        /// </summary>
        public FPVector2 TargetPosition;
        /// <summary>
        /// 公开的CastForward数据。
        /// </summary>
        public FPVector2 CastForward;
        /// <summary>
        /// 公开的Start帧数据。
        /// </summary>
        public int StartFrame;
        /// <summary>
        /// 公开的Next操作索引数据。
        /// </summary>
        public int NextOperationIndex;
    }

    public sealed class SkillComponent
    {
        private readonly Dictionary<int, int> cooldownEndFrames =
            new Dictionary<int, int>();

        /// <summary>
        /// 指示是否具有Active技能。
        /// </summary>
        public bool HasActiveSkill { get; set; }
        /// <summary>
        /// 向调用方提供Active技能。
        /// </summary>
        public ActiveSkillState ActiveSkill { get; set; }

        /// <summary>
        /// 获取Cooldown结束帧。
        /// </summary>
        public int GetCooldownEndFrame(int skillId)
        {
            return cooldownEndFrames.TryGetValue(
                skillId,
                out int frame)
                ? frame
                : 0;
        }

        /// <summary>
        /// 设置Cooldown结束帧。
        /// </summary>
        public void SetCooldownEndFrame(int skillId, int frame)
        {
            cooldownEndFrames[skillId] = frame;
        }

        /// <summary>
        /// 获取SortedCooldown技能Ids。
        /// </summary>
        public int[] GetSortedCooldownSkillIds()
        {
            var result = new int[cooldownEndFrames.Count];
            cooldownEndFrames.Keys.CopyTo(result, 0);
            System.Array.Sort(result);
            return result;
        }
    }

    public struct ActiveBuffState
    {
        /// <summary>
        /// 公开的增益标识数据。
        /// </summary>
        public int BuffId;
        /// <summary>
        /// 公开的源数据实体标识数据。
        /// </summary>
        public int SourceEntityId;
        /// <summary>
        /// 公开的Start帧数据。
        /// </summary>
        public int StartFrame;
        /// <summary>
        /// 公开的End帧数据。
        /// </summary>
        public int EndFrame;
        /// <summary>
        /// 公开的NextTick帧数据。
        /// </summary>
        public int NextTickFrame;
        /// <summary>
        /// 公开的Stacks数据。
        /// </summary>
        public int Stacks;
    }

    public sealed class BuffComponent
    {
        /// <summary>
        /// 公开的激活Buffs数据。
        /// </summary>
        public readonly List<ActiveBuffState> ActiveBuffs =
            new List<ActiveBuffState>();
    }

    /// <summary>
    /// ECS 风格组件仓库。业务系统只按排序后的 EntityId 遍历，绝不依赖
    /// Dictionary 的内部枚举顺序。
    /// </summary>
    public sealed class ComponentStore<T>
    {
        private readonly Dictionary<int, T> values =
            new Dictionary<int, T>();

        /// <summary>
        /// 当前的数量。
        /// </summary>
        public int Count => values.Count;

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(int entityId, T value)
        {
            values.Add(entityId, value);
        }

        /// <summary>
        /// 执行判断是否相关逻辑。
        /// </summary>
        public bool Has(int entityId) =>
            values.ContainsKey(entityId);

        /// <summary>
        /// 尝试获取，并返回是否成功。
        /// </summary>
        public bool TryGet(int entityId, out T value) =>
            values.TryGetValue(entityId, out value);

        /// <summary>
        /// 执行获取相关逻辑。
        /// </summary>
        public T Get(int entityId)
        {
            if (!values.TryGetValue(entityId, out T value))
            {
                throw new KeyNotFoundException(
                    $"实体 {entityId} 缺少组件 {typeof(T).Name}。");
            }

            return value;
        }

        /// <summary>
        /// 执行设置相关逻辑。
        /// </summary>
        public void Set(int entityId, T value)
        {
            if (!values.ContainsKey(entityId))
            {
                throw new KeyNotFoundException(
                    $"实体 {entityId} 缺少组件 {typeof(T).Name}。");
            }

            values[entityId] = value;
        }

        /// <summary>
        /// 执行移除相关逻辑。
        /// </summary>
        public bool Remove(int entityId) => values.Remove(entityId);
    }
}
