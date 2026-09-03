using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class DropdownHandler : AFieldBindHandler<Dropdown>
    {
        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(Dropdown comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected override bool GetBool(Dropdown comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Interactable:
                    return comp.interactable;
            }

            return false;
        }

        /// <summary>
        /// 处理整数。
        /// </summary>
        protected override void HandleInt(Dropdown comp, FieldBindEnum type, int value)
        {
            switch (type)
            {
                case FieldBindEnum.Dropdown_Value:
                    comp.value = value;
                    break;
            }
        }

        /// <summary>
        /// 获取整数。
        /// </summary>
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