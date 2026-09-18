using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class ToggleHandler : AFieldBindHandler<Toggle>
    {
        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(Toggle comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Toggle_IsOn:
                    comp.isOn = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
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