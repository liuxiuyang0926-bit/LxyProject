using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Presentation
{
    /// <summary>
    /// 项目级表现扩展点。模型替换、格子刷新、背景和音频系统等依赖具体项目的能力
    /// 通过该接口接入；实现不得反向修改战斗逻辑结果。
    /// </summary>
    public interface ITurnBasedExpressionExtension
    {
        bool TryStartExpressionClip(
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            DamageResolvedEvent damage);

        void ResetExpressionPresentation();
    }
}
