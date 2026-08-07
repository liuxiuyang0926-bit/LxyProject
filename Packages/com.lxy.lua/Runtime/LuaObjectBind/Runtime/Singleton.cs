using System;

namespace LuaObjectBind
{
    public interface ISingletonReverseDispose
    {
        
    }
    
    public abstract class ASingleton: DisposeObject
    {
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

        internal override void Register()
        {
            Instance = (T)this;
        }

        public bool IsDisposed()
        {
            return this.isDisposed;
        }

        protected virtual void Destroy()
        {
            
        }

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
        protected static void ResetHandler(UnityEditor.EnterPlayModeOptions options)
        {
            if (!options.HasFlag(UnityEditor.EnterPlayModeOptions.DisableDomainReload))
                return;
            instance = default(T);
        }
#endif
    }
}