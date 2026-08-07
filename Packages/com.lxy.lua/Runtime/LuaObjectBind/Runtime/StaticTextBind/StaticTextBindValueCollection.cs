using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace LuaObjectBind
{
    /// <summary>
    /// 静态文本绑定集合 - 管理多个静态文本绑定
    /// </summary>
    [System.Serializable]
    public class StaticTextBindValueCollection
    {
        [SerializeField] private List<StaticTextBindValue> binds;

        /// <summary>
        /// 只读的绑定列表
        /// </summary>
        public ReadOnlyCollection<StaticTextBindValue> Binds
        {
            get
            {
                if (binds == null)
                    binds = new List<StaticTextBindValue>();
                return binds.AsReadOnly();
            }
        }

        /// <summary>
        /// 通过索引访问绑定项
        /// </summary>
        public StaticTextBindValue this[int index]
        {
            get
            {
                if (binds == null)
                    binds = new List<StaticTextBindValue>();
                return binds[index];
            }
        }

        /// <summary>
        /// 根据名称获取静态文本值
        /// </summary>
        public object Get(string name)
        {
            if (this.binds == null || this.binds.Count <= 0)
                return null;
            var bind = this.binds.Find(v => v.Name.Equals(name));
            if (bind == null)
                return null;
            return bind.GetValue();
        }

        /// <summary>
        /// 根据名称获取静态文本值（泛型版本）
        /// </summary>
        public T Get<T>(string name)
        {
            if (this.binds == null || this.binds.Count <= 0)
                return default(T);
            var bind = this.binds.Find(v => v.Name.Equals(name));
            if (bind == null)
                return default(T);
            return bind.GetValue<T>();
        }

        /// <summary>
        /// 隐式转换为List
        /// </summary>
        public static implicit operator List<StaticTextBindValue>(StaticTextBindValueCollection valueCollection)
        {
            return valueCollection.binds;
        }

        /// <summary>
        /// 从List隐式转换
        /// </summary>
        public static implicit operator StaticTextBindValueCollection(List<StaticTextBindValue> binds)
        {
            return new StaticTextBindValueCollection() { binds = binds };
        }
    }
}

