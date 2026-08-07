using TMPro;

namespace LuaObjectBind.Handlers
{
    public class TMP_InputFieldHandler : AFieldBindHandler<TMP_InputField>
    {
        protected override void HandleString(TMP_InputField comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Text:
                    comp.text = value;
                    break;
            }
        }

        protected override string GetString(TMP_InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        protected override void HandleBool(TMP_InputField comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(TMP_InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 