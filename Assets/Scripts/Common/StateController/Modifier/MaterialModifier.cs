using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI; // 添加此 using
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace StateControl.Runtime
{
    [Serializable]
    public class MaterialModifier : BaseModifier
    {
        public Material Value;

        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is UnityEngine.UI.Image imageEx)
            {
                switch (ModifierType)
                {
                    default:
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is UnityEngine.UI.Image imageEx)
            {
                switch (ModifierType)
                {
                    default:
                        break;
                }
            }
        }

        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new ObjectField(ModifierType.GetChineseName());
            field.objectType = typeof(Material);
            field.value = Value;
            field.RegisterValueChangedCallback(evt =>
            {
                Value = evt.newValue as Material;
                onValueChanged?.Invoke();
            });
            root.Add(field);
#endif
        }

        public override BaseModifier Clone()
        {
            return new MaterialModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
}
