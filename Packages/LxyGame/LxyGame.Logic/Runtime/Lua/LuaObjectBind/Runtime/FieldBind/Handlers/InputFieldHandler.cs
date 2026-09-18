using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class InputFieldHandler : AFieldBindHandler<InputField>
    {
        /// <summary>
        /// 将字符串值写入输入框组件。
        /// </summary>
        protected override void HandleString(InputField comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Text:
                    comp.text = value;
                    break;
            }
        }

        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected override string GetString(InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(InputField comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected override bool GetBool(InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.InputField_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 