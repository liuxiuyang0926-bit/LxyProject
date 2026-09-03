using System;
using System.Reflection;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ContinuousModifierTypeAttribute : Attribute
    {
        /// <summary>
        /// 向调用方提供UseModifier类型。
        /// </summary>
        public Type UseModifierType { get; private set; }
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 创建Continuous修饰器TypeAttribute实例。
        /// </summary>
        public ContinuousModifierTypeAttribute(Type modifierType, string name)
        {
            if (!typeof(IContinuousModifier).IsAssignableFrom(modifierType))
            {
                throw new ArgumentException($"Type {modifierType} must inherit from BaseModifier");
            } 
            
            UseModifierType = modifierType; 
            Name = name;
        }

        /// <summary>
        /// 获取类型名称。
        /// </summary>
        public static string GetTypeName(ContinuousModifierTypeEnum enumType)
        {
            FieldInfo fieldInfo = enumType.GetType().GetField(enumType.ToString());
            if (fieldInfo == null) return null;

            ContinuousModifierTypeAttribute attribute = fieldInfo.GetCustomAttribute<ContinuousModifierTypeAttribute>();
            if (attribute == null) return null;
                
            return attribute.Name;
        }
    }
}