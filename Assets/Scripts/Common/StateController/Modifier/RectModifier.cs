using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class RectModifier : BaseModifier
    {
        public float AnchorMinX;
        public float AnchorMinY;
        public float AnchorMaxX;
        public float AnchorMaxY;
        public float PivotX;
        public float PivotY;

        public float OffsetMinX;
        public float OffsetMinY;
        public float OffsetMaxX;
        public float OffsetMaxY;

        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is RectTransform rectTransform)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.RectTransformRect:
                        rectTransform.pivot = new Vector2(PivotX, PivotY);
                        rectTransform.offsetMax = new Vector2(OffsetMaxX, OffsetMaxY);
                        rectTransform.offsetMin = new Vector2(OffsetMinX, OffsetMinY);
                        rectTransform.anchorMin = new Vector2(AnchorMinX, AnchorMinY);
                        rectTransform.anchorMax = new Vector2(AnchorMaxX, AnchorMaxY);
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
                    case ModifierTypeEnum.RectTransformRect:
                        PivotX = rectTransform.pivot.x;
                        PivotY = rectTransform.pivot.y;
                        AnchorMinX = rectTransform.anchorMin.x;
                        AnchorMinY = rectTransform.anchorMin.y;
                        AnchorMaxX = rectTransform.anchorMax.x;
                        AnchorMaxY = rectTransform.anchorMax.y;
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
            var row1 = new VisualElement();
            var row2 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.justifyContent = Justify.SpaceBetween;
            row2.style.flexDirection = FlexDirection.Row;
            row2.style.justifyContent = Justify.SpaceBetween;
            posXField = new FloatField("PosX");
            posXField.Q<Label>().style.minWidth = 0;
            posXField.style.flexGrow = 1;
            leftField = new FloatField("Left");
            leftField.Q<Label>().style.minWidth = 0;
            leftField.style.flexGrow = 1;
            posYField = new FloatField("PosY");
            posYField.Q<Label>().style.minWidth = 0;
            posYField.style.flexGrow = 1;
            topField = new FloatField("Top");
            topField.Q<Label>().style.minWidth = 0;
            topField.style.flexGrow = 1;
            widthField = new FloatField("Width");
            widthField.Q<Label>().style.minWidth = 0;
            widthField.style.flexGrow = 1;
            rightField = new FloatField("Right");
            rightField.Q<Label>().style.minWidth = 0;
            rightField.style.flexGrow = 1;
            heightField = new FloatField("Height");
            heightField.Q<Label>().style.minWidth = 0;
            heightField.style.flexGrow = 1;
            bottomField = new FloatField("Bottom");
            bottomField.Q<Label>().style.minWidth = 0;
            bottomField.style.flexGrow = 1;
            var posZField = new FloatField("PosZ");
            posZField.Q<Label>().style.minWidth = 0;
            posZField.style.flexGrow = 1;

            // 为字段添加修改值回调
            posXField.RegisterValueChangedCallback(evt =>
            {
                if (posXField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            posYField.RegisterValueChangedCallback(evt =>
            {
                if (posYField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            widthField.RegisterValueChangedCallback(evt =>
            {
                if (widthField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            heightField.RegisterValueChangedCallback(evt =>
            {
                if (heightField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            leftField.RegisterValueChangedCallback(evt =>
            {
                if (leftField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            rightField.RegisterValueChangedCallback(evt =>
            {
                if (rightField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            topField.RegisterValueChangedCallback(evt =>
            {
                if (topField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });
            
            bottomField.RegisterValueChangedCallback(evt =>
            {
                if (bottomField.style.display == DisplayStyle.Flex)
                {
                    CalculateOffsetFromVisibleFields(target, onValueChanged);
                }
            });

            row1.Add(posXField);
            row1.Add(leftField);
            row1.Add(posYField);
            row1.Add(topField);
            row1.Add(posZField);
            row2.Add(widthField);
            row2.Add(rightField);
            row2.Add(heightField);
            row2.Add(bottomField);
            
            root.Add(row1);
            root.Add(row2);

            var offsetMinField = new Vector2Field("OffsetMin");
            offsetMinField.value = new Vector2(OffsetMinX, OffsetMinY);
            offsetMinField.RegisterValueChangedCallback(evt =>
            {
                OffsetMinX = evt.newValue.x;
                OffsetMinY = evt.newValue.y;
                onValueChanged?.Invoke();
            });
            offsetMinField.style.display = DisplayStyle.None;
            root.Add(offsetMinField);

            var offsetMaxField = new Vector2Field("OffsetMax");
            offsetMaxField.value = new Vector2(OffsetMaxX, OffsetMaxY);
            offsetMaxField.RegisterValueChangedCallback(evt =>
            {
                OffsetMaxX = evt.newValue.x;
                OffsetMaxY = evt.newValue.y;
                onValueChanged?.Invoke();
            });
            offsetMaxField.style.display = DisplayStyle.None;
            root.Add(offsetMaxField);

            // 创建锚点预设下拉框
            root.Add(new Label("Anchor"));
            var anchorPresetDropdown = new DropdownField("", GetAnchorPresetOptions(), 0);
            anchorPresetDropdown.RegisterValueChangedCallback(evt =>
            {
                ApplyAnchorPreset(evt.newValue, target, offsetMinField, offsetMaxField);
                onValueChanged?.Invoke();
            });
            root.Add(anchorPresetDropdown);

            root.Add(new Label("Pivot"));
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var xField = new FloatField { label = "X", value = PivotX };
            xField.labelElement.style.minWidth = 12;
            xField.labelElement.style.marginRight = 2;
            xField.style.flexGrow = 1;
            xField.RegisterValueChangedCallback(evt =>
            {
                PivotX = evt.newValue;
                UpdateRelateFields(target);
                onValueChanged?.Invoke();
            });
            var yField = new FloatField { label = "Y", value = PivotY };
            yField.labelElement.style.minWidth = 12;
            yField.labelElement.style.marginRight = 2;
            yField.style.flexGrow = 1;
            yField.RegisterValueChangedCallback(evt =>
            {
                PivotY = evt.newValue;
                onValueChanged?.Invoke();
            });
            row.Add(xField);
            row.Add(yField);
            root.Add(row);
            
            // 获取当前应该处于的anchor预设并设置dropdown
            string currentPreset = GetCurrentAnchorPreset();
            var presetOptions = GetAnchorPresetOptions();
            int presetIndex = presetOptions.IndexOf(currentPreset);
            anchorPresetDropdown.index = presetIndex;
            ApplyAnchorPreset(currentPreset, target, offsetMinField, offsetMaxField);
            #endif
        }
#if UNITY_EDITOR
        private FloatField posXField;
        private FloatField posYField;
        private FloatField widthField;
        private FloatField heightField;
        private FloatField topField;
        private FloatField bottomField;
        private FloatField leftField;
        private FloatField rightField;

        void UpdateRelateFields(ModifierTarget target)
        {
            UpdateRelateFields(target, new Vector2(AnchorMinX,AnchorMinY),  new Vector2(AnchorMaxX,AnchorMaxY),
                new Vector2(OffsetMinX,OffsetMinY), new Vector2(OffsetMaxX,OffsetMaxY));
        }
        
        void UpdateRelateFields(ModifierTarget target, Vector2 oldAnchorMin, Vector2 oldAnchorMax, Vector2 oldOffsetMin, Vector2 oldOffsetMax)
        {
            if (target?.TargetObject is RectTransform rectTransform && rectTransform.parent is RectTransform parentRect)
            {
                // 获取父节点的尺寸和位置（注意rect的位置信息）
                Rect parentRectData = parentRect.rect;
                float parentWidth = parentRectData.width;
                float parentHeight = parentRectData.height;
                float parentX = parentRectData.x;
                float parentY = parentRectData.y;

                // 计算绝对边界
                Vector2 absoluteMin = new Vector2(
                    parentX + parentRectData.width * oldAnchorMin.x + oldOffsetMin.x,
                    parentY + parentRectData.height * oldAnchorMin.y + oldOffsetMin.y
                );
                Vector2 absoluteMax = new Vector2(
                    parentX + parentRectData.width * oldAnchorMax.x + oldOffsetMax.x,
                    parentY + parentRectData.height * oldAnchorMax.y + oldOffsetMax.y
                );
                
                // 计算锚点的实际位置（在父节点坐标系中）
                float anchorMinPosX = parentX + AnchorMinX * parentWidth;
                float anchorMinPosY = parentY + AnchorMinY * parentHeight;
                float anchorMaxPosX = parentX + AnchorMaxX * parentWidth;
                float anchorMaxPosY = parentY + AnchorMaxY * parentHeight;

                // 计算矩形的四个边界在父节点坐标系中的绝对位置
                float left = anchorMinPosX + OffsetMinX;
                float right = anchorMaxPosX + OffsetMaxX;
                float bottom = anchorMinPosY + OffsetMinY;
                float top = anchorMaxPosY + OffsetMaxY;

                // 计算宽度和高度
                float width = absoluteMax.x - absoluteMin.x;
                float height = absoluteMax.y - absoluteMin.y;

                // 计算不考虑pivot的矩形中心
                float centerX = (absoluteMin.x + absoluteMax.x) * 0.5f;
                float centerY = (absoluteMin.y + absoluteMax.y) * 0.5f;

                Vector2 anchorPos = new Vector2(
                    parentRectData.x + parentRectData.width * AnchorMinX,
                    parentRectData.y + parentRectData.height * AnchorMinY
                );
    
                Vector2 pivotAbsolutePos = new Vector2(
                    centerX + (PivotX - 0.5f) * width,
                    centerY + (PivotY - 0.5f) * height
                );
    
                var posX = pivotAbsolutePos.x - anchorPos.x;
                var posY = pivotAbsolutePos.y - anchorPos.y;

                // 计算四边的距离（相对于父节点边界）
                float leftDistance = left - parentX; // 相对于父节点左边界的距离
                float rightDistance = (parentX + parentWidth) - right; // 相对于父节点右边界的距离
                float topDistance = (parentY + parentHeight) - top; // 相对于父节点上边界的距离
                float bottomDistance = bottom - parentY; // 相对于父节点下边界的距离

                // 更新字段值
                posXField.SetValueWithoutNotify(posX);
                posYField.SetValueWithoutNotify(posY);
                widthField.SetValueWithoutNotify(width);
                heightField.SetValueWithoutNotify(height);
                topField.SetValueWithoutNotify(topDistance);
                bottomField.SetValueWithoutNotify(bottomDistance);
                leftField.SetValueWithoutNotify(leftDistance);
                rightField.SetValueWithoutNotify(rightDistance);

                // 确定显示哪些字段
                bool anchorsAreSinglePointX = Mathf.Approximately(AnchorMinX, AnchorMaxX);
                bool anchorsAreSinglePointY = Mathf.Approximately(AnchorMinY, AnchorMaxY);

                // 设置字段可见性
                posXField.style.display = anchorsAreSinglePointX ? DisplayStyle.Flex : DisplayStyle.None;
                posYField.style.display = anchorsAreSinglePointY ? DisplayStyle.Flex : DisplayStyle.None;
                widthField.style.display = anchorsAreSinglePointX ? DisplayStyle.Flex : DisplayStyle.None;
                heightField.style.display = anchorsAreSinglePointY ? DisplayStyle.Flex : DisplayStyle.None;

                leftField.style.display = !anchorsAreSinglePointX ? DisplayStyle.Flex : DisplayStyle.None;
                rightField.style.display = !anchorsAreSinglePointX ? DisplayStyle.Flex : DisplayStyle.None;
                topField.style.display = !anchorsAreSinglePointY ? DisplayStyle.Flex : DisplayStyle.None;
                bottomField.style.display = !anchorsAreSinglePointY ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                // 如果没有有效的RectTransform，重置所有字段
                ResetAllFields();
            }
        }

// 辅助方法：重置所有字段
        void ResetAllFields()
        {
            posXField.SetValueWithoutNotify(0);
            posYField.SetValueWithoutNotify(0);
            widthField.SetValueWithoutNotify(0);
            heightField.SetValueWithoutNotify(0);
            topField.SetValueWithoutNotify(0);
            bottomField.SetValueWithoutNotify(0);
            leftField.SetValueWithoutNotify(0);
            rightField.SetValueWithoutNotify(0);

            // 隐藏所有字段
            posXField.style.display = DisplayStyle.None;
            posYField.style.display = DisplayStyle.None;
            widthField.style.display = DisplayStyle.None;
            heightField.style.display = DisplayStyle.None;
            leftField.style.display = DisplayStyle.None;
            rightField.style.display = DisplayStyle.None;
            topField.style.display = DisplayStyle.None;
            bottomField.style.display = DisplayStyle.None;
        }

        // 根据可见字段值计算并更新 offset 值
        void CalculateOffsetFromVisibleFields(ModifierTarget target, Action onValueChanged)
        {
            if (target?.TargetObject is not RectTransform rectTransform ||
                rectTransform.parent is not RectTransform parentRect)
                return;

            Rect parentRectData = parentRect.rect;
            float parentWidth = parentRectData.width;
            float parentHeight = parentRectData.height;
            float parentX = parentRectData.x;
            float parentY = parentRectData.y;

            // 确定字段可见性
            bool anchorsAreSinglePointX = Mathf.Approximately(AnchorMinX, AnchorMaxX);
            bool anchorsAreSinglePointY = Mathf.Approximately(AnchorMinY, AnchorMaxY);

            // 计算锚点的实际位置（在父节点坐标系中）
            float anchorMinPosX = parentX + AnchorMinX * parentWidth;
            float anchorMinPosY = parentY + AnchorMinY * parentHeight;
            float anchorMaxPosX = parentX + AnchorMaxX * parentWidth;
            float anchorMaxPosY = parentY + AnchorMaxY * parentHeight;

            // 计算 X 轴的 offset
            if (anchorsAreSinglePointX)
            {
                // 使用 posX 和 width
                float posX = posXField.value;
                float width = widthField.value;
                
                // 计算矩形中心（不考虑 pivot）
                float anchorPosX = anchorMinPosX; // 单点锚点时 anchorMinPosX == anchorMaxPosX
                float centerX = anchorPosX + posX + (PivotX - 0.5f) * width;
                
                // 计算 left 和 right
                float left = centerX - width * 0.5f;
                float right = centerX + width * 0.5f;
                
                // 计算 offset
                OffsetMinX = left - anchorMinPosX;
                OffsetMaxX = right - anchorMaxPosX;
            }
            else
            {
                // 使用 left 和 right
                float leftDistance = leftField.value;
                float rightDistance = rightField.value;
                
                // 计算绝对位置
                float left = parentX + leftDistance;
                float right = (parentX + parentWidth) - rightDistance;
                
                // 计算 offset
                OffsetMinX = left - anchorMinPosX;
                OffsetMaxX = right - anchorMaxPosX;
            }

            // 计算 Y 轴的 offset
            if (anchorsAreSinglePointY)
            {
                // 使用 posY 和 height
                float posY = posYField.value;
                float height = heightField.value;
                
                // 计算矩形中心（不考虑 pivot）
                float anchorPosY = anchorMinPosY; // 单点锚点时 anchorMinPosY == anchorMaxPosY
                float centerY = anchorPosY + posY + (PivotY - 0.5f) * height;
                
                // 计算 bottom 和 top
                float bottom = centerY - height * 0.5f;
                float top = centerY + height * 0.5f;
                
                // 计算 offset
                OffsetMinY = bottom - anchorMinPosY;
                OffsetMaxY = top - anchorMaxPosY;
            }
            else
            {
                // 使用 top 和 bottom
                float topDistance = topField.value;
                float bottomDistance = bottomField.value;
                
                // 计算绝对位置
                float bottom = parentY + bottomDistance;
                float top = (parentY + parentHeight) - topDistance;
                
                // 计算 offset
                OffsetMinY = bottom - anchorMinPosY;
                OffsetMaxY = top - anchorMaxPosY;
            }

            onValueChanged?.Invoke();
        }

// 获取锚点预设选项列表
        private List<string> GetAnchorPresetOptions()
        {
            return new List<string>
            {
                "左上角",
                "上边居中",
                "右上角",
                "左边居中",
                "中心",
                "右边居中",
                "左下角",
                "下边居中",
                "右下角",
                "水平拉伸-顶部",
                "水平拉伸-居中",
                "水平拉伸-底部",
                "垂直拉伸-左边",
                "垂直拉伸-居中",
                "垂直拉伸-右边",
                "四边拉伸"
            };
        }

// 根据当前锚点值获取对应的预设名称
        private string GetCurrentAnchorPreset()
        {
            // 使用 Mathf.Approximately 来比较浮点数，避免精度问题
            bool minXIs0 = Mathf.Approximately(AnchorMinX, 0f);
            bool minXIs05 = Mathf.Approximately(AnchorMinX, 0.5f);
            bool minXIs1 = Mathf.Approximately(AnchorMinX, 1f);
            bool minYIs0 = Mathf.Approximately(AnchorMinY, 0f);
            bool minYIs05 = Mathf.Approximately(AnchorMinY, 0.5f);
            bool minYIs1 = Mathf.Approximately(AnchorMinY, 1f);
            bool maxXIs0 = Mathf.Approximately(AnchorMaxX, 0f);
            bool maxXIs05 = Mathf.Approximately(AnchorMaxX, 0.5f);
            bool maxXIs1 = Mathf.Approximately(AnchorMaxX, 1f);
            bool maxYIs0 = Mathf.Approximately(AnchorMaxY, 0f);
            bool maxYIs05 = Mathf.Approximately(AnchorMaxY, 0.5f);
            bool maxYIs1 = Mathf.Approximately(AnchorMaxY, 1f);

            // 检查是否为单点锚点（Min和Max相同）
            bool isSinglePointX = Mathf.Approximately(AnchorMinX, AnchorMaxX);
            bool isSinglePointY = Mathf.Approximately(AnchorMinY, AnchorMaxY);

            if (isSinglePointX && isSinglePointY)
            {
                // 单点锚点预设
                if (minXIs0 && minYIs1) return "左上角";
                if (minXIs05 && minYIs1) return "上边居中";
                if (minXIs1 && minYIs1) return "右上角";
                if (minXIs0 && minYIs05) return "左边居中";
                if (minXIs05 && minYIs05) return "中心";
                if (minXIs1 && minYIs05) return "右边居中";
                if (minXIs0 && minYIs0) return "左下角";
                if (minXIs05 && minYIs0) return "下边居中";
                if (minXIs1 && minYIs0) return "右下角";
            }
            else if (!isSinglePointX && isSinglePointY)
            {
                // 水平拉伸预设
                if (minXIs0 && maxXIs1 && minYIs1 && maxYIs1) return "水平拉伸-顶部";
                if (minXIs0 && maxXIs1 && minYIs05 && maxYIs05) return "水平拉伸-居中";
                if (minXIs0 && maxXIs1 && minYIs0 && maxYIs0) return "水平拉伸-底部";
            }
            else if (isSinglePointX && !isSinglePointY)
            {
                // 垂直拉伸预设
                if (minXIs0 && maxXIs0 && minYIs0 && maxYIs1) return "垂直拉伸-左边";
                if (minXIs05 && maxXIs05 && minYIs0 && maxYIs1) return "垂直拉伸-居中";
                if (minXIs1 && maxXIs1 && minYIs0 && maxYIs1) return "垂直拉伸-右边";
            }
            else if (!isSinglePointX && !isSinglePointY)
            {
                // 四边拉伸
                if (minXIs0 && maxXIs1 && minYIs0 && maxYIs1) return "四边拉伸";
            }

            // 如果没有匹配的预设，返回第一个预设作为默认值
            return "左上角";
        }

// 应用锚点预设
        private void ApplyAnchorPreset(string presetName, ModifierTarget target, Vector2Field offsetMinField,
            Vector2Field offsetMaxField)
        {
            if (target?.TargetObject is not RectTransform rectTransform ||
                rectTransform.parent is not RectTransform parentRect)
                return;

            float parentWidth = parentRect.rect.width;
            float parentHeight = parentRect.rect.height;

            // 保存当前的矩形信息用于计算
            float oldAnchorMinX = AnchorMinX;
            float oldAnchorMinY = AnchorMinY;
            float oldAnchorMaxX = AnchorMaxX;
            float oldAnchorMaxY = AnchorMaxY;

            // 先设置新的锚点值
            switch (presetName)
            {
                case "左上角":
                    AnchorMinX = 0f;
                    AnchorMinY = 1f;
                    AnchorMaxX = 0f;
                    AnchorMaxY = 1f;
                    break;
                case "上边居中":
                    AnchorMinX = 0.5f;
                    AnchorMinY = 1f;
                    AnchorMaxX = 0.5f;
                    AnchorMaxY = 1f;
                    break;
                case "右上角":
                    AnchorMinX = 1f;
                    AnchorMinY = 1f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 1f;
                    break;
                case "左边居中":
                    AnchorMinX = 0f;
                    AnchorMinY = 0.5f;
                    AnchorMaxX = 0f;
                    AnchorMaxY = 0.5f;
                    break;
                case "中心":
                    AnchorMinX = 0.5f;
                    AnchorMinY = 0.5f;
                    AnchorMaxX = 0.5f;
                    AnchorMaxY = 0.5f;
                    break;
                case "右边居中":
                    AnchorMinX = 1f;
                    AnchorMinY = 0.5f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 0.5f;
                    break;
                case "左下角":
                    AnchorMinX = 0f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 0f;
                    AnchorMaxY = 0f;
                    break;
                case "下边居中":
                    AnchorMinX = 0.5f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 0.5f;
                    AnchorMaxY = 0f;
                    break;
                case "右下角":
                    AnchorMinX = 1f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 0f;
                    break;
                case "水平拉伸-顶部":
                    AnchorMinX = 0f;
                    AnchorMinY = 1f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 1f;
                    break;
                case "水平拉伸-居中":
                    AnchorMinX = 0f;
                    AnchorMinY = 0.5f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 0.5f;
                    break;
                case "水平拉伸-底部":
                    AnchorMinX = 0f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 0f;
                    break;
                case "垂直拉伸-左边":
                    AnchorMinX = 0f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 0f;
                    AnchorMaxY = 1f;
                    break;
                case "垂直拉伸-居中":
                    AnchorMinX = 0.5f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 0.5f;
                    AnchorMaxY = 1f;
                    break;
                case "垂直拉伸-右边":
                    AnchorMinX = 1f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 1f;
                    break;
                case "四边拉伸":
                    AnchorMinX = 0f;
                    AnchorMinY = 0f;
                    AnchorMaxX = 1f;
                    AnchorMaxY = 1f;
                    break;
            }

            // 计算锚点变化量并调整offset值，保持当前位置和尺寸
            float deltaMinX = AnchorMinX - oldAnchorMinX;
            float deltaMinY = AnchorMinY - oldAnchorMinY;
            float deltaMaxX = AnchorMaxX - oldAnchorMaxX;
            float deltaMaxY = AnchorMaxY - oldAnchorMaxY;

            var oldAnchorMin = new Vector2(oldAnchorMinX, oldAnchorMinY);
            var oldAnchorMax = new Vector2(oldAnchorMaxX, oldAnchorMaxY);
            var oldOffsetMin = new Vector2(OffsetMinX, OffsetMinY);
            var oldOffsetMax = new Vector2(OffsetMaxX, OffsetMaxY);
            
            // 调整offset值来补偿锚点变化
            OffsetMinX -= deltaMinX * parentWidth;
            OffsetMinY -= deltaMinY * parentHeight;
            OffsetMaxX -= deltaMaxX * parentWidth;
            OffsetMaxY -= deltaMaxY * parentHeight;

            // 更新UI字段
            offsetMinField.value = new Vector2(OffsetMinX, OffsetMinY);
            offsetMaxField.value = new Vector2(OffsetMaxX, OffsetMaxY);
            UpdateRelateFields(target, oldAnchorMin,  oldAnchorMax, oldOffsetMin, oldOffsetMax);
        }
#endif
        public override BaseModifier Clone()
        {
            return new RectModifier
            {
                ModifierType = this.ModifierType,
                OffsetMinX = this.OffsetMinX,
                OffsetMinY = this.OffsetMinY,
                OffsetMaxX = this.OffsetMaxX,
                OffsetMaxY = this.OffsetMaxY,
                AnchorMinX = this.AnchorMinX,
                AnchorMinY = this.AnchorMinY,
                AnchorMaxX = this.AnchorMaxX,
                AnchorMaxY = this.AnchorMaxY,
                PivotX = this.PivotX,
                PivotY = this.PivotY
            };
        }
    }
}