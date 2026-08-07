using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class SliderHandler : AFieldBindHandler<Slider>
    {
        protected override void HandleFloat(Slider comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Value:
                    comp.value = value;
                    break;
            }
        }

        protected override float GetFloat(Slider comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Value:
                    return comp.value;
            }

            return 0;
        }

        protected override void HandleBool(Slider comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        protected override bool GetBool(Slider comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 