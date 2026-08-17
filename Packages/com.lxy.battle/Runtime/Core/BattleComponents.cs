using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public readonly struct BattleEntity
    {
        public BattleEntity(int entityId, int playerId, int teamId)
        {
            EntityId = entityId;
            PlayerId = playerId;
            TeamId = teamId;
        }

        public int EntityId { get; }
        public int PlayerId { get; }
        public int TeamId { get; }
    }

    public struct TransformComponent
    {
        public FPVector2 Position;
        public FPVector2 Forward;
    }

    public struct MovementComponent
    {
        public FPVector2 Direction;
        public FP BaseSpeed;
        public bool IsMoving;
    }

    public struct HealthComponent
    {
        public int Current;
        public int Maximum;
        public bool IsDead;
    }

    public struct CollisionComponent
    {
        public BattleCollisionShape Shape;
        public FPVector2 HalfExtents;
        public FP RotationDegrees;
    }

    public struct ActiveSkillState
    {
        public int SkillId;
        public int TargetEntityId;
        public FPVector2 TargetPosition;
        public FPVector2 CastForward;
        public int StartFrame;
        public int NextOperationIndex;
    }

    public sealed class SkillComponent
    {
        private readonly Dictionary<int, int> cooldownEndFrames =
            new Dictionary<int, int>();

        public bool HasActiveSkill { get; set; }
        public ActiveSkillState ActiveSkill { get; set; }

        public int GetCooldownEndFrame(int skillId)
        {
            return cooldownEndFrames.TryGetValue(
                skillId,
                out int frame)
                ? frame
                : 0;
        }

        public void SetCooldownEndFrame(int skillId, int frame)
        {
            cooldownEndFrames[skillId] = frame;
        }

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
        public int BuffId;
        public int SourceEntityId;
        public int StartFrame;
        public int EndFrame;
        public int NextTickFrame;
        public int Stacks;
    }

    public sealed class BuffComponent
    {
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

        public int Count => values.Count;

        public void Add(int entityId, T value)
        {
            values.Add(entityId, value);
        }

        public bool Has(int entityId) =>
            values.ContainsKey(entityId);

        public bool TryGet(int entityId, out T value) =>
            values.TryGetValue(entityId, out value);

        public T Get(int entityId)
        {
            if (!values.TryGetValue(entityId, out T value))
            {
                throw new KeyNotFoundException(
                    $"实体 {entityId} 缺少组件 {typeof(T).Name}。");
            }

            return value;
        }

        public void Set(int entityId, T value)
        {
            if (!values.ContainsKey(entityId))
            {
                throw new KeyNotFoundException(
                    $"实体 {entityId} 缺少组件 {typeof(T).Name}。");
            }

            values[entityId] = value;
        }

        public bool Remove(int entityId) => values.Remove(entityId);
    }
}
