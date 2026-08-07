using Game;
using TMPro;
using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class TextExHandler : AFieldBindHandler<Text>
    {
        protected override void HandleString(Text comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    comp.text = value;
                    break;
            }
        }

        protected override string GetString(Text comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        protected override void HandleFloat(Text obj, FieldBindEnum type, float value)
        {
            switch (type)
            {
                default:
                    break;
            }
        }

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