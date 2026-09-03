using TMPro;

namespace LuaObjectBind.Handlers
{
    public class TMP_InputFieldHandler : AFieldBindHandler<TMP_InputField>
    {
        /// <summary>
        /// 处理字符串。
        /// </summary>
        protected override void HandleString(TMP_InputField comp, FieldBindEnum type, string value)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Text:
                    comp.text = value;
                    break;
            }
        }

        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected override string GetString(TMP_InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Text:
                    return comp.text;
            }

            return string.Empty;
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(TMP_InputField comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected override bool GetBool(TMP_InputField comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.TMP_InputField_Interactable:
                    return comp.interactable;
            }

            return false;
        }
    }
} 