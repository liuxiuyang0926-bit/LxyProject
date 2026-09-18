using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class SliderHandler : AFieldBindHandler<Slider>
    {
        /// <summary>
        /// 将浮点值写入滑块组件。
        /// </summary>
        protected override void HandleFloat(Slider comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Value:
                    comp.value = value;
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(Slider comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Value:
                    return comp.value;
            }

            return 0;
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(Slider comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.Slider_Interactable:
                    comp.interactable = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
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