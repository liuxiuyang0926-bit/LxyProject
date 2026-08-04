using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class ContinuousStateGroup
    {
        public string Name;
        
        [SerializeReference]
        public List<IContinuousModifier> Modifiers;

        private float _currentValue;
        public float CurrentValue
        {
            get => _currentValue;
            set
            {
                Apply(value);
            }
        }
        
        public void Apply(float progress)
        {
            // 查找匹配名称的状态
            _currentValue = progress;
            foreach (var m in Modifiers)
            {
                m.Apply(progress);
            }
        }

        public string GetShowName()
        {
            if (IsBuiltInContinuousStateEnum(Name, out BuiltInContinuousStateEnum builtInState))
            {
                string displayName = BuiltInContinuousStateEnumAttribute.GetName(builtInState);
                if (!string.IsNullOrEmpty(displayName))
                    return $"{displayName}({Name})";
            }

            return Name;
        }

        public static bool IsBuiltInContinuousStateEnum(string groupName, out BuiltInContinuousStateEnum builtInState)
        {
            return Enum.TryParse(groupName, out builtInState)
                   && Enum.IsDefined(typeof(BuiltInContinuousStateEnum), builtInState);
        }
        
        public void Add(ContinuousModifierTypeEnum targetEnum)
        {
            if (Modifiers == null)
            {
                Modifiers = new List<IContinuousModifier>();
            }
            
            FieldInfo fieldInfo = targetEnum.GetType().GetField(targetEnum.ToString());
            if (fieldInfo == null)
                return;

            ContinuousModifierTypeAttribute attribute = fieldInfo.GetCustomAttribute<ContinuousModifierTypeAttribute>();
            if (attribute == null)
                return;
            
            Type targetType = attribute.UseModifierType;
            var target = (IContinuousModifier)Activator.CreateInstance(targetType);
            Modifiers.Add(target);
        }
    }
}
