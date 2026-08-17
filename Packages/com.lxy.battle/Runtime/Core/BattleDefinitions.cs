using System;
using System.Collections.Generic;
using Game.Battle.Core.Math;

namespace Game.Battle.Core
{
    public static class BattleConst
    {
        public const int LogicFps = 20;
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

        public int Frame { get; }
        public int Order { get; }
        public BattleSkillOperationType OperationType { get; }
        public BattleCollisionShape HitShape { get; }
        public int Damage { get; }
        public int BuffId { get; }
        public FPVector2 LocalOffset { get; }
        public FPVector2 HalfExtents { get; }
        public FP RotationDegrees { get; }
        public FPVector2 LocalDisplacement { get; }
    }

    public readonly struct BattleSkillDefinition
    {
        private readonly BattleSkillFrameOperationDefinition[] operations;

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

        public int Id { get; }
        public string Name { get; }
        public int TotalFrames { get; }
        public int CooldownFrames { get; }
        public IReadOnlyList<BattleSkillFrameOperationDefinition>
            Operations => operations ??
                Array.Empty<BattleSkillFrameOperationDefinition>();

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

        public int Id { get; }
        public string Name { get; }
        public BattleBuffType Type { get; }
        public int DurationFrames { get; }
        public int IntervalFrames { get; }
        public int Value { get; }
        public BattleBuffStackMode StackMode { get; }
        public int MaxStacks { get; }
    }

    public interface IBattleConfigProvider
    {
        bool TryGetSkill(int id, out BattleSkillDefinition definition);
        bool TryGetBuff(int id, out BattleBuffDefinition definition);
    }

    public sealed class BattleConfigCatalog : IBattleConfigProvider
    {
        private readonly Dictionary<int, BattleSkillDefinition> skills =
            new Dictionary<int, BattleSkillDefinition>();
        private readonly Dictionary<int, BattleBuffDefinition> buffs =
            new Dictionary<int, BattleBuffDefinition>();

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

        public bool TryGetSkill(
            int id,
            out BattleSkillDefinition definition) =>
            skills.TryGetValue(id, out definition);

        public bool TryGetBuff(
            int id,
            out BattleBuffDefinition definition) =>
            buffs.TryGetValue(id, out definition);

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
