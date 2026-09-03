using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class ContinuousStateGroup
    {
        /// <summary>
        /// 公开的名称数据。
        /// </summary>
        public string Name;
        
        [SerializeReference]
        /// <summary>
        /// 公开的Modifiers数据。
        /// </summary>
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
        
        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(float progress)
        {
            // 查找匹配名称的状态
            _currentValue = progress;
            foreach (var m in Modifiers)
            {
                m.Apply(progress);
            }
        }

        /// <summary>
        /// 获取Show名称。
        /// </summary>
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

        /// <summary>
        /// 执行判断是否BuiltInContinuous状态Enum相关逻辑。
        /// </summary>
        public static bool IsBuiltInContinuousStateEnum(string groupName, out BuiltInContinuousStateEnum builtInState)
        {
            return Enum.TryParse(groupName, out builtInState)
                   && Enum.IsDefined(typeof(BuiltInContinuousStateEnum), builtInState);
        }
        
        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
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
