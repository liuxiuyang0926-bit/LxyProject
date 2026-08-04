using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class RectTransformOffsetModifier : BaseModifier
    {
        public float OffsetMinX;
        public float OffsetMinY;
        public float OffsetMaxX;
        public float OffsetMaxY;
        private Vector2 _originOffsetMin;
        private Vector2 _originOffsetMax;

        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformOffset:
                        rectTransform.offsetMin = new Vector2(OffsetMinX, OffsetMinY);
                        rectTransform.offsetMax = new Vector2(OffsetMaxX, OffsetMaxY);
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformOffset:
                        _originOffsetMin = rectTransform.offsetMin;
                        _originOffsetMax = rectTransform.offsetMax;
                        OffsetMinX = rectTransform.offsetMin.x;
                        OffsetMinY = rectTransform.offsetMin.y;
                        OffsetMaxX = rectTransform.offsetMax.x;
                        OffsetMaxY = rectTransform.offsetMax.y;
                        break;
                }
            }
        }

        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            root.Add(new Label("Offset Min/Max"));
            var rowMin = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var minXField = new FloatField { label = "Min X", value = OffsetMinX };
            minXField.labelElement.style.minWidth = 12;
            minXField.labelElement.style.marginRight = 2;
            minXField.style.flexGrow = 1;
            minXField.RegisterValueChangedCallback(evt => { OffsetMinX = evt.newValue; onValueChanged?.Invoke(); });
            var minYField = new FloatField { label = "Min Y", value = OffsetMinY };
            minYField.labelElement.style.minWidth = 12;
            minYField.labelElement.style.marginRight = 2;
            minYField.style.flexGrow = 1;
            minYField.RegisterValueChangedCallback(evt => { OffsetMinY = evt.newValue; onValueChanged?.Invoke(); });
            rowMin.Add(minXField);
            rowMin.Add(minYField);
            root.Add(rowMin);

            var rowMax = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var maxXField = new FloatField { label = "Max X", value = OffsetMaxX };
            maxXField.labelElement.style.minWidth = 12;
            maxXField.labelElement.style.marginRight = 2;
            maxXField.style.flexGrow = 1;
            maxXField.RegisterValueChangedCallback(evt => { OffsetMaxX = evt.newValue; onValueChanged?.Invoke(); });
            var maxYField = new FloatField { label = "Max Y", value = OffsetMaxY };
            maxYField.labelElement.style.minWidth = 12;
            maxYField.labelElement.style.marginRight = 2;
            maxYField.style.flexGrow = 1;
            maxYField.RegisterValueChangedCallback(evt => { OffsetMaxY = evt.newValue; onValueChanged?.Invoke(); });
            rowMax.Add(maxXField);
            rowMax.Add(maxYField);
            root.Add(rowMax);
#endif
        }

        public override BaseModifier Clone()
        {
            return new RectTransformOffsetModifier
            {
                ModifierType = this.ModifierType,
                OffsetMinX = this.OffsetMinX,
                OffsetMinY = this.OffsetMinY,
                OffsetMaxX = this.OffsetMaxX,
                OffsetMaxY = this.OffsetMaxY
            };
        }
    }
}
