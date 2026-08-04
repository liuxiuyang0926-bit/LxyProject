#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime.ContinuousModifier
{
    [Serializable]
    public class SizeDeltaModifier : IContinuousModifier
    {
        public RectTransform Target;
        public Vector2 From;
        public Vector2 To;

        public ContinuousModifierTypeEnum ModifierType => ContinuousModifierTypeEnum.SizeDelta;

        public void Apply(float progress)
        {
            if(Target == null)
                return;
            Vector2 newSize = Vector2.Lerp(From, To, progress);
            Target.sizeDelta = newSize;
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
            var fromField = new Vector2Field("起始大小")
            {
                value = From
            };
            fromField.RegisterValueChangedCallback(evt =>
            {
                From = evt.newValue;
            });
            root.Add(fromField); 
            var toField = new Vector2Field("最终大小") {
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