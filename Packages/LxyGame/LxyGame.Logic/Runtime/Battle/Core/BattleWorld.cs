using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;
using Game.Battle.Protocol;

namespace Game.Battle.Core
{
    /// <summary>
    /// 确定性战斗世界。此文件及 Core/Math/Protocol 目录禁止依赖
    /// UnityEngine、Time、物理系统或非确定性随机数。
    /// </summary>
    public sealed class BattleWorld
    {
        private readonly Dictionary<int, BattleEntity> entities =
            new Dictionary<int, BattleEntity>();
        private readonly List<int> entityIds = new List<int>();

        private readonly BattleCommandSystem commandSystem;
        private readonly BattleMovementSystem movementSystem;
        private readonly BattleSkillSystem skillSystem;
        private readonly BattleBuffSystem buffSystem;
        private readonly BattleDeathSystem deathSystem;

        /// <summary>
        /// 创建战斗世界实例。
        /// </summary>
        public BattleWorld(IBattleConfigProvider config, int randomSeed)
        {
            Config = config ??
                     throw new ArgumentNullException(nameof(config));
            Random = new BattleRandom(randomSeed);
            Events = new BattleEventCollector();

            commandSystem = new BattleCommandSystem();
            movementSystem = new BattleMovementSystem();
            skillSystem = new BattleSkillSystem();
            buffSystem = new BattleBuffSystem();
            deathSystem = new BattleDeathSystem();
        }

        /// <summary>
        /// 向调用方提供当前帧。
        /// </summary>
        public int CurrentFrame { get; private set; }
        /// <summary>
        /// 向调用方提供配置。
        /// </summary>
        public IBattleConfigProvider Config { get; }
        /// <summary>
        /// 向调用方提供Random。
        /// </summary>
        public BattleRandom Random { get; }
        /// <summary>
        /// 向调用方提供事件。
        /// </summary>
        public BattleEventCollector Events { get; }
        /// <summary>
        /// 向调用方提供实体Ids。
        /// </summary>
        public IReadOnlyList<int> EntityIds => entityIds;

        /// <summary>
        /// 向调用方提供Transforms。
        /// </summary>
        public ComponentStore<TransformComponent> Transforms { get; } =
            new ComponentStore<TransformComponent>();
        /// <summary>
        /// 向调用方提供Movements。
        /// </summary>
        public ComponentStore<MovementComponent> Movements { get; } =
            new ComponentStore<MovementComponent>();
        /// <summary>
        /// 向调用方提供Health。
        /// </summary>
        public ComponentStore<HealthComponent> Health { get; } =
            new ComponentStore<HealthComponent>();
        /// <summary>
        /// 向调用方提供Collisions。
        /// </summary>
        public ComponentStore<CollisionComponent> Collisions { get; } =
            new ComponentStore<CollisionComponent>();
        /// <summary>
        /// 向调用方提供Skills。
        /// </summary>
        public ComponentStore<SkillComponent> Skills { get; } =
            new ComponentStore<SkillComponent>();
        /// <summary>
        /// 向调用方提供Buffs。
        /// </summary>
        public ComponentStore<BuffComponent> Buffs { get; } =
            new ComponentStore<BuffComponent>();

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        public void Initialize()
        {
            CurrentFrame = 0;
            Events.BeginFrame(0);
        }

        /// <summary>
        /// 添加实体。
        /// </summary>
        public BattleEntity AddEntity(
            int entityId,
            int playerId,
            int teamId,
            FPVector2 position,
            int maximumHealth,
            FP moveSpeed,
            FPVector2? collisionHalfExtents = null,
            BattleCollisionShape collisionShape =
                BattleCollisionShape.Aabb,
            FP collisionRotationDegrees = default)
        {
            if (entityId <= 0 || playerId <= 0 || maximumHealth <= 0 ||
                moveSpeed <= FP.Zero)
            {
                throw new ArgumentException("实体初始参数无效。");
            }

            if (collisionHalfExtents.HasValue &&
                (collisionHalfExtents.Value.X <= FP.Zero ||
                 collisionHalfExtents.Value.Y <= FP.Zero))
            {
                throw new ArgumentException(
                    "实体碰撞盒半尺寸必须大于零。",
                    nameof(collisionHalfExtents));
            }

            var entity = new BattleEntity(entityId, playerId, teamId);
            entities.Add(entityId, entity);
            entityIds.Add(entityId);
            entityIds.Sort();

            Transforms.Add(
                entityId,
                new TransformComponent
                {
                    Position = position,
                    Forward = FPVector2.Right,
                });
            Movements.Add(
                entityId,
                new MovementComponent
                {
                    Direction = FPVector2.Zero,
                    BaseSpeed = moveSpeed,
                    IsMoving = false,
                });
            Health.Add(
                entityId,
                new HealthComponent
                {
                    Current = maximumHealth,
                    Maximum = maximumHealth,
                    IsDead = false,
                });
            FPVector2 halfExtents = collisionHalfExtents ??
                new FPVector2(
                    FP.FromRatio(1L, 2L),
                    FP.FromRatio(1L, 2L));
            Collisions.Add(
                entityId,
                new CollisionComponent
                {
                    Shape = collisionShape,
                    HalfExtents = halfExtents,
                    RotationDegrees =
                        FP.NormalizeAngle(collisionRotationDegrees),
                });
            Skills.Add(entityId, new SkillComponent());
            Buffs.Add(entityId, new BuffComponent());
            return entity;
        }

        /// <summary>
        /// 尝试获取实体，并返回是否成功。
        /// </summary>
        public bool TryGetEntity(int entityId, out BattleEntity entity) =>
            entities.TryGetValue(entityId, out entity);

        /// <summary>
        /// 尝试查找玩家实体，并返回是否成功。
        /// </summary>
        public bool TryFindPlayerEntity(
            int playerId,
            out BattleEntity entity)
        {
            for (int index = 0; index < entityIds.Count; index++)
            {
                BattleEntity candidate = entities[entityIds[index]];
                if (candidate.PlayerId == playerId)
                {
                    entity = candidate;
                    return true;
                }
            }

            entity = default;
            return false;
        }

        /// <summary>
        /// 执行判断是否Alive相关逻辑。
        /// </summary>
        public bool IsAlive(int entityId)
        {
            return Health.TryGet(entityId, out HealthComponent health) &&
                   !health.IsDead;
        }

        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(FrameData frameData)
        {
            if (frameData == null)
            {
                throw new ArgumentNullException(nameof(frameData));
            }

            if (frameData.Frame != CurrentFrame)
            {
                throw new InvalidOperationException(
                    $"逻辑帧不连续：Current={CurrentFrame}，" +
                    $"Receive={frameData.Frame}。");
            }

            Events.BeginFrame(CurrentFrame);
            frameData.SortCommands();
            commandSystem.Tick(this, frameData);
            movementSystem.Tick(this);
            skillSystem.Tick(this);
            buffSystem.Tick(this);
            deathSystem.Tick(this);
            CurrentFrame++;
        }

        /// <summary>
        /// 应用Damage。
        /// </summary>
        internal void ApplyDamage(
            int sourceEntityId,
            int targetEntityId,
            int value,
            bool fromBuff)
        {
            if (value <= 0 || !IsAlive(targetEntityId))
            {
                return;
            }

            HealthComponent health = Health.Get(targetEntityId);
            health.Current = System.Math.Max(0, health.Current - value);
            Health.Set(targetEntityId, health);
            Events.Add(
                new BattleEvent
                {
                    Type = fromBuff
                        ? BattleEventType.BuffTicked
                        : BattleEventType.Damage,
                    SourceEntityId = sourceEntityId,
                    TargetEntityId = targetEntityId,
                    IntValue1 = value,
                });
        }

        /// <summary>
        /// 应用Heal。
        /// </summary>
        internal void ApplyHeal(
            int sourceEntityId,
            int targetEntityId,
            int value,
            bool fromBuff)
        {
            if (value <= 0 || !IsAlive(targetEntityId))
            {
                return;
            }

            HealthComponent health = Health.Get(targetEntityId);
            health.Current = System.Math.Min(
                health.Maximum,
                health.Current + value);
            Health.Set(targetEntityId, health);
            Events.Add(
                new BattleEvent
                {
                    Type = fromBuff
                        ? BattleEventType.BuffTicked
                        : BattleEventType.Heal,
                    SourceEntityId = sourceEntityId,
                    TargetEntityId = targetEntityId,
                    IntValue1 = value,
                });
        }

        /// <summary>
        /// 应用增益。
        /// </summary>
        internal void ApplyBuff(
            int sourceEntityId,
            int targetEntityId,
            int buffId)
        {
            buffSystem.Apply(
                this,
                sourceEntityId,
                targetEntityId,
                buffId);
        }

        /// <summary>
        /// 获取移动SpeedMultiplier。
        /// </summary>
        internal FP GetMoveSpeedMultiplier(int entityId)
        {
            FP multiplier = FP.One;
            BuffComponent component = Buffs.Get(entityId);
            for (int index = 0;
                 index < component.ActiveBuffs.Count;
                 index++)
            {
                ActiveBuffState active = component.ActiveBuffs[index];
                if (!Config.TryGetBuff(
                        active.BuffId,
                        out BattleBuffDefinition definition) ||
                    definition.Type !=
                    BattleBuffType.MoveSpeedPercent)
                {
                    continue;
                }

                multiplier += FP.FromRatio(
                    (long)definition.Value * active.Stacks,
                    100L);
            }

            return FP.Max(FP.Zero, multiplier);
        }

        /// <summary>
        /// 计算状态哈希。
        /// </summary>
        public ulong CalculateStateHash()
        {
            var hash = new BattleHash();
            hash.Add(CurrentFrame);
            hash.Add(Random.State);
            for (int index = 0; index < entityIds.Count; index++)
            {
                int entityId = entityIds[index];
                BattleEntity entity = entities[entityId];
                TransformComponent transform = Transforms.Get(entityId);
                MovementComponent movement = Movements.Get(entityId);
                HealthComponent health = Health.Get(entityId);
                CollisionComponent collision =
                    Collisions.Get(entityId);
                SkillComponent skill = Skills.Get(entityId);
                BuffComponent buffs = Buffs.Get(entityId);

                hash.Add(entity.EntityId);
                hash.Add(entity.PlayerId);
                hash.Add(entity.TeamId);
                hash.Add(transform.Position.X.RawValue);
                hash.Add(transform.Position.Y.RawValue);
                hash.Add(transform.Forward.X.RawValue);
                hash.Add(transform.Forward.Y.RawValue);
                hash.Add(movement.Direction.X.RawValue);
                hash.Add(movement.Direction.Y.RawValue);
                hash.Add(movement.IsMoving);
                hash.Add(health.Current);
                hash.Add(health.Maximum);
                hash.Add(health.IsDead);
                hash.Add((int)collision.Shape);
                hash.Add(collision.HalfExtents.X.RawValue);
                hash.Add(collision.HalfExtents.Y.RawValue);
                hash.Add(collision.RotationDegrees.RawValue);
                hash.Add(skill.HasActiveSkill);
                if (skill.HasActiveSkill)
                {
                    ActiveSkillState active = skill.ActiveSkill;
                    hash.Add(active.SkillId);
                    hash.Add(active.TargetEntityId);
                    hash.Add(active.TargetPosition.X.RawValue);
                    hash.Add(active.TargetPosition.Y.RawValue);
                    hash.Add(active.CastForward.X.RawValue);
                    hash.Add(active.CastForward.Y.RawValue);
                    hash.Add(active.StartFrame);
                    hash.Add(active.NextOperationIndex);
                }

                int[] cooldownIds = skill.GetSortedCooldownSkillIds();
                hash.Add(cooldownIds.Length);
                for (int cooldownIndex = 0;
                     cooldownIndex < cooldownIds.Length;
                     cooldownIndex++)
                {
                    int skillId = cooldownIds[cooldownIndex];
                    hash.Add(skillId);
                    hash.Add(skill.GetCooldownEndFrame(skillId));
                }

                hash.Add(buffs.ActiveBuffs.Count);
                for (int buffIndex = 0;
                     buffIndex < buffs.ActiveBuffs.Count;
                     buffIndex++)
                {
                    ActiveBuffState buff = buffs.ActiveBuffs[buffIndex];
                    hash.Add(buff.BuffId);
                    hash.Add(buff.SourceEntityId);
                    hash.Add(buff.StartFrame);
                    hash.Add(buff.EndFrame);
                    hash.Add(buff.NextTickFrame);
                    hash.Add(buff.Stacks);
                }
            }

            return hash.Value;
        }
    }
}
