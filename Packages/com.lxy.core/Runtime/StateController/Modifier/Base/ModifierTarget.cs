using System;
using Object = UnityEngine.Object;

namespace StateControl.Runtime
{
    
    [Serializable]
    public class ModifierTarget
    {
        /// <summary>
        /// 公开的目标对象数据。
        /// </summary>
        public Object TargetObject;
        /// <summary>
        /// 公开的Modifier类型数据。
        /// </summary>
        public ModifierTypeEnum ModifierType;
    }
}