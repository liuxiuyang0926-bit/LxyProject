using System;
using UnityEngine;

namespace Game.Contracts
{
    /// <summary>
    /// 主包启动层与热更新层共享的运行时配置入口。
    /// 首次启动必须保留一个随主包发布的种子地址；热更新层可以在
    /// 成功启动后覆盖并持久化地址，供本次会话和下一次启动使用。
    /// </summary>
    public static class GameRuntimeConfig
    {
        private const string SeedGameConfigUrl =
            "http://47.97.108.193:8080/LoadConfig/gameConfig.json";
        private const int SeedRequestTimeoutSeconds = 15;

        private const string GameConfigUrlPlayerPrefsKey =
            "bootstrap.game_config_url";
        private const string RequestTimeoutPlayerPrefsKey =
            "bootstrap.game_config_timeout_seconds";

        private static bool initialized;
        private static string gameConfigUrl;
        private static int requestTimeoutSeconds;

        public static string GameConfigUrl
        {
            get
            {
                EnsureInitialized();
                return gameConfigUrl;
            }
        }

        public static int GameConfigRequestTimeoutSeconds
        {
            get
            {
                EnsureInitialized();
                return requestTimeoutSeconds;
            }
        }

        /// <summary>
        /// 由热更新配置调用。配置立即对当前会话生效；persist 为 true
        /// 时也会作为下一次启动的资源配置地址。
        /// </summary>
        public static bool TryApplyBootstrapOverride(
            string configUrl,
            int timeoutSeconds,
            bool persist,
            out string error)
        {
            EnsureInitialized();

            string normalizedUrl = configUrl?.Trim();
            bool hasUrlOverride =
                !string.IsNullOrEmpty(normalizedUrl);
            if (hasUrlOverride &&
                !TryValidateHttpUrl(normalizedUrl, out error))
            {
                return false;
            }

            if (timeoutSeconds <= 0)
            {
                error = "远程配置请求超时时间必须大于 0 秒。";
                return false;
            }

            if (hasUrlOverride)
            {
                gameConfigUrl = normalizedUrl;
            }

            requestTimeoutSeconds = timeoutSeconds;
            error = null;

            if (!persist)
            {
                return true;
            }

            if (hasUrlOverride)
            {
                PlayerPrefs.SetString(
                    GameConfigUrlPlayerPrefsKey,
                    gameConfigUrl);
            }
            PlayerPrefs.SetInt(
                RequestTimeoutPlayerPrefsKey,
                requestTimeoutSeconds);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetBootstrapOverride()
        {
            gameConfigUrl = SeedGameConfigUrl;
            requestTimeoutSeconds = SeedRequestTimeoutSeconds;
            initialized = true;

            PlayerPrefs.DeleteKey(GameConfigUrlPlayerPrefsKey);
            PlayerPrefs.DeleteKey(RequestTimeoutPlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            string persistedUrl = PlayerPrefs.GetString(
                GameConfigUrlPlayerPrefsKey,
                SeedGameConfigUrl);
            gameConfigUrl = TryValidateHttpUrl(
                persistedUrl,
                out _)
                ? persistedUrl.Trim()
                : SeedGameConfigUrl;

            requestTimeoutSeconds = Mathf.Max(
                1,
                PlayerPrefs.GetInt(
                    RequestTimeoutPlayerPrefsKey,
                    SeedRequestTimeoutSeconds));
            initialized = true;
        }

        private static bool TryValidateHttpUrl(
            string value,
            out string error)
        {
            if (!Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "远程配置地址必须是有效的 HTTP/HTTPS URL。";
                return false;
            }

            error = null;
            return true;
        }
    }
}
