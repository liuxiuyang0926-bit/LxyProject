using System;
using Object = UnityEngine.Object;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ModifierRequireAttribute : Attribute
    {
        public Type UseModifierType { get; private set; }
        public Type TargetType { get; private set; }

        public ModifierRequireAttribute(Type targetType, Type modifierType)
        {
            if (!typeof(BaseModifier).IsAssignableFrom(modifierType))
            {
                throw new ArgumentException($"Type {modifierType} must inherit from BaseModifier");
            }
            
            if (!typeof(Object).IsAssignableFrom(targetType))
            {
                throw new ArgumentException($"Type {targetType} must inherit from UnityEngine.Object");
            }
            
            UseModifierType = modifierType;
            TargetType = targetType;
        }
    }
} 