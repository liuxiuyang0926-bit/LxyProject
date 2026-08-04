using System;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ModifierNameAttribute : Attribute
    {
        public string ChineseName { get; private set; }
        
        public ModifierNameAttribute(string chineseName)
        {
            ChineseName = chineseName;
        }
    }
} 