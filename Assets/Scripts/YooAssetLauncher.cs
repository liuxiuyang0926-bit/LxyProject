using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

public sealed class YooAssetLauncher : MonoBehaviour
{
    private const string GameConfigUrl =
        "http://47.97.108.193:8080/LoadConfig/gameConfig.json";

    private const int GameConfigRequestTimeoutSeconds = 15;

    [Serializable]
    private sealed class GameConfigResponse
    {
        public int code;
        public string message;
        public GameConfigData data;
    }

    [Serializable]
    private sealed class GameConfigData
    {
        public string version;
        public string downloadUrl;
    }

    public enum PlayMode
    {
        /// <summary>
        /// Unity 编辑器直接通过 AssetDatabase 读取资源。
        /// </summary>
        EditorAssetDatabase,

        /// <summary>
        /// 只使用 APK 内置资源，不连接服务器。
        /// </summary>
        Offline,

        /// <summary>
        /// 使用内置资源、缓存资源和远程服务器资源。
        /// </summary>
        Host
    }

    [Header("运行模式")]
    [SerializeField]
    private PlayMode playMode = PlayMode.Host;

    [Header("Bundle Collector 中的 Package 名")]
    [SerializeField]
    private string packageName = "DefaultPackage";

    [Header("Host 模式启动时下载全部缺失资源")]
    [SerializeField]
    private bool downloadAllOnStart = true;

    [Header("服务器异常时切换到 APK 内置资源")]
    [SerializeField]
    private bool fallbackToOffline = true;

    [Header("同时下载文件数量")]
    [SerializeField]
    private int maxDownloadCount = 10;

    [Header("下载失败重试次数")]
    [SerializeField]
    private int downloadRetryCount = 3;

    public static YooAssetLauncher Instance { get; private set; }

    public ResourcePackage Package { get; private set; }

    public bool IsReady { get; private set; }
    public bool IsInitializing { get; private set; }
    public string LastError { get; private set; }
    public string PackageName => packageName;
    public PlayMode ActiveMode => _activeMode;
    public string DefaultHostServer { get; private set; }
    public string FallbackHostServer { get; private set; }

    private PlayMode _activeMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 由 UIStartup 统一调用。编辑器不初始化 YooAsset，Player 才执行
    /// Offline/Host、版本清单和资源下载流程。
    /// </summary>
    public IEnumerator InitializeAsync()
    {
        if (IsReady)
        {
            yield break;
        }

        if (IsInitializing)
        {
            while (IsInitializing)
            {
                yield return null;
            }

            yield break;
        }

        IsInitializing = true;
        IsReady = false;
        LastError = null;
        packageName = string.IsNullOrWhiteSpace(packageName)
            ? "DefaultPackage"
            : packageName.Trim();

#if UNITY_EDITOR
        _activeMode = PlayMode.EditorAssetDatabase;
        IsReady = true;
        IsInitializing = false;
        Debug.Log(
            "[YooAsset] Unity 编辑器使用 AssetDatabase 模式");
        yield break;
#else
        _activeMode = playMode;
        if (_activeMode == PlayMode.EditorAssetDatabase)
        {
            Debug.LogWarning(
                "[YooAsset] Player 不支持 AssetDatabase，自动切换为 Host"
            );

            _activeMode = PlayMode.Host;
        }

        Debug.Log("[YooAsset] 初始化资源系统");
        if (!YooAssets.IsInitialized)
        {
            YooAssets.Initialize();
        }

        if (!YooAssets.TryGetPackage(
                packageName,
                out ResourcePackage package))
        {
            package = YooAssets.CreatePackage(packageName);
        }

        Package = package;

        bool initialized =
            Package.InitializeStatus ==
            EOperationStatus.Succeeded;

        if (!initialized)
        {
            switch (_activeMode)
            {
                case PlayMode.Offline:
                    yield return InitializeOffline(
                        result => initialized = result
                    );
                    break;

                case PlayMode.Host:
                    yield return InitializeHost(
                        result => initialized = result
                    );
                    break;
            }
        }

        if (!initialized)
        {
            FailInitialization("资源系统初始化失败");
            yield break;
        }

        if (_activeMode == PlayMode.Host &&
            downloadAllOnStart)
        {
            bool downloaded = false;

            yield return DownloadRemoteResources(
                result => downloaded = result
            );

            if (!downloaded)
            {
                if (!fallbackToOffline)
                {
                    FailInitialization(
                        "资源下载失败，终止启动");
                    yield break;
                }

                Debug.LogWarning(
                    "[YooAsset] 下载失败，切换到 APK 内置资源"
                );

                bool offlineReady = false;

                yield return SwitchToOffline(
                    result => offlineReady = result
                );

                if (!offlineReady)
                {
                    FailInitialization(
                        "切换内置资源失败");
                    yield break;
                }
            }
        }

        IsReady = true;
        IsInitializing = false;
        Debug.Log(
            $"[YooAsset] 资源系统就绪：{packageName}，" +
            $"模式：{_activeMode}");
#endif
    }

    #region 初始化模式

    private IEnumerator InitializeOffline(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 使用 Offline 模式");

        var builtinFileSystemParameters =
            FileSystemParameters
                .CreateDefaultBuiltinFileSystemParameters();

        var options = new OfflinePlayModeOptions
        {
            BuiltinFileSystemParameters =
                builtinFileSystemParameters
        };

        var operation =
            Package.InitializePackageAsync(options);

        yield return operation;

        if (operation.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError(
                "[YooAsset] Offline 初始化失败：\n" +
                operation.Error
            );

            completed?.Invoke(false);
            yield break;
        }

        yield return PreparePackageManifest(completed);
    }

    private IEnumerator InitializeHost(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 使用 Host 模式");

        bool hostServerReady = false;
        yield return RequestHostServers(
            result => hostServerReady = result);
        if (!hostServerReady)
        {
            if (fallbackToOffline)
            {
                Debug.LogWarning(
                    "[YooAsset] 获取远程资源地址失败，" +
                    "切换到 APK 内置资源");
                yield return SwitchToOffline(completed);
            }
            else
            {
                completed?.Invoke(false);
            }

            yield break;
        }

        var remoteServices = new RemoteServices(
            DefaultHostServer,
            FallbackHostServer
        );

        var builtinFileSystemParameters =
            FileSystemParameters
                .CreateDefaultBuiltinFileSystemParameters();

        var cacheFileSystemParameters =
            FileSystemParameters
                .CreateDefaultSandboxFileSystemParameters(
                    remoteServices
                );

        var options = new HostPlayModeOptions
        {
            BuiltinFileSystemParameters =
                builtinFileSystemParameters,

            CacheFileSystemParameters =
                cacheFileSystemParameters
        };

        var operation =
            Package.InitializePackageAsync(options);

        yield return operation;

        if (operation.Status != EOperationStatus.Succeeded)
        {
            Debug.LogWarning(
                "[YooAsset] Host 初始化失败：\n" +
                operation.Error
            );

            if (fallbackToOffline)
            {
                yield return SwitchToOffline(completed);
            }
            else
            {
                completed?.Invoke(false);
            }

            yield break;
        }

        bool manifestReady = false;

        yield return PreparePackageManifest(
            result => manifestReady = result
        );

        if (manifestReady)
        {
            completed?.Invoke(true);
            yield break;
        }

        if (fallbackToOffline)
        {
            Debug.LogWarning(
                "[YooAsset] 远程版本或清单请求失败，" +
                "切换到 APK 内置资源"
            );

            yield return SwitchToOffline(completed);
        }
        else
        {
            completed?.Invoke(false);
        }
    }

    private IEnumerator RequestHostServers(
        Action<bool> completed)
    {
        Debug.Log(
            $"[YooAsset] 请求远程资源配置：{GameConfigUrl}");

        using (UnityWebRequest request =
               UnityWebRequest.Get(GameConfigUrl))
        {
            request.timeout =
                GameConfigRequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    "[YooAsset] 请求远程资源配置失败：\n" +
                    request.error);
                completed?.Invoke(false);
                yield break;
            }

            if (!TryBuildHostServer(
                    request.downloadHandler.text,
                    Application.platform,
                    out string hostServer,
                    out string error))
            {
                Debug.LogWarning(
                    "[YooAsset] 远程资源配置无效：\n" +
                    error);
                completed?.Invoke(false);
                yield break;
            }

            DefaultHostServer = hostServer;
            FallbackHostServer = hostServer;
            Debug.Log(
                "[YooAsset] 远程资源地址：" +
                DefaultHostServer);
            completed?.Invoke(true);
        }
    }

    private static bool TryBuildHostServer(
        string json,
        RuntimePlatform platform,
        out string hostServer,
        out string error)
    {
        hostServer = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "gameConfig.json 返回内容为空。";
            return false;
        }

        GameConfigResponse response;
        try
        {
            response =
                JsonUtility.FromJson<GameConfigResponse>(json);
        }
        catch (Exception exception)
        {
            error = "gameConfig.json 解析失败：" +
                    exception.Message;
            return false;
        }

        if (response == null)
        {
            error = "gameConfig.json 解析结果为空。";
            return false;
        }

        if (response.code != 200)
        {
            error =
                $"接口返回 code={response.code}，" +
                $"message={response.message}";
            return false;
        }

        if (response.data == null)
        {
            error = "gameConfig.json 缺少 data。";
            return false;
        }

        string version =
            response.data.version?.Trim().Trim('/') ??
            string.Empty;
        string downloadUrl =
            response.data.downloadUrl?.Trim().TrimEnd('/') ??
            string.Empty;
        if (version.Length == 0 || downloadUrl.Length == 0)
        {
            error = "gameConfig.json 缺少 version 或 downloadUrl。";
            return false;
        }

        if (!Uri.TryCreate(
                downloadUrl,
                UriKind.Absolute,
                out Uri downloadUri) ||
            (downloadUri.Scheme != Uri.UriSchemeHttp &&
             downloadUri.Scheme != Uri.UriSchemeHttps))
        {
            error = "downloadUrl 必须是有效的 HTTP/HTTPS 地址。";
            return false;
        }

        string platformFolder;
        switch (platform)
        {
            case RuntimePlatform.Android:
                platformFolder = "Android";
                break;
            case RuntimePlatform.IPhonePlayer:
                platformFolder = "IPhone";
                break;
            default:
                error = $"Host 模式暂不支持平台：{platform}";
                return false;
        }

        hostServer =
            $"{downloadUrl}/{platformFolder}/{version}";
        return true;
    }

    #endregion

    #region 版本与清单

    private IEnumerator PreparePackageManifest(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 请求 Package 版本");

        var versionOperation =
            Package.RequestPackageVersionAsync();

        yield return versionOperation;

        if (versionOperation.Status !=
            EOperationStatus.Succeeded)
        {
            Debug.LogWarning(
                "[YooAsset] 请求 Package 版本失败：\n" +
                versionOperation.Error
            );

            completed?.Invoke(false);
            yield break;
        }

        string packageVersion =
            versionOperation.PackageVersion;

        Debug.Log(
            $"[YooAsset] Package 版本：{packageVersion}"
        );

        var options = new LoadPackageManifestOptions(
            packageVersion,
            60
        );

        var manifestOperation =
            Package.LoadPackageManifestAsync(options);

        yield return manifestOperation;

        if (manifestOperation.Status !=
            EOperationStatus.Succeeded)
        {
            Debug.LogWarning(
                "[YooAsset] 加载 Package 清单失败：\n" +
                manifestOperation.Error
            );

            completed?.Invoke(false);
            yield break;
        }

        Debug.Log("[YooAsset] Package 清单加载成功");

        completed?.Invoke(true);
    }

    #endregion

    #region 下载

    private IEnumerator DownloadRemoteResources(
        Action<bool> completed)
    {
        var options = new ResourceDownloaderOptions(
            Mathf.Max(1, maxDownloadCount),
            Mathf.Max(0, downloadRetryCount)
        );

        var downloader =
            Package.CreateResourceDownloader(options);

        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log(
                "[YooAsset] 当前没有需要下载的资源"
            );

            completed?.Invoke(true);
            yield break;
        }

        Debug.Log(
            $"[YooAsset] 需要下载文件：" +
            $"{downloader.TotalDownloadCount} 个，" +
            $"大小：{FormatBytes(downloader.TotalDownloadBytes)}"
        );

        downloader.StartDownload();

        yield return downloader;

        if (downloader.Status !=
            EOperationStatus.Succeeded)
        {
            Debug.LogWarning(
                "[YooAsset] 资源下载失败"
            );

            completed?.Invoke(false);
            yield break;
        }

        Debug.Log("[YooAsset] 资源下载完成");

        completed?.Invoke(true);
    }

    #endregion

    #region Offline 降级

    private IEnumerator SwitchToOffline(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 正在切换到 Offline 模式");

        if (Package != null)
        {
            string oldPackageName =
                Package.PackageName;

            var destroyOperation =
                Package.DestroyPackageAsync();

            yield return destroyOperation;

            YooAssets.RemovePackage(oldPackageName);
        }

        Package =
            YooAssets.CreatePackage(packageName);

        _activeMode = PlayMode.Offline;

        bool offlineReady = false;

        yield return InitializeOffline(
            result => offlineReady = result
        );

        completed?.Invoke(offlineReady);
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void FailInitialization(string message)
    {
        LastError = message;
        IsReady = false;
        IsInitializing = false;
        Debug.LogError("[YooAsset] " + message);
    }

    private static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        if (bytes >= gb)
        {
            return $"{bytes / (double)gb:F2} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / (double)mb:F2} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / (double)kb:F2} KB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// 根据 YooAsset 提供的文件名拼接完整远程地址。
    /// </summary>
    private sealed class RemoteServices :
        IRemoteService
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(
            string defaultHostServer,
            string fallbackHostServer)
        {
            _defaultHostServer =
                defaultHostServer.TrimEnd('/');

            _fallbackHostServer =
                fallbackHostServer.TrimEnd('/');
        }

        public IReadOnlyList<string> GetRemoteUrls(
            string fileName)
        {
            return new[]
            {
                $"{_defaultHostServer}/{fileName}",
                $"{_fallbackHostServer}/{fileName}"
            };
        }
    }
}
