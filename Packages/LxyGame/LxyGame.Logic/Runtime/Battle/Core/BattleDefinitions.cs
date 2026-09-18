using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public static class BattleConst
    {
        /// <summary>
        /// 公开的LogicFps数据。
        /// </summary>
        public const int LogicFps = 20;
        /// <summary>
        /// 公开的帧Delta数据。
        /// </summary>
        public static readonly FP FrameDelta =
            FP.FromRatio(1, LogicFps);
    }

    public enum BattleBuffType
    {
        DamageOverTime = 0,
        HealOverTime = 1,
        MoveSpeedPercent = 2,
    }

    public enum BattleBuffStackMode
    {
        RefreshDuration = 0,
        StackAndRefresh = 1,
    }

    public enum BattleCollisionShape
    {
        Aabb = 0,
        Obb = 1,
    }

    public enum BattleSkillOperationType
    {
        DamageBox = 0,
        ApplyBuffToTarget = 1,
        DisplaceCaster = 2,
    }

    public readonly struct BattleSkillFrameOperationDefinition
    {
        /// <summary>
        /// 创建战斗Skill帧操作定义实例。
        /// </summary>
        public BattleSkillFrameOperationDefinition(
            int frame,
            int order,
            BattleSkillOperationType operationType,
            BattleCollisionShape hitShape,
            int damage,
            int buffId,
            FPVector2 localOffset,
            FPVector2 halfExtents,
            FP rotationDegrees,
            FPVector2 localDisplacement)
        {
            Frame = frame;
            Order = order;
            OperationType = operationType;
            HitShape = hitShape;
            Damage = damage;
            BuffId = buffId;
            LocalOffset = localOffset;
            HalfExtents = halfExtents;
            RotationDegrees = FP.NormalizeAngle(rotationDegrees);
            LocalDisplacement = localDisplacement;
        }

        /// <summary>
        /// 向调用方提供帧。
        /// </summary>
        public int Frame { get; }
        /// <summary>
        /// 向调用方提供Order。
        /// </summary>
        public int Order { get; }
        /// <summary>
        /// 向调用方提供操作类型。
        /// </summary>
        public BattleSkillOperationType OperationType { get; }
        /// <summary>
        /// 向调用方提供HitShape。
        /// </summary>
        public BattleCollisionShape HitShape { get; }
        /// <summary>
        /// 向调用方提供Damage。
        /// </summary>
        public int Damage { get; }
        /// <summary>
        /// 向调用方提供增益标识。
        /// </summary>
        public int BuffId { get; }
        /// <summary>
        /// 向调用方提供LocalOffset。
        /// </summary>
        public FPVector2 LocalOffset { get; }
        /// <summary>
        /// 向调用方提供HalfExtents。
        /// </summary>
        public FPVector2 HalfExtents { get; }
        /// <summary>
        /// 向调用方提供旋转角度。
        /// </summary>
        public FP RotationDegrees { get; }
        /// <summary>
        /// 向调用方提供LocalDisplacement。
        /// </summary>
        public FPVector2 LocalDisplacement { get; }
    }

    public readonly struct BattleSkillDefinition
    {
        private readonly BattleSkillFrameOperationDefinition[] operations;

        /// <summary>
        /// 创建战斗Skill定义实例。
        /// </summary>
        public BattleSkillDefinition(
            int id,
            string name,
            int totalFrames,
            int cooldownFrames,
            BattleSkillFrameOperationDefinition[] operations)
        {
            Id = id;
            Name = name ?? string.Empty;
            TotalFrames = totalFrames;
            CooldownFrames = cooldownFrames;
            this.operations = operations == null
                ? Array.Empty<BattleSkillFrameOperationDefinition>()
                : (BattleSkillFrameOperationDefinition[])operations.Clone();
            Array.Sort(
                this.operations,
                CompareOperations);
        }

        /// <summary>
        /// 向调用方提供标识。
        /// </summary>
        public int Id { get; }
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 向调用方提供总数Frames。
        /// </summary>
        public int TotalFrames { get; }
        /// <summary>
        /// 向调用方提供CooldownFrames。
        /// </summary>
        public int CooldownFrames { get; }
        public IReadOnlyList<BattleSkillFrameOperationDefinition>
            Operations => operations ??
                Array.Empty<BattleSkillFrameOperationDefinition>();

        /// <summary>
        /// 执行比较操作相关逻辑。
        /// </summary>
        private static int CompareOperations(
            BattleSkillFrameOperationDefinition left,
            BattleSkillFrameOperationDefinition right)
        {
            int frameResult = left.Frame.CompareTo(right.Frame);
            return frameResult != 0
                ? frameResult
                : left.Order.CompareTo(right.Order);
        }
    }

    public readonly struct BattleBuffDefinition
    {
        /// <summary>
        /// 创建战斗Buff定义实例。
        /// </summary>
        public BattleBuffDefinition(
            int id,
            string name,
            BattleBuffType type,
            int durationFrames,
            int intervalFrames,
            int value,
            BattleBuffStackMode stackMode,
            int maxStacks)
        {
            Id = id;
            Name = name ?? string.Empty;
            Type = type;
            DurationFrames = durationFrames;
            IntervalFrames = intervalFrames;
            Value = value;
            StackMode = stackMode;
            MaxStacks = maxStacks;
        }

        /// <summary>
        /// 向调用方提供标识。
        /// </summary>
        public int Id { get; }
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 向调用方提供类型。
        /// </summary>
        public BattleBuffType Type { get; }
        /// <summary>
        /// 向调用方提供DurationFrames。
        /// </summary>
        public int DurationFrames { get; }
        /// <summary>
        /// 向调用方提供IntervalFrames。
        /// </summary>
        public int IntervalFrames { get; }
        /// <summary>
        /// 向调用方提供值。
        /// </summary>
        public int Value { get; }
        /// <summary>
        /// 向调用方提供栈Mode。
        /// </summary>
        public BattleBuffStackMode StackMode { get; }
        /// <summary>
        /// 向调用方提供MaxStacks。
        /// </summary>
        public int MaxStacks { get; }
    }

    public interface IBattleConfigProvider
    {
        /// <summary>
        /// 尝试获取技能，并返回是否成功。
        /// </summary>
        bool TryGetSkill(int id, out BattleSkillDefinition definition);
        /// <summary>
        /// 尝试获取增益，并返回是否成功。
        /// </summary>
        bool TryGetBuff(int id, out BattleBuffDefinition definition);
    }

    public sealed class BattleConfigCatalog : IBattleConfigProvider
    {
        private readonly Dictionary<int, BattleSkillDefinition> skills =
            new Dictionary<int, BattleSkillDefinition>();
        private readonly Dictionary<int, BattleBuffDefinition> buffs =
            new Dictionary<int, BattleBuffDefinition>();

        /// <summary>
        /// 添加技能。
        /// </summary>
        public void AddSkill(BattleSkillDefinition definition)
        {
            ValidateSkill(definition);
            for (int index = 0;
                 index < definition.Operations.Count;
                 index++)
            {
                int buffId = definition.Operations[index].BuffId;
                if (buffId > 0 && !buffs.ContainsKey(buffId))
                {
                    throw new InvalidOperationException(
                        $"技能 {definition.Id} 引用了不存在的 Buff：" +
                        buffId);
                }
            }

            if (skills.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException(
                    $"技能 ID 重复：{definition.Id}");
            }

            skills.Add(definition.Id, definition);
        }

        /// <summary>
        /// 添加增益。
        /// </summary>
        public void AddBuff(BattleBuffDefinition definition)
        {
            ValidateBuff(definition);
            if (buffs.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException(
                    $"Buff ID 重复：{definition.Id}");
            }

            buffs.Add(definition.Id, definition);
        }

        /// <summary>
        /// 尝试获取技能，并返回是否成功。
        /// </summary>
        public bool TryGetSkill(
            int id,
            out BattleSkillDefinition definition) =>
            skills.TryGetValue(id, out definition);

        /// <summary>
        /// 尝试获取增益，并返回是否成功。
        /// </summary>
        public bool TryGetBuff(
            int id,
            out BattleBuffDefinition definition) =>
            buffs.TryGetValue(id, out definition);

        /// <summary>
        /// 校验技能。
        /// </summary>
        private static void ValidateSkill(
            BattleSkillDefinition definition)
        {
            if (definition.Id <= 0 || definition.TotalFrames <= 0 ||
                definition.CooldownFrames < 0 ||
                definition.Operations.Count == 0)
            {
                throw new ArgumentException(
                    $"技能配置无效：{definition.Id}");
            }

            for (int index = 0;
                 index < definition.Operations.Count;
                 index++)
            {
                BattleSkillFrameOperationDefinition operation =
                    definition.Operations[index];
                if (operation.Frame < 0 ||
                    operation.Frame >= definition.TotalFrames)
                {
                    throw new ArgumentException(
                        $"技能 {definition.Id} 的帧操作超出总帧数：" +
                        $"Frame={operation.Frame}");
                }

                switch (operation.OperationType)
                {
                    case BattleSkillOperationType.DamageBox:
                        if (operation.Damage < 0 ||
                            operation.BuffId < 0 ||
                            (operation.Damage == 0 &&
                             operation.BuffId == 0) ||
                            operation.HalfExtents.X <= FP.Zero ||
                            operation.HalfExtents.Y <= FP.Zero)
                        {
                            throw new ArgumentException(
                                $"技能 {definition.Id} 的伤害盒配置无效：" +
                                $"Frame={operation.Frame}");
                        }
                        break;

                    case BattleSkillOperationType.ApplyBuffToTarget:
                        if (operation.BuffId <= 0)
                        {
                            throw new ArgumentException(
                                $"技能 {definition.Id} 的目标 Buff 无效：" +
                                $"Frame={operation.Frame}");
                        }
                        break;

                    case BattleSkillOperationType.DisplaceCaster:
                        if (operation.LocalDisplacement == FPVector2.Zero)
                        {
                            throw new ArgumentException(
                                $"技能 {definition.Id} 的位移不能为零：" +
                                $"Frame={operation.Frame}");
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(operation.OperationType));
                }
            }
        }

        /// <summary>
        /// 校验增益。
        /// </summary>
        private static void ValidateBuff(
            BattleBuffDefinition definition)
        {
            if (definition.Id <= 0 || definition.DurationFrames <= 0 ||
                definition.IntervalFrames < 0 ||
                definition.MaxStacks <= 0)
            {
                throw new ArgumentException(
                    $"Buff 配置无效：{definition.Id}");
            }

            bool periodic =
                definition.Type == BattleBuffType.DamageOverTime ||
                definition.Type == BattleBuffType.HealOverTime;
            if (periodic && definition.IntervalFrames <= 0)
            {
                throw new ArgumentException(
                    $"周期 Buff 的触发间隔必须大于零：{definition.Id}");
            }
        }
    }
}
