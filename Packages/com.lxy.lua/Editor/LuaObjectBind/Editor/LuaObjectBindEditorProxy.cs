using System;
using System.Collections.Generic;
using Game;
using StateControl.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LuaObjectBind.Editor
{
    public static class LuaObjectBindEditorProxy
    {
        private static Dictionary<string, Type> comp2Types = new Dictionary<string, Type>()
        {
            {"Go", null},
            
            {"Btn", typeof(Button)},
            {"BtnEx", typeof(Button)},
            
            {"DropDownEx", typeof(Dropdown)},
            
            {"Img", typeof(Image)},
            {"Image", typeof(Image)},
            {"Image", typeof(Image)},
            //{"ImageFilled", typeof(ImageFilled)},
            //{"FilledEx", typeof(ImageFilled)},
            
            {"Input", typeof(InputField)},
            {"InputField", typeof(InputField)},
            {"InF", typeof(InputField)},
            
            //{"List", typeof(CircularScroll.CircularScrollListView)},
            //{"ListView", typeof(CircularScroll.CircularScrollListView)},
            
            {"RawImage", typeof(RawImage)},
            
            {"Tf", typeof(Transform)},
            {"Trans", typeof(Transform)},
            {"Rtf", typeof(RectTransform)},
            {"RectTrans", typeof(RectTransform)},
            
            //{"ScrollView", typeof(ScrollView)},
            //{"SelectScroll", typeof(SelectScroll)},
            
            {"Sld", typeof(Slider)},
            {"Slider", typeof(Slider)},
            
            {"SR", typeof(ScrollRect)},
            {"ScrollRect", typeof(ScrollRect)},
            
            {"SCon", typeof(StateController)},
            {"StateController", typeof(StateController)},
            
            {"Txt", typeof(Text)},
            {"Text", typeof(Text)},
            {"Text", typeof(Text)},
            
            {"Tgl", typeof(Toggle)},
            {"Toggle", typeof(Toggle)},
            {"ToggleGroup", typeof(ToggleGroup)},
            
            {"Animator", typeof(Animator)},
            {"Animation", typeof(Animation)},
            //{"AnimController", typeof(AnimationController)}
        };
        
        /// <summary>
        /// 获取ComponentAbbr。
        /// </summary>
        public static Object GetComponentAbbr(GameObject gameObject, string compName)
        {
            if (!comp2Types.TryGetValue(compName, out var type))
                return null;

            if (type == null)
                return gameObject;

            return gameObject.GetComponent(type);
        }

        /// <summary>
        /// 获取Field绑定EnumAbbr。
        /// </summary>
        public static FieldBindEnum GetFieldBindEnumAbbr(string name)
        {
            switch (name)
            {
                case "Text":
                    return FieldBindEnum.TextMeshProUGUI_Text;
                case "InputText":
                    return FieldBindEnum.TMP_InputField_Text;
                case "Active":
                    return FieldBindEnum.GameObject_Active;
                case "ImgSprite":
                    return FieldBindEnum.Image_SpritePath;
                case "ImgColor":
                    return FieldBindEnum.Image_Color;
            }

            return FieldBindEnum.None;
        }
    }
}