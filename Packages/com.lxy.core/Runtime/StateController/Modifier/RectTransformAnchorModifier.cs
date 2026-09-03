using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class RectTransformAnchorModifier : BaseModifier
    {
        /// <summary>
        /// 公开的Anchor最小X数据。
        /// </summary>
        public float AnchorMinX;
        /// <summary>
        /// 公开的Anchor最小Y数据。
        /// </summary>
        public float AnchorMinY;
        /// <summary>
        /// 公开的Anchor最大X数据。
        /// </summary>
        public float AnchorMaxX;
        /// <summary>
        /// 公开的Anchor最大Y数据。
        /// </summary>
        public float AnchorMaxY;
        private Vector2 _originAnchorMin;
        private Vector2 _originAnchorMax;

        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformAnchor:
                        rectTransform.anchorMin = new Vector2(AnchorMinX, AnchorMinY);
                        rectTransform.anchorMax = new Vector2(AnchorMaxX, AnchorMaxY);
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
                    case ModifierTypeEnum.RectTransformAnchor:
                        _originAnchorMin = rectTransform.anchorMin;
                        _originAnchorMax = rectTransform.anchorMax;
                        AnchorMinX = rectTransform.anchorMin.x;
                        AnchorMinY = rectTransform.anchorMin.y;
                        AnchorMaxX = rectTransform.anchorMax.x;
                        AnchorMaxY = rectTransform.anchorMax.y;
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
            root.Add(new Label("Anchor Min/Max"));
            var rowMin = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var minXField = new FloatField { label = "Min X", value = AnchorMinX };
            minXField.labelElement.style.minWidth = 12;
            minXField.labelElement.style.marginRight = 2;
            minXField.style.flexGrow = 1;
            minXField.RegisterValueChangedCallback(evt => { AnchorMinX = evt.newValue; onValueChanged?.Invoke(); });
            var minYField = new FloatField { label = "Min Y", value = AnchorMinY };
            minYField.labelElement.style.minWidth = 12;
            minYField.labelElement.style.marginRight = 2;
            minYField.style.flexGrow = 1;
            minYField.RegisterValueChangedCallback(evt => { AnchorMinY = evt.newValue; onValueChanged?.Invoke(); });
            rowMin.Add(minXField);
            rowMin.Add(minYField);
            root.Add(rowMin);

            var rowMax = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var maxXField = new FloatField { label = "Max X", value = AnchorMaxX };
            maxXField.labelElement.style.minWidth = 12;
            maxXField.labelElement.style.marginRight = 2;
            maxXField.style.flexGrow = 1;
            maxXField.RegisterValueChangedCallback(evt => { AnchorMaxX = evt.newValue; onValueChanged?.Invoke(); });
            var maxYField = new FloatField { label = "Max Y", value = AnchorMaxY };
            maxYField.labelElement.style.minWidth = 12;
            maxYField.labelElement.style.marginRight = 2;
            maxYField.style.flexGrow = 1;
            maxYField.RegisterValueChangedCallback(evt => { AnchorMaxY = evt.newValue; onValueChanged?.Invoke(); });
            rowMax.Add(maxXField);
            rowMax.Add(maxYField);
            root.Add(rowMax);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new RectTransformAnchorModifier
            {
                ModifierType = this.ModifierType,
                AnchorMinX = this.AnchorMinX,
                AnchorMinY = this.AnchorMinY,
                AnchorMaxX = this.AnchorMaxX,
                AnchorMaxY = this.AnchorMaxY
            };
        }
    }
} 