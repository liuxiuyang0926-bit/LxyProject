using UnityEngine;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class CanvasGroupHandler : AFieldBindHandler<CanvasGroup>
    {
        protected override void HandleBool(CanvasGroup comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.CanvasGroup_Interactable:
                    comp.interactable = value;
                    break;
                case FieldBindEnum.CanvasGroup_BlocksRaycasts:
                    comp.blocksRaycasts = value;
                    break;
            }
        }

        protected override bool GetBool(CanvasGroup comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.CanvasGroup_Interactable:
                    return comp.interactable;
                case FieldBindEnum.CanvasGroup_BlocksRaycasts:
                    return comp.blocksRaycasts;
            }

            return false;
        }

        protected override void HandleFloat(CanvasGroup comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.CanvasGroup_Alpha:
                    comp.alpha = value;
                    break;
            }
        }

        protected override float GetFloat(CanvasGroup comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.CanvasGroup_Alpha:
                    return comp.alpha;
            }

            return 0;
        }
    }
} 