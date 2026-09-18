using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI; // 添加此 using
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace StateControl.Runtime
{
    [Serializable]
    public class MaterialModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public Material Value;

        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is UnityEngine.UI.Image imageEx)
            {
                switch (ModifierType)
                {
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is UnityEngine.UI.Image imageEx)
            {
                switch (ModifierType)
                {
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 添加Field。
        /// </summary>
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var field = new ObjectField(ModifierType.GetChineseName());
            field.objectType = typeof(Material);
            field.value = Value;
            field.RegisterValueChangedCallback(evt =>
            {
                Value = evt.newValue as Material;
                onValueChanged?.Invoke();
            });
            root.Add(field);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new MaterialModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
}
