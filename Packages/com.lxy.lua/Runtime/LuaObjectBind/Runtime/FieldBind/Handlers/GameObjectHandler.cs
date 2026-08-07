using UnityEngine;
using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class GameObjectHandler : AFieldBindHandler<GameObject>
    {
        protected override void HandleBool(GameObject comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.GameObject_Active:
                    comp.SetActive(value);
                    break;
            }
        }

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