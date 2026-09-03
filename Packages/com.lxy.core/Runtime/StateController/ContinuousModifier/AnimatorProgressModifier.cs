#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime.ContinuousModifier
{
    [Serializable]
    public class AnimatorProgressModifier : IContinuousModifier
    {
        /// <summary>
        /// 公开的目标数据。
        /// </summary>
        public Animation Target;
        /// <summary>
        /// 公开的Clip数据。
        /// </summary>
        public AnimationClip Clip;
        [Range(0f, 1f)]
        /// <summary>
        /// 公开的From数据。
        /// </summary>
        public float From;
        [Range(0f, 1f)]
        /// <summary>
        /// 公开的To数据。
        /// </summary>
        public float To;

        /// <summary>
        /// 向调用方提供Modifier类型。
        /// </summary>
        public ContinuousModifierTypeEnum ModifierType => ContinuousModifierTypeEnum.AnimatorProgress;

        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(float progress)
        {
            if (Target == null || Clip == null)
                return;

            float normalizedTime = Mathf.Clamp01(Mathf.Lerp(From, To, progress));
            Clip.SampleAnimation(Target.gameObject, Clip.length * normalizedTime);

            string clipName = Clip.name;
            AnimationState state = Target[clipName];
            if (state == null)
            {
                Target.AddClip(Clip, clipName);
                state = Target[clipName];
                if (state == null)
                    return;
            }

            state.normalizedTime = normalizedTime;
            state.time = Clip.length * normalizedTime;
            state.weight = 1f;
            state.enabled = true;
        }

        /// <summary>
        /// 添加Field。
        /// </summary>
        public void AddField(VisualElement root)
        {
#if UNITY_EDITOR
            var targetField = new ObjectField("目标Animation")
            {
                objectType = typeof(Animation),
                value = Target
            };
            targetField.RegisterValueChangedCallback(evt =>
            {
                Target = evt.newValue as Animation;
            });
            root.Add(targetField);
            var clipField = new ObjectField("动画片段")
            {
                objectType = typeof(AnimationClip),
                value = Clip
            };
            clipField.RegisterValueChangedCallback(evt =>
            {
                Clip = evt.newValue as AnimationClip;
            });
            root.Add(clipField);
            var fromField = new FloatField("起始进度")
            {
                value = From
            };
            fromField.RegisterValueChangedCallback(evt =>
            {
                From = Mathf.Clamp01(evt.newValue);
            });
            root.Add(fromField);
            var toField = new FloatField("最终进度")
            {
                value = To
            };
            toField.RegisterValueChangedCallback(evt =>
            {
                To = Mathf.Clamp01(evt.newValue);
            });
            root.Add(toField);
#endif
        }
    }
}
