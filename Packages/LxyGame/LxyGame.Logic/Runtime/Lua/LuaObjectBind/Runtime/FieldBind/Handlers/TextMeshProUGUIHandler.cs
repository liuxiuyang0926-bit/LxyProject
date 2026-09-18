using TMPro;

namespace LuaObjectBind.Handlers
{
    public class TextMeshProUGUIHandler : AFieldBindHandler<TextMeshProUGUI>
    {
        /// <summary>
        /// 处理字符串。
        /// </summary>
        protected override void HandleString(TextMeshProUGUI comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    comp.text = value;
                    break;
            }
        }

        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected override string GetString(TextMeshProUGUI comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        /// <summary>
        /// 处理浮点数。
        /// </summary>
        protected override void HandleFloat(TextMeshProUGUI obj, FieldBindEnum type, float value)
        {
            switch (type)
            {   
                case FieldBindEnum.TextMeshProUGUI_FontSize:
                    obj.fontSize = value;
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(TextMeshProUGUI obj, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Text_FontSize:
                    return obj.fontSize;
            }

            return 0;
        }
    }
} 