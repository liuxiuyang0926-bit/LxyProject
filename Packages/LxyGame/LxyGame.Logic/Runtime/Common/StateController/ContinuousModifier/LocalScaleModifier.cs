#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime.ContinuousModifier
{
    [Serializable]
    public class LocalScaleModifier : IContinuousModifier
    {
        /// <summary>
        /// 公开的目标数据。
        /// </summary>
        public RectTransform Target;
        /// <summary>
        /// 公开的From数据。
        /// </summary>
        public Vector3 From;
        /// <summary>
        /// 公开的To数据。
        /// </summary>
        public Vector3 To;

        /// <summary>
        /// 向调用方提供Modifier类型。
        /// </summary>
        public ContinuousModifierTypeEnum ModifierType => ContinuousModifierTypeEnum.LocalScale;

        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(float progress)
        {
            if (Target == null)
                return;
            Vector3 newScale = Vector3.Lerp(From, To, progress);
            Target.localScale = newScale;
        }

        /// <summary>
        /// 添加Field。
        /// </summary>
        public void AddField(VisualElement root)
        {
#if UNITY_EDITOR
            var targetField = new ObjectField("目标UI")
            {
                objectType = typeof(RectTransform),
                value = Target
            };
            targetField.RegisterValueChangedCallback(evt =>
            {
                Target = evt.newValue as RectTransform;
            });
            root.Add(targetField);
            var fromField = new Vector3Field("起始缩放")
            {
                value = From
            };
            fromField.RegisterValueChangedCallback(evt =>
            {
                From = evt.newValue;
            });
            root.Add(fromField);
            var toField = new Vector3Field("最终缩放")
            {
                value = To
            };
            toField.RegisterValueChangedCallback(evt =>
            {
                To = evt.newValue;
            });
            root.Add(toField);
#endif
        }
    }
}
