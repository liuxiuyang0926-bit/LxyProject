using Game;
using TMPro;
using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class TextExHandler : AFieldBindHandler<Text>
    {
        /// <summary>
        /// 将字符串值写入文本组件。
        /// </summary>
        protected override void HandleString(Text comp, FieldBindEnum type, string value)
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
        protected override string GetString(Text comp, FieldBindEnum type)
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
        protected override void HandleFloat(Text obj, FieldBindEnum type, float value)
        {
            switch (type)
            {
                default:
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(Text obj, FieldBindEnum type)
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