using System;
using Game.Battle.Config;
using Game.Battle.Core;
using Game.Battle.Core.Math;
using Game.Battle.Protocol;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class BattleDeterminismValidator
    {
        /// <summary>
        /// 执行校验相关逻辑。
        /// </summary>
        [MenuItem("工具/战斗/运行确定性自检", false, 30)]
        public static void Validate()
        {
            ValidateFixedPointBoundaries();
            ValidateDeterministicMath();
            ValidateCollision();

            BattleWorld first = CreateWorld();
            BattleWorld second = CreateWorld();

            for (int frame = 0; frame < 240; frame++)
            {
                FrameData input = CreateFrame(frame);
                first.Tick(input.Clone());
                second.Tick(input.Clone());
                ulong firstHash = first.CalculateStateHash();
                ulong secondHash = second.CalculateStateHash();
                if (firstHash != secondHash)
                {
                    throw new InvalidOperationException(
                        $"确定性校验失败：Frame={frame}，" +
                        $"A={firstHash:X16}，B={secondHash:X16}");
                }
            }

            Debug.Log(
                $"[Battle] 定点数边界与确定性自检通过：Frame=240，" +
                $"Hash={first.CalculateStateHash():X16}");
        }

        /// <summary>
        /// 校验确定性数学。
        /// </summary>
        private static void ValidateDeterministicMath()
        {
            AssertNear(FP.Sin(FP.Zero), FP.Zero, 2L, "Sin(0)");
            AssertNear(
                FP.Sin(FP.Degrees90),
                FP.One,
                2L,
                "Sin(90)");
            AssertNear(
                FP.Sin(FP.FromInt(30)),
                FP.FromRatio(1L, 2L),
                3L,
                "Sin(30)");
            AssertNear(
                FP.Cos(FP.FromInt(60)),
                FP.FromRatio(1L, 2L),
                3L,
                "Cos(60)");
            AssertNear(
                FP.Cos(FP.Degrees180),
                -FP.One,
                2L,
                "Cos(180)");
            AssertNear(
                FP.Atan2(FP.One, FP.Zero),
                FP.Degrees90,
                2L,
                "Atan2(1, 0)");
            AssertNear(
                FP.Atan2(FP.One, FP.One),
                FP.FromInt(45),
                2L,
                "Atan2(1, 1)");
            AssertRaw(
                FP.NormalizeAngle(FP.FromInt(540)),
                -FP.Degrees180.RawValue,
                "角度规整");

            FPVector2 normalized = FPVector2.Normalize(
                new FPVector2(FP.FromInt(3), FP.FromInt(4)));
            AssertRaw(normalized.X, 6000L, "Normalize.X");
            AssertRaw(normalized.Y, 8000L, "Normalize.Y");
            AssertRaw(
                FP.Clamp(
                    FP.FromInt(5),
                    FP.Zero,
                    FP.FromInt(3)),
                FP.FromInt(3).RawValue,
                "Clamp");
            AssertRaw(
                FP.Abs(FP.FromInt(-2)),
                FP.FromInt(2).RawValue,
                "Abs");
        }

        /// <summary>
        /// 校验碰撞。
        /// </summary>
        private static void ValidateCollision()
        {
            var aabb = new BattleBox2(
                BattleCollisionShape.Aabb,
                FPVector2.Zero,
                new FPVector2(FP.One, FP.One),
                FP.Zero);
            var touchingAabb = new BattleBox2(
                BattleCollisionShape.Aabb,
                new FPVector2(FP.FromInt(2), FP.Zero),
                new FPVector2(FP.One, FP.One),
                FP.Zero);
            var separatedAabb = new BattleBox2(
                BattleCollisionShape.Aabb,
                new FPVector2(FP.FromRaw(20001L), FP.Zero),
                new FPVector2(FP.One, FP.One),
                FP.Zero);
            if (!BattleCollision.Overlaps(aabb, touchingAabb) ||
                BattleCollision.Overlaps(aabb, separatedAabb))
            {
                throw new InvalidOperationException(
                    "AABB 边界判定失败。");
            }

            var obb = new BattleBox2(
                BattleCollisionShape.Obb,
                FPVector2.Zero,
                new FPVector2(
                    FP.FromInt(2),
                    FP.FromRatio(1L, 2L)),
                FP.FromInt(45));
            var inside = new BattleBox2(
                BattleCollisionShape.Aabb,
                new FPVector2(FP.One, FP.One),
                new FPVector2(
                    FP.FromRatio(1L, 10L),
                    FP.FromRatio(1L, 10L)),
                FP.Zero);
            var outside = new BattleBox2(
                BattleCollisionShape.Aabb,
                new FPVector2(FP.FromInt(2), FP.Zero),
                new FPVector2(
                    FP.FromRatio(1L, 10L),
                    FP.FromRatio(1L, 10L)),
                FP.Zero);
            if (!BattleCollision.Overlaps(obb, inside) ||
                BattleCollision.Overlaps(obb, outside))
            {
                throw new InvalidOperationException(
                    "OBB SAT 判定失败。");
            }
        }

        /// <summary>
        /// 校验定点点Boundaries。
        /// </summary>
        private static void ValidateFixedPointBoundaries()
        {
            FP largeProduct =
                FP.FromRaw(4_000_000_000L) *
                FP.FromRaw(4_000_000_000L);
            AssertRaw(
                largeProduct,
                1_600_000_000_000_000L,
                "128 位中间值乘法");
            AssertRaw(
                FP.MaxValue / FP.One,
                long.MaxValue,
                "128 位中间值除法");
            AssertRaw(
                FP.FromRatio(long.MaxValue, FP.Precision),
                long.MaxValue,
                "128 位中间值比例换算");
            AssertRaw(
                FP.Sqrt(FP.MaxValue),
                303_700_049_997L,
                "128 位平方根");

            if (FP.TryAdd(
                    FP.MaxValue,
                    FP.FromRaw(1L),
                    out _) ||
                FP.TrySubtract(
                    FP.MinValue,
                    FP.FromRaw(1L),
                    out _) ||
                FP.TryMultiply(
                    FP.MaxValue,
                    FP.FromInt(2),
                    out _) ||
                FP.TryDivide(
                    FP.MinValue,
                    FP.FromRaw(-1L),
                    out _))
            {
                throw new InvalidOperationException(
                    "FP Try 运算未正确报告溢出。");
            }

            ExpectOverflow(
                () => _ = FP.MaxValue + FP.FromRaw(1L),
                "加法");
            ExpectOverflow(
                () => _ = FP.MinValue - FP.FromRaw(1L),
                "减法");
            ExpectOverflow(
                () => _ = -FP.MinValue,
                "取负");
            ExpectOverflow(
                () => _ = FP.Abs(FP.MinValue),
                "绝对值");
        }

        /// <summary>
        /// 断言原始值。
        /// </summary>
        private static void AssertRaw(
            FP actual,
            long expectedRaw,
            string operation)
        {
            if (actual.RawValue != expectedRaw)
            {
                throw new InvalidOperationException(
                    $"FP {operation}校验失败：ExpectedRaw=" +
                    $"{expectedRaw}，ActualRaw={actual.RawValue}。");
            }
        }

        /// <summary>
        /// 断言Near。
        /// </summary>
        private static void AssertNear(
            FP actual,
            FP expected,
            long toleranceRaw,
            string operation)
        {
            long difference = actual.RawValue - expected.RawValue;
            if (difference < 0L)
            {
                difference = -difference;
            }

            if (difference > toleranceRaw)
            {
                throw new InvalidOperationException(
                    $"FP {operation}校验失败：ExpectedRaw=" +
                    $"{expected.RawValue}，ActualRaw={actual.RawValue}。");
            }
        }

        /// <summary>
        /// 验证预期溢出。
        /// </summary>
        private static void ExpectOverflow(
            Action operation,
            string operationName)
        {
            try
            {
                operation();
            }
            catch (OverflowException)
            {
                return;
            }

            throw new InvalidOperationException(
                $"FP {operationName}没有抛出预期的溢出异常。");
        }

        /// <summary>
        /// 创建世界。
        /// </summary>
        private static BattleWorld CreateWorld()
        {
            var world = new BattleWorld(
                BattleConfigDatabase.CreateDefaultCatalog(),
                123456);
            world.Initialize();
            world.AddEntity(
                1,
                1001,
                1,
                new FPVector2(FP.FromRaw(-15000), FP.Zero),
                1000,
                FP.FromInt(4));
            world.AddEntity(
                2,
                1002,
                2,
                new FPVector2(FP.FromRaw(15000), FP.Zero),
                1000,
                FP.FromInt(3));
            return world;
        }

        /// <summary>
        /// 创建帧。
        /// </summary>
        private static FrameData CreateFrame(int frame)
        {
            var data = new FrameData(frame);
            if (frame == 0)
            {
                data.Commands.Add(
                    new FrameCommand
                    {
                        PlayerId = 1001,
                        Sequence = 1,
                        CommandType = BattleCommandType.Move,
                        MoveX = FP.Precision,
                    });
            }
            else if (frame == 10)
            {
                data.Commands.Add(
                    new FrameCommand
                    {
                        PlayerId = 1001,
                        Sequence = 2,
                        CommandType = BattleCommandType.CastSkill,
                        SkillId = 1001,
                        TargetId = 2,
                    });
            }
            else if (frame == 20)
            {
                data.Commands.Add(
                    new FrameCommand
                    {
                        PlayerId = 1001,
                        Sequence = 3,
                        CommandType = BattleCommandType.StopMove,
                    });
            }
            else if (frame == 80)
            {
                data.Commands.Add(
                    new FrameCommand
                    {
                        PlayerId = 1002,
                        Sequence = 4,
                        CommandType = BattleCommandType.CastSkill,
                        SkillId = 1001,
                        TargetId = 1,
                    });
            }

            return data;
        }
    }
}
