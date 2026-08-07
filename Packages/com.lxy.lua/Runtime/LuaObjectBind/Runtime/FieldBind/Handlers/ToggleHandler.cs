using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class ToggleHandler : AFieldBindHandler<Toggle>
    {
        protected override void HandleBool(Toggle comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Toggle_IsOn:
                    comp.isOn = value;
                    break;
            }
        }

        protected override bool GetBool(Toggle comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Toggle_IsOn:
                    return comp.isOn;
            }

            return false;
        }
    }
} 