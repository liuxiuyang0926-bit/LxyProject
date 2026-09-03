using System;
using DG.Tweening;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    public enum DOTweenAnimationAction
    {
        Play,
        Stop
    }

    [Serializable]
    public class DOTweenAnimationModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public DOTweenAnimationAction Value = DOTweenAnimationAction.Play;

        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is DOTweenAnimation dotweenAnimation)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.DOTweenAnimationPlay:
                        if (Value == DOTweenAnimationAction.Play)
                        {
                            dotweenAnimation.DORestart();
                        }
                        else
                        {
                            dotweenAnimation.DOPause();
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            // DOTweenAnimation playback is a command-style modifier, so no origin value is recorded.
        }

        /// <summary>
        /// 添加Field。
        /// </summary>
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new EnumField(ModifierType.GetChineseName(), Value);
            field.RegisterValueChangedCallback(evt =>
            {
                Value = (DOTweenAnimationAction)evt.newValue;
                onValueChanged?.Invoke();
            });
            root.Add(field);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new DOTweenAnimationModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
}
