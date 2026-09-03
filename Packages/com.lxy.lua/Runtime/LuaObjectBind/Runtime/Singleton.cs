using System;

namespace LuaObjectBind
{
    public interface ISingletonReverseDispose
    {
        
    }
    
    public abstract class ASingleton: DisposeObject
    {
        /// <summary>
        /// 注册当前实例。
        /// </summary>
        internal abstract void Register();
    }
    
    public abstract class Singleton<T>: ASingleton where T: Singleton<T>
    {
        private bool isDisposed;

#pragma warning disable SA003 // Open generic static state
        private static T instance;
#pragma warning restore SA003 // Open generic static state

        public static T Instance
        {
            get
            {
                return instance;
            }
            private set
            {
                instance = value;
            }
        }

        /// <summary>
        /// 注册当前实例。
        /// </summary>
        internal override void Register()
        {
            Instance = (T)this;
        }

        /// <summary>
        /// 执行判断是否Disposed相关逻辑。
        /// </summary>
        public bool IsDisposed()
        {
            return this.isDisposed;
        }

        /// <summary>
        /// 销毁目标对象。
        /// </summary>
        protected virtual void Destroy()
        {
            
        }

        /// <summary>
        /// 释放当前实例持有的资源。
        /// </summary>
        public override void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }
            
            this.isDisposed = true;

            this.Destroy();
            
            Instance = null;
        }

        // Roslyn Auto Gen - ResetHandler
#if UNITY_EDITOR
        /// <summary>
        /// 重置处理器。
        /// </summary>
        protected static void ResetHandler(UnityEditor.EnterPlayModeOptions options)
        {
            if (!options.HasFlag(UnityEditor.EnterPlayModeOptions.DisableDomainReload))
                return;
            instance = default(T);
        }
#endif
    }
}