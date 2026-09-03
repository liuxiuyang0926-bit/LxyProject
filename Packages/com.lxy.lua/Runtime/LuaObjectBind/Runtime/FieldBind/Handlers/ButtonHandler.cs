using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class ButtonHandler : AFieldBindHandler<Button>
    {
        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(Button comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Button_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
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