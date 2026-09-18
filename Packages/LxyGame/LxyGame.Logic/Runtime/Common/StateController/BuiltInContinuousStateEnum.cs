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
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 创建BuiltInContinuous状态EnumAttribute实例。
        /// </summary>
        public BuiltInContinuousStateEnumAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 获取名称。
        /// </summary>
        public static string GetName(BuiltInContinuousStateEnum enumType)
        {
            var attribute = (BuiltInContinuousStateEnumAttribute)GetCustomAttribute(
                typeof(BuiltInContinuousStateEnum).GetField(enumType.ToString()), typeof(BuiltInContinuousStateEnumAttribute));
            return attribute?.Name;
        }
    }
}

