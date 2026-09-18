using System;

namespace Game.Battle.TurnBased.RuntimeData
{
    public enum BattleExpressionClipType
    {
        Animation = 0,
        MoveToTarget = 1,
        MoveHome = 2,
        Effect = 3,
        Audio = 4,
        Hit = 5,
        ColorFlash = 6,
        CheckDead = 7,
        BuffText = 8,
        CameraFocus = 9,
        CameraShake = 10,
        ProjectileEffect = 11,
        EffectAnimation = 12,
        RemoveEffect = 13,
        DarkenOthers = 14,
        ClearDark = 15,
        SetBackgroundVisible = 16,
        SetUnitVisible = 17,
        SetHudVisible = 18,
        SetSkin = 19,
        ToggleLoopEffect = 20,
        UnitShake = 21,
        PresentationTimeScale = 22,
        SwitchForm = 23,
        ChangeModel = 24,
        LeaveField = 25,
        FloatingTip = 26,
        BubbleTip = 27,
        BackgroundAudio = 28,
        SwapPosition = 29,
        Summon = 30,
        SetPetVisible = 31,
        RefreshGrid = 32,
        HitReaction = 33,
        SetShadowVisible = 34,
    }

    public enum BattleExpressionSubject
    {
        Caster = 0,
        PrimaryTarget = 1,
    }

    public enum BattleExpressionAnchor
    {
        Subject = 0,
        Caster = 1,
        PrimaryTarget = 2,
        CampCenter = 3,
        RowCenter = 4,
        ColumnCenter = 5,
        ScreenCenter = 6,
    }

    public readonly struct BattleExpressionVector
    {
        public BattleExpressionVector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public readonly struct BattleExpressionColor
    {
        public BattleExpressionColor(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Alpha { get; }
    }

    public readonly struct CompiledBattleExpressionClip
    {
        public CompiledBattleExpressionClip(
            BattleExpressionClipType type,
            BattleExpressionSubject subject,
            int startFrame,
            int durationFrames,
            string resourceKey,
            BattleExpressionVector offset,
            BattleExpressionColor color,
            float intensity,
            string operationName = "",
            string parallelGroup = "",
            string logicOutputKey = "",
            int previewTargetSlot = 10,
            string secondaryResourceKey = "",
            string anchorKey = "",
            BattleExpressionAnchor anchor = BattleExpressionAnchor.PrimaryTarget,
            BattleExpressionVector targetOffset = default,
            bool state = true,
            bool loop = false,
            int option = 0,
            float frequency = 8f,
            int authoringOrder = 0)
        {
            if (startFrame < 0 || durationFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame));
            }

            Type = type;
            Subject = subject;
            StartFrame = startFrame;
            DurationFrames = durationFrames;
            ResourceKey = resourceKey ?? string.Empty;
            Offset = offset;
            Color = color;
            Intensity = intensity;
            OperationName = operationName ?? string.Empty;
            ParallelGroup = parallelGroup ?? string.Empty;
            LogicOutputKey = logicOutputKey ?? string.Empty;
            PreviewTargetSlot = previewTargetSlot < 1
                ? 10
                : previewTargetSlot > 18
                    ? 18
                    : previewTargetSlot;
            SecondaryResourceKey = secondaryResourceKey ?? string.Empty;
            AnchorKey = anchorKey ?? string.Empty;
            Anchor = anchor;
            TargetOffset = targetOffset;
            State = state;
            Loop = loop;
            Option = option;
            Frequency = frequency;
            AuthoringOrder = authoringOrder < 0 ? 0 : authoringOrder;
        }

        public BattleExpressionClipType Type { get; }
        public BattleExpressionSubject Subject { get; }
        public int StartFrame { get; }
        public int DurationFrames { get; }
        public int EndFrame => StartFrame + DurationFrames;
        public string ResourceKey { get; }
        public BattleExpressionVector Offset { get; }
        public BattleExpressionColor Color { get; }
        public float Intensity { get; }
        public string OperationName { get; }
        public string ParallelGroup { get; }
        public string LogicOutputKey { get; }
        public int PreviewTargetSlot { get; }
        public string SecondaryResourceKey { get; }
        public string AnchorKey { get; }
        public BattleExpressionAnchor Anchor { get; }
        public BattleExpressionVector TargetOffset { get; }
        public bool State { get; }
        public bool Loop { get; }
        public int Option { get; }
        public float Frequency { get; }
        public int AuthoringOrder { get; }
    }

    public sealed class CompiledBattleExpression
    {
        public CompiledBattleExpression(
            string id,
            int framesPerSecond,
            int durationFrames,
            CompiledBattleExpressionClip[] clips)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("表现 ID 不能为空。", nameof(id));
            }

            if (framesPerSecond <= 0 || durationFrames <= 0)
            {
                throw new ArgumentException("表现帧率与总帧数必须大于零。");
            }

            Id = id;
            FramesPerSecond = framesPerSecond;
            DurationFrames = durationFrames;
            Clips = clips == null
                ? Array.Empty<CompiledBattleExpressionClip>()
                : (CompiledBattleExpressionClip[])clips.Clone();
        }

        public string Id { get; }
        public int FramesPerSecond { get; }
        public int DurationFrames { get; }
        public CompiledBattleExpressionClip[] Clips { get; }
        public float DurationSeconds => DurationFrames / (float)FramesPerSecond;
    }
}
