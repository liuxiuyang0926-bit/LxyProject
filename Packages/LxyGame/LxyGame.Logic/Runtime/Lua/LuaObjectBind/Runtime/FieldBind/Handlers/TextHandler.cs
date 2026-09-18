using UnityEngine.UI;
using UnityEngine;

namespace LuaObjectBind.Handlers
{
    public class TextHandler : AFieldBindHandler<Text>
    {
        /// <summary>
        /// 处理字符串。
        /// </summary>
        protected override void HandleString(Text comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.Text_Text:
                    comp.text = value;
                    break;
            }
        }
        
        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected override string GetString(Text comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Text_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        /// <summary>
        /// 处理浮点数。
        /// </summary>
        protected override void HandleFloat(Text comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.Text_FontSize:
                    comp.fontSize = Mathf.RoundToInt(value);
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(Text comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Text_FontSize:
                    return comp.fontSize;
            }

            return 0;
        }
    }
}