using System;
using TMPro;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class TMPTextAlignmentModifier : BaseModifier
    {
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public TextAlignmentOptions Value;
        private TextAlignmentOptions _originValue;
        
        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is TMP_Text tmpText)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.TMPTextAlignment:
                        tmpText.alignment = Value;
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
                    case ModifierTypeEnum.TMPTextAlignment:
                        _originValue = tmpText.alignment;
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
            root.Add(new Label("文本对齐方式"));
            // 定义方向
            var verticals = new (string label, TextAlignmentOptions[] options)[] {
                ("上", new[]{ TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top, TextAlignmentOptions.TopRight }),
                ("中", new[]{ TextAlignmentOptions.Left, TextAlignmentOptions.Center, TextAlignmentOptions.Right }),
                ("下", new[]{ TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom, TextAlignmentOptions.BottomRight }),
            };
            var horizontals = new (string label, int col)[] {
                ("左", 0), ("中", 1), ("右", 2)
            };
            int selectedRow = 0, selectedCol = 0;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (verticals[row].options[col] == Value) { selectedRow = row; selectedCol = col; }
            Button[] vBtns = new Button[3];
            Button[] hBtns = new Button[3];
            var vRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            for (int i = 0; i < 3; i++)
            {
                var idx = i;
                var btn = new Button(() => {
                    selectedRow = idx;
                    Value = verticals[selectedRow].options[selectedCol];
                    onValueChanged?.Invoke();
                    for (int j = 0; j < 3; j++)
                        vBtns[j].style.backgroundColor = (j == selectedRow) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
                    for (int j = 0; j < 3; j++)
                        hBtns[j].style.backgroundColor = (j == selectedCol) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
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
                    Value = verticals[selectedRow].options[selectedCol];
                    onValueChanged?.Invoke();
                    for (int j = 0; j < 3; j++)
                        vBtns[j].style.backgroundColor = (j == selectedRow) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
                    for (int j = 0; j < 3; j++)
                        hBtns[j].style.backgroundColor = (j == selectedCol) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
                }) { text = horizontals[i].label };
                btn.style.flexGrow = 1;
                hRow.Add(btn);
                hBtns[i] = btn;
            }
            root.Add(hRow);
            for (int j = 0; j < 3; j++)
                vBtns[j].style.backgroundColor = (j == selectedRow) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
            for (int j = 0; j < 3; j++)
                hBtns[j].style.backgroundColor = (j == selectedCol) ? new UnityEngine.Color(0.2f,0.5f,1f,0.5f) : UnityEngine.Color.clear;
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new TMPTextAlignmentModifier
            {
                ModifierType = this.ModifierType,
                Value = this.Value
            };
        }
    }
} 