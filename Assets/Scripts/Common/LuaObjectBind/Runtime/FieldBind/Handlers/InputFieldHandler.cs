using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class InputFieldHandler : AFieldBindHandler<InputField>
    {
        protected override void HandleString(InputField comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Text:
                    comp.text = value;
                    break;
            }
        }

        protected override string GetString(InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        protected override void HandleBool(InputField comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 