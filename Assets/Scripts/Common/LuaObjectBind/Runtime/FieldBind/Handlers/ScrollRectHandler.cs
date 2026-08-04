using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class ScrollRectHandler : AFieldBindHandler<ScrollRect>
    {
        protected override void HandleFloat(ScrollRect comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.ScrollRect_HorizontalNormalizedPosition:
                    comp.horizontalNormalizedPosition = value;
                    break;
                case FieldBindEnum.ScrollRect_VerticalNormalizedPosition:
                    comp.verticalNormalizedPosition = value;
                    break;
            }
        }

        protected override float GetFloat(ScrollRect comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.ScrollRect_HorizontalNormalizedPosition:
                    return comp.horizontalNormalizedPosition;
                case FieldBindEnum.ScrollRect_VerticalNormalizedPosition:
                    return comp.verticalNormalizedPosition;
            }

            return 0;
        }
    }
} 