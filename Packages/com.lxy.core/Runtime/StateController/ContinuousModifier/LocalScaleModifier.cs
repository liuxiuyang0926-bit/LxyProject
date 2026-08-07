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
        public RectTransform Target;
        public Vector3 From;
        public Vector3 To;

        public ContinuousModifierTypeEnum ModifierType => ContinuousModifierTypeEnum.LocalScale;

        public void Apply(float progress)
        {
            if (Target == null)
                return;
            Vector3 newScale = Vector3.Lerp(From, To, progress);
            Target.localScale = newScale;
        }

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
