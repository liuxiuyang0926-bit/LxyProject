using System;

namespace StateControl.Runtime
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ModifierNameAttribute : Attribute
    {
        /// <summary>
        /// 向调用方提供Chinese名称。
        /// </summary>
        public string ChineseName { get; private set; }
        
        /// <summary>
        /// 创建修饰器NameAttribute实例。
        /// </summary>
        public ModifierNameAttribute(string chineseName)
        {
            ChineseName = chineseName;
        }
    }
} 