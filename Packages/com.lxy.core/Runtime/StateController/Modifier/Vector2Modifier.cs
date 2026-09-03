using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class Vector2Modifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public Vector2 Value;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformAnchoredPosition:
                        rectTransform.anchoredPosition = Value;
                        break;
                    case ModifierTypeEnum.RectTransformSizeDelta:
                        rectTransform.sizeDelta = Value;
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformAnchoredPosition:
                        Value = rectTransform.anchoredPosition;
                        break;
                    case ModifierTypeEnum.RectTransformSizeDelta:
                        Value = rectTransform.sizeDelta;
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
            var field = new Vector2Field(ModifierType.GetChineseName());
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
            return new Vector2Modifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 