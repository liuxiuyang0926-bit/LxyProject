using StateControl.Runtime.ContinuousModifier;

namespace StateControl.Runtime
{
    public enum ContinuousModifierTypeEnum
    {
        [ContinuousModifierType(typeof(SizeDeltaModifier), "UI大小")]
        SizeDelta,
        [ContinuousModifierType(typeof(AnchoredPositionModifier), "UI锚点位置")]
        AnchoredPosition,
        [ContinuousModifierType(typeof(AnimatorProgressModifier), "动画进度")]
        AnimatorProgress,
        [ContinuousModifierType(typeof(LocalScaleModifier), "UI缩放")]
        LocalScale,
    }
}