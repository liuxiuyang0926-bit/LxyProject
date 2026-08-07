using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class Vector3Modifier : BaseModifier
    {
        public Vector3 Value;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Transform transform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TransformPosition:
                        transform.position = Value;
                        break;
                    case ModifierTypeEnum.TransformScale:
                        transform.localScale = Value;
                        break;
                    case ModifierTypeEnum.TransformRotation:
                        transform.rotation = Quaternion.Euler(Value);
                        break;
                    case ModifierTypeEnum.RectTransformPosition:
                        transform.localPosition = Value;
                        break;
                }
            }
            else if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformAnchoredPosition3D:
                        rectTransform.anchoredPosition3D = Value;
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is Transform transform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TransformPosition:
                        Value = transform.position;
                        break;
                    case ModifierTypeEnum.TransformScale:
                        Value = transform.localScale;
                        break;
                    case ModifierTypeEnum.TransformRotation:
                        Value = transform.rotation.eulerAngles;
                        break;
                    case ModifierTypeEnum.RectTransformPosition:
                        Value = transform.localPosition;
                        break;
                }
            }
            else if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformAnchoredPosition3D:
                        Value = rectTransform.anchoredPosition3D;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new Vector3Field(ModifierType.GetChineseName());
            field.value = Value;
            field.RegisterValueChangedCallback(evt =>
            {
                Value = evt.newValue;
                onValueChanged?.Invoke();
            });
            root.Add(field);
#endif
        }

        public override BaseModifier Clone()
        {
            return new Vector3Modifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 