using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public enum BindValueType
    {
        Object,
    }

    [System.Serializable]
    public class BindValue
    {
        [SerializeField] protected string name = "";

        [SerializeField] protected UnityEngine.Object objectValue;

        [SerializeField] protected BindValueType bindValueType;

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public UnityEngine.Object GetObjectValue
        {
            get { return objectValue; }
            private set { }
        }

        public System.Type ValueType
        {
            get
            {
                switch (this.bindValueType)
                {
                    case BindValueType.Object:
                        return this.objectValue == null ? typeof(Object) : this.objectValue.GetType();
                    default:
                        throw new System.NotSupportedException();
                }
            }
        }

        public T GetValue<T>()
        {
            return (T)GetValue();
        }

        public void SetValue(object value)
        {
            switch (this.bindValueType)
            {
                case BindValueType.Object:
                    this.objectValue = (Object)value;
                    break;
                default:
                    throw new System.NotSupportedException();
            }
        }

        public object GetValue()
        {
            switch (this.bindValueType)
            {
                case BindValueType.Object:
                    return this.objectValue;
                default:
                    throw new System.NotSupportedException();
            }
        }
    }
}