using System;
using System.Collections;
using Game.Contracts;
using Game.Resource;
using UnityEngine;
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

        public static UIStartup Instance => instance;
        public YooAssetLauncher ResourceLauncher =>
            resourceLauncher;
        public UILayerRoot LayerRoot => layerRoot;
        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }
        public string LastError { get; private set; }
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
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            if (initializeOnStart)
            {
                yield return InitializeAsync();
            }
        }

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

            IsInitialized = true;
            IsInitializing = false;
            Debug.Log(
                UIRuntimeConfig.EnableLuaRuntime
                    ? "[UIStartup] 资源、UIManager 和 Lua 启动完成。"
                    : "[UIStartup] 资源和 UIManager 启动完成，" +
                      "当前为纯 C# 模式。",
                this);
        }

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

        private void FailStartup(string message)
        {
            LastError = message;
            IsInitialized = false;
            IsInitializing = false;
            Debug.LogError("[UIStartup] " + message, this);
        }

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
            FirstSceneRuntimeBridge.Unregister(this);
            instance = null;
        }
    }
}
