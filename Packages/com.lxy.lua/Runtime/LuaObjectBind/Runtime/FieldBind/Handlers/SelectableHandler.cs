using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class SelectableHandler : AFieldBindHandler<Selectable>
    {
        protected override void HandleBool(Selectable comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Selectable_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(Selectable comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Selectable_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 