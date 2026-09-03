using System;
using System.Collections;
using System.Collections.Generic;
using Game.Contracts;
using Game.Resource;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using YooAsset;

public sealed class YooAssetLauncher : MonoBehaviour
{
    // 这些 Location 必须存在于当前激活的 Manifest 中，否则下载器
    // 不会包含它们，后续 HybridCLR 加载必然得到 Location is invalid。
    private static readonly IReadOnlyList<string>
        RequiredStartupLocations = new[]
        {
            HybridCLRAssemblyManifest.ManifestLocation,
        };

    [Serializable]
    private sealed class GameConfigResponse
    {
        /// <summary>
        /// 公开的code数据。
        /// </summary>
        public int code;
        /// <summary>
        /// 公开的消息数据。
        /// </summary>
        public string message;
        /// <summary>
        /// 公开的数据数据。
        /// </summary>
        public GameConfigData data;
    }

    [Serializable]
    private sealed class GameConfigData
    {
        /// <summary>
        /// 公开的版本数据。
        /// </summary>
        public string version;
        /// <summary>
        /// 公开的下载地址数据。
        /// </summary>
        public string downloadUrl;
        /// <summary>
        /// 公开的最小App版本数据。
        /// </summary>
        public string minimumAppVersion;
        /// <summary>
        /// 公开的最小Android版本Code数据。
        /// </summary>
        public int minimumAndroidVersionCode;
        /// <summary>
        /// 公开的app下载地址数据。
        /// </summary>
        public string appDownloadUrl;
        /// <summary>
        /// 公开的forceUpdate消息数据。
        /// </summary>
        public string forceUpdateMessage;
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
        CheckingAppVersion,
        ForceUpdateRequired,
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
        /// <summary>
        /// 更新快照。
        /// </summary>
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

        /// <summary>
        /// 当前操作所处的执行阶段。
        /// </summary>
        public UpdateStage Stage { get; }
        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; }
        /// <summary>
        /// 当前操作的状态说明文本。
        /// </summary>
        public string Message { get; }
        /// <summary>
        /// 当前已完成下载的文件数量。
        /// </summary>
        public int CurrentDownloadCount { get; }
        /// <summary>
        /// 本次下载所需的文件总数。
        /// </summary>
        public int TotalDownloadCount { get; }
        /// <summary>
        /// 当前已下载的字节数。
        /// </summary>
        public long CurrentDownloadBytes { get; }
        /// <summary>
        /// 本次下载的字节总数。
        /// </summary>
        public long TotalDownloadBytes { get; }
    }

    [Header("运行模式")]
    [SerializeField]
    private PlayMode playMode = PlayMode.Host;

    [Header("Bundle Collector 中的 Package 名")]
    [SerializeField]
    private string packageName = "DefaultPackage";

    [FormerlySerializedAs("downloadAllOnStart")]
    [Header("Host 模式启动时下载 Mandatory 强更资源")]
    [SerializeField]
    private bool downloadMandatoryOnStart = true;

    [Header("服务器异常时允许降级到内置资源（强更正式包应关闭）")]
    [SerializeField]
    private bool fallbackToOffline = false;

    [Header("同时下载文件数量")]
    [SerializeField]
    private int maxDownloadCount = 10;

    [Header("下载失败重试次数")]
    [SerializeField]
    private int downloadRetryCount = 3;

    [Header("更新成功后清理废弃 Bundle 缓存")]
    [SerializeField]
    private bool clearUnusedCacheAfterUpdate = true;

    /// <summary>
    /// 仅在编辑器中启用的整包强更模拟开关，用于验证强更交互流程。
    /// </summary>
    [Header("仅编辑器：模拟客户端整包强更")]
    [SerializeField]
    private bool simulateForceUpdateInEditor;

    [SerializeField]
    private string simulatedMinimumAppVersion = "99.0.0";

    [SerializeField]
    private string simulatedAppDownloadUrl =
        "https://example.com/download";

    /// <summary>
    /// 向调用方提供实例。
    /// </summary>
    public static YooAssetLauncher Instance { get; private set; }

    /// <summary>
    /// 向调用方提供资源包。
    /// </summary>
    public ResourcePackage Package { get; private set; }

    /// <summary>
    /// 指示当前对象是否已就绪。
    /// </summary>
    public bool IsReady { get; private set; }
    /// <summary>
    /// 指示当前对象是否正在初始化。
    /// </summary>
    public bool IsInitializing { get; private set; }
    /// <summary>
    /// 最近一次操作失败的错误信息；未发生错误时为 null。
    /// </summary>
    public string LastError { get; private set; }
    /// <summary>
    /// 向调用方提供资源包名称。
    /// </summary>
    public string PackageName => packageName;
    /// <summary>
    /// 向调用方提供资源包版本。
    /// </summary>
    public string PackageVersion { get; private set; }
    /// <summary>
    /// 向调用方提供ActiveMode。
    /// </summary>
    public PlayMode ActiveMode => _activeMode;
    /// <summary>
    /// 向调用方提供DefaultHostServer。
    /// </summary>
    public string DefaultHostServer { get; private set; }
    /// <summary>
    /// 向调用方提供FallbackHostServer。
    /// </summary>
    public string FallbackHostServer { get; private set; }
    /// <summary>
    /// 当前操作所处的执行阶段。
    /// </summary>
    public UpdateStage Stage { get; private set; } =
        UpdateStage.Idle;
    /// <summary>
    /// 当前操作的归一化进度，取值范围为 0 到 1。
    /// </summary>
    public float Progress { get; private set; }
    /// <summary>
    /// 当前操作的状态说明文本。
    /// </summary>
    public string StatusMessage { get; private set; } =
        string.Empty;
    /// <summary>
    /// 当前已完成下载的文件数量。
    /// </summary>
    public int CurrentDownloadCount { get; private set; }
    /// <summary>
    /// 本次下载所需的文件总数。
    /// </summary>
    public int TotalDownloadCount { get; private set; }
    /// <summary>
    /// 当前已下载的字节数。
    /// </summary>
    public long CurrentDownloadBytes { get; private set; }
    /// <summary>
    /// 本次下载的字节总数。
    /// </summary>
    public long TotalDownloadBytes { get; private set; }
    /// <summary>
    /// 当前正在下载的文件名称。
    /// </summary>
    public string CurrentDownloadFile { get; private set; }
    /// <summary>
    /// 指示下载任务是否处于暂停状态。
    /// </summary>
    public bool IsDownloadPaused { get; private set; }
    /// <summary>
    /// 指示当前客户端是否必须执行整包更新。
    /// </summary>
    public bool IsForceUpdateRequired { get; private set; }
    /// <summary>
    /// 向调用方提供当前App版本。
    /// </summary>
    public string CurrentAppVersion { get; private set; }
    /// <summary>
    /// 向调用方提供当前Android版本Code。
    /// </summary>
    public int CurrentAndroidVersionCode { get; private set; } = -1;
    /// <summary>
    /// 向调用方提供MinimumApp版本。
    /// </summary>
    public string MinimumAppVersion { get; private set; }
    /// <summary>
    /// 向调用方提供MinimumAndroid版本Code。
    /// </summary>
    public int MinimumAndroidVersionCode { get; private set; }
    /// <summary>
    /// 向调用方提供App下载Url。
    /// </summary>
    public string AppDownloadUrl { get; private set; }
    /// <summary>
    /// 当前操作的状态说明文本。
    /// </summary>
    public string ForceUpdateMessage { get; private set; }

    public event Action<UpdateSnapshot> StatusChanged;
    public event Action<DownloadProgressChangedEventArgs>
        DownloadProgressChanged;
    public event Action<DownloadErrorEventArgs> DownloadError;

    private PlayMode _activeMode;
    private DownloaderOperation activeDownloader;
    private bool downloadCancelled;
    private bool preventOfflineFallback;
    private GameResourceManager resourceManager;

    /// <summary>
    /// 初始化组件的运行时状态。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        resourceManager =
            GameResourceManager.GetOrCreate(gameObject);
    }

    /// <summary>
    /// 由 UIStartup 统一调用。编辑器不初始化 YooAsset，Player 才执行
    /// Offline/Host、版本清单和资源下载流程。
    /// </summary>
    /// <param name="prepareBeforeDownload">
    /// 可选的启动资源准备流程。在 Manifest 生效后、启动强更下载前执行。
    /// </param>
    public IEnumerator InitializeAsync(
        Func<ResourcePackage, IEnumerator> prepareBeforeDownload = null)
    {
        if (IsReady)
        {
            if (prepareBeforeDownload != null)
            {
                yield return prepareBeforeDownload(Package);
            }

            yield break;
        }

        if (IsInitializing)
        {
            while (IsInitializing)
            {
                yield return null;
            }

            if (IsReady && prepareBeforeDownload != null)
            {
                yield return prepareBeforeDownload(Package);
            }

            yield break;
        }

        IsInitializing = true;
        IsReady = false;
        LastError = null;
        downloadCancelled = false;
        preventOfflineFallback = false;
        ResetForceUpdateState();
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
        if (simulateForceUpdateInEditor)
        {
            ActivateForceUpdate(
                simulatedMinimumAppVersion,
                0,
                simulatedAppDownloadUrl,
                "当前为编辑器整包强更模拟，请点击前往更新。");
            IsInitializing = false;
            yield break;
        }

        PackageVersion = "Editor";
        if (prepareBeforeDownload != null)
        {
            yield return prepareBeforeDownload(null);
        }

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
            if (IsForceUpdateRequired)
            {
                IsInitializing = false;
                yield break;
            }

            FailInitialization(
                string.IsNullOrEmpty(LastError)
                    ? "资源系统初始化失败"
                    : LastError);
            yield break;
        }

        resourceManager ??=
            GameResourceManager.GetOrCreate(gameObject);
        resourceManager.SetDefaultPackage(Package);

        if (prepareBeforeDownload != null)
        {
            yield return prepareBeforeDownload(Package);
        }

        if (_activeMode == PlayMode.Host &&
            downloadMandatoryOnStart)
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

    /// <summary>
    /// 初始化Offline。
    /// </summary>
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

    /// <summary>
    /// 初始化主机。
    /// </summary>
    private IEnumerator InitializeHost(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 使用 Host 模式");

        bool hostServerReady = false;
        yield return RequestHostServers(
            result => hostServerReady = result);
        if (!hostServerReady)
        {
            if (preventOfflineFallback)
            {
                completed?.Invoke(false);
            }
            else if (fallbackToOffline)
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

    /// <summary>
    /// 执行RequestHostServers相关逻辑。
    /// </summary>
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

            if (!TryParseGameConfig(
                    request.downloadHandler.text,
                    out GameConfigData config,
                    out string error))
            {
                Debug.LogWarning(
                    "[YooAsset] 远程资源配置无效：\n" +
                    error);
                completed?.Invoke(false);
                yield break;
            }

            SetStage(
                UpdateStage.CheckingAppVersion,
                0.08f,
                "检查客户端版本");
            if (!TryApplyAppVersionPolicy(
                    config,
                    Application.platform,
                    out error))
            {
                preventOfflineFallback = true;
                LastError = "客户端强更配置无效：" + error;
                Debug.LogError("[YooAsset] " + LastError);
                completed?.Invoke(false);
                yield break;
            }

            if (IsForceUpdateRequired)
            {
                preventOfflineFallback = true;
                completed?.Invoke(false);
                yield break;
            }

            if (!TryBuildHostServer(
                    config,
                    Application.platform,
                    out string hostServer,
                    out error))
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

    /// <summary>
    /// 尝试解析游戏配置，并返回是否成功。
    /// </summary>
    private static bool TryParseGameConfig(
        string json,
        out GameConfigData config,
        out string error)
    {
        config = null;
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

        config = response.data;
        return true;
    }

    /// <summary>
    /// 尝试构建主机服务器，并返回是否成功。
    /// </summary>
    private static bool TryBuildHostServer(
        GameConfigData config,
        RuntimePlatform platform,
        out string hostServer,
        out string error)
    {
        hostServer = null;
        error = null;

        string version =
            config.version?.Trim().Trim('/') ??
            string.Empty;
        string downloadUrl =
            config.downloadUrl?.Trim().TrimEnd('/') ??
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

    /// <summary>
    /// 尝试应用App版本Policy，并返回是否成功。
    /// </summary>
    private bool TryApplyAppVersionPolicy(
        GameConfigData config,
        RuntimePlatform platform,
        out string error)
    {
        error = null;
        string minimumVersion =
            config.minimumAppVersion?.Trim() ?? string.Empty;
        int minimumVersionCode =
            config.minimumAndroidVersionCode;

        if (minimumVersionCode < 0)
        {
            error = "minimumAndroidVersionCode 不能小于 0。";
            return false;
        }

        bool versionNameRequiresUpdate = false;
        if (minimumVersion.Length > 0)
        {
            if (!TryCompareVersionNames(
                    CurrentAppVersion,
                    minimumVersion,
                    out int comparison,
                    out error))
            {
                return false;
            }

            versionNameRequiresUpdate = comparison < 0;
        }

        bool versionCodeRequiresUpdate = false;
        if (platform == RuntimePlatform.Android &&
            minimumVersionCode > 0)
        {
            if (!TryGetAndroidVersionCode(
                    out int currentVersionCode,
                    out error))
            {
                return false;
            }

            CurrentAndroidVersionCode = currentVersionCode;
            versionCodeRequiresUpdate =
                currentVersionCode < minimumVersionCode;
        }

        if (!versionNameRequiresUpdate &&
            !versionCodeRequiresUpdate)
        {
            return true;
        }

        string updateUrl = config.appDownloadUrl?.Trim();
        if (!TryGetHttpUri(updateUrl, out Uri normalizedUri))
        {
            error = "触发整包强更时 appDownloadUrl 必须是" +
                    "有效的 HTTP/HTTPS 地址。";
            return false;
        }

        ActivateForceUpdate(
            minimumVersion,
            minimumVersionCode,
            normalizedUri.AbsoluteUri,
            config.forceUpdateMessage);
        return true;
    }

    /// <summary>
    /// 执行Activate强制更新相关逻辑。
    /// </summary>
    private void ActivateForceUpdate(
        string minimumVersion,
        int minimumVersionCode,
        string updateUrl,
        string message)
    {
        IsForceUpdateRequired = true;
        MinimumAppVersion = minimumVersion?.Trim() ?? string.Empty;
        MinimumAndroidVersionCode = Mathf.Max(0, minimumVersionCode);
        AppDownloadUrl = updateUrl?.Trim() ?? string.Empty;
        ForceUpdateMessage = string.IsNullOrWhiteSpace(message)
            ? "检测到必须安装的新客户端版本，" +
              "请更新后重新进入游戏。"
            : message.Trim();
        LastError = ForceUpdateMessage;

        SetStage(
            UpdateStage.ForceUpdateRequired,
            0.08f,
            ForceUpdateMessage);
        Debug.LogWarning(
            $"[YooAsset] 客户端需要整包强更：" +
            $"当前 version={CurrentAppVersion}, " +
            $"versionCode={CurrentAndroidVersionCode}；" +
            $"最低 version={MinimumAppVersion}, " +
            $"versionCode={MinimumAndroidVersionCode}。");
    }

    /// <summary>
    /// 尝试打开状态强制UpdatePage，并返回是否成功。
    /// </summary>
    public bool TryOpenForceUpdatePage(out string error)
    {
        if (!IsForceUpdateRequired)
        {
            error = "当前没有需要执行的整包强更。";
            return false;
        }

        if (!TryGetOpenableHttpUri(
                AppDownloadUrl,
                out Uri updateUri))
        {
            error = "整包下载地址无效。请联系运营检查 " +
                    "gameConfig.json 的 appDownloadUrl。";
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!TryOpenAndroidUpdatePage(updateUri, out error))
        {
            return false;
        }
#else
        Application.OpenURL(updateUri.AbsoluteUri);
        error = null;
#endif
        Debug.Log(
            "[YooAsset] 已请求打开客户端更新页面：" +
            updateUri.AbsoluteUri);
        return true;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 通过 Android VIEW Intent 打开整包下载页面，以便在没有可处理
    /// HTTP 链接的应用时向启动界面返回明确错误。
    /// </summary>
    private static bool TryOpenAndroidUpdatePage(
        Uri updateUri,
        out string error)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (var uriClass = new AndroidJavaClass(
                       "android.net.Uri"))
            using (AndroidJavaObject androidUri =
                   uriClass.CallStatic<AndroidJavaObject>(
                       "parse",
                       updateUri.AbsoluteUri))
            using (var intent = new AndroidJavaObject(
                       "android.content.Intent",
                       "android.intent.action.VIEW",
                       androidUri))
            using (AndroidJavaObject packageManager =
                   activity.Call<AndroidJavaObject>(
                       "getPackageManager"))
            using (AndroidJavaObject targetActivity =
                   intent.Call<AndroidJavaObject>(
                       "resolveActivity",
                       packageManager))
            {
                if (targetActivity == null)
                {
                    error = "设备没有可打开更新链接的浏览器或应用。";
                    return false;
                }

                activity.Call("startActivity", intent);
            }
        }
        catch (Exception exception)
        {
            error = "无法打开更新页面：" + exception.Message;
            Debug.LogWarning("[YooAsset] " + error);
            return false;
        }

        error = null;
        return true;
    }
#endif

    /// <summary>
    /// 尝试比较版本名称，并返回是否成功。
    /// </summary>
    private static bool TryCompareVersionNames(
        string current,
        string minimum,
        out int comparison,
        out string error)
    {
        comparison = 0;
        if (!TryParseVersionParts(
                current,
                out int[] currentParts))
        {
            error = $"当前 Application.version 无效：{current}。" +
                    "版本号必须是 1 到 4 段非负整数。";
            return false;
        }

        if (!TryParseVersionParts(
                minimum,
                out int[] minimumParts))
        {
            error = $"minimumAppVersion 无效：{minimum}。" +
                    "版本号必须是 1 到 4 段非负整数。";
            return false;
        }

        const int maxVersionParts = 4;
        for (int i = 0; i < maxVersionParts; i++)
        {
            int currentPart = i < currentParts.Length
                ? currentParts[i]
                : 0;
            int minimumPart = i < minimumParts.Length
                ? minimumParts[i]
                : 0;
            if (currentPart == minimumPart)
            {
                continue;
            }

            comparison = currentPart < minimumPart ? -1 : 1;
            break;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// 尝试解析版本Parts，并返回是否成功。
    /// </summary>
    private static bool TryParseVersionParts(
        string value,
        out int[] parts)
    {
        parts = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] sourceParts = value.Trim().Split('.');
        if (sourceParts.Length == 0 || sourceParts.Length > 4)
        {
            return false;
        }

        parts = new int[sourceParts.Length];
        for (int i = 0; i < sourceParts.Length; i++)
        {
            if (sourceParts[i].Length == 0 ||
                !int.TryParse(sourceParts[i], out int part) ||
                part < 0)
            {
                parts = null;
                return false;
            }

            parts[i] = part;
        }

        return true;
    }

    /// <summary>
    /// 尝试获取Android版本代码，并返回是否成功。
    /// </summary>
    private static bool TryGetAndroidVersionCode(
        out int versionCode,
        out string error)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (AndroidJavaObject packageManager =
                   activity.Call<AndroidJavaObject>(
                       "getPackageManager"))
            using (AndroidJavaObject packageInfo =
                   packageManager.Call<AndroidJavaObject>(
                       "getPackageInfo",
                       activity.Call<string>("getPackageName"),
                       0))
            {
                versionCode = packageInfo.Get<int>("versionCode");
            }

            if (versionCode <= 0)
            {
                error = "Android versionCode 必须大于 0。";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            versionCode = -1;
            error = "读取 Android versionCode 失败：" +
                    exception.Message;
            return false;
        }
#else
        versionCode = -1;
        error = "当前运行环境无法读取 Android versionCode。";
        return false;
#endif
    }

    /// <summary>
    /// 尝试获取HttpUri，并返回是否成功。
    /// </summary>
    private static bool TryGetHttpUri(
        string value,
        out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// 校验可交给系统浏览器打开的 HttpUri。远端配置读取阶段保持
    /// 强更状态，点击时再将格式错误明确反馈给用户。
    /// </summary>
    private static bool TryGetOpenableHttpUri(
        string value,
        out Uri uri)
    {
        return TryGetHttpUri(value, out uri) &&
               uri.IsWellFormedOriginalString() &&
               !string.IsNullOrWhiteSpace(uri.Host);
    }

    /// <summary>
    /// 重置强制Update状态。
    /// </summary>
    private void ResetForceUpdateState()
    {
        IsForceUpdateRequired = false;
        CurrentAppVersion =
            string.IsNullOrWhiteSpace(Application.version)
                ? string.Empty
                : Application.version.Trim();
        CurrentAndroidVersionCode = -1;
        MinimumAppVersion = string.Empty;
        MinimumAndroidVersionCode = 0;
        AppDownloadUrl = string.Empty;
        ForceUpdateMessage = string.Empty;
    }

    #endregion

    #region 版本与清单

    /// <summary>
    /// 准备资源包清单。
    /// </summary>
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

    /// <summary>
    /// 校验必需项启动位置。
    /// </summary>
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

    /// <summary>
    /// 执行下载远端资源相关逻辑。
    /// </summary>
    private IEnumerator DownloadRemoteResources(
        Action<bool> completed)
    {
        SetStage(
            UpdateStage.CreatingDownloader,
            0.5f,
            "计算需要更新的资源");

        var options = new ResourceDownloaderOptions(
            YooAssetContentTags.Mandatory,
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
                "启动强更资源已是最新版本");
            Debug.Log(
                "[YooAsset] 当前没有需要下载的 Mandatory 资源"
            );

            activeDownloader = null;
            completed?.Invoke(true);
            yield break;
        }

        Debug.Log(
            $"[YooAsset] 启动强更需要下载：" +
            $"{downloader.TotalDownloadCount} 个，" +
            $"大小：{FormatBytes(downloader.TotalDownloadBytes)}"
        );

        SetStage(
            UpdateStage.Downloading,
            0.5f,
            $"准备下载启动强更资源 {TotalDownloadCount} 个文件，" +
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

        Debug.Log("[YooAsset] 启动强更资源下载完成");
        CurrentDownloadCount = TotalDownloadCount;
        CurrentDownloadBytes = TotalDownloadBytes;
        SetStage(
            UpdateStage.Downloading,
            0.9f,
            "启动强更资源下载完成");

        completed?.Invoke(true);
    }

    /// <summary>
    /// 暂停下载。
    /// </summary>
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

    /// <summary>
    /// 恢复下载。
    /// </summary>
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

    /// <summary>
    /// 取消下载。
    /// </summary>
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

    /// <summary>
    /// 响应下载进度Changed事件。
    /// </summary>
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

    /// <summary>
    /// 响应下载错误事件。
    /// </summary>
    private void OnDownloadError(DownloadErrorEventArgs args)
    {
        SafeInvoke(() => DownloadError?.Invoke(args));
        Debug.LogWarning(
            $"[YooAsset] 文件下载失败：{args.FileName}\n" +
            args.ErrorInfo);
    }

    /// <summary>
    /// 响应下载文件Started事件。
    /// </summary>
    private void OnDownloadFileStarted(
        DownloadFileStartedEventArgs args)
    {
        CurrentDownloadFile = args.FileName;
    }

    /// <summary>
    /// 清空Unused缓存。
    /// </summary>
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

    /// <summary>
    /// 执行切换转换为Offline相关逻辑。
    /// </summary>
    private IEnumerator SwitchToOffline(
        Action<bool> completed)
    {
        Debug.Log("[YooAsset] 正在切换到 Offline 模式");

        if (Package != null)
        {
            string oldPackageName =
                Package.PackageName;

            resourceManager ??=
                GameResourceManager.GetOrCreate(gameObject);
            resourceManager.ReleaseAll();
            resourceManager.SetDefaultPackage(null);

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

        if (offlineReady)
        {
            resourceManager.SetDefaultPackage(Package);
        }

        completed?.Invoke(offlineReady);
    }

    #endregion

    /// <summary>
    /// 设置阶段。
    /// </summary>
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

    /// <summary>
    /// 重置下载Statistics。
    /// </summary>
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

    /// <summary>
    /// 执行安全调用相关逻辑。
    /// </summary>
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

    /// <summary>
    /// 释放持有的资源并解除事件订阅。
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 执行标记失败Initialization相关逻辑。
    /// </summary>
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

    /// <summary>
    /// 格式化字节数。
    /// </summary>
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

        /// <summary>
        /// 创建远端服务实例。
        /// </summary>
        public RemoteServices(
            string defaultHostServer,
            string fallbackHostServer)
        {
            _defaultHostServer =
                defaultHostServer.TrimEnd('/');

            _fallbackHostServer =
                fallbackHostServer.TrimEnd('/');
        }

        /// <summary>
        /// 获取远端地址列表。
        /// </summary>
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
