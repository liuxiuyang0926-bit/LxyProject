using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class RectTransformPivotModifier : BaseModifier
    {
        /// <summary>
        /// 公开的X数据。
        /// </summary>
        public float X;
        /// <summary>
        /// 公开的Y数据。
        /// </summary>
        public float Y;
        private Vector2 _originValue;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformPivot:
                        rectTransform.pivot = new Vector2(X, Y);
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
                    case ModifierTypeEnum.RectTransformPivot:
                        _originValue = rectTransform.pivot;
                        X = rectTransform.pivot.x;
                        Y = rectTransform.pivot.y;
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
            root.Add(new Label("Pivot"));
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var xField = new FloatField { label = "X", value = X };
            xField.labelElement.style.minWidth = 12;
            xField.labelElement.style.marginRight = 2;
            xField.style.flexGrow = 1;
            xField.RegisterValueChangedCallback(evt => { X = evt.newValue; onValueChanged?.Invoke(); });
            var yField = new FloatField { label = "Y", value = Y };
            yField.labelElement.style.minWidth = 12;
            yField.labelElement.style.marginRight = 2;
            yField.style.flexGrow = 1;
            yField.RegisterValueChangedCallback(evt => { Y = evt.newValue; onValueChanged?.Invoke(); });
            row.Add(xField);
            row.Add(yField);
            root.Add(row);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new RectTransformPivotModifier
            {
                ModifierType = this.ModifierType,
                X = this.X,
                Y = this.Y
            };
        }
    }
} 