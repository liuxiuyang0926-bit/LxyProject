using System;
using TMPro;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class StringModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public string Value;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextString:
                        tmpText.text = Value;
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextString:
                        Value = tmpText.text;
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
            var field = new TextField(ModifierType.GetChineseName());
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
            return new StringModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 