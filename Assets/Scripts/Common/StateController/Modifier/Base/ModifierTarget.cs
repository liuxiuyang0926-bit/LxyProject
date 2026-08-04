using System;
using Object = UnityEngine.Object;

namespace StateControl.Runtime
{
    
    [Serializable]
    public class ModifierTarget
    {
        public Object TargetObject;
        public ModifierTypeEnum ModifierType;
    }
}