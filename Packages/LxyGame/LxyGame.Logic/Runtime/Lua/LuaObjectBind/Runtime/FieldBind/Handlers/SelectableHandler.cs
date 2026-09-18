using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class SelectableHandler : AFieldBindHandler<Selectable>
    {
        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(Selectable comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Selectable_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
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