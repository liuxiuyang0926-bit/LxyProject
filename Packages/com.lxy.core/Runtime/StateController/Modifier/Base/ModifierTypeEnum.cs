using System;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StateControl.Runtime
{
    // 有新增枚举要写到最下面，顺序不能更改
    public enum ModifierTypeEnum
    {
        [ModifierRequire(typeof(GameObject), typeof(BoolModifier))]
        [ModifierName("游戏对象激活")]
        GameObjectActive,
        
        [ModifierRequire(typeof(UnityEngine.UI.Image), typeof(ColorModifier))]
        [ModifierName("图片颜色")]
        ImageColor,
        
        [ModifierRequire(typeof(UnityEngine.UI.Text), typeof(IntModifier))]
        [ModifierName("文本整数(不要用)")]
        TextInt,
        
        [ModifierRequire(typeof(TMP_Text), typeof(ColorModifier))]
        [ModifierName("文本颜色")]
        TextColor,
        
        [ModifierRequire(typeof(UnityEngine.UI.Text), typeof(FloatModifier))]
        [ModifierName("文本浮点数(不要用)")]
        TextFloat,
        
        [ModifierRequire(typeof(Transform), typeof(Vector3Modifier))]
        [ModifierName("位置")]
        TransformPosition,
        
        [ModifierRequire(typeof(RectTransform), typeof(Vector3Modifier))]
        [ModifierName("Rect位置")]
        RectTransformPosition,
        
        [ModifierRequire(typeof(Transform), typeof(Vector3Modifier))]
        [ModifierName("缩放")]
        TransformScale,
        
        [ModifierRequire(typeof(Transform), typeof(Vector3Modifier))]
        [ModifierName("旋转")]
        TransformRotation,
        
        [ModifierRequire(typeof(Behaviour), typeof(BoolModifier))]
        [ModifierName("组件启用")]
        ComponentEnabled,
        
        [ModifierRequire(typeof(TMP_Text), typeof(StringModifier))]
        [ModifierName("TMP文本(不要用)")]
        TMPTextString,
        
        [ModifierRequire(typeof(ScrollRect), typeof(BoolModifier))]
        [ModifierName("滚动视图启用")]
        ScrollRectEnabled,
        
        [ModifierRequire(typeof(ScrollRect), typeof(FloatModifier))]
        [ModifierName("水平滚动位置")]
        ScrollRectHorizontal,
        
        [ModifierRequire(typeof(ScrollRect), typeof(FloatModifier))]
        [ModifierName("垂直滚动位置")]
        ScrollRectVertical,
        
        [ModifierRequire(typeof(RectTransform), typeof(Vector2Modifier))]
        [ModifierName("锚点位置")]
        RectTransformAnchoredPosition,
        
        [ModifierRequire(typeof(RectTransform), typeof(Vector3Modifier))]
        [ModifierName("锚点位置3D")]
        RectTransformAnchoredPosition3D,
        
        [ModifierRequire(typeof(RectTransform), typeof(Vector2Modifier))]
        [ModifierName("尺寸大小")]
        RectTransformSizeDelta,
        
        [ModifierRequire(typeof(Canvas), typeof(IntModifier))]
        [ModifierName("画布排序")]
        CanvasSortingOrder,
        
        [ModifierRequire(typeof(UnityEngine.UI.Button), typeof(BoolModifier))]
        [ModifierName("按钮可交互")]
        ButtonInteractable,    
        
        [ModifierRequire(typeof(UnityEngine.UI.Image), typeof(SpriteModifier))]
        [ModifierName("图片")]
        ImageSprite,
        
        [ModifierRequire(typeof(TMP_Text), typeof(FloatModifier))]
        [ModifierName("字号大小")]
        TMPTextFontSize,
        
        [ModifierRequire(typeof(TMP_Text), typeof(TMPTextAlignmentModifier))]
        [ModifierName("文本对齐方式")]
        TMPTextAlignment,
        
        [ModifierRequire(typeof(UnityEngine.UI.LayoutGroup), typeof(LayoutGroupChildAlignmentModifier))]
        [ModifierName("子项对齐方式")]
        LayoutGroupChildAlignment,
        
        [ModifierRequire(typeof(UnityEngine.UI.LayoutGroup), typeof(LayoutGroupPaddingModifier))]
        [ModifierName("内边距")]
        LayoutGroupPadding,
        
        [ModifierRequire(typeof(UnityEngine.RectTransform), typeof(RectTransformPivotModifier))]
        [ModifierName("Pivot")]
        RectTransformPivot,
        
        [ModifierRequire(typeof(UnityEngine.RectTransform), typeof(RectTransformAnchorModifier))]
        [ModifierName("Anchor")]
        RectTransformAnchor,
        
        [ModifierRequire(typeof(UnityEngine.RectTransform), typeof(RectTransformOffsetModifier))]
        [ModifierName("Offset")]
        RectTransformOffset,
        
        [ModifierRequire(typeof(UnityEngine.RectTransform), typeof(RectModifier))]
        [ModifierName("整个RectTransform")]
        RectTransformRect,

        [ModifierRequire(typeof(StateController), typeof(StateControlModifier))]
        [ModifierName("状态控制")]
        StateControlChangeState,

        [ModifierRequire(typeof(DOTweenAnimation), typeof(DOTweenAnimationModifier))]
        [ModifierName("DOTween动画播放")]
        DOTweenAnimationPlay,
    }
    
    public static class EnumExtension
    {
        public static Type GetUseModifierType(this ModifierTypeEnum em)
        {
            Type type = em.GetType();
            FieldInfo fd = type.GetField(em.ToString());
            if (fd == null)
                return null;
            object[] attrs = fd.GetCustomAttributes(typeof(ModifierRequireAttribute), false);
            Type result = null;
            foreach (ModifierRequireAttribute attr in attrs)
            {
                result = attr.UseModifierType;
            }
            return result;
        }
        
        public static Type GetTargetType(this ModifierTypeEnum em)
        {
            Type type = em.GetType();
            FieldInfo fd = type.GetField(em.ToString());
            if (fd == null)
                return null;
            object[] attrs = fd.GetCustomAttributes(typeof(ModifierRequireAttribute), false);
            Type result = null;
            foreach (ModifierRequireAttribute attr in attrs)
            {
                result = attr.TargetType;
            }
            return result;
        }
        
        public static string GetChineseName(this ModifierTypeEnum em)
        {
            Type type = em.GetType();
            FieldInfo fd = type.GetField(em.ToString());
            if (fd == null)
                return em.ToString();
            object[] attrs = fd.GetCustomAttributes(typeof(ModifierNameAttribute), false);
            foreach (ModifierNameAttribute attr in attrs)
            {
                return attr.ChineseName;
            }
            return em.ToString();
        }
    }
}
