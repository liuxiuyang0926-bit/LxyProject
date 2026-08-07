using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class FieldBindValue
    {
        [SerializeField] protected string name = "";

        [SerializeField] protected Object objectValue;

        [SerializeField] protected FieldBindEnum fieldBindType;

        public FieldBindEnum FieldBindType => fieldBindType;
        public string Name => name;

        public System.Type ValueType
        {
            get
            {
                return this.objectValue == null ? typeof(Object) : this.objectValue.GetType();
            }
        }

        public T GetValue<T>()
        {
            return (T)GetValue();
        }

        public void SetValue(object value)
        {
            objectValue = (Object)value;
        }

        public object GetValue()
        {
            return this.objectValue;
        }
        
    }
}
