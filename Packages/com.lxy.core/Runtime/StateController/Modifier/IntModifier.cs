using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using Text = UnityEngine.UI.Text;

namespace StateControl.Runtime
{
    [Serializable]
    public class IntModifier : BaseModifier
    {
        public int Value;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextInt:
                        text.text = Value.ToString();
                        break;
                }
            }
            else if (target.TargetObject is Canvas canvas)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.CanvasSortingOrder:
                        canvas.sortingOrder = Value;
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
                    case ModifierTypeEnum.TextInt:
                        Value = int.Parse(text.text);
                        break;
                }
            }
            else if (target.TargetObject is Canvas canvas)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.CanvasSortingOrder:
                        Value = canvas.sortingOrder;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new IntegerField(ModifierType.GetChineseName());
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
            return new IntModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 