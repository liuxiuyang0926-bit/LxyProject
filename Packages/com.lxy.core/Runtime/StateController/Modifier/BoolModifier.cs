using System;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace StateControl.Runtime
{
    [Serializable]
    public class BoolModifier : BaseModifier
    {
        public bool Value;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Button buttonA)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ButtonInteractable:
                        buttonA.interactable = Value;
                        break;
                }
                
            }else if (target.TargetObject is GameObject gameObject)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.GameObjectActive:
                        gameObject.SetActive(Value);
                        break;
                }
            }
            else if (target.TargetObject is Behaviour behaviour)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ComponentEnabled:
                    case ModifierTypeEnum.ScrollRectEnabled:
                        behaviour.enabled = Value;
                        break;
                }
            }
        }
        
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is Button buttonA)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ButtonInteractable:
                        Value = buttonA.interactable;
                        break;
                }
                
            }else if (target.TargetObject is GameObject gameObject)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.GameObjectActive:
                        Value = gameObject.activeSelf;
                        break;
                }
            }
            else if (target.TargetObject is Behaviour behaviour)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ComponentEnabled:
                    case ModifierTypeEnum.ScrollRectEnabled:
                        Value = behaviour.enabled;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new Toggle(ModifierType.GetChineseName());
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
            return new BoolModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
}