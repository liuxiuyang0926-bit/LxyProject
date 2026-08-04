using System;
using System.Reflection;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ContinuousModifierTypeAttribute : Attribute
    {
        public Type UseModifierType { get; private set; }
        public string Name { get; private set; }

        public ContinuousModifierTypeAttribute(Type modifierType, string name)
        {
            if (!typeof(IContinuousModifier).IsAssignableFrom(modifierType))
            {
                throw new ArgumentException($"Type {modifierType} must inherit from BaseModifier");
            } 
            
            UseModifierType = modifierType; 
            Name = name;
        }

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