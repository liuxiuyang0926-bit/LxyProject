using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class ButtonHandler : AFieldBindHandler<Button>
    {
        protected override void HandleBool(Button comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Button_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(Button comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Button_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 