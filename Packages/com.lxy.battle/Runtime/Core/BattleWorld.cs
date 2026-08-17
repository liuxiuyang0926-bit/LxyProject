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

        public int CurrentFrame { get; private set; }
        public IBattleConfigProvider Config { get; }
        public BattleRandom Random { get; }
        public BattleEventCollector Events { get; }
        public IReadOnlyList<int> EntityIds => entityIds;

        public ComponentStore<TransformComponent> Transforms { get; } =
            new ComponentStore<TransformComponent>();
        public ComponentStore<MovementComponent> Movements { get; } =
            new ComponentStore<MovementComponent>();
        public ComponentStore<HealthComponent> Health { get; } =
            new ComponentStore<HealthComponent>();
        public ComponentStore<CollisionComponent> Collisions { get; } =
            new ComponentStore<CollisionComponent>();
        public ComponentStore<SkillComponent> Skills { get; } =
            new ComponentStore<SkillComponent>();
        public ComponentStore<BuffComponent> Buffs { get; } =
            new ComponentStore<BuffComponent>();

        public void Initialize()
        {
            CurrentFrame = 0;
            Events.BeginFrame(0);
        }

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

        public bool TryGetEntity(int entityId, out BattleEntity entity) =>
            entities.TryGetValue(entityId, out entity);

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

        public bool IsAlive(int entityId)
        {
            return Health.TryGet(entityId, out HealthComponent health) &&
                   !health.IsDead;
        }

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
