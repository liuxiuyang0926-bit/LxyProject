using System;
using System.Collections;
using System.Collections.Generic;
using Game.Contracts;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

public sealed class YooAssetLauncher : MonoBehaviour
{
    // 这些 Location 必须存在于当前激活的 Manifest 中，否则下载器
    // 不会包含它们，后续 HybridCLR 加载必然得到 Location is invalid。
    private static readonly IReadOnlyList<string>
        RequiredStartupLocations = new[]
        {
            HybridCLRAssemblyManifest.ManifestLocation,
            HybridCLRAssemblyManifest.DefaultEntryAssemblyName +
            ".dll",
        };

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

    public enum UpdateStage
    {
        Idle,
        Initializing,
        RequestingServerConfig,
        InitializingPackage,
        RequestingVersion,
        LoadingManifest,
        ValidatingManifest,
        CreatingDownloader,
        Downloading,
        Paused,
        ClearingCache,
        Ready,
        Cancelled,
        Failed
    }

    public readonly struct UpdateSnapshot
    {
        public UpdateSnapshot(
            UpdateStage stage,
            float progress,
            string message,
            int currentDownloadCount,
            int totalDownloadCount,
            long currentDownloadBytes,
            long totalDownloadBytes)
        {
            Stage = stage;
            Progress = Mathf.Clamp01(progress);
            Message = message ?? string.Empty;
            CurrentDownloadCount = currentDownloadCount;
            TotalDownloadCount = totalDownloadCount;
            CurrentDownloadBytes = currentDownloadBytes;
            TotalDownloadBytes = totalDownloadBytes;
        }

        public UpdateStage Stage { get; }
        public float Progress { get; }
        public string Message { get; }
        public int CurrentDownloadCount { get; }
        public int TotalDownloadCount { get; }
        public long CurrentDownloadBytes { get; }
        public long TotalDownloadBytes { get; }
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

    [Header("更新成功后清理废弃 Bundle 缓存")]
    [SerializeField]
    private bool clearUnusedCacheAfterUpdate = true;

    public static YooAssetLauncher Instance { get; private set; }

    public ResourcePackage Package { get; private set; }

    public bool IsReady { get; private set; }
    public bool IsInitializing { get; private set; }
    public string LastError { get; private set; }
    public string PackageName => packageName;
    public string PackageVersion { get; private set; }
    public PlayMode ActiveMode => _activeMode;
    public string DefaultHostServer { get; private set; }
    public string FallbackHostServer { get; private set; }
    public UpdateStage Stage { get; private set; } =
        UpdateStage.Idle;
    public float Progress { get; private set; }
    public string StatusMessage { get; private set; } =
        string.Empty;
    public int CurrentDownloadCount { get; private set; }
    public int TotalDownloadCount { get; private set; }
    public long CurrentDownloadBytes { get; private set; }
    public long TotalDownloadBytes { get; private set; }
    public string CurrentDownloadFile { get; private set; }
    public bool IsDownloadPaused { get; private set; }

    public event Action<UpdateSnapshot> StatusChanged;
    public event Action<DownloadProgressChangedEventArgs>
        DownloadProgressChanged;
    public event Action<DownloadErrorEventArgs> DownloadError;

    private PlayMode _activeMode;
    private DownloaderOperation activeDownloader;
    private bool downloadCancelled;

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
        downloadCancelled = false;
        ResetDownloadStatistics();
        packageName = string.IsNullOrWhiteSpace(packageName)
            ? "DefaultPackage"
            : packageName.Trim();
        SetStage(
            UpdateStage.Initializing,
            0.02f,
            "初始化资源更新服务");

#if UNITY_EDITOR
        _activeMode = PlayMode.EditorAssetDatabase;
        PackageVersion = "Editor";
        IsReady = true;
        IsInitializing = false;
        SetStage(
            UpdateStage.Ready,
            1f,
            "Editor AssetDatabase 资源已就绪");
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

        if (initialized)
        {
            PackageVersion = Package.GetPackageVersion();
        }

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
            FailInitialization(
                string.IsNullOrEmpty(LastError)
                    ? "资源系统初始化失败"
                    : LastError);
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
                if (downloadCancelled)
                {
                    FailInitialization("资源下载已取消");
                    yield break;
                }

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
            else if (clearUnusedCacheAfterUpdate)
            {
                yield return ClearUnusedCache();
            }
        }

        IsReady = true;
        IsInitializing = false;
        SetStage(
            UpdateStage.Ready,
            1f,
            $"资源系统已就绪（{PackageVersion}）");
        Debug.Log(
            $"[YooAsset] 资源系统就绪：{packageName}，" +
            $"模式：{_activeMode}");
#endif
    }

    #region 初始化模式

    private IEnumerator InitializeOffline(
        Action<bool> completed)
    {
        SetStage(
            UpdateStage.InitializingPackage,
            0.12f,
            "初始化 APK 内置资源");
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

        SetStage(
            UpdateStage.InitializingPackage,
            0.12f,
            "初始化远程资源 Package");

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
                "[YooAsset] 远程清单不可用或缺少启动资源，" +
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
        string gameConfigUrl = GameRuntimeConfig.GameConfigUrl;

        SetStage(
            UpdateStage.RequestingServerConfig,
            0.06f,
            "请求远程资源配置");
        Debug.Log(
            $"[YooAsset] 请求远程资源配置：{gameConfigUrl}");

        using (UnityWebRequest request =
               UnityWebRequest.Get(gameConfigUrl))
        {
            request.timeout =
                GameRuntimeConfig.GameConfigRequestTimeoutSeconds;
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
        SetStage(
            UpdateStage.RequestingVersion,
            0.22f,
            "请求资源版本");
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
        PackageVersion = packageVersion;

        Debug.Log(
            $"[YooAsset] Package 版本：{packageVersion}"
        );

        var options = new LoadPackageManifestOptions(
            packageVersion,
            60
        );

        SetStage(
            UpdateStage.LoadingManifest,
            0.32f,
            $"更新资源清单（{packageVersion}）");

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

        SetStage(
            UpdateStage.ValidatingManifest,
            0.44f,
            "校验启动必需资源");

        if (!ValidateRequiredStartupLocations(out string error))
        {
            LastError = error;
            Debug.LogWarning("[YooAsset] " + error);
            completed?.Invoke(false);
            yield break;
        }

        LastError = null;
        Debug.Log(
            $"[YooAsset] Package 清单加载成功并通过启动资源校验：" +
            packageVersion);

        completed?.Invoke(true);
    }

    private bool ValidateRequiredStartupLocations(
        out string error)
    {
        var missingLocations = new List<string>();
        foreach (string location in RequiredStartupLocations)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                continue;
            }

            if (!Package.IsLocationValid(location))
            {
                missingLocations.Add(location);
            }
        }

        if (missingLocations.Count == 0)
        {
            error = null;
            return true;
        }

        string manifestVersion = Package.GetPackageVersion();
        error =
            $"Package Manifest {manifestVersion} 缺少启动资源：" +
            string.Join(", ", missingLocations) +
            "。请重新构建并发布 YooAsset 清单和对应 Bundle。";
        return false;
    }

    #endregion

    #region 下载

    private IEnumerator DownloadRemoteResources(
        Action<bool> completed)
    {
        SetStage(
            UpdateStage.CreatingDownloader,
            0.5f,
            "计算需要更新的资源");

        var options = new ResourceDownloaderOptions(
            Mathf.Max(1, maxDownloadCount),
            Mathf.Max(0, downloadRetryCount)
        );

        activeDownloader =
            Package.CreateResourceDownloader(options);
        DownloaderOperation downloader = activeDownloader;

        TotalDownloadCount = downloader.TotalDownloadCount;
        TotalDownloadBytes = downloader.TotalDownloadBytes;

        if (downloader.TotalDownloadCount == 0)
        {
            SetStage(
                UpdateStage.Downloading,
                0.9f,
                "当前已是最新资源");
            Debug.Log(
                "[YooAsset] 当前没有需要下载的资源"
            );

            activeDownloader = null;
            completed?.Invoke(true);
            yield break;
        }

        Debug.Log(
            $"[YooAsset] 需要下载文件：" +
            $"{downloader.TotalDownloadCount} 个，" +
            $"大小：{FormatBytes(downloader.TotalDownloadBytes)}"
        );

        SetStage(
            UpdateStage.Downloading,
            0.5f,
            $"准备下载 {TotalDownloadCount} 个文件，" +
            FormatBytes(TotalDownloadBytes));

        downloader.DownloadProgressChanged +=
            OnDownloadProgressChanged;
        downloader.DownloadError += OnDownloadError;
        downloader.DownloadFileStarted +=
            OnDownloadFileStarted;

        downloader.StartDownload();

        yield return downloader;

        downloader.DownloadProgressChanged -=
            OnDownloadProgressChanged;
        downloader.DownloadError -= OnDownloadError;
        downloader.DownloadFileStarted -=
            OnDownloadFileStarted;
        activeDownloader = null;
        IsDownloadPaused = false;

        if (downloader.Status !=
            EOperationStatus.Succeeded)
        {
            Debug.LogWarning(
                "[YooAsset] 资源下载失败：" +
                downloader.Error
            );

            completed?.Invoke(false);
            yield break;
        }

        Debug.Log("[YooAsset] 资源下载完成");
        CurrentDownloadCount = TotalDownloadCount;
        CurrentDownloadBytes = TotalDownloadBytes;
        SetStage(
            UpdateStage.Downloading,
            0.9f,
            "资源下载完成");

        completed?.Invoke(true);
    }

    public bool PauseDownload()
    {
        if (activeDownloader == null ||
            activeDownloader.IsDone ||
            IsDownloadPaused)
        {
            return false;
        }

        activeDownloader.PauseDownload();
        IsDownloadPaused = true;
        SetStage(
            UpdateStage.Paused,
            Progress,
            "资源下载已暂停");
        return true;
    }

    public bool ResumeDownload()
    {
        if (activeDownloader == null ||
            activeDownloader.IsDone ||
            !IsDownloadPaused)
        {
            return false;
        }

        activeDownloader.ResumeDownload();
        IsDownloadPaused = false;
        SetStage(
            UpdateStage.Downloading,
            Progress,
            "继续下载资源");
        return true;
    }

    public bool CancelDownload()
    {
        if (activeDownloader == null ||
            activeDownloader.IsDone)
        {
            return false;
        }

        downloadCancelled = true;
        IsDownloadPaused = false;
        activeDownloader.CancelDownload();
        SetStage(
            UpdateStage.Cancelled,
            Progress,
            "资源下载已取消");
        return true;
    }

    private void OnDownloadProgressChanged(
        DownloadProgressChangedEventArgs args)
    {
        CurrentDownloadCount = args.CurrentDownloadCount;
        TotalDownloadCount = args.TotalDownloadCount;
        CurrentDownloadBytes = args.CurrentDownloadBytes;
        TotalDownloadBytes = args.TotalDownloadBytes;

        SetStage(
            IsDownloadPaused
                ? UpdateStage.Paused
                : UpdateStage.Downloading,
            Mathf.Lerp(0.5f, 0.9f, args.Progress),
            $"下载资源 {args.CurrentDownloadCount}/" +
            $"{args.TotalDownloadCount}，" +
            $"{FormatBytes(args.CurrentDownloadBytes)}/" +
            FormatBytes(args.TotalDownloadBytes));
        SafeInvoke(
            () => DownloadProgressChanged?.Invoke(args));
    }

    private void OnDownloadError(DownloadErrorEventArgs args)
    {
        SafeInvoke(() => DownloadError?.Invoke(args));
        Debug.LogWarning(
            $"[YooAsset] 文件下载失败：{args.FileName}\n" +
            args.ErrorInfo);
    }

    private void OnDownloadFileStarted(
        DownloadFileStartedEventArgs args)
    {
        CurrentDownloadFile = args.FileName;
    }

    private IEnumerator ClearUnusedCache()
    {
        SetStage(
            UpdateStage.ClearingCache,
            0.94f,
            "清理过期资源缓存");

        var options = new ClearCacheOptions(
            ClearCacheMethods.ClearUnusedBundleFiles);
        ClearCacheOperation operation =
            Package.ClearCacheAsync(options);
        yield return operation;

        if (operation.Status != EOperationStatus.Succeeded)
        {
            // 清理旧缓存不影响当前 Manifest 可用性，
            // 因此只记录告警，不中断本次启动。
            Debug.LogWarning(
                "[YooAsset] 清理过期缓存失败：\n" +
                operation.Error);
        }
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

    private void SetStage(
        UpdateStage stage,
        float progress,
        string message)
    {
        Stage = stage;
        Progress = Mathf.Clamp01(progress);
        StatusMessage = message ?? string.Empty;

        var snapshot = new UpdateSnapshot(
            Stage,
            Progress,
            StatusMessage,
            CurrentDownloadCount,
            TotalDownloadCount,
            CurrentDownloadBytes,
            TotalDownloadBytes);
        SafeInvoke(() => StatusChanged?.Invoke(snapshot));
    }

    private void ResetDownloadStatistics()
    {
        activeDownloader = null;
        IsDownloadPaused = false;
        CurrentDownloadCount = 0;
        TotalDownloadCount = 0;
        CurrentDownloadBytes = 0;
        TotalDownloadBytes = 0;
        CurrentDownloadFile = null;
    }

    private static void SafeInvoke(Action callback)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

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
        SetStage(
            downloadCancelled
                ? UpdateStage.Cancelled
                : UpdateStage.Failed,
            Progress,
            message);
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
