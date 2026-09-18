using System;
using System.ComponentModel;

namespace LuaObjectBind
{
    public abstract class DisposeObject: Object, IDisposable, ISupportInitialize
    {
        /// <summary>
        /// 释放当前实例持有的资源。
        /// </summary>
        public virtual void Dispose()
        {
        }
        
        /// <summary>
        /// 执行Begin初始化相关逻辑。
        /// </summary>
        public virtual void BeginInit()
        {
        }
        
        /// <summary>
        /// 执行End初始化相关逻辑。
        /// </summary>
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