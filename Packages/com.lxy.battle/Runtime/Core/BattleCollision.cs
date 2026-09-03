using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public readonly struct BattleBox2
    {
        /// <summary>
        /// 创建战斗Box2实例。
        /// </summary>
        public BattleBox2(
            BattleCollisionShape shape,
            FPVector2 center,
            FPVector2 halfExtents,
            FP rotationDegrees)
        {
            if (halfExtents.X <= FP.Zero ||
                halfExtents.Y <= FP.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfExtents),
                    "碰撞盒半尺寸必须大于零。");
            }

            Shape = shape;
            Center = center;
            HalfExtents = halfExtents;
            RotationDegrees = shape == BattleCollisionShape.Aabb
                ? FP.Zero
                : FP.NormalizeAngle(rotationDegrees);
        }

        /// <summary>
        /// 向调用方提供Shape。
        /// </summary>
        public BattleCollisionShape Shape { get; }
        /// <summary>
        /// 向调用方提供Center。
        /// </summary>
        public FPVector2 Center { get; }
        /// <summary>
        /// 向调用方提供HalfExtents。
        /// </summary>
        public FPVector2 HalfExtents { get; }
        /// <summary>
        /// 向调用方提供旋转角度。
        /// </summary>
        public FP RotationDegrees { get; }
    }

    /// <summary>
    /// 完全使用定点数的二维 AABB/OBB 相交测试。
    /// OBB 使用四条分离轴进行 SAT 判定，边界接触视为命中。
    /// </summary>
    public static class BattleCollision
    {
        /// <summary>
        /// 执行判断是否重叠相关逻辑。
        /// </summary>
        public static bool Overlaps(
            BattleBox2 left,
            BattleBox2 right)
        {
            if (left.Shape == BattleCollisionShape.Aabb &&
                right.Shape == BattleCollisionShape.Aabb)
            {
                return OverlapsAabb(
                    left.Center,
                    left.HalfExtents,
                    right.Center,
                    right.HalfExtents);
            }

            return OverlapsObb(left, right);
        }

        /// <summary>
        /// 执行判断是否重叠Aabb相关逻辑。
        /// </summary>
        public static bool OverlapsAabb(
            FPVector2 leftCenter,
            FPVector2 leftHalfExtents,
            FPVector2 rightCenter,
            FPVector2 rightHalfExtents)
        {
            ValidateHalfExtents(leftHalfExtents);
            ValidateHalfExtents(rightHalfExtents);
            FPVector2 delta = FPVector2.Abs(
                rightCenter - leftCenter);
            return delta.X <=
                       leftHalfExtents.X + rightHalfExtents.X &&
                   delta.Y <=
                       leftHalfExtents.Y + rightHalfExtents.Y;
        }

        /// <summary>
        /// 通用 OBB SAT。传入 AABB 时会将其视为旋转角为零的 OBB。
        /// </summary>
        public static bool OverlapsObb(
            BattleBox2 left,
            BattleBox2 right)
        {
            GetAxes(
                left.RotationDegrees,
                out FPVector2 leftX,
                out FPVector2 leftY);
            GetAxes(
                right.RotationDegrees,
                out FPVector2 rightX,
                out FPVector2 rightY);
            FPVector2 centerDelta = right.Center - left.Center;
            return !IsSeparated(
                       centerDelta,
                       left,
                       leftX,
                       leftY,
                       right,
                       rightX,
                       rightY,
                       leftX) &&
                   !IsSeparated(
                       centerDelta,
                       left,
                       leftX,
                       leftY,
                       right,
                       rightX,
                       rightY,
                       leftY) &&
                   !IsSeparated(
                       centerDelta,
                       left,
                       leftX,
                       leftY,
                       right,
                       rightX,
                       rightY,
                       rightX) &&
                   !IsSeparated(
                       centerDelta,
                       left,
                       leftX,
                       leftY,
                       right,
                       rightX,
                       rightY,
                       rightY);
        }

        /// <summary>
        /// 执行收集目标相关逻辑。
        /// </summary>
        internal static void CollectTargets(
            BattleWorld world,
            int sourceEntityId,
            BattleBox2 hitBox,
            List<int> results)
        {
            results.Clear();
            if (!world.TryGetEntity(
                    sourceEntityId,
                    out BattleEntity source))
            {
                return;
            }

            for (int index = 0;
                 index < world.EntityIds.Count;
                 index++)
            {
                int entityId = world.EntityIds[index];
                if (entityId == sourceEntityId ||
                    !world.IsAlive(entityId) ||
                    !world.TryGetEntity(
                        entityId,
                        out BattleEntity target) ||
                    target.TeamId == source.TeamId)
                {
                    continue;
                }

                BattleBox2 targetBox = CreateEntityBox(
                    world,
                    entityId);
                if (Overlaps(hitBox, targetBox))
                {
                    results.Add(entityId);
                }
            }
        }

        /// <summary>
        /// 创建实体Box。
        /// </summary>
        internal static BattleBox2 CreateEntityBox(
            BattleWorld world,
            int entityId)
        {
            TransformComponent transform =
                world.Transforms.Get(entityId);
            CollisionComponent collision =
                world.Collisions.Get(entityId);
            FP rotation = FP.Zero;
            if (collision.Shape == BattleCollisionShape.Obb)
            {
                rotation = FP.Atan2(
                               transform.Forward.Y,
                               transform.Forward.X) +
                           collision.RotationDegrees;
            }

            return new BattleBox2(
                collision.Shape,
                transform.Position,
                collision.HalfExtents,
                rotation);
        }

        /// <summary>
        /// 校验HalfExtents。
        /// </summary>
        private static void ValidateHalfExtents(
            FPVector2 halfExtents)
        {
            if (halfExtents.X <= FP.Zero ||
                halfExtents.Y <= FP.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfExtents),
                    "碰撞盒半尺寸必须大于零。");
            }
        }

        /// <summary>
        /// 执行判断是否Separated相关逻辑。
        /// </summary>
        private static bool IsSeparated(
            FPVector2 centerDelta,
            BattleBox2 left,
            FPVector2 leftX,
            FPVector2 leftY,
            BattleBox2 right,
            FPVector2 rightX,
            FPVector2 rightY,
            FPVector2 axis)
        {
            FP distance = FP.Abs(FPVector2.Dot(
                centerDelta,
                axis));
            FP leftRadius =
                left.HalfExtents.X *
                FP.Abs(FPVector2.Dot(leftX, axis)) +
                left.HalfExtents.Y *
                FP.Abs(FPVector2.Dot(leftY, axis));
            FP rightRadius =
                right.HalfExtents.X *
                FP.Abs(FPVector2.Dot(rightX, axis)) +
                right.HalfExtents.Y *
                FP.Abs(FPVector2.Dot(rightY, axis));
            return distance > leftRadius + rightRadius;
        }

        /// <summary>
        /// 获取Axes。
        /// </summary>
        private static void GetAxes(
            FP rotationDegrees,
            out FPVector2 xAxis,
            out FPVector2 yAxis)
        {
            FP.SinCos(
                rotationDegrees,
                out FP sin,
                out FP cos);
            xAxis = new FPVector2(cos, sin);
            yAxis = new FPVector2(-sin, cos);
        }
    }
}
