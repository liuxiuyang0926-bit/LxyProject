using System;
using TMPro;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class StringModifier : BaseModifier
    {
        public string Value;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextString:
                        tmpText.text = Value;
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextString:
                        Value = tmpText.text;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new TextField(ModifierType.GetChineseName());
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
            return new StringModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 