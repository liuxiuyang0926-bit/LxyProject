using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace LuaObjectBind.Handlers
{
    public class ImageHandler : AFieldBindHandler<Image>
    {
        /// <summary>
        /// 处理浮点数。
        /// </summary>
        protected override void HandleFloat(Image comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.Image_FillAmount:
                    comp.fillAmount = value;
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(Image comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Image_FillAmount:
                    return comp.fillAmount;
            }

            return 0;
        }

        /// <summary>
        /// 处理Lua表。
        /// </summary>
        protected override void HandleLuaTable(Image obj, FieldBindEnum type, LuaTable value)
        {
            switch (type)
            {
                case FieldBindEnum.Image_Color:
                    float r = value.Get<float>("r");
                    float g = value.Get<float>("g");
                    float b = value.Get<float>("b");
                    float a = value.Get<float>("a");
                    Color color = new Color(r,  g, b, a);
                    obj.color = color;
                    break;
            }
        }
        
        /// <summary>
        /// 获取Lua表。
        /// </summary>
        protected override LuaTable GetLuaTable(Image obj, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Image_Color:
                    Color color = obj.color;
                    LuaEnv luaEnv = LuaObjectBindProxy.GetLuaEnv();
                    LuaTable luaTable = luaEnv.NewTable();
                    luaTable.Set("r", color.r);
                    luaTable.Set("g", color.g);
                    luaTable.Set("b", color.b);
                    luaTable.Set("a", color.a);
                    return luaTable;
            }

            return null;
        }

        /// <summary>
        /// 处理字符串。
        /// </summary>
        protected override void HandleString(Image obj, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.Image_SpritePath:
                    LuaObjectBindProxy.SetSpriteByPath(obj, value);
                    break;
            }
        }
        
        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected override string GetString(Image obj, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Image_SpritePath:
                    return LuaObjectBindProxy.GetSpritePath(obj.sprite);
            }

            return string.Empty;
        }
    }
} 