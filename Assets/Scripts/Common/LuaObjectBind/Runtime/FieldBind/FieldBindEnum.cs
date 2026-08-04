using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LuaObjectBind
{
    public enum FieldBindEnum
    {
        None = 0,
        [FieldBindType(FieldBindTypeEnum.String, typeof(Text))]
        Text_Text,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(Text))]
        Text_FontSize,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(GameObject))]
        GameObject_Active,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(MonoBehaviour))]
        MonoBehaviour_Enabled,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(Button))]
        Button_Interactable,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(Toggle))]
        Toggle_IsOn,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(Slider))]
        Slider_Value,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(ScrollRect))]
        ScrollRect_HorizontalNormalizedPosition,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(ScrollRect))]
        ScrollRect_VerticalNormalizedPosition,
        [FieldBindType(FieldBindTypeEnum.String, typeof(InputField))]
        InputField_Text,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(CanvasGroup))]
        CanvasGroup_Interactable,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(CanvasGroup))]
        CanvasGroup_Alpha,
        [FieldBindType(FieldBindTypeEnum.Vector2, typeof(RectTransform))]
        RectTransform_AnchoredPosition,
        [FieldBindType(FieldBindTypeEnum.Vector2, typeof(RectTransform))]
        RectTransform_SizeDelta,
        [FieldBindType(FieldBindTypeEnum.Vector3, typeof(Transform))]
        Transform_Position,
        [FieldBindType(FieldBindTypeEnum.Vector3, typeof(Transform))]
        Transform_LocalPosition,
        [FieldBindType(FieldBindTypeEnum.Vector3, typeof(Transform))]
        Transform_EulerAngles,
        [FieldBindType(FieldBindTypeEnum.Vector3, typeof(Transform))]
        Transform_LocalEulerAngles,
        [FieldBindType(FieldBindTypeEnum.Vector3, typeof(Transform))]
        Transform_LocalScale,
        [FieldBindType(FieldBindTypeEnum.String, typeof(TextMeshProUGUI))]
        TextMeshProUGUI_Text,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(TextMeshProUGUI))]
        TextMeshProUGUI_FontSize,
        [FieldBindType(FieldBindTypeEnum.String, typeof(TMP_InputField))]
        TMP_InputField_Text,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(Image))]
        Image_FillAmount,
        [FieldBindType(FieldBindTypeEnum.String, typeof(Image))]
        Image_SpritePath,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(Dropdown))]
        Dropdown_Interactable,
        [FieldBindType(FieldBindTypeEnum.Int, typeof(Dropdown))]
        Dropdown_Value,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(Slider))]
        Slider_Interactable,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(InputField))]
        InputField_Interactable,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(TMP_InputField))]
        TMP_InputField_Interactable,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(Selectable))]
        Selectable_Interactable,
        [FieldBindType(FieldBindTypeEnum.Float, typeof(AudioSource))]
        AudioSource_Volume,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(AudioSource))]
        AudioSource_Mute,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(AudioSource))]
        AudioSource_PlayOnAwake,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(AudioSource))]
        AudioSource_Loop,
        [FieldBindType(FieldBindTypeEnum.Bool, typeof(CanvasGroup))]
        CanvasGroup_BlocksRaycasts,
        [FieldBindType(FieldBindTypeEnum.LuaTable, typeof(Image))]
        Image_Color,
    }

    public enum FieldBindTypeEnum
    {
        None = 0,
        Int,
        Float,
        String,
        Bool,
        Vector2,
        Vector3,
        LuaTable,
    }
    
    public class FieldBindTypeAttribute: Attribute
    {
        public FieldBindTypeEnum FieldBindType { get; private set; }
        public Type CanBindObjectType { get; private set; }

        public FieldBindTypeAttribute(FieldBindTypeEnum fieldBindType, Type canBindObjectType)
        {
            FieldBindType = fieldBindType;
            CanBindObjectType = canBindObjectType;
        }
    }
}