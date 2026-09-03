using System;
using TMPro;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace StateControl.Runtime
{
    [Serializable]
    public class ColorModifier:BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public Color Value;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Image image)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ImageColor:
                        image.color = Value;
                        break;
                }
            }else if (target.TargetObject is Text textEx)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextColor:
                        textEx.color = Value;
                        break;
                }
            }else if (target.TargetObject is TMP_Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextColor:
                        text.color = Value;
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is Image image)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ImageColor:
                        Value = image.color;
                        break;
                }
            }else if (target.TargetObject is Text textEx)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextColor:
                        Value = textEx.color;
                        break;
                }
            }else if (target.TargetObject is TMP_Text text)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TextColor:
                        Value = text.color;
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
            var field = new ColorField(ModifierType.GetChineseName());
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
            return new ColorModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
}