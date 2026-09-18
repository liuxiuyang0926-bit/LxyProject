using System;
using Object = UnityEngine.Object;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ModifierRequireAttribute : Attribute
    {
        /// <summary>
        /// 向调用方提供UseModifier类型。
        /// </summary>
        public Type UseModifierType { get; private set; }
        /// <summary>
        /// 向调用方提供Target类型。
        /// </summary>
        public Type TargetType { get; private set; }

        /// <summary>
        /// 创建修饰器RequireAttribute实例。
        /// </summary>
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