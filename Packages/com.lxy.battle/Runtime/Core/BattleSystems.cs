using Game.Battle.Core.Math;
using Game.Battle.Protocol;
using System.Collections.Generic;

namespace Game.Battle.Core
{
    internal sealed class BattleCommandSystem
    {
        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(BattleWorld world, FrameData frameData)
        {
            for (int index = 0;
                 index < frameData.Commands.Count;
                 index++)
            {
                Execute(world, frameData.Commands[index]);
            }
        }

        /// <summary>
        /// 执行当前操作。
        /// </summary>
        private static void Execute(
            BattleWorld world,
            FrameCommand command)
        {
            if (!world.TryFindPlayerEntity(
                    command.PlayerId,
                    out BattleEntity entity) ||
                !world.IsAlive(entity.EntityId))
            {
                return;
            }

            switch (command.CommandType)
            {
                case BattleCommandType.Move:
                {
                    var direction = new FPVector2(
                        FP.FromRaw(command.MoveX),
                        FP.FromRaw(command.MoveY));
                    MovementComponent movement =
                        world.Movements.Get(entity.EntityId);
                    if (direction == FPVector2.Zero)
                    {
                        movement.Direction = FPVector2.Zero;
                        movement.IsMoving = false;
                    }
                    else
                    {
                        movement.Direction = direction.Normalized;
                        movement.IsMoving = true;
                    }

                    world.Movements.Set(entity.EntityId, movement);
                    break;
                }

                case BattleCommandType.StopMove:
                {
                    MovementComponent movement =
                        world.Movements.Get(entity.EntityId);
                    movement.Direction = FPVector2.Zero;
                    movement.IsMoving = false;
                    world.Movements.Set(entity.EntityId, movement);
                    break;
                }

                case BattleCommandType.CastSkill:
                    BattleSkillSystem.TryCast(
                        world,
                        entity.EntityId,
                        command.SkillId,
                        command.TargetId,
                        new FPVector2(
                            FP.FromRaw(command.TargetX),
                            FP.FromRaw(command.TargetY)));
                    break;
            }
        }
    }

    internal sealed class BattleMovementSystem
    {
        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(BattleWorld world)
        {
            for (int index = 0;
                 index < world.EntityIds.Count;
                 index++)
            {
                int entityId = world.EntityIds[index];
                if (!world.IsAlive(entityId))
                {
                    continue;
                }

                MovementComponent movement =
                    world.Movements.Get(entityId);
                if (!movement.IsMoving ||
                    movement.Direction == FPVector2.Zero)
                {
                    continue;
                }

                FP speed = movement.BaseSpeed *
                           world.GetMoveSpeedMultiplier(entityId);
                TransformComponent transform =
                    world.Transforms.Get(entityId);
                transform.Position +=
                    movement.Direction * speed * BattleConst.FrameDelta;
                transform.Forward = movement.Direction;
                world.Transforms.Set(entityId, transform);
            }
        }
    }

    internal sealed class BattleSkillSystem
    {
        private readonly List<int> hitTargets = new List<int>(16);

        /// <summary>
        /// 尝试Cast，并返回是否成功。
        /// </summary>
        public static bool TryCast(
            BattleWorld world,
            int casterEntityId,
            int skillId,
            int targetEntityId,
            FPVector2 targetPosition)
        {
            if (!world.Config.TryGetSkill(
                    skillId,
                    out BattleSkillDefinition definition) ||
                !world.IsAlive(casterEntityId))
            {
                return false;
            }

            SkillComponent component =
                world.Skills.Get(casterEntityId);
            if (component.HasActiveSkill ||
                world.CurrentFrame <
                component.GetCooldownEndFrame(skillId))
            {
                return false;
            }

            TransformComponent casterTransform =
                world.Transforms.Get(casterEntityId);
            FPVector2 aimPosition = targetPosition;
            if (world.IsAlive(targetEntityId))
            {
                aimPosition = world.Transforms
                    .Get(targetEntityId)
                    .Position;
            }

            FPVector2 aimDirection =
                aimPosition - casterTransform.Position;
            if (aimDirection != FPVector2.Zero)
            {
                casterTransform.Forward = aimDirection.Normalized;
                world.Transforms.Set(
                    casterEntityId,
                    casterTransform);
            }

            component.ActiveSkill = new ActiveSkillState
            {
                SkillId = skillId,
                TargetEntityId = targetEntityId,
                TargetPosition = aimPosition,
                CastForward = casterTransform.Forward,
                StartFrame = world.CurrentFrame,
                NextOperationIndex = 0,
            };
            component.HasActiveSkill = true;

            component.SetCooldownEndFrame(
                skillId,
                world.CurrentFrame + definition.CooldownFrames);

            world.Events.Add(
                new BattleEvent
                {
                    Type = BattleEventType.SkillStarted,
                    SourceEntityId = casterEntityId,
                    TargetEntityId = targetEntityId,
                    IntValue1 = skillId,
                });
            return true;
        }

        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(BattleWorld world)
        {
            for (int index = 0;
                 index < world.EntityIds.Count;
                 index++)
            {
                int casterEntityId = world.EntityIds[index];
                SkillComponent component =
                    world.Skills.Get(casterEntityId);
                if (!component.HasActiveSkill)
                {
                    continue;
                }

                ActiveSkillState active = component.ActiveSkill;
                if (!world.IsAlive(casterEntityId) ||
                    !world.Config.TryGetSkill(
                        active.SkillId,
                        out BattleSkillDefinition definition))
                {
                    component.HasActiveSkill = false;
                    continue;
                }

                int elapsedFrame =
                    world.CurrentFrame - active.StartFrame;
                while (active.NextOperationIndex <
                       definition.Operations.Count)
                {
                    BattleSkillFrameOperationDefinition operation =
                        definition.Operations[
                            active.NextOperationIndex];
                    if (operation.Frame > elapsedFrame)
                    {
                        break;
                    }

                    ExecuteOperation(
                        world,
                        casterEntityId,
                        active,
                        definition.Id,
                        operation);
                    active.NextOperationIndex++;
                }

                if (elapsedFrame >= definition.TotalFrames)
                {
                    component.HasActiveSkill = false;
                    world.Events.Add(
                        new BattleEvent
                        {
                            Type = BattleEventType.SkillFinished,
                            SourceEntityId = casterEntityId,
                            TargetEntityId = active.TargetEntityId,
                            IntValue1 = active.SkillId,
                        });
                }
                else
                {
                    component.ActiveSkill = active;
                }
            }
        }

        /// <summary>
        /// 执行执行操作相关逻辑。
        /// </summary>
        private void ExecuteOperation(
            BattleWorld world,
            int casterEntityId,
            ActiveSkillState active,
            int skillId,
            BattleSkillFrameOperationDefinition operation)
        {
            switch (operation.OperationType)
            {
                case BattleSkillOperationType.DamageBox:
                    ExecuteDamageBox(
                        world,
                        casterEntityId,
                        skillId,
                        active.CastForward,
                        operation);
                    break;

                case BattleSkillOperationType.ApplyBuffToTarget:
                    if (world.IsAlive(active.TargetEntityId))
                    {
                        world.ApplyBuff(
                            casterEntityId,
                            active.TargetEntityId,
                            operation.BuffId);
                    }
                    break;

                case BattleSkillOperationType.DisplaceCaster:
                    ExecuteDisplacement(
                        world,
                        casterEntityId,
                        active.CastForward,
                        operation.LocalDisplacement);
                    break;
            }
        }

        /// <summary>
        /// 执行执行DamageBox相关逻辑。
        /// </summary>
        private void ExecuteDamageBox(
            BattleWorld world,
            int casterEntityId,
            int skillId,
            FPVector2 castForward,
            BattleSkillFrameOperationDefinition operation)
        {
            TransformComponent casterTransform =
                world.Transforms.Get(casterEntityId);
            FP facingDegrees = FP.Atan2(
                castForward.Y,
                castForward.X);
            FPVector2 center = casterTransform.Position +
                               FPVector2.Rotate(
                                   operation.LocalOffset,
                                   facingDegrees);
            FP boxRotation =
                operation.HitShape == BattleCollisionShape.Obb
                    ? facingDegrees + operation.RotationDegrees
                    : FP.Zero;
            var hitBox = new BattleBox2(
                operation.HitShape,
                center,
                operation.HalfExtents,
                boxRotation);
            BattleCollision.CollectTargets(
                world,
                casterEntityId,
                hitBox,
                hitTargets);
            for (int index = 0; index < hitTargets.Count; index++)
            {
                int targetEntityId = hitTargets[index];
                TransformComponent targetTransform =
                    world.Transforms.Get(targetEntityId);
                world.Events.Add(
                    new BattleEvent
                    {
                        Type = BattleEventType.SkillHit,
                        SourceEntityId = casterEntityId,
                        TargetEntityId = targetEntityId,
                        IntValue1 = skillId,
                        IntValue2 = operation.Order,
                        Position = targetTransform.Position,
                    });
                if (operation.Damage > 0)
                {
                    world.ApplyDamage(
                        casterEntityId,
                        targetEntityId,
                        operation.Damage,
                        false);
                }

                if (operation.BuffId > 0)
                {
                    world.ApplyBuff(
                        casterEntityId,
                        targetEntityId,
                        operation.BuffId);
                }
            }
        }

        /// <summary>
        /// 执行执行Displacement相关逻辑。
        /// </summary>
        private static void ExecuteDisplacement(
            BattleWorld world,
            int casterEntityId,
            FPVector2 castForward,
            FPVector2 localDisplacement)
        {
            TransformComponent transform =
                world.Transforms.Get(casterEntityId);
            FP facingDegrees = FP.Atan2(
                castForward.Y,
                castForward.X);
            transform.Position += FPVector2.Rotate(
                localDisplacement,
                facingDegrees);
            world.Transforms.Set(casterEntityId, transform);
        }
    }

    internal sealed class BattleBuffSystem
    {
        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(
            BattleWorld world,
            int sourceEntityId,
            int targetEntityId,
            int buffId)
        {
            if (!world.IsAlive(targetEntityId) ||
                !world.Config.TryGetBuff(
                    buffId,
                    out BattleBuffDefinition definition))
            {
                return;
            }

            BuffComponent component = world.Buffs.Get(targetEntityId);
            int existingIndex = FindBuff(component, buffId);
            ActiveBuffState active;
            if (existingIndex >= 0)
            {
                active = component.ActiveBuffs[existingIndex];
                active.SourceEntityId = sourceEntityId;
                active.StartFrame = world.CurrentFrame;
                active.EndFrame =
                    world.CurrentFrame + definition.DurationFrames;
                active.NextTickFrame =
                    world.CurrentFrame + definition.IntervalFrames;
                active.Stacks =
                    definition.StackMode ==
                    BattleBuffStackMode.StackAndRefresh
                        ? System.Math.Min(
                            definition.MaxStacks,
                            active.Stacks + 1)
                        : 1;
                component.ActiveBuffs[existingIndex] = active;
            }
            else
            {
                active = new ActiveBuffState
                {
                    BuffId = buffId,
                    SourceEntityId = sourceEntityId,
                    StartFrame = world.CurrentFrame,
                    EndFrame =
                        world.CurrentFrame + definition.DurationFrames,
                    NextTickFrame =
                        world.CurrentFrame + definition.IntervalFrames,
                    Stacks = 1,
                };
                component.ActiveBuffs.Add(active);
            }

            world.Events.Add(
                new BattleEvent
                {
                    Type = BattleEventType.BuffApplied,
                    SourceEntityId = sourceEntityId,
                    TargetEntityId = targetEntityId,
                    IntValue1 = buffId,
                    IntValue2 = active.Stacks,
                });
        }

        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(BattleWorld world)
        {
            for (int entityIndex = 0;
                 entityIndex < world.EntityIds.Count;
                 entityIndex++)
            {
                int entityId = world.EntityIds[entityIndex];
                BuffComponent component = world.Buffs.Get(entityId);
                for (int buffIndex =
                         component.ActiveBuffs.Count - 1;
                     buffIndex >= 0;
                     buffIndex--)
                {
                    ActiveBuffState active =
                        component.ActiveBuffs[buffIndex];
                    if (!world.Config.TryGetBuff(
                            active.BuffId,
                            out BattleBuffDefinition definition) ||
                        world.CurrentFrame >= active.EndFrame ||
                        !world.IsAlive(entityId))
                    {
                        component.ActiveBuffs.RemoveAt(buffIndex);
                        world.Events.Add(
                            new BattleEvent
                            {
                                Type = BattleEventType.BuffRemoved,
                                SourceEntityId = active.SourceEntityId,
                                TargetEntityId = entityId,
                                IntValue1 = active.BuffId,
                            });
                        continue;
                    }

                    if (definition.IntervalFrames <= 0 ||
                        world.CurrentFrame < active.NextTickFrame)
                    {
                        continue;
                    }

                    int value = checked(
                        definition.Value * active.Stacks);
                    switch (definition.Type)
                    {
                        case BattleBuffType.DamageOverTime:
                            world.ApplyDamage(
                                active.SourceEntityId,
                                entityId,
                                value,
                                true);
                            break;
                        case BattleBuffType.HealOverTime:
                            world.ApplyHeal(
                                active.SourceEntityId,
                                entityId,
                                value,
                                true);
                            break;
                    }

                    active.NextTickFrame = checked(
                        active.NextTickFrame +
                        definition.IntervalFrames);
                    component.ActiveBuffs[buffIndex] = active;
                }
            }
        }

        /// <summary>
        /// 查找增益。
        /// </summary>
        private static int FindBuff(
            BuffComponent component,
            int buffId)
        {
            for (int index = 0;
                 index < component.ActiveBuffs.Count;
                 index++)
            {
                if (component.ActiveBuffs[index].BuffId == buffId)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    internal sealed class BattleDeathSystem
    {
        /// <summary>
        /// 推进当前帧的运行逻辑。
        /// </summary>
        public void Tick(BattleWorld world)
        {
            for (int index = 0;
                 index < world.EntityIds.Count;
                 index++)
            {
                int entityId = world.EntityIds[index];
                HealthComponent health = world.Health.Get(entityId);
                if (health.IsDead || health.Current > 0)
                {
                    continue;
                }

                health.IsDead = true;
                world.Health.Set(entityId, health);

                MovementComponent movement =
                    world.Movements.Get(entityId);
                movement.IsMoving = false;
                movement.Direction = FPVector2.Zero;
                world.Movements.Set(entityId, movement);

                SkillComponent skill = world.Skills.Get(entityId);
                skill.HasActiveSkill = false;

                world.Events.Add(
                    new BattleEvent
                    {
                        Type = BattleEventType.EntityDied,
                        TargetEntityId = entityId,
                    });
            }
        }
    }
}
