using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public abstract class BaseModifier
    {
        [SerializeField]
        public ModifierTypeEnum ModifierType;
        // Unity序列化需要无参构造函数
        protected BaseModifier()
        {
            
        }
        
        public abstract void Modify(ModifierTarget target);
        public abstract void AddField(VisualElement root, Action onValueChanged, ModifierTarget target);
        public abstract void RecordOriginValue(ModifierTarget target);
        public abstract BaseModifier Clone();
    }
}