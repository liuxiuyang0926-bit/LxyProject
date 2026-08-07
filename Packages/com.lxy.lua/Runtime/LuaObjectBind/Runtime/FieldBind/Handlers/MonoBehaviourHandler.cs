using UnityEngine;
using UnityEngine.UI;

namespace LuaObjectBind.Handlers
{
    public class MonoBehaviourHandler : AFieldBindHandler<MonoBehaviour>
    {
        protected override void HandleBool(MonoBehaviour comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.MonoBehaviour_Enabled:
                    comp.enabled = value;
                    break;
            }
        }

        protected override bool GetBool(MonoBehaviour comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.GameObject_Active:
                    return comp.enabled;
            }

            return false;
        }
    }
}