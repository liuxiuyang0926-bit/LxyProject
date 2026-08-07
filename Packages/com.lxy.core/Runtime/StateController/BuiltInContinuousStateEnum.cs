using System;

namespace StateControl.Runtime
{
    public enum BuiltInContinuousStateEnum
    {
        [BuiltInContinuousStateEnum("【列表】子项" )]
        _ListItem,
    }
    
    public class BuiltInContinuousStateEnumAttribute : Attribute
    {
        public string Name { get; private set; }

        public BuiltInContinuousStateEnumAttribute(string name)
        {
            Name = name;
        }

        public static string GetName(BuiltInContinuousStateEnum enumType)
        {
            var attribute = (BuiltInContinuousStateEnumAttribute)GetCustomAttribute(
                typeof(BuiltInContinuousStateEnum).GetField(enumType.ToString()), typeof(BuiltInContinuousStateEnumAttribute));
            return attribute?.Name;
        }
    }
}

