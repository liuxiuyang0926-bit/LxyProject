using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace StateControl.Runtime
{
    [Serializable]
    public class LayoutGroupChildAlignmentModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public TextAnchor Value;
        private TextAnchor _originValue;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is LayoutGroup layoutGroup)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.LayoutGroupChildAlignment:
                        layoutGroup.childAlignment = Value;
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            if (target.TargetObject is LayoutGroup layoutGroup)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.LayoutGroupChildAlignment:
                        _originValue = layoutGroup.childAlignment;
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
            root.Add(new Label("子项对齐方式"));
            // 上中下 + 左中右组合
            var verticals = new (string label, int v)[] { ("上", 0), ("中", 1), ("下", 2) };
            var horizontals = new (string label, int h)[] { ("左", 0), ("中", 1), ("右", 2) };
            // 9宫格TextAnchor
            TextAnchor[,] anchors = new TextAnchor[3,3]
            {
                { TextAnchor.UpperLeft, TextAnchor.UpperCenter, TextAnchor.UpperRight },
                { TextAnchor.MiddleLeft, TextAnchor.MiddleCenter, TextAnchor.MiddleRight },
                { TextAnchor.LowerLeft, TextAnchor.LowerCenter, TextAnchor.LowerRight },
            };
            int selectedRow = 0, selectedCol = 0;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (anchors[row, col] == Value) { selectedRow = row; selectedCol = col; }
            Button[] vBtns = new Button[3];
            Button[] hBtns = new Button[3];
            var vRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            for (int i = 0; i < 3; i++)
            {
                var idx = i;
                var btn = new Button(() => {
                    selectedRow = idx;
                    Value = anchors[selectedRow, selectedCol];
                    onValueChanged?.Invoke();
                    for (int j = 0; j < 3; j++)
                        vBtns[j].style.backgroundColor = (j == selectedRow) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
                    for (int j = 0; j < 3; j++)
                        hBtns[j].style.backgroundColor = (j == selectedCol) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
                }) { text = verticals[i].label };
                btn.style.flexGrow = 1;
                vRow.Add(btn);
                vBtns[i] = btn;
            }
            root.Add(vRow);
            var hRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            for (int i = 0; i < 3; i++)
            {
                var idx = i;
                var btn = new Button(() => {
                    selectedCol = idx;
                    Value = anchors[selectedRow, selectedCol];
                    onValueChanged?.Invoke();
                    for (int j = 0; j < 3; j++)
                        vBtns[j].style.backgroundColor = (j == selectedRow) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
                    for (int j = 0; j < 3; j++)
                        hBtns[j].style.backgroundColor = (j == selectedCol) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
                }) { text = horizontals[i].label };
                btn.style.flexGrow = 1;
                hRow.Add(btn);
                hBtns[i] = btn;
            }
            root.Add(hRow);
            for (int j = 0; j < 3; j++)
                vBtns[j].style.backgroundColor = (j == selectedRow) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
            for (int j = 0; j < 3; j++)
                hBtns[j].style.backgroundColor = (j == selectedCol) ? new Color(0.2f,0.5f,1f,0.5f) : Color.clear;
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new LayoutGroupChildAlignmentModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 