using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class FieldBindValue
    {
        [SerializeField] protected string name = "";

        /// <summary>
        /// 绑定字段关联的 Unity 对象；其实际类型决定可用的字段绑定方式。
        /// </summary>
        [SerializeField] protected Object objectValue;

        [SerializeField] protected FieldBindEnum fieldBindType;

        /// <summary>
        /// 向调用方提供FieldBind类型。
        /// </summary>
        public FieldBindEnum FieldBindType => fieldBindType;
        /// <summary>
        /// 向调用方提供名称。
        /// </summary>
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

        /// <summary>
        /// 设置值。
        /// </summary>
        public void SetValue(object value)
        {
            objectValue = (Object)value;
        }

        /// <summary>
        /// 获取值。
        /// </summary>
        public object GetValue()
        {
            return this.objectValue;
        }
        
    }
}
