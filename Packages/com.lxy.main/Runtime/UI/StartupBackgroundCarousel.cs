using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Resource;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Main
{
    /// <summary>
    /// 从 YooAsset 配置加载启动背景，并在不修改预制体的情况下
    /// 控制单图显示或多图轮播。
    /// </summary>
    internal sealed class StartupBackgroundCarousel : IDisposable
    {
        private const string ConfigLocation =
            "StartupBackgroundConfig";

        private const int DownloadConcurrency = 4;
        private const int DownloadRetryCount = 2;

#if UNITY_EDITOR
        private const string ConfigAssetPath =
            "Assets/GameResources/UITexture/" +
            "StartupBackgroundConfig.json";

        private const string UITextureFolder =
            "Assets/GameResources/UITexture";
#endif

        [Serializable]
        private sealed class BackgroundConfig
        {
            public bool enabled = true;
            public bool enableRotation;
            public float rotationIntervalSeconds = 5f;
            public float fadeDurationSeconds = 0.5f;
            public string[] backgroundLocations =
                Array.Empty<string>();
        }

        private readonly MonoBehaviour host;
        private readonly Image targetImage;
        private readonly List<Sprite> backgrounds =
            new List<Sprite>();
        private readonly List<GameResourceHandle<Sprite>>
            assetHandles =
                new List<GameResourceHandle<Sprite>>();

        private Coroutine rotationCoroutine;
        private Color originalColor;
        private int currentIndex;
        private bool disposed;

        public StartupBackgroundCarousel(
            MonoBehaviour host,
            Image targetImage)
        {
            this.host = host;
            this.targetImage = targetImage;
            originalColor = targetImage != null
                ? targetImage.color
                : Color.white;
        }

        public IEnumerator PrepareAsync(ResourcePackage package)
        {
            if (disposed || host == null || targetImage == null)
            {
                yield break;
            }

#if UNITY_EDITOR
            yield return PrepareFromAssetDatabaseAsync();
#else
            if (package == null)
            {
                Debug.LogWarning(
                    "[StartupBackground] YooAsset Package 为空，" +
                    "继续使用内置背景。");
                yield break;
            }

            yield return PrepareFromPackageAsync(package);
#endif
        }

#if UNITY_EDITOR
        private IEnumerator PrepareFromAssetDatabaseAsync()
        {
            TextAsset configAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ConfigAssetPath);
            if (!TryParseConfig(configAsset, out BackgroundConfig config))
            {
                yield break;
            }

            if (!config.enabled)
            {
                Debug.Log(
                    "[StartupBackground] 背景热更功能已在配置中关闭。");
                yield break;
            }

            foreach (string location in GetUniqueLocations(config))
            {
                Sprite sprite = FindEditorSprite(location);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StartupBackground] 找不到背景资源：{location}");
                    continue;
                }

                backgrounds.Add(sprite);
            }

            yield return ActivateAsync(config);
        }

        private static Sprite FindEditorSprite(string location)
        {
            if (location.StartsWith(
                    "Assets/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(location);
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { UITextureFolder });
            var matches = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        location,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(path);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            matches.Sort(StringComparer.OrdinalIgnoreCase);
            if (matches.Count > 1)
            {
                Debug.LogWarning(
                    $"[StartupBackground] Location 重名：{location}，" +
                    $"使用 {matches[0]}。");
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(matches[0]);
        }
#endif

        private IEnumerator PrepareFromPackageAsync(
            ResourcePackage package)
        {
            if (!package.IsLocationValid(ConfigLocation))
            {
                Debug.LogWarning(
                    $"[StartupBackground] Manifest 中不存在配置：" +
                    $"{ConfigLocation}，继续使用内置背景。");
                yield break;
            }

            AssetInfo configInfo = package.GetAssetInfo(
                ConfigLocation,
                typeof(TextAsset));
            bool configDownloaded = false;
            yield return DownloadAssetsAsync(
                package,
                new[] { configInfo },
                "启动背景配置",
                result => configDownloaded = result);
            if (!configDownloaded)
            {
                yield break;
            }

            GameResourceHandle<TextAsset> configHandle =
                GameResourceManager.GetOrCreate()
                    .LoadAssetAsync<TextAsset>(
                        package,
                        ConfigLocation);
            yield return configHandle;

            if (configHandle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogWarning(
                    "[StartupBackground] 加载配置失败：" +
                    configHandle.Error);
                configHandle.Release();
                yield break;
            }

            TextAsset configAsset = configHandle.Asset;
            bool parsed = TryParseConfig(
                configAsset,
                out BackgroundConfig config);
            configHandle.Release();
            if (!parsed || !config.enabled)
            {
                if (parsed)
                {
                    Debug.Log(
                        "[StartupBackground] 背景热更功能已在配置中关闭。");
                }

                yield break;
            }

            var validLocations = new List<string>();
            var assetInfos = new List<AssetInfo>();
            foreach (string location in GetUniqueLocations(config))
            {
                if (!package.IsLocationValid(location))
                {
                    Debug.LogWarning(
                        $"[StartupBackground] Manifest 中不存在背景：" +
                        location);
                    continue;
                }

                AssetInfo assetInfo = package.GetAssetInfo(
                    location,
                    typeof(Sprite));
                if (!assetInfo.IsValid)
                {
                    Debug.LogWarning(
                        $"[StartupBackground] 背景信息无效：{location}，" +
                        assetInfo.Error);
                    continue;
                }

                validLocations.Add(location);
                assetInfos.Add(assetInfo);
            }

            if (assetInfos.Count == 0)
            {
                Debug.Log(
                    "[StartupBackground] 配置中没有可用背景，" +
                    "继续使用内置背景。");
                yield break;
            }

            bool backgroundsDownloaded = false;
            yield return DownloadAssetsAsync(
                package,
                assetInfos.ToArray(),
                "启动背景",
                result => backgroundsDownloaded = result);
            if (!backgroundsDownloaded)
            {
                yield break;
            }

            foreach (string location in validLocations)
            {
                GameResourceHandle<Sprite> handle =
                    GameResourceManager.GetOrCreate()
                        .LoadAssetAsync<Sprite>(
                            package,
                            location);
                yield return handle;

                if (handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogWarning(
                        $"[StartupBackground] 加载背景失败：{location}，" +
                        handle.Error);
                    handle.Release();
                    continue;
                }

                Sprite sprite = handle.Asset;
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StartupBackground] 资源不是 Sprite：{location}");
                    handle.Release();
                    continue;
                }

                assetHandles.Add(handle);
                backgrounds.Add(sprite);
            }

            yield return ActivateAsync(config);
        }

        private static IEnumerator DownloadAssetsAsync(
            ResourcePackage package,
            AssetInfo[] assetInfos,
            string displayName,
            Action<bool> completed)
        {
            var options = new BundleDownloaderOptions(
                assetInfos,
                true,
                DownloadConcurrency,
                DownloadRetryCount);
            ResourceDownloaderOperation downloader =
                package.CreateResourceDownloader(options);

            if (downloader.TotalDownloadCount == 0)
            {
                completed?.Invoke(true);
                yield break;
            }

            Debug.Log(
                $"[StartupBackground] 优先下载{displayName}：" +
                $"{downloader.TotalDownloadCount} 个文件，" +
                $"{downloader.TotalDownloadBytes} 字节。");
            downloader.StartDownload();
            yield return downloader;

            bool succeeded =
                downloader.Status == EOperationStatus.Succeeded;
            if (!succeeded)
            {
                Debug.LogWarning(
                    $"[StartupBackground] 下载{displayName}失败，" +
                    $"继续使用内置背景：{downloader.Error}");
            }

            completed?.Invoke(succeeded);
        }

        private IEnumerator ActivateAsync(BackgroundConfig config)
        {
            if (backgrounds.Count == 0)
            {
                yield break;
            }

            currentIndex = 0;
            float fadeDuration = Mathf.Max(
                0f,
                config.fadeDurationSeconds);
            yield return FadeToAsync(
                backgrounds[currentIndex],
                fadeDuration);

            bool shouldRotate =
                config.enableRotation && backgrounds.Count > 1;
            Debug.Log(
                $"[StartupBackground] 已加载 {backgrounds.Count} 张背景，" +
                $"轮播：{(shouldRotate ? "开启" : "关闭")}。");

            if (shouldRotate && !disposed)
            {
                rotationCoroutine = host.StartCoroutine(
                    RotateAsync(
                        Mathf.Max(
                            0.5f,
                            config.rotationIntervalSeconds),
                        fadeDuration));
            }
        }

        private IEnumerator RotateAsync(
            float intervalSeconds,
            float fadeDurationSeconds)
        {
            var wait = new WaitForSecondsRealtime(intervalSeconds);
            while (!disposed && backgrounds.Count > 1)
            {
                yield return wait;
                currentIndex = (currentIndex + 1) % backgrounds.Count;
                yield return FadeToAsync(
                    backgrounds[currentIndex],
                    fadeDurationSeconds);
            }
        }

        private IEnumerator FadeToAsync(
            Sprite sprite,
            float durationSeconds)
        {
            if (targetImage == null || sprite == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                targetImage.sprite = sprite;
                yield break;
            }

            float halfDuration = durationSeconds * 0.5f;
            yield return FadeAlphaAsync(
                originalColor.a,
                0f,
                halfDuration);
            targetImage.sprite = sprite;
            yield return FadeAlphaAsync(
                0f,
                originalColor.a,
                halfDuration);
        }

        private IEnumerator FadeAlphaAsync(
            float from,
            float to,
            float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                SetTargetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (!disposed && elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetTargetAlpha(Mathf.Lerp(
                    from,
                    to,
                    Mathf.Clamp01(elapsed / durationSeconds)));
                yield return null;
            }

            if (!disposed)
            {
                SetTargetAlpha(to);
            }
        }

        private void SetTargetAlpha(float alpha)
        {
            if (targetImage == null)
            {
                return;
            }

            Color color = originalColor;
            color.a = alpha;
            targetImage.color = color;
        }

        private static bool TryParseConfig(
            TextAsset configAsset,
            out BackgroundConfig config)
        {
            config = null;
            if (configAsset == null ||
                string.IsNullOrWhiteSpace(configAsset.text))
            {
                Debug.LogWarning(
                    "[StartupBackground] 启动背景配置不存在或内容为空。");
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<BackgroundConfig>(
                    configAsset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[StartupBackground] 配置 JSON 解析失败：" +
                    exception.Message);
                return false;
            }

            if (config == null)
            {
                Debug.LogWarning(
                    "[StartupBackground] 配置 JSON 无效。");
                return false;
            }

            config.backgroundLocations ??= Array.Empty<string>();
            return true;
        }

        private static IEnumerable<string> GetUniqueLocations(
            BackgroundConfig config)
        {
            var unique = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string rawLocation in config.backgroundLocations)
            {
                string location = rawLocation?.Trim();
                if (string.IsNullOrEmpty(location) ||
                    !unique.Add(location))
                {
                    continue;
                }

                yield return location;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (rotationCoroutine != null && host != null)
            {
                host.StopCoroutine(rotationCoroutine);
                rotationCoroutine = null;
            }

            if (targetImage != null)
            {
                targetImage.color = originalColor;
            }

            foreach (GameResourceHandle<Sprite> handle in
                     assetHandles)
            {
                handle?.Release();
            }

            assetHandles.Clear();
            backgrounds.Clear();
        }
    }
}
