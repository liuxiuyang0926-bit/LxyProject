using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class DropdownHandler : AFieldBindHandler<Dropdown>
    {
        protected override void HandleBool(Dropdown comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(Dropdown comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Interactable:
                    return comp.interactable;
            }

            return false;
        }

        protected override void HandleInt(Dropdown comp, FieldBindEnum type, int value)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Value:
                    comp.value = value;
                    break;
            }
        }

        protected override int GetInt(Dropdown comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Value:
                    return comp.value;
            }

            return 0;
        }
    }
} 