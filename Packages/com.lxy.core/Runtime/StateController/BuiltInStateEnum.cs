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
        public string Name { get; private set; }
        public string[] States { get; private set; }

        public BuiltInStateEnumAttribute(string name, string[] states)
        {
            Name = name;
            States = states;
        }
    }
}