using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using Text = UnityEngine.UI.Text;

namespace StateControl.Runtime
{
    [Serializable]
    public class IntModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public int Value;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextInt:
                        text.text = Value.ToString();
                        break;
                }
            }
            else if (target.TargetObject is Canvas canvas)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.CanvasSortingOrder:
                        canvas.sortingOrder = Value;
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextInt:
                        Value = int.Parse(text.text);
                        break;
                }
            }
            else if (target.TargetObject is Canvas canvas)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.CanvasSortingOrder:
                        Value = canvas.sortingOrder;
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
            var field = new IntegerField(ModifierType.GetChineseName());
            field.value = Value;
            field.RegisterValueChangedCallback(evt =>
            {
                Value = evt.newValue;
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
            return new IntModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 