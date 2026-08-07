using System;
using TMPro;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine.UI;
using UnityEngine.UIElements;
using Text = UnityEngine.UI.Text;

namespace StateControl.Runtime
{
    [Serializable]
    public class FloatModifier : BaseModifier
    {
        public float Value;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextFloat:
                        text.text = Value.ToString();
                        break;
                }
            }
            else if (target.TargetObject is ScrollRect scrollRect)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ScrollRectHorizontal:
                        scrollRect.horizontalNormalizedPosition = Value;
                        break;
                    case ModifierTypeEnum.ScrollRectVertical:
                        scrollRect.verticalNormalizedPosition = Value;
                        break;
                }
            }else if(target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextFontSize:
                        tmpText.fontSize = Value;
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextFloat:
                        Value = float.Parse(text.text);
                        break;
                }
            }
            else if (target.TargetObject is ScrollRect scrollRect)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ScrollRectHorizontal:
                        Value = scrollRect.horizontalNormalizedPosition;
                        break;
                    case ModifierTypeEnum.ScrollRectVertical:
                        Value = scrollRect.verticalNormalizedPosition;
                        break;
                }
            }else if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextFontSize:
                        Value = tmpText.fontSize;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new FloatField(ModifierType.GetChineseName());
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
            return new FloatModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 