using System;
using System.Collections;
using Game.Contracts;
using Game.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace LxyDemo.UIFramework
{
    /// <summary>
    /// 工程唯一启动入口。依次完成资源系统、UICanvasRoot、C#/Lua UI
    /// 管理器和 Main.lua 初始化。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/UI/UI Startup")]
    public sealed class UIStartup :
        MonoBehaviour,
        IFirstSceneRuntime
    {
        [Header("资源系统")]
        [SerializeField]
        private YooAssetLauncher resourceLauncher;

        [Tooltip("UICanvasRoot 的完整 Assets 路径，也是 Player 中的 YooAsset Location。")]
        [SerializeField]
        private string uiCanvasRootLocation =
            "Assets/GameResources/Prefabs/UICanvasRoot.prefab";

        [SerializeField]
        private bool initializeOnStart = true;

        [SerializeField]
        private bool createEventSystem = true;

        private static UIStartup instance;

        private UILayerRoot layerRoot;
        private GameResourceInstanceHandle canvasRootInstanceHandle;
        private bool ownsLayerRoot;
        private Coroutine loginPanelCoroutine;

        /// <summary>
        /// 向调用方提供实例。
        /// </summary>
        public static UIStartup Instance => instance;
        /// <summary>
        /// 向调用方提供资源Launcher。
        /// </summary>
        public YooAssetLauncher ResourceLauncher =>
            resourceLauncher;
        /// <summary>
        /// 向调用方提供层级Root。
        /// </summary>
        public UILayerRoot LayerRoot => layerRoot;
        /// <summary>
        /// 指示当前对象是否已初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }
        /// <summary>
        /// 指示当前对象是否正在初始化。
        /// </summary>
        public bool IsInitializing { get; private set; }
        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string LastError { get; private set; }
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
            FirstSceneRuntimeBridge.Register(this);
            resourceLauncher = ResolveResourceLauncher();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 启动组件的运行流程。
        /// </summary>
        private IEnumerator Start()
        {
            if (initializeOnStart)
            {
                yield return InitializeAsync();
            }
        }

        /// <summary>
        /// 执行初始化异步相关逻辑。
        /// </summary>
        public IEnumerator InitializeAsync()
        {
            if (IsInitialized)
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
            LastError = null;

            resourceLauncher = ResolveResourceLauncher();

            if (resourceLauncher == null)
            {
                FailStartup("缺少 YooAssetLauncher 资源服务组件。");
                yield break;
            }

            yield return resourceLauncher.InitializeAsync();
            if (!resourceLauncher.IsReady)
            {
                FailStartup(
                    resourceLauncher.LastError ??
                    "YooAsset 资源系统初始化失败。");
                yield break;
            }

            yield return CreateLayerRootAsync();
            if (layerRoot == null)
            {
                FailStartup(
                    LastError ?? "UICanvasRoot 加载失败。");
                yield break;
            }

            try
            {
                layerRoot.EnsureInitialized(createEventSystem);
                UIManager.Instance.Initialize(null, layerRoot);

                if (UIRuntimeConfig.EnableLuaRuntime)
                {
                    LuaUIRuntime luaRuntime =
                        LuaUIRuntime.Instance;
                    luaRuntime.ConfigureResourcePackage(
                        resourceLauncher.PackageName);
                    luaRuntime.Initialize(layerRoot);
                    luaRuntime.StartMain(
                        UIRuntimeConfig.LuaMainModule);
                }
            }
            catch (Exception exception)
            {
                FailStartup(
                    "UI 或 Lua 运行时初始化失败：" +
                    exception.Message);
                Debug.LogException(exception, this);
                yield break;
            }

            yield return OpenLoginPanelIfNeededAsync();
            if (!string.IsNullOrEmpty(LastError))
            {
                FailStartup(LastError);
                yield break;
            }

            IsInitialized = true;
            IsInitializing = false;
            Debug.Log(
                UIRuntimeConfig.EnableLuaRuntime
                    ? "[UIStartup] 资源、UIManager 和 Lua 启动完成。"
                    : "[UIStartup] 资源和 UIManager 启动完成，" +
                      "当前为纯 C# 模式。",
                this);
        }

        /// <summary>
        /// Lua 入口未创建登录界面时，使用同一套 UIManager/资源系统打开它。
        /// 这样 Login 场景仍然保持空壳，热更新程序集可以决定首界面行为。
        /// </summary>
        private IEnumerator OpenLoginPanelIfNeededAsync()
        {
            if (!UIRuntimeConfig.OpenLoginPanelAfterStartup ||
                !string.Equals(
                    SceneManager.GetActiveScene().name,
                    UIRuntimeConfig.LoginSceneName,
                    StringComparison.Ordinal))
            {
                yield break;
            }

            LastError = null;
            string panelId = UIRuntimeConfig.LoginPanelId?.Trim();
            if (string.IsNullOrEmpty(panelId))
            {
                LastError = "登录面板 ID 不能为空。";
                yield break;
            }

            UIManager manager = UIManager.Instance;
            if (manager.IsPanelOpen(panelId) ||
                (UIRuntimeConfig.EnableLuaRuntime &&
                 LuaUIRuntime.Instance.IsPanelOpen(panelId)))
            {
                yield break;
            }

            UIAsyncOperation<UIPanelLogic> operation;
            try
            {
                var config = new UIPanelConfig
                {
                    Id = panelId,
                    PackageName = resourceLauncher.PackageName,
                    Location = UIRuntimeConfig.LoginPanelLocation,
                    Layer = UILayer.Stack,
                    CloseType = UIPanelCloseType.Destroy,
                    AutoDestroyWhenSceneChanged = true
                };
                manager.Register(config);
                operation = manager.OpenPanelAsync(panelId);
            }
            catch (Exception exception)
            {
                LastError =
                    "注册登录面板失败：" + exception.Message;
                yield break;
            }

            yield return operation;
            if (!operation.IsSucceeded)
            {
                LastError =
                    "打开登录面板失败：" +
                    operation.Exception?.Message;
                yield break;
            }

            Debug.Log(
                "[UIStartup] 已通过 C# UIManager 打开登录面板：" +
                panelId,
                this);
        }

        /// <summary>
        /// 从 Battle 等业务场景返回 Login 时，重新建立已随场景销毁的登录面板。
        /// </summary>
        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            if (!IsInitialized ||
                !UIRuntimeConfig.OpenLoginPanelAfterStartup ||
                !string.Equals(
                    current.name,
                    UIRuntimeConfig.LoginSceneName,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (loginPanelCoroutine != null)
            {
                StopCoroutine(loginPanelCoroutine);
            }

            loginPanelCoroutine =
                StartCoroutine(ReopenLoginPanelAsync());
        }

        /// <summary>
        /// 等 UIManager 完成场景切换清理后再恢复登录面板。
        /// </summary>
        private IEnumerator ReopenLoginPanelAsync()
        {
            yield return null;
            yield return OpenLoginPanelIfNeededAsync();
            loginPanelCoroutine = null;
            if (!string.IsNullOrEmpty(LastError))
            {
                Debug.LogError("[UIStartup] " + LastError, this);
            }
        }

        /// <summary>
        /// 解析资源Launcher。
        /// </summary>
        private YooAssetLauncher ResolveResourceLauncher()
        {
            // 正常启动时复用 Start 场景中已经完成下载和 HybridCLR
            // 加载的常驻 Launcher，避免进入业务场景后再次初始化。
            if (YooAssetLauncher.Instance != null)
            {
                return YooAssetLauncher.Instance;
            }

            if (resourceLauncher != null)
            {
                return resourceLauncher;
            }

            YooAssetLauncher localLauncher =
                GetComponent<YooAssetLauncher>();
            if (localLauncher != null)
            {
                return localLauncher;
            }

            // 保留从 Login 场景直接 Play 的编辑器调试能力。
            return gameObject.AddComponent<YooAssetLauncher>();
        }

        /// <summary>
        /// 异步创建层级根节点。
        /// </summary>
        private IEnumerator CreateLayerRootAsync()
        {
            layerRoot = FindObjectOfType<UILayerRoot>(true);
            if (layerRoot != null)
            {
                yield break;
            }

            string location =
                NormalizeAssetLocation(uiCanvasRootLocation);
            ResourcePackage package =
                resourceLauncher.Package;
#if !UNITY_EDITOR
            if (package == null)
            {
                LastError = "YooAsset ResourcePackage 为空。";
                yield break;
            }
#endif

            GameResourceManager manager =
                GameResourceManager.GetOrCreate(gameObject);
            canvasRootInstanceHandle =
                manager.InstantiateAsync(package, location);
            yield return canvasRootInstanceHandle;
            if (canvasRootInstanceHandle.Status !=
                EOperationStatus.Succeeded)
            {
                LastError =
                    "加载 UICanvasRoot 失败：" +
                    canvasRootInstanceHandle.Error;
                canvasRootInstanceHandle.Release();
                canvasRootInstanceHandle = null;
                yield break;
            }

            GameObject rootInstance =
                canvasRootInstanceHandle.Result;

            if (rootInstance == null)
            {
                LastError = "UICanvasRoot 实例化结果为空。";
                yield break;
            }

            rootInstance.name =
                System.IO.Path.GetFileNameWithoutExtension(
                    location);
            layerRoot =
                rootInstance.GetComponent<UILayerRoot>() ??
                rootInstance.AddComponent<UILayerRoot>();
            ownsLayerRoot = true;
        }

        /// <summary>
        /// 执行规范化资源Location相关逻辑。
        /// </summary>
        private static string NormalizeAssetLocation(
            string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException(
                    "资源 Location 不能为空。",
                    nameof(location));
            }

            return location.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// 执行标记失败启动相关逻辑。
        /// </summary>
        private void FailStartup(string message)
        {
            LastError = message;
            IsInitialized = false;
            IsInitializing = false;
            Debug.LogError("[UIStartup] " + message, this);
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

            if (canvasRootInstanceHandle != null)
            {
                canvasRootInstanceHandle.Release();
            }
            else if (ownsLayerRoot && layerRoot != null)
            {
                Destroy(layerRoot.gameObject);
            }

            canvasRootInstanceHandle = null;
            layerRoot = null;
            if (loginPanelCoroutine != null)
            {
                StopCoroutine(loginPanelCoroutine);
                loginPanelCoroutine = null;
            }

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            FirstSceneRuntimeBridge.Unregister(this);
            instance = null;
        }
    }
}
