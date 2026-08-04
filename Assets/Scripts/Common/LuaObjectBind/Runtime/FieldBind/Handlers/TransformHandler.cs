using UnityEngine;

namespace LuaObjectBind.Handlers
{
    public class TransformHandler : AFieldBindHandler<Transform>
    {
        protected override void HandleVector3(Transform comp, FieldBindEnum type, Vector3 value)
        {
            switch (type)
            {
                case FieldBindEnum.Transform_Position:
                    comp.position = value;
                    break;
                case FieldBindEnum.Transform_LocalPosition:
                    comp.localPosition = value;
                    break;
                case FieldBindEnum.Transform_EulerAngles:
                    comp.eulerAngles = value;
                    break;
                case FieldBindEnum.Transform_LocalEulerAngles:
                    comp.localEulerAngles = value;
                    break;
                case FieldBindEnum.Transform_LocalScale:
                    comp.localScale = value;
                    break;
            }
        }

        protected override Vector3 GetVector3(Transform comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.Transform_Position:
                    return comp.position;
                case FieldBindEnum.Transform_LocalPosition:
                    return comp.localPosition;
                case FieldBindEnum.Transform_EulerAngles:
                    return comp.eulerAngles;
                case FieldBindEnum.Transform_LocalEulerAngles:
                    return comp.localEulerAngles;
                case FieldBindEnum.Transform_LocalScale:
                    return comp.localScale;
            }

            return Vector3.zero;
        }
    }
} 