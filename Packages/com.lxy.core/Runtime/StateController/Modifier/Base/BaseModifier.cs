using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public abstract class BaseModifier
    {
        [SerializeField]
        /// <summary>
        /// 公开的Modifier类型数据。
        /// </summary>
        public ModifierTypeEnum ModifierType;
        // Unity序列化需要无参构造函数
        /// <summary>
        /// 创建基础状态修饰器实例。
        /// </summary>
        protected BaseModifier()
        {
            
        }
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public abstract void Modify(ModifierTarget target);
        /// <summary>
        /// 添加Field。
        /// </summary>
        public abstract void AddField(VisualElement root, Action onValueChanged, ModifierTarget target);
        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public abstract void RecordOriginValue(ModifierTarget target);
        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public abstract BaseModifier Clone();
    }
}