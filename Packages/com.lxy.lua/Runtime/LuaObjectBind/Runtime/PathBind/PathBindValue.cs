using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class PathBindValue
    {
        [SerializeField] protected string name = "";

        [SerializeField] protected string path;
        
        public string Name 
        { 
            get => name;
            set => name = value;
        }

        public T GetValue<T>()
        {
            return (T)GetValue();
        }

        /// <summary>
        /// 设置值。
        /// </summary>
        public void SetValue(string value)
        {
            path = value;
        }

        /// <summary>
        /// 获取值。
        /// </summary>
        public object GetValue()
        {
            return this.path;
        }
        
    }
}
