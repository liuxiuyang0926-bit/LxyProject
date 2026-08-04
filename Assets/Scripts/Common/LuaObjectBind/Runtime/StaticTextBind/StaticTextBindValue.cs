using UnityEngine;

namespace LuaObjectBind
{
    /// <summary>
    /// 静态文本绑定值 - 用于在Prefab中直接填写静态文本，然后在Lua中通过self.Widget.xxx访问
    /// </summary>
    [System.Serializable]
    public class StaticTextBindValue
    {
        [SerializeField] protected string name = "";

        [SerializeField] protected string text = "";
        
        /// <summary>
        /// 绑定变量名
        /// </summary>
        public string Name 
        { 
            get => name;
            set => name = value;
        }

        /// <summary>
        /// 获取静态文本值
        /// </summary>
        public T GetValue<T>()
        {
            return (T)GetValue();
        }

        /// <summary>
        /// 设置静态文本值
        /// </summary>
        public void SetValue(string value)
        {
            text = value;
        }

        /// <summary>
        /// 获取静态文本值（返回object类型）
        /// </summary>
        public object GetValue()
        {
            return this.text;
        }
    }
}

