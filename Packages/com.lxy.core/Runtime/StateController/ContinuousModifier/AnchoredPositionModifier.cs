#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime.ContinuousModifier
{
    [Serializable]
    public class AnchoredPositionModifier : IContinuousModifier
    {
        public RectTransform Target;
        public Vector2 From;
        public Vector2 To;

        public ContinuousModifierTypeEnum ModifierType => ContinuousModifierTypeEnum.AnchoredPosition;

        public void Apply(float progress)
        {
            if(Target == null)
                return;
            Vector2 newVal = Vector2.Lerp(From, To, progress);
            Target.anchoredPosition = newVal;
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
            var fromField = new Vector2Field("起始位置")
            {
                value = From
            };
            fromField.RegisterValueChangedCallback(evt =>
            {
                From = evt.newValue;
            });
            root.Add(fromField); 
            var toField = new Vector2Field("最终位置") {
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