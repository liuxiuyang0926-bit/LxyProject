using System;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class LayoutGroupPaddingModifier : BaseModifier
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
        private RectOffset _originValue;
        
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is LayoutGroup layoutGroup)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.LayoutGroupPadding:
                        layoutGroup.padding = new RectOffset(Left, Right, Top, Bottom);
                        break;
                }
            }
        }

        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is LayoutGroup layoutGroup)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.LayoutGroupPadding:
                        _originValue = new RectOffset(
                            layoutGroup.padding.left,
                            layoutGroup.padding.right,
                            layoutGroup.padding.top,
                            layoutGroup.padding.bottom
                        );
                        Left = layoutGroup.padding.left;
                        Right = layoutGroup.padding.right;
                        Top = layoutGroup.padding.top;
                        Bottom = layoutGroup.padding.bottom;
                        break;
                }
            }
        }
        
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            root.Add(new Label("内边距"));
            // 第一行：左、右
            var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };
            var leftField = new IntegerField { label = "左", value = Left };
            leftField.labelElement.style.minWidth = 16;
            leftField.labelElement.style.marginRight = 2;
            leftField.style.flexGrow = 1;
            leftField.RegisterValueChangedCallback(evt => { Left = evt.newValue; onValueChanged?.Invoke(); });
            var rightField = new IntegerField { label = "右", value = Right };
            rightField.labelElement.style.minWidth = 16;
            rightField.labelElement.style.marginRight = 2;
            rightField.style.flexGrow = 1;
            rightField.RegisterValueChangedCallback(evt => { Right = evt.newValue; onValueChanged?.Invoke(); });
            row1.Add(leftField);
            row1.Add(rightField);
            root.Add(row1);
            // 第二行：上、下
            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var topField = new IntegerField { label = "上", value = Top };
            topField.labelElement.style.minWidth = 16;
            topField.labelElement.style.marginRight = 2;
            topField.style.flexGrow = 1;
            topField.RegisterValueChangedCallback(evt => { Top = evt.newValue; onValueChanged?.Invoke(); });
            var bottomField = new IntegerField { label = "下", value = Bottom };
            bottomField.labelElement.style.minWidth = 16;
            bottomField.labelElement.style.marginRight = 2;
            bottomField.style.flexGrow = 1;
            bottomField.RegisterValueChangedCallback(evt => { Bottom = evt.newValue; onValueChanged?.Invoke(); });
            row2.Add(topField);
            row2.Add(bottomField);
            root.Add(row2);
#endif
        }

        public override BaseModifier Clone()
        {
            return new LayoutGroupPaddingModifier
            {
                ModifierType = this.ModifierType,
                Left = this.Left,
                Right = this.Right,
                Top = this.Top,
                Bottom = this.Bottom
            };
        }
    }
} 