using UnityEngine;
using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class GameObjectHandler : AFieldBindHandler<GameObject>
    {
        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(GameObject comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.GameObject_Active:
                    comp.SetActive(value);
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected override bool GetBool(GameObject comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.GameObject_Active:
                    return comp.activeSelf;
            }

            return false;
        }
    }
}