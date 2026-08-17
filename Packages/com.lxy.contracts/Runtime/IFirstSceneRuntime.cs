using System.Collections;

namespace Game.Contracts
{
    /// <summary>
    /// 首个业务场景运行时的稳定契约。实现可以位于 AOT 或任意
    /// HybridCLR 程序集中，Game.Main 不需要引用具体业务程序集。
    /// </summary>
    public interface IFirstSceneRuntime
    {
        bool IsInitialized { get; }
        string LastError { get; }
        IEnumerator InitializeAsync();
    }

    public static class FirstSceneRuntimeBridge
    {
        public static IFirstSceneRuntime Current { get; private set; }

        public static void Register(IFirstSceneRuntime runtime)
        {
            Current = runtime;
        }

        public static void Unregister(IFirstSceneRuntime runtime)
        {
            if (ReferenceEquals(Current, runtime))
            {
                Current = null;
            }
        }
    }
}
