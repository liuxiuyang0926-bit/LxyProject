using TMPro;

namespace LuaObjectBind.Handlers
{
    public class TextMeshProUGUIHandler : AFieldBindHandler<TextMeshProUGUI>
    {
        protected override void HandleString(TextMeshProUGUI comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    comp.text = value;
                    break;
            }
        }

        protected override string GetString(TextMeshProUGUI comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TextMeshProUGUI_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        protected override void HandleFloat(TextMeshProUGUI obj, FieldBindEnum type, float value)
        {
            switch (type)
            {   
                case FieldBindEnum.TextMeshProUGUI_FontSize:
                    obj.fontSize = value;
                    break;
            }
        }

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