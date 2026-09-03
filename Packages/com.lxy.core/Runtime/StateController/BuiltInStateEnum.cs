using System;

namespace StateControl.Runtime
{
    public enum BuiltInStateEnum
    {
        [BuiltInStateEnum("无", new[] { "None" })]
        None = 0,
        [BuiltInStateEnum("ToggleEx选中", new[] { "Selected", "UnSelected" })]
        _ToggleEx = 1,
        [BuiltInStateEnum("【列表】的选中", new[] { "UnSelected", "Selected" })]
        _ListToggleItem = 2,
    }
    
    public class BuiltInStateEnumAttribute : Attribute
    {
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 向调用方提供States。
        /// </summary>
        public string[] States { get; private set; }

        /// <summary>
        /// 创建BuiltIn状态EnumAttribute实例。
        /// </summary>
        public BuiltInStateEnumAttribute(string name, string[] states)
        {
            Name = name;
            States = states;
        }
    }
}