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
        public DOTweenAnimationAction Value = DOTweenAnimationAction.Play;

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

        public override void RecordOriginValue(ModifierTarget target)
        {
            // DOTweenAnimation playback is a command-style modifier, so no origin value is recorded.
        }

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
