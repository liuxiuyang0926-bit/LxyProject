using System;
using System.Collections;
using System.Collections.Generic;
using Game.Resource;
using LuaObjectBind;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace LxyDemo.UIFramework
{
    [DefaultExecutionOrder(-8000)]
    public sealed class UIManager : MonoBehaviour
    {
        private sealed class UIPanelRecord
        {
            /// <summary>
            /// 公开的配置数据。
            /// </summary>
            public UIPanelConfig Config;
            /// <summary>
            /// 公开的Logic数据。
            /// </summary>
            public UIPanelLogic Logic;
            /// <summary>
            /// 公开的状态数据。
            /// </summary>
            public UIPanelState State;
            /// <summary>
            /// 公开的请求标识数据。
            /// </summary>
            public long RequestId;
            /// <summary>
            /// 指示User数据。
            /// </summary>
            public object UserData;
            /// <summary>
            /// 公开的ShowWhen已加载数据。
            /// </summary>
            public bool ShowWhenLoaded;
            /// <summary>
            /// 公开的PendingClose目标数据。
            /// </summary>
            public string PendingCloseTarget;
            /// <summary>
            /// 公开的PendingCloseImmediate数据。
            /// </summary>
            public bool PendingCloseImmediate;
            /// <summary>
            /// 公开的CloseForceDestroy数据。
            /// </summary>
            public bool CloseForceDestroy;
            /// <summary>
            /// 公开的CloseSkip动画数据。
            /// </summary>
            public bool CloseSkipAnimation;
            /// <summary>
            /// 公开的Resolved层级数据。
            /// </summary>
            public UILayer ResolvedLayer;
            /// <summary>
            /// 公开的Backdrop数据。
            /// </summary>
            public GameObject Backdrop;
            /// <summary>
            /// 公开的实例句柄数据。
            /// </summary>
            public GameResourceInstanceHandle InstanceHandle;
            /// <summary>
            /// 公开的实例数据。
            /// </summary>
            public GameObject Instance;
            /// <summary>
            /// 指示是否为加载中。
            /// </summary>
            public bool IsLoading;
            public readonly List<UIAsyncOperation<UIPanelLogic>>
                Waiters =
                    new List<UIAsyncOperation<UIPanelLogic>>();
        }

        private static UIManager instance;
        private static bool applicationIsQuitting;

        private readonly Dictionary<string, UIPanelConfig> configs =
            new Dictionary<string, UIPanelConfig>(
                StringComparer.Ordinal);
        private readonly Dictionary<string, Func<UIPanelLogic>>
            logicFactories =
                new Dictionary<string, Func<UIPanelLogic>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject>
            localPrefabs =
                new Dictionary<string, GameObject>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, UIPanelRecord> panelRecords =
            new Dictionary<string, UIPanelRecord>(
                StringComparer.Ordinal);
        private readonly List<UIStackEntry> stackRecords =
            new List<UIStackEntry>();
        private readonly Dictionary<string, List<UIStackSnapshot>>
            savedStacks =
                new Dictionary<string, List<UIStackSnapshot>>(
                    StringComparer.Ordinal);
        private readonly UIStackCoordinator stackCoordinator =
            new UIStackCoordinator();

        private UIManagerSettings settings;
        private UILayerRoot layerRoot;
        private bool ownsLayerRoot;
        private bool isInitialized;
        private bool sceneEventSubscribed;
        private bool isShuttingDown;
        private bool verboseLogging;
        private bool closePanelsOnSceneChanged = true;
        private bool persistAcrossScenes = true;
        private long nextRequestId;
        private IUIBackdropService backdropService =
            new DefaultUIBackdropService();

        public static UIManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                if (applicationIsQuitting)
                {
                    throw new UIFrameworkException(
                        UIFrameworkErrorCode.ManagerShuttingDown,
                        "应用正在退出，不能创建 UIManager。");
                }

                var managerObject =
                    new GameObject("[UIManager]");
                instance = managerObject.AddComponent<UIManager>();
                DontDestroyOnLoad(managerObject);
                return instance;
            }
        }

        /// <summary>
        /// 指示当前对象是否已初始化。
        /// </summary>
        public bool IsInitialized => isInitialized;
        /// <summary>
        /// 向调用方提供层级Root。
        /// </summary>
        public UILayerRoot LayerRoot => EnsureLayerRoot();
        /// <summary>
        /// 当前栈的数量。
        /// </summary>
        public int StackCount => stackRecords.Count;
        /// <summary>
        /// 向调用方提供Top面板标识。
        /// </summary>
        public string TopPanelId =>
            stackCoordinator.GetTop(stackRecords)?.Config.Id;
        public IUIBackdropService BackdropService
        {
            get => backdropService;
            set => backdropService =
                value ?? new DefaultUIBackdropService();
        }

        public event Action<
            string,
            UIPanelState,
            UIPanelState> PanelStateChanged;
        public event Action<string, Exception> PanelLoadFailed;

        /// <summary>
        /// 重置Statics。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            applicationIsQuitting = false;
        }

        /// <summary>
        /// 初始化组件的运行时状态。
        /// </summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 响应ApplicationQuit事件。
        /// </summary>
        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            ShutdownInternal();
            instance = null;
        }

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        public void Initialize(
            UIManagerSettings managerSettings = null,
            UILayerRoot uiLayerRoot = null)
        {
            if (isShuttingDown)
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.ManagerShuttingDown,
                    "UIManager 正在关闭。");
            }

            settings = managerSettings;
            closePanelsOnSceneChanged =
                settings == null ||
                settings.ClosePanelsOnActiveSceneChanged;
            persistAcrossScenes =
                settings == null || settings.DontDestroyOnLoad;
            verboseLogging =
                settings != null && settings.VerboseLogging;

            if (uiLayerRoot != null)
            {
                if (ownsLayerRoot &&
                    layerRoot != null &&
                    layerRoot != uiLayerRoot &&
                    panelRecords.Count == 0)
                {
                    Destroy(layerRoot.gameObject);
                }

                layerRoot = uiLayerRoot;
                ownsLayerRoot = false;
            }

            EnsureLayerRoot();

            if (settings != null)
            {
                foreach (UIPanelConfig config in settings.Panels)
                {
                    if (config != null)
                    {
                        Register(config);
                    }
                }
            }

            if (!sceneEventSubscribed)
            {
                SceneManager.activeSceneChanged +=
                    OnActiveSceneChanged;
                sceneEventSubscribed = true;
            }

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
                if (layerRoot != null)
                {
                    DontDestroyOnLoad(
                        layerRoot.transform.root.gameObject);
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// 注册当前实例。
        /// </summary>
        public void Register(
            UIPanelConfig config,
            Func<UIPanelLogic> logicFactory = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Validate();
            if (panelRecords.TryGetValue(
                    config.Id,
                    out UIPanelRecord existingRecord) &&
                IsRecordAlive(existingRecord))
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.DuplicatePanelId,
                    $"面板 {config.Id} 已有活动实例，不能替换配置。",
                    config.Id);
            }

            configs[config.Id] = config;
            logicFactories[config.Id] =
                logicFactory ?? CreateConfiguredLogicFactory(config);
        }

        public void Register<TLogic>(UIPanelConfig config)
            where TLogic : UIPanelLogic, new()
        {
            Register(config, () => new TLogic());
        }

        /// <summary>
        /// 注册本地预制体。
        /// </summary>
        public void RegisterLocalPrefab(
            GameObject prefab,
            UIPanelConfig config = null,
            Func<UIPanelLogic> logicFactory = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            UICodeBinder binder =
                prefab.GetComponent<UICodeBinder>();
            ObjectBinder objectBinder =
                prefab.GetComponent<ObjectBinder>();
            if (config == null)
            {
                string panelId =
                    binder != null &&
                    !string.IsNullOrWhiteSpace(binder.PanelId)
                        ? binder.PanelId
                        : objectBinder != null &&
                          !string.IsNullOrWhiteSpace(
                              objectBinder.uiScriptGeneration?.panelId)
                            ? objectBinder.uiScriptGeneration.panelId
                        : prefab.name;
                string logicTypeName =
                    binder != null &&
                    !string.IsNullOrWhiteSpace(
                        binder.CodeNamespace) &&
                    !string.IsNullOrWhiteSpace(
                        binder.LogicClassName)
                        ? $"{binder.CodeNamespace}." +
                          $"{binder.LogicClassName}, " +
                          "Game.Logic"
                        : objectBinder != null &&
                          objectBinder.uiScriptGeneration?.scriptType ==
                          UIObjectBinderScriptType.CSharp &&
                          !string.IsNullOrWhiteSpace(
                              objectBinder.uiScriptGeneration
                                  .csharpNamespace) &&
                          !string.IsNullOrWhiteSpace(
                              objectBinder.uiScriptGeneration
                                  .csharpClassName)
                            ? objectBinder.uiScriptGeneration
                                  .csharpNamespace +
                              "." +
                              objectBinder.uiScriptGeneration
                                  .csharpClassName +
                              ", Game.Logic"
                            : string.Empty;
                UILayer configuredLayer =
                    binder != null
                        ? binder.Layer
                        : objectBinder?.uiScriptGeneration != null
                            ? (UILayer)(int)objectBinder
                                .uiScriptGeneration.uiLayer
                            : UILayer.Auto;

                config = new UIPanelConfig
                {
                    Id = panelId,
                    PackageName = "@local",
                    Location = prefab.name,
                    LogicTypeName = logicTypeName,
                    Layer = configuredLayer
                };
            }

            Register(config, logicFactory);
            localPrefabs[config.Id] = prefab;
        }

        /// <summary>
        /// 注销当前实例。
        /// </summary>
        public bool Unregister(string panelId)
        {
            if (panelRecords.TryGetValue(
                    panelId,
                    out UIPanelRecord record) &&
                IsRecordAlive(record))
            {
                return false;
            }

            logicFactories.Remove(panelId);
            localPrefabs.Remove(panelId);
            return configs.Remove(panelId);
        }

        /// <summary>
        /// 异步打开面板。
        /// </summary>
        public UIAsyncOperation<UIPanelLogic> OpenPanelAsync(
            string panelId,
            object userData = null)
        {
            EnsureInitialized();
            var operation =
                new UIAsyncOperation<UIPanelLogic>();

            if (!TryGetConfig(
                    panelId,
                    out UIPanelConfig config,
                    out Exception error))
            {
                operation.Fail(error);
                return operation;
            }

            OpenPanelInternal(
                config,
                userData,
                true,
                operation);
            return operation;
        }

        public UIAsyncOperation<TLogic> OpenPanelAsync<TLogic>(
            string panelId,
            object userData = null)
            where TLogic : UIPanelLogic
        {
            var typedOperation =
                new UIAsyncOperation<TLogic>();
            UIAsyncOperation<UIPanelLogic> operation =
                OpenPanelAsync(panelId, userData);
            operation.Completed += completed =>
            {
                if (!completed.IsSucceeded)
                {
                    typedOperation.Fail(completed.Exception);
                    return;
                }

                if (completed.Result is TLogic typedLogic)
                {
                    typedOperation.Succeed(typedLogic);
                }
                else
                {
                    typedOperation.Fail(new UIFrameworkException(
                        UIFrameworkErrorCode.LogicCreationFailed,
                        $"面板 {panelId} 的逻辑不是 " +
                        typeof(TLogic).Name,
                        panelId));
                }
            };
            return typedOperation;
        }

        /// <summary>
        /// 打开面板。
        /// </summary>
        public void OpenPanel(
            string panelId,
            object userData,
            Action<UIPanelLogic, Exception> completed)
        {
            UIAsyncOperation<UIPanelLogic> operation =
                OpenPanelAsync(panelId, userData);
            operation.Completed += result =>
            {
                completed?.Invoke(
                    result.IsSucceeded ? result.Result : null,
                    result.Exception);
            };
        }

        /// <summary>
        /// 执行预加载面板异步相关逻辑。
        /// </summary>
        public UIAsyncOperation<UIPanelLogic> PreloadPanelAsync(
            string panelId)
        {
            EnsureInitialized();
            var operation =
                new UIAsyncOperation<UIPanelLogic>();

            if (!TryGetConfig(
                    panelId,
                    out UIPanelConfig config,
                    out Exception error))
            {
                operation.Fail(error);
                return operation;
            }

            OpenPanelInternal(
                config,
                null,
                false,
                operation);
            return operation;
        }

        /// <summary>
        /// 执行预加载Panels异步相关逻辑。
        /// </summary>
        public UIAsyncOperation<bool> PreloadPanelsAsync(
            IEnumerable<string> panelIds,
            float timeoutPerPanelSeconds = 30f)
        {
            EnsureInitialized();
            var operation = new UIAsyncOperation<bool>();

            if (panelIds == null)
            {
                operation.Fail(new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    "预加载面板列表不能为空。"));
                return operation;
            }

            StartCoroutine(PreloadPanelsRoutine(
                new List<string>(panelIds),
                Mathf.Max(0f, timeoutPerPanelSeconds),
                operation));
            return operation;
        }

        /// <summary>
        /// 关闭面板。
        /// </summary>
        public bool ClosePanel(
            string panelId,
            bool forceDestroy = false,
            bool skipAnimation = false)
        {
            if (string.IsNullOrWhiteSpace(panelId) ||
                !panelRecords.TryGetValue(
                    panelId,
                    out UIPanelRecord record) ||
                !IsRecordAlive(record))
            {
                return false;
            }

            InternalClosePanel(
                record,
                forceDestroy,
                false,
                skipAnimation);
            return true;
        }

        /// <summary>
        /// 关闭Top面板。
        /// </summary>
        public bool CloseTopPanel(
            bool forceDestroy = false,
            bool skipAnimation = false)
        {
            UIStackEntry top =
                stackCoordinator.GetTop(stackRecords);
            return top != null &&
                   ClosePanel(
                       top.Config.Id,
                       forceDestroy,
                       skipAnimation);
        }

        /// <summary>
        /// 关闭全部面板。
        /// </summary>
        public void CloseAllPanels(
            bool forceDestroy = true,
            bool includePersistentPanels = true)
        {
            var targets = new List<UIPanelRecord>();
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (IsRecordAlive(record) &&
                    (includePersistentPanels ||
                     record.Config.AutoDestroyWhenSceneChanged))
                {
                    targets.Add(record);
                }
            }

            foreach (UIPanelRecord record in targets)
            {
                stackCoordinator.Remove(
                    stackRecords,
                    record.Config.Id);
                InternalClosePanel(
                    record,
                    forceDestroy,
                    true,
                    true);
            }
        }

        /// <summary>
        /// 执行判断是否面板打开相关逻辑。
        /// </summary>
        public bool IsPanelOpen(string panelId)
        {
            return panelRecords.TryGetValue(
                       panelId,
                       out UIPanelRecord record) &&
                   IsRecordAlive(record) &&
                   record.Logic.IsVisible;
        }

        /// <summary>
        /// 执行判断是否面板Loaded相关逻辑。
        /// </summary>
        public bool IsPanelLoaded(string panelId)
        {
            return panelRecords.TryGetValue(
                       panelId,
                       out UIPanelRecord record) &&
                   IsRecordAlive(record) &&
                   record.Logic.IsLoaded;
        }

        /// <summary>
        /// 执行判断是否Top面板相关逻辑。
        /// </summary>
        public bool IsTopPanel(string panelId)
        {
            return string.Equals(
                TopPanelId,
                panelId,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取面板Logic。
        /// </summary>
        public UIPanelLogic GetPanelLogic(string panelId)
        {
            return panelRecords.TryGetValue(
                       panelId,
                       out UIPanelRecord record) &&
                   IsRecordAlive(record)
                ? record.Logic
                : null;
        }

        public TLogic GetPanelLogic<TLogic>(string panelId)
            where TLogic : UIPanelLogic
        {
            return GetPanelLogic(panelId) as TLogic;
        }

        /// <summary>
        /// 获取面板状态。
        /// </summary>
        public UIPanelState GetPanelState(string panelId)
        {
            return panelRecords.TryGetValue(
                panelId,
                out UIPanelRecord record)
                ? record.State
                : UIPanelState.None;
        }

        public IReadOnlyList<UIStackSnapshot>
            CaptureNavigationStack()
        {
            var result =
                new List<UIStackSnapshot>(stackRecords.Count);
            foreach (UIStackEntry entry in stackRecords)
            {
                result.Add(new UIStackSnapshot(
                    entry.Config.Id,
                    entry.UserData));
            }

            return result;
        }

        /// <summary>
        /// 保存Navigation栈。
        /// </summary>
        public void SaveNavigationStack(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "导航栈 key 不能为空。",
                    nameof(key));
            }

            savedStacks[key] =
                new List<UIStackSnapshot>(
                    CaptureNavigationStack());
        }

        public UIAsyncOperation<UIPanelLogic>
            RestoreNavigationStackAsync(string key)
        {
            var operation =
                new UIAsyncOperation<UIPanelLogic>();
            if (string.IsNullOrWhiteSpace(key) ||
                !savedStacks.TryGetValue(
                    key,
                    out List<UIStackSnapshot> saved) ||
                saved.Count == 0)
            {
                operation.Fail(new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    $"不存在已保存的 UI 导航栈：{key}"));
                return operation;
            }

            stackRecords.Clear();
            foreach (UIStackSnapshot snapshot in saved)
            {
                if (configs.TryGetValue(
                        snapshot.PanelId,
                        out UIPanelConfig config) &&
                    !config.IgnoreStack)
                {
                    stackRecords.Add(new UIStackEntry
                    {
                        Config = config,
                        UserData = snapshot.UserData
                    });
                }
            }

            UIStackEntry top =
                stackCoordinator.GetTop(stackRecords);
            if (top == null)
            {
                operation.Fail(new UIFrameworkException(
                    UIFrameworkErrorCode.PanelNotRegistered,
                    $"导航栈 {key} 中没有可恢复的面板。"));
                return operation;
            }

            UIAsyncOperation<UIPanelLogic> open =
                OpenPanelAsync(top.Config.Id, top.UserData);
            open.Completed += result =>
            {
                if (result.IsSucceeded)
                {
                    operation.Succeed(result.Result);
                }
                else
                {
                    operation.Fail(result.Exception);
                }
            };
            return operation;
        }

        /// <summary>
        /// 清空SavedNavigation栈。
        /// </summary>
        public void ClearSavedNavigationStack(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                savedStacks.Remove(key);
            }
        }

        public IReadOnlyList<UIPanelRuntimeSnapshot>
            GetRuntimeSnapshots()
        {
            var result =
                new List<UIPanelRuntimeSnapshot>(
                    panelRecords.Count);
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                result.Add(new UIPanelRuntimeSnapshot(
                    record.Config.Id,
                    record.State,
                    record.Logic != null &&
                    record.Logic.IsLoaded,
                    record.Logic != null &&
                    record.Logic.IsVisible,
                    stackCoordinator.Contains(
                        stackRecords,
                        record.Config.Id),
                    record.PendingCloseImmediate
                        ? "<Immediate>"
                        : record.PendingCloseTarget,
                    record.RequestId));
            }

            result.Sort(
                (left, right) => string.Compare(
                    left.PanelId,
                    right.PanelId,
                    StringComparison.Ordinal));
            return result;
        }

        /// <summary>
        /// 执行关闭相关逻辑。
        /// </summary>
        public void Shutdown()
        {
            ShutdownInternal();
        }

        /// <summary>
        /// 打开面板Internal。
        /// </summary>
        private void OpenPanelInternal(
            UIPanelConfig config,
            object userData,
            bool wantsVisible,
            UIAsyncOperation<UIPanelLogic> operation)
        {
            UIPanelRecord record = GetOrCreateRecord(config);
            record.UserData = userData;
            record.Waiters.Add(operation);

            if (record.IsLoading)
            {
                if (wantsVisible)
                {
                    record.ShowWhenLoaded = true;
                    ClearPendingClose(record);
                    if (record.State == UIPanelState.Closing)
                    {
                        Transition(
                            record,
                            UIPanelState.Loading,
                            "reopen while loading");
                    }

                    HandlePanelPush(record, userData);
                }

                return;
            }

            if (IsRecordAlive(record) &&
                record.Logic.IsLoaded)
            {
                if (!wantsVisible)
                {
                    operation.Succeed(record.Logic);
                    record.Waiters.Remove(operation);
                    return;
                }

                ClearPendingClose(record);
                HandlePanelPush(record, userData);
                try
                {
                    ShowPanel(record, userData);
                }
                catch (Exception exception)
                {
                    HandlePanelFailure(
                        record,
                        new UIFrameworkException(
                            UIFrameworkErrorCode.ShowFailed,
                            $"显示面板 {config.Id} 失败。",
                            config.Id,
                            exception));
                }

                return;
            }

            CreateLogicAndBeginLoad(
                record,
                userData,
                wantsVisible);
        }

        /// <summary>
        /// 创建LogicAnd开始加载。
        /// </summary>
        private void CreateLogicAndBeginLoad(
            UIPanelRecord record,
            object userData,
            bool wantsVisible)
        {
            ClearRecordForRecreate(record);
            Transition(
                record,
                UIPanelState.Creating,
                "create logic");

            try
            {
                if (!logicFactories.TryGetValue(
                        record.Config.Id,
                        out Func<UIPanelLogic> factory))
                {
                    factory = () => new UIDefaultPanelLogic();
                }

                record.Logic = factory();
                if (record.Logic == null)
                {
                    throw new InvalidOperationException(
                        "LogicFactory 返回 null。");
                }

                record.Logic.Initialize(
                    this,
                    record.Config,
                    userData);
            }
            catch (Exception exception)
            {
                HandlePanelFailure(
                    record,
                    new UIFrameworkException(
                        UIFrameworkErrorCode.LogicCreationFailed,
                        $"创建面板逻辑失败：{record.Config.Id}",
                        record.Config.Id,
                        exception));
                return;
            }

            record.ShowWhenLoaded = wantsVisible;
            record.UserData = userData;
            if (wantsVisible)
            {
                HandlePanelPush(record, userData);
            }

            BeginPanelLoad(record);
        }

        /// <summary>
        /// 执行Begin面板加载相关逻辑。
        /// </summary>
        private void BeginPanelLoad(UIPanelRecord record)
        {
            record.RequestId = ++nextRequestId;
            long requestId = record.RequestId;
            Transition(record, UIPanelState.Loading, "begin load");

            RectTransform parent =
                EnsureLayerRoot().GetLayer(
                    record.Config.EffectiveLayer);
            record.ResolvedLayer =
                record.Config.EffectiveLayer;
            record.IsLoading = true;

            try
            {
                if (localPrefabs.TryGetValue(
                        record.Config.Id,
                        out GameObject localPrefab) &&
                    localPrefab != null)
                {
                    record.Instance =
                        UnityEngine.Object.Instantiate(
                            localPrefab,
                            parent,
                            false);
                }
                else
                {
                    GameResourceManager resourceManager =
                        GameResourceManager.GetOrCreate(gameObject);
                    record.InstanceHandle =
                        resourceManager.InstantiateAsync(
                            record.Config.Location,
                            parent,
                            false,
                            true,
                            record.Config.PackageName);
                }

                StartCoroutine(
                    LoadPanelRoutine(record, requestId));
            }
            catch (Exception exception)
            {
                HandlePanelFailure(
                    record,
                    new UIFrameworkException(
                        UIFrameworkErrorCode.AssetLoadFailed,
                        $"启动 YooAsset UI 加载失败：" +
                        record.Config.Id,
                        record.Config.Id,
                        exception));
            }
        }

        /// <summary>
        /// 加载面板Routine。
        /// </summary>
        private IEnumerator LoadPanelRoutine(
            UIPanelRecord record,
            long requestId)
        {
            GameResourceInstanceHandle instanceHandle =
                record.InstanceHandle;
            GameObject localInstance = record.Instance;

            while (instanceHandle != null &&
                   !instanceHandle.IsDone)
            {
                SetWaiterProgress(
                    record,
                    0.1f + instanceHandle.Progress * 0.75f);
                yield return null;
            }

            if (record.RequestId != requestId)
            {
                ReleaseDetachedLoad(
                    instanceHandle,
                    localInstance);
                yield break;
            }

            if (instanceHandle != null &&
                instanceHandle.Status !=
                EOperationStatus.Succeeded)
            {
                HandlePanelFailure(
                    record,
                    new UIFrameworkException(
                        UIFrameworkErrorCode.AssetLoadFailed,
                        $"加载 UI Prefab 失败：{record.Config.Id}",
                        record.Config.Id,
                        new InvalidOperationException(
                            instanceHandle.Error)));
                yield break;
            }

            GameObject panelObject =
                instanceHandle != null
                    ? instanceHandle.Result
                    : localInstance;
            if (panelObject == null)
            {
                HandlePanelFailure(
                    record,
                    new UIFrameworkException(
                        UIFrameworkErrorCode.AssetLoadFailed,
                        $"YooAsset 实例化 UI Prefab 返回 null：" +
                        record.Config.Id,
                        record.Config.Id));
                yield break;
            }

            record.Instance = panelObject;
            record.IsLoading = false;

            try
            {
                panelObject.name = record.Config.Id;
                record.ResolvedLayer =
                    ResolvePanelLayer(record, panelObject);
                RectTransform panelParent =
                    EnsureLayerRoot().GetLayer(
                        record.ResolvedLayer);
                panelObject.transform.SetParent(panelParent, false);
                UILayerRoot.SetLayerRecursively(
                    panelObject,
                    panelParent.gameObject.layer);
                panelObject.transform.localScale = Vector3.one;
                panelObject.transform.localRotation =
                    Quaternion.identity;
                record.Logic.BindGameObject(panelObject);
            }
            catch (Exception exception)
            {
                HandlePanelFailure(
                    record,
                    new UIFrameworkException(
                        UIFrameworkErrorCode.BindFailed,
                        $"绑定 UI Prefab 失败：{record.Config.Id}",
                        record.Config.Id,
                        exception));
                yield break;
            }

            bool hasPendingClose =
                record.PendingCloseImmediate ||
                !string.IsNullOrEmpty(
                    record.PendingCloseTarget);
            bool canShow =
                record.ShowWhenLoaded &&
                !hasPendingClose &&
                (record.Config.IgnoreStack ||
                 IsTopPanel(record.Config.Id));

            if (canShow)
            {
                try
                {
                    ShowPanel(record, record.UserData);
                }
                catch (Exception exception)
                {
                    HandlePanelFailure(
                        record,
                        new UIFrameworkException(
                            UIFrameworkErrorCode.ShowFailed,
                            $"显示面板失败：{record.Config.Id}",
                            record.Config.Id,
                            exception));
                }

                yield break;
            }

            if (!record.ShowWhenLoaded && !hasPendingClose)
            {
                HideLoadedPreload(record);
                CompleteWaiters(record, true, null);
                yield break;
            }

            CompleteWaiters(
                record,
                false,
                CreateSupersededException(record.Config.Id));
            ApplyClosePolicy(
                record,
                record.CloseForceDestroy,
                record.CloseSkipAnimation);
        }

        /// <summary>
        /// 执行预加载PanelsRoutine相关逻辑。
        /// </summary>
        private IEnumerator PreloadPanelsRoutine(
            List<string> panelIds,
            float timeoutPerPanelSeconds,
            UIAsyncOperation<bool> operation)
        {
            for (int index = 0; index < panelIds.Count; index++)
            {
                if (operation.IsCancellationRequested)
                {
                    yield break;
                }

                UIAsyncOperation<UIPanelLogic> preload =
                    PreloadPanelAsync(panelIds[index]);
                float startTime = Time.realtimeSinceStartup;

                while (!preload.IsDone)
                {
                    if (timeoutPerPanelSeconds > 0f &&
                        Time.realtimeSinceStartup - startTime >=
                        timeoutPerPanelSeconds)
                    {
                        preload.Cancel();
                        operation.Fail(new UIFrameworkException(
                            UIFrameworkErrorCode.PreloadTimeout,
                            $"预加载面板超时：{panelIds[index]}",
                            panelIds[index]));
                        yield break;
                    }

                    operation.SetProgress(
                        (index + preload.Progress) /
                        Mathf.Max(1f, panelIds.Count));
                    yield return null;
                }

                if (!preload.IsSucceeded)
                {
                    operation.Fail(preload.Exception);
                    yield break;
                }
            }

            operation.Succeed(true);
        }

        /// <summary>
        /// 处理面板Push。
        /// </summary>
        private void HandlePanelPush(
            UIPanelRecord record,
            object userData)
        {
            UIStackPushResult pushResult =
                stackCoordinator.Push(
                    stackRecords,
                    record.Config,
                    userData);
            LogVerbose(
                $"Push {record.Config.Id}, top={TopPanelId}");

            if (pushResult.PreviousTop == null)
            {
                return;
            }

            UIPanelRecord previousRecord =
                GetOrCreateRecord(pushResult.PreviousTop);
            SetPendingClose(
                previousRecord,
                record.Config.Id,
                false,
                false);
        }

        /// <summary>
        /// 执行内部关闭面板相关逻辑。
        /// </summary>
        private void InternalClosePanel(
            UIPanelRecord record,
            bool forceDestroy,
            bool isClear,
            bool skipAnimation)
        {
            UIStackPopResult popResult =
                stackCoordinator.Pop(
                    stackRecords,
                    record.Config,
                    isClear);

            if (isClear)
            {
                stackCoordinator.Remove(
                    stackRecords,
                    record.Config.Id);
            }

            if (popResult.WasTop &&
                popResult.NewTop != null)
            {
                SetPendingClose(
                    record,
                    popResult.NewTop.Config.Id,
                    forceDestroy,
                    skipAnimation);
                OpenPanelAsync(
                    popResult.NewTop.Config.Id,
                    popResult.NewTop.UserData);
                return;
            }

            if (record.IsLoading)
            {
                if (forceDestroy)
                {
                    DestroyPanel(record);
                }
                else
                {
                    SetPendingClose(
                        record,
                        null,
                        false,
                        skipAnimation);
                }

                return;
            }

            ApplyClosePolicy(
                record,
                forceDestroy,
                skipAnimation);
        }

        /// <summary>
        /// 设置Pending关闭。
        /// </summary>
        private void SetPendingClose(
            UIPanelRecord record,
            string targetPanelId,
            bool forceDestroy,
            bool skipAnimation)
        {
            if (!string.IsNullOrEmpty(targetPanelId) &&
                string.Equals(
                    targetPanelId,
                    record.Config.Id,
                    StringComparison.Ordinal))
            {
                ClearPendingClose(record);
                return;
            }

            if (!string.IsNullOrEmpty(targetPanelId))
            {
                foreach (UIPanelRecord other in panelRecords.Values)
                {
                    if (other == record ||
                        !string.Equals(
                            other.PendingCloseTarget,
                            record.Config.Id,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(
                            other.Config.Id,
                            targetPanelId,
                            StringComparison.Ordinal))
                    {
                        ClearPendingClose(other);
                    }
                    else
                    {
                        other.PendingCloseTarget = targetPanelId;
                    }
                }
            }

            record.PendingCloseImmediate =
                string.IsNullOrEmpty(targetPanelId);
            record.PendingCloseTarget = targetPanelId;
            record.CloseForceDestroy = forceDestroy;
            record.CloseSkipAnimation = skipAnimation;
            record.ShowWhenLoaded = false;

            if (record.State == UIPanelState.Loading ||
                record.State == UIPanelState.Visible ||
                record.State == UIPanelState.Hidden)
            {
                Transition(
                    record,
                    UIPanelState.Closing,
                    "pending close");
            }

            if (record.IsLoading)
            {
                CompleteWaiters(
                    record,
                    false,
                    CreateSupersededException(
                        record.Config.Id));
            }
        }

        /// <summary>
        /// 清空Pending关闭。
        /// </summary>
        private void ClearPendingClose(UIPanelRecord record)
        {
            record.PendingCloseTarget = null;
            record.PendingCloseImmediate = false;
            record.CloseForceDestroy = false;
            record.CloseSkipAnimation = false;
        }

        /// <summary>
        /// 执行流程PanelsWaitingFor相关逻辑。
        /// </summary>
        private void ProcessPanelsWaitingFor(string targetPanelId)
        {
            var waitingRecords = new List<UIPanelRecord>();
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (!string.Equals(
                        record.PendingCloseTarget,
                        targetPanelId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        record.Config.Id,
                        targetPanelId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                waitingRecords.Add(record);
            }

            foreach (UIPanelRecord waiting in waitingRecords)
            {
                bool forceDestroy = waiting.CloseForceDestroy;
                bool skipAnimation = waiting.CloseSkipAnimation;
                ClearPendingClose(waiting);
                ApplyClosePolicy(
                    waiting,
                    forceDestroy,
                    skipAnimation);
            }
        }

        /// <summary>
        /// 执行显示面板相关逻辑。
        /// </summary>
        private void ShowPanel(
            UIPanelRecord record,
            object userData)
        {
            if (record.Logic == null ||
                !record.Logic.IsLoaded)
            {
                throw new InvalidOperationException(
                    $"面板 {record.Config.Id} 尚未加载。");
            }

            ClearPendingClose(record);
            record.ShowWhenLoaded = true;
            GameObject panelObject = record.Logic.GameObject;
            panelObject.SetActive(true);

            CreateBackdropIfNeeded(record);
            panelObject.transform.SetAsLastSibling();
            record.Logic.Show(userData);
            Transition(record, UIPanelState.Visible, "show");
            CompleteWaiters(record, true, null);

            if (!record.Config.IgnoreStack &&
                record.Config.ClosePopupsWhenShown)
            {
                CloseVisiblePopupsExceptDebug();
            }

            ProcessPanelsWaitingFor(record.Config.Id);
        }

        /// <summary>
        /// 执行隐藏Loaded预加载相关逻辑。
        /// </summary>
        private void HideLoadedPreload(UIPanelRecord record)
        {
            ReleaseBackdrop(record);
            if (record.Logic?.GameObject != null)
            {
                record.Logic.GameObject.SetActive(false);
            }

            Transition(record, UIPanelState.Hidden, "preloaded");
        }

        /// <summary>
        /// 执行隐藏面板相关逻辑。
        /// </summary>
        private void HidePanel(
            UIPanelRecord record,
            bool skipAnimation)
        {
            if (record.Logic == null)
            {
                return;
            }

            ReleaseBackdrop(record);
            try
            {
                record.Logic.Hide(skipAnimation);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (record.Logic.GameObject != null)
            {
                record.Logic.GameObject.SetActive(false);
            }

            ClearPendingClose(record);
            Transition(record, UIPanelState.Hidden, "hide");
        }

        /// <summary>
        /// 执行销毁面板相关逻辑。
        /// </summary>
        private void DestroyPanel(UIPanelRecord record)
        {
            record.RequestId = ++nextRequestId;
            ReleaseBackdrop(record);

            if (record.Logic != null)
            {
                try
                {
                    record.Logic.Hide(true);
                    record.Logic.DisposeLogic();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            ReleasePanelResources(record);
            record.Logic = null;
            record.ShowWhenLoaded = false;
            record.ResolvedLayer =
                record.Config.EffectiveLayer;
            ClearPendingClose(record);
            CompleteWaiters(
                record,
                false,
                CreateSupersededException(
                    record.Config.Id));
            Transition(
                record,
                UIPanelState.Destroyed,
                "destroy",
                true);
        }

        /// <summary>
        /// 应用关闭Policy。
        /// </summary>
        private void ApplyClosePolicy(
            UIPanelRecord record,
            bool forceDestroy,
            bool skipAnimation)
        {
            if (!IsRecordAlive(record))
            {
                return;
            }

            if (forceDestroy ||
                record.Config.CloseType ==
                UIPanelCloseType.Destroy)
            {
                DestroyPanel(record);
            }
            else
            {
                HidePanel(record, skipAnimation);
            }
        }

        /// <summary>
        /// 处理面板Failure。
        /// </summary>
        private void HandlePanelFailure(
            UIPanelRecord record,
            Exception error)
        {
            string failedPanelId = record.Config.Id;
            UIStackPopResult popResult =
                stackCoordinator.Pop(
                    stackRecords,
                    record.Config,
                    false);

            record.RequestId = ++nextRequestId;
            ReleaseBackdrop(record);
            record.Logic?.DisposeLogic();
            ReleasePanelResources(record);
            record.Logic = null;
            record.ShowWhenLoaded = false;
            record.ResolvedLayer =
                record.Config.EffectiveLayer;
            ClearPendingClose(record);
            Transition(
                record,
                UIPanelState.Failed,
                "load failed",
                true);
            CompleteWaiters(record, false, error);

            try
            {
                PanelLoadFailed?.Invoke(failedPanelId, error);
            }
            catch (Exception callbackException)
            {
                Debug.LogException(callbackException);
            }

            Debug.LogError(
                $"[UIManager] {error.Message}\n{error}");

            ResolvePendingAfterFailure(
                failedPanelId,
                popResult.WasTop
                    ? popResult.NewTop
                    : null);
        }

        /// <summary>
        /// 解析PendingAfterFailure。
        /// </summary>
        private void ResolvePendingAfterFailure(
            string failedPanelId,
            UIStackEntry newTop)
        {
            var affected = new List<UIPanelRecord>();
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (string.Equals(
                        record.PendingCloseTarget,
                        failedPanelId,
                        StringComparison.Ordinal))
                {
                    affected.Add(record);
                }
            }

            if (newTop == null)
            {
                foreach (UIPanelRecord record in affected)
                {
                    bool force = record.CloseForceDestroy;
                    bool skip = record.CloseSkipAnimation;
                    ClearPendingClose(record);
                    ApplyClosePolicy(record, force, skip);
                }

                return;
            }

            foreach (UIPanelRecord record in affected)
            {
                if (string.Equals(
                        record.Config.Id,
                        newTop.Config.Id,
                        StringComparison.Ordinal))
                {
                    ClearPendingClose(record);
                }
                else
                {
                    record.PendingCloseTarget =
                        newTop.Config.Id;
                }
            }

            OpenPanelAsync(
                newTop.Config.Id,
                newTop.UserData);
        }

        /// <summary>
        /// 解析面板层级。
        /// </summary>
        private static UILayer ResolvePanelLayer(
            UIPanelRecord record,
            GameObject panelObject)
        {
            UICodeBinder binder =
                panelObject == null
                    ? null
                    : panelObject.GetComponent<UICodeBinder>();
            if (binder != null &&
                binder.Layer != UILayer.Auto)
            {
                return binder.Layer;
            }

            ObjectBinder objectBinder =
                panelObject == null
                    ? null
                    : panelObject.GetComponent<ObjectBinder>();
            UILayer objectBinderLayer =
                objectBinder?.uiScriptGeneration == null
                    ? UILayer.Auto
                    : (UILayer)(int)objectBinder
                        .uiScriptGeneration.uiLayer;
            return objectBinderLayer != UILayer.Auto
                ? objectBinderLayer
                : record.Config.EffectiveLayer;
        }

        /// <summary>
        /// 获取Record层级。
        /// </summary>
        private static UILayer GetRecordLayer(
            UIPanelRecord record)
        {
            return record.ResolvedLayer == UILayer.Auto
                ? record.Config.EffectiveLayer
                : record.ResolvedLayer;
        }

        /// <summary>
        /// 关闭可见状态PopupsExcept调试。
        /// </summary>
        private void CloseVisiblePopupsExceptDebug()
        {
            var popupIds = new List<string>();
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (record.Config.IgnoreStack &&
                    GetRecordLayer(record) != UILayer.Debug &&
                    record.Logic != null &&
                    record.Logic.IsVisible)
                {
                    popupIds.Add(record.Config.Id);
                }
            }

            foreach (string popupId in popupIds)
            {
                ClosePanel(popupId);
            }
        }

        /// <summary>
        /// 创建BackdropIfNeeded。
        /// </summary>
        private void CreateBackdropIfNeeded(UIPanelRecord record)
        {
            ReleaseBackdrop(record);
            if (!record.Config.BlurMode)
            {
                return;
            }

            RectTransform parent = EnsureLayerRoot().GetLayer(
                GetRecordLayer(record));
            string panelId = record.Config.Id;
            record.Backdrop = backdropService.CreateBackdrop(
                record.Config,
                parent,
                () => ClosePanel(panelId));
        }

        /// <summary>
        /// 释放Backdrop。
        /// </summary>
        private void ReleaseBackdrop(UIPanelRecord record)
        {
            if (record.Backdrop != null)
            {
                backdropService.ReleaseBackdrop(record.Backdrop);
                record.Backdrop = null;
            }
        }

        /// <summary>
        /// 执行CompleteWaiters相关逻辑。
        /// </summary>
        private void CompleteWaiters(
            UIPanelRecord record,
            bool success,
            Exception error)
        {
            if (record.Waiters.Count == 0)
            {
                return;
            }

            UIAsyncOperation<UIPanelLogic>[] waiters =
                record.Waiters.ToArray();
            record.Waiters.Clear();
            foreach (UIAsyncOperation<UIPanelLogic> waiter in waiters)
            {
                if (waiter.IsDone)
                {
                    continue;
                }

                if (success)
                {
                    waiter.Succeed(record.Logic);
                }
                else
                {
                    waiter.Fail(error);
                }
            }
        }

        /// <summary>
        /// 设置Waiter进度。
        /// </summary>
        private static void SetWaiterProgress(
            UIPanelRecord record,
            float progress)
        {
            for (int index = record.Waiters.Count - 1;
                 index >= 0;
                 index--)
            {
                UIAsyncOperation<UIPanelLogic> waiter =
                    record.Waiters[index];
                if (waiter.IsDone)
                {
                    record.Waiters.RemoveAt(index);
                }
                else
                {
                    waiter.SetProgress(progress);
                }
            }
        }

        /// <summary>
        /// 获取Or创建Record。
        /// </summary>
        private UIPanelRecord GetOrCreateRecord(
            UIPanelConfig config)
        {
            if (!panelRecords.TryGetValue(
                    config.Id,
                    out UIPanelRecord record))
            {
                record = new UIPanelRecord
                {
                    Config = config,
                    State = UIPanelState.None,
                    ResolvedLayer = config.EffectiveLayer
                };
                panelRecords.Add(config.Id, record);
            }
            else
            {
                record.Config = config;
                if (!IsRecordAlive(record))
                {
                    record.ResolvedLayer =
                        config.EffectiveLayer;
                }
            }

            return record;
        }

        private static Func<UIPanelLogic>
            CreateConfiguredLogicFactory(UIPanelConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.LogicTypeName))
            {
                return () => new UIDefaultPanelLogic();
            }

            return () =>
            {
                string lookupName =
                    config.LogicTypeName.Split(',')[0].Trim();
                Type logicType = Type.GetType(
                    config.LogicTypeName,
                    false);
                if (logicType == null)
                {
                    foreach (System.Reflection.Assembly assembly in
                             AppDomain.CurrentDomain.GetAssemblies())
                    {
                        logicType = assembly.GetType(
                            lookupName,
                            false);
                        if (logicType != null)
                        {
                            break;
                        }
                    }
                }

                if (logicType == null ||
                    !typeof(UIPanelLogic).IsAssignableFrom(logicType) ||
                    logicType.IsAbstract)
                {
                    throw new UIFrameworkException(
                        UIFrameworkErrorCode.LogicCreationFailed,
                        $"无法解析面板 {config.Id} 的 Logic 类型：" +
                        config.LogicTypeName,
                        config.Id);
                }

                return (UIPanelLogic)Activator.CreateInstance(
                    logicType);
            };
        }

        /// <summary>
        /// 释放面板资源。
        /// </summary>
        private static void ReleasePanelResources(
            UIPanelRecord record)
        {
            if (record.InstanceHandle != null)
            {
                record.InstanceHandle.Release();
            }
            else if (record.Instance != null)
            {
                UnityEngine.Object.Destroy(record.Instance);
            }

            record.InstanceHandle = null;
            record.Instance = null;
            record.IsLoading = false;
        }

        /// <summary>
        /// 释放Detached加载。
        /// </summary>
        private static void ReleaseDetachedLoad(
            GameResourceInstanceHandle instanceHandle,
            GameObject localInstance)
        {
            if (instanceHandle != null)
            {
                instanceHandle.Release();
            }
            else if (localInstance != null)
            {
                UnityEngine.Object.Destroy(localInstance);
            }
        }

        /// <summary>
        /// 清空RecordForRecreate。
        /// </summary>
        private void ClearRecordForRecreate(UIPanelRecord record)
        {
            record.RequestId = ++nextRequestId;
            record.Logic?.DisposeLogic();
            ReleasePanelResources(record);
            record.Logic = null;
            record.ResolvedLayer =
                record.Config.EffectiveLayer;
            ReleaseBackdrop(record);
            ClearPendingClose(record);
        }

        /// <summary>
        /// 尝试获取配置，并返回是否成功。
        /// </summary>
        private bool TryGetConfig(
            string panelId,
            out UIPanelConfig config,
            out Exception error)
        {
            config = null;
            error = null;

            if (isShuttingDown || applicationIsQuitting)
            {
                error = new UIFrameworkException(
                    UIFrameworkErrorCode.ManagerShuttingDown,
                    "UIManager 正在关闭。",
                    panelId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(panelId))
            {
                error = new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    "面板 ID 不能为空。");
                return false;
            }

            if (!configs.TryGetValue(panelId, out config))
            {
                error = new UIFrameworkException(
                    UIFrameworkErrorCode.PanelNotRegistered,
                    $"面板未注册：{panelId}",
                    panelId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 执行切换相关逻辑。
        /// </summary>
        private void Transition(
            UIPanelRecord record,
            UIPanelState nextState,
            string reason,
            bool force = false)
        {
            UIPanelState previous = record.State;
            if (previous == nextState)
            {
                return;
            }

            if (!force && !IsTransitionAllowed(previous, nextState))
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidStateTransition,
                    $"非法 UI 状态迁移：{record.Config.Id} " +
                    $"{previous} -> {nextState} ({reason})",
                    record.Config.Id);
            }

            record.State = nextState;
            LogVerbose(
                $"{record.Config.Id}: {previous} -> " +
                $"{nextState} ({reason})");

            try
            {
                PanelStateChanged?.Invoke(
                    record.Config.Id,
                    previous,
                    nextState);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 执行判断是否切换Allowed相关逻辑。
        /// </summary>
        private static bool IsTransitionAllowed(
            UIPanelState previous,
            UIPanelState next)
        {
            switch (previous)
            {
                case UIPanelState.None:
                    return next == UIPanelState.Creating;
                case UIPanelState.Creating:
                    return next == UIPanelState.Loading ||
                           next == UIPanelState.Failed ||
                           next == UIPanelState.Destroyed;
                case UIPanelState.Loading:
                    return next == UIPanelState.Visible ||
                           next == UIPanelState.Hidden ||
                           next == UIPanelState.Closing ||
                           next == UIPanelState.Failed ||
                           next == UIPanelState.Destroyed;
                case UIPanelState.Visible:
                    return next == UIPanelState.Hidden ||
                           next == UIPanelState.Closing ||
                           next == UIPanelState.Destroyed ||
                           next == UIPanelState.Failed;
                case UIPanelState.Hidden:
                    return next == UIPanelState.Visible ||
                           next == UIPanelState.Closing ||
                           next == UIPanelState.Destroyed ||
                           next == UIPanelState.Failed;
                case UIPanelState.Closing:
                    return next == UIPanelState.Loading ||
                           next == UIPanelState.Visible ||
                           next == UIPanelState.Hidden ||
                           next == UIPanelState.Destroyed ||
                           next == UIPanelState.Failed;
                case UIPanelState.Destroyed:
                case UIPanelState.Failed:
                    return next == UIPanelState.Creating;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 响应激活项场景Changed事件。
        /// </summary>
        private void OnActiveSceneChanged(
            Scene previousScene,
            Scene nextScene)
        {
            if (!closePanelsOnSceneChanged ||
                isShuttingDown)
            {
                return;
            }

            var targets = new List<UIPanelRecord>();
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (IsRecordAlive(record) &&
                    (!persistAcrossScenes ||
                     record.Config.AutoDestroyWhenSceneChanged))
                {
                    targets.Add(record);
                }
            }

            foreach (UIPanelRecord record in targets)
            {
                stackCoordinator.Remove(
                    stackRecords,
                    record.Config.Id);
                InternalClosePanel(
                    record,
                    true,
                    true,
                    true);
            }

            RepairPendingTargets();
        }

        /// <summary>
        /// 执行修复Pending目标相关逻辑。
        /// </summary>
        private void RepairPendingTargets()
        {
            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (string.IsNullOrEmpty(
                        record.PendingCloseTarget))
                {
                    continue;
                }

                if (!panelRecords.TryGetValue(
                        record.PendingCloseTarget,
                        out UIPanelRecord target) ||
                    !IsRecordAlive(target))
                {
                    ClearPendingClose(record);
                    if (record.Logic != null &&
                        record.Logic.IsVisible &&
                        record.State == UIPanelState.Closing)
                    {
                        Transition(
                            record,
                            UIPanelState.Visible,
                            "repair pending close");
                    }
                }
            }
        }

        /// <summary>
        /// 确保层级根节点。
        /// </summary>
        private UILayerRoot EnsureLayerRoot()
        {
            if (layerRoot == null)
            {
                layerRoot = UILayerRoot.CreateRuntime();
                ownsLayerRoot = true;
            }

            layerRoot.EnsureInitialized();
            return layerRoot;
        }

        /// <summary>
        /// 确保Initialized。
        /// </summary>
        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 执行关闭内部相关逻辑。
        /// </summary>
        private void ShutdownInternal()
        {
            if (isShuttingDown)
            {
                return;
            }

            isShuttingDown = true;
            if (sceneEventSubscribed)
            {
                SceneManager.activeSceneChanged -=
                    OnActiveSceneChanged;
                sceneEventSubscribed = false;
            }

            foreach (UIPanelRecord record in panelRecords.Values)
            {
                if (IsRecordAlive(record))
                {
                    DestroyPanel(record);
                }
            }

            panelRecords.Clear();
            stackRecords.Clear();
            savedStacks.Clear();
            configs.Clear();
            logicFactories.Clear();
            localPrefabs.Clear();

            if (ownsLayerRoot && layerRoot != null)
            {
                Destroy(layerRoot.gameObject);
            }

            layerRoot = null;
            ownsLayerRoot = false;
            isInitialized = false;
            isShuttingDown = false;
        }

        /// <summary>
        /// 执行判断是否记录Alive相关逻辑。
        /// </summary>
        private static bool IsRecordAlive(UIPanelRecord record)
        {
            return record != null &&
                   record.Logic != null &&
                   !record.Logic.IsDisposed &&
                   record.State != UIPanelState.Destroyed &&
                   record.State != UIPanelState.Failed;
        }

        private static UIFrameworkException
            CreateSupersededException(string panelId)
        {
            return new UIFrameworkException(
                UIFrameworkErrorCode.OperationCancelled,
                $"面板 {panelId} 的打开请求已被关闭或更新的导航请求取代。",
                panelId);
        }

        /// <summary>
        /// 执行LogVerbose相关逻辑。
        /// </summary>
        private void LogVerbose(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[UIManager] {message}");
            }
        }
    }
}
