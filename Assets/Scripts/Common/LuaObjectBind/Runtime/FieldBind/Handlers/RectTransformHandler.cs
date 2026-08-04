using UnityEngine;

namespace LuaObjectBind.Handlers
{
    public class RectTransformHandler : AFieldBindHandler<RectTransform>
    {
        protected override void HandleVector2(RectTransform comp, FieldBindEnum type, Vector2 value)
        {
            switch (type)
            {
                case FieldBindEnum.RectTransform_AnchoredPosition:
                    comp.anchoredPosition = value;
                    break;
                case FieldBindEnum.RectTransform_SizeDelta:
                    comp.sizeDelta = value;
                    break;
            }
        }

        protected override Vector2 GetVector2(RectTransform comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.RectTransform_AnchoredPosition:
                    return comp.anchoredPosition;
                case FieldBindEnum.RectTransform_SizeDelta:
                    return comp.sizeDelta;
            }

            return Vector2.zero;
        }
    }
} 