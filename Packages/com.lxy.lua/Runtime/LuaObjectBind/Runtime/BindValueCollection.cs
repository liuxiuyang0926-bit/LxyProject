using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class BindValueCollection
    {
        [SerializeField] private List<BindValue> binds = new ();

        public ReadOnlyCollection<BindValue> Binds
        {
            get { return binds.AsReadOnly(); }
        }

        public BindValue this[int index]
        {
            get { return binds[index]; }
        }

        /// <summary>
        /// 执行获取相关逻辑。
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

        public T Get<T>(string name)
        {
            if (this.binds == null || this.binds.Count <= 0)
                return default(T);
            var bind = this.binds.Find(v => v.Name.Equals(name));
            if (bind == null)
                return default(T);
            return bind.GetValue<T>();
        }

        public static implicit operator List<BindValue>(BindValueCollection valueCollection)
        {
            return valueCollection.binds;
        }

        public static implicit operator BindValueCollection(List<BindValue> binds)
        {
            return new BindValueCollection() { binds = binds };
        }
    }
}