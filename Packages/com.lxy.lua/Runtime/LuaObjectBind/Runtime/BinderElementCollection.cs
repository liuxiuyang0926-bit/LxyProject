using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace LuaObjectBind
{
    [Serializable]
    public class BinderElement
    {
        [SerializeField] private string name = "";
        [SerializeField] private ObjectBinder objectBinder;

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public ObjectBinder Value
        {
            get { return this.objectBinder; }
            set { this.objectBinder = value; }
        }
    }

    [Serializable]
    public class BinderElementCollection
    {
        [SerializeField] private List<BinderElement> binds = new ();

        public ReadOnlyCollection<BinderElement> Binds
        {
            get { return binds.AsReadOnly(); }
        }

        public BinderElement this[int index]
        {
            get { return binds[index]; }
        }

        public ObjectBinder Get(string name)
        {
            if (this.binds == null || this.binds.Count <= 0)
                return null;
            var bind = this.binds.Find(v => v.Name.Equals(name));
            if (bind == null)
                return null;
            return bind.Value;
        }

        public static implicit operator List<BinderElement>(BinderElementCollection valueCollection)
        {
            return valueCollection.binds;
        }

        public static implicit operator BinderElementCollection(List<BinderElement> binds)
        {
            return new BinderElementCollection() { binds = binds };
        }
    }
} 