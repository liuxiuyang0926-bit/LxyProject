using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class PathBindValueCollection
    {
        [SerializeField] private List<PathBindValue> binds;

        public ReadOnlyCollection<PathBindValue> Binds
        {
            get
            {
                if (binds == null)
                    binds = new List<PathBindValue>();
                return binds.AsReadOnly();
            }
        }

        public PathBindValue this[int index]
        {
            get
            {
                if (binds == null)
                    binds = new List<PathBindValue>();
                return binds[index];
            }
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

        public static implicit operator List<PathBindValue>(PathBindValueCollection valueCollection)
        {
            return valueCollection.binds;
        }

        public static implicit operator PathBindValueCollection(List<PathBindValue> binds)
        {
            return new PathBindValueCollection() { binds = binds };
        }
    }
}