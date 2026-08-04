using UnityEngine.UI;
using UnityEngine;

namespace LuaObjectBind.Handlers
{
    public class TextHandler : AFieldBindHandler<Text>
    {
        protected override void HandleString(Text comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.Text_Text:
                    comp.text = value;
                    break;
            }
        }
        
        protected override string GetString(Text comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Text_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        protected override void HandleFloat(Text comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.Text_FontSize:
                    comp.fontSize = Mathf.RoundToInt(value);
                    break;
            }
        }

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