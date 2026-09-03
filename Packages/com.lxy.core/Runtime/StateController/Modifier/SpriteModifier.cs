using System;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace StateControl.Runtime
{
    [Serializable]
    public class SpriteModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public Sprite Value;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is Image image)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.ImageSprite:
                        image.sprite = Value as Sprite;
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
                    case ModifierTypeEnum.ImageSprite:
                        Value = image.sprite;
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
            var spriteField = new ObjectField(ModifierType.GetChineseName())
            {
                objectType = typeof(Sprite),
                value = Value
            };
            spriteField.RegisterValueChangedCallback(evt => 
            {
                Value = evt.newValue as Sprite;
                onValueChanged?.Invoke();
            });
            root.Add(spriteField);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new SpriteModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 