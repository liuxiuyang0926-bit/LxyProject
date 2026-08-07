using System;
using System.ComponentModel;

namespace LuaObjectBind
{
    public abstract class DisposeObject: Object, IDisposable, ISupportInitialize
    {
        public virtual void Dispose()
        {
        }
        
        public virtual void BeginInit()
        {
        }
        
        public virtual void EndInit()
        {
        }
    }

    public interface IPool: IDisposable
    {
        bool IsFromPool
        {
            get;
            set;
        }
    }
}