using Game.Contracts;

namespace Game.HotUpdate
{
    /// <summary>
    /// 可随 Game.HotUpdate.dll 发布的统一运行时配置。
    /// 修改这里并重新发布热更新 DLL 后，新值会立即生效并持久化到
    /// 下一次启动；空地址表示继续使用主包种子地址或上次有效地址。
    /// </summary>
    public static class HotUpdateRuntimeConfig
    {
        public const string GameConfigUrlOverride = "";
        public const int GameConfigRequestTimeoutSeconds = 15;
        public const float UIStartupTimeoutSeconds = 30f;

        internal static bool Apply(out string error)
        {
            return GameRuntimeConfig.TryApplyBootstrapOverride(
                GameConfigUrlOverride,
                GameConfigRequestTimeoutSeconds,
                true,
                out error);
        }
    }
}
