using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Resource;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuaObjectBind;
using XLua;
using YooAsset;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LxyDemo.UIFramework
{
    /// <summary>
    /// xLua runtime entry for the Lua UI framework.
    /// Editor Lua modules are read directly from the project Lua folder;
    /// editor prefabs use AssetDatabase. Player resources are loaded from
    /// the initialized YooAsset ResourcePackage.
    /// </summary>
    [DefaultExecutionOrder(-9990)]
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/UI/Lua UI Runtime")]
    public sealed class LuaUIRuntime : MonoBehaviour
    {
        private const string BootstrapModule =
            "Framework.UI.Bootstrap";
        private const string LuaAssetRoot =
            "Assets/GameResources/Lua/";

        private sealed class LuaPanelAssetRecord
        {
            /// <summary>
            /// 公开的实例数据。
            /// </summary>
            public GameObject Instance;
            /// <summary>
            /// 公开的句柄数据。
            /// </summary>
            public GameResourceInstanceHandle Handle;
        }

        private static LuaUIRuntime instance;

        private LuaEnv luaEnv;
        private LuaTable bootstrap;
        private LuaFunction initializeFunction;
        private LuaFunction registerPrefabFunction;
        private LuaFunction openPanelFunction;
        private LuaFunction closePanelFunction;
        private LuaFunction preloadPanelFunction;
        private LuaFunction isPanelOpenFunction;
        private LuaFunction sceneChangedFunction;
        private LuaFunction shutdownFunction;
        private float nextTickTime;
        private bool mainStarted;
        private string resourcePackageName = "DefaultPackage";
        private readonly Dictionary<
            string,
            GameResourceHandle<TextAsset>>
            luaModuleHandles =
                new Dictionary<
                    string,
                    GameResourceHandle<TextAsset>>(
                        StringComparer.Ordinal);
        private readonly Dictionary<int, LuaPanelAssetRecord>
            panelAssetRecords =
                new Dictionary<int, LuaPanelAssetRecord>();

        public static LuaUIRuntime Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = FindObjectOfType<LuaUIRuntime>(true);
                if (instance != null)
                {
                    return instance;
                }

                var runtimeObject = new GameObject(
                    "[LuaUIRuntime]",
                    typeof(LuaUIRuntime));
                instance =
                    runtimeObject.GetComponent<LuaUIRuntime>();
                DontDestroyOnLoad(runtimeObject);
                return instance;
            }
        }

        /// <summary>
        /// 指示当前对象是否已初始化。
        /// </summary>
        public bool IsInitialized =>
            luaEnv != null && bootstrap != null;

        /// <summary>
        /// 指示MainStarted是否成立。
        /// </summary>
        public bool IsMainStarted => mainStarted;
        /// <summary>
        /// 向调用方提供资源资源包名称。
        /// </summary>
        public string ResourcePackageName =>
            resourcePackageName;

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
        /// 更新组件的运行时状态。
        /// </summary>
        private void Update()
        {
            if (luaEnv == null || Time.unscaledTime < nextTickTime)
            {
                return;
            }

            nextTickTime = Time.unscaledTime + 0.5f;
            luaEnv.Tick();
        }

        /// <summary>
        /// 在组件启用时建立运行时关联。
        /// </summary>
        private void OnEnable()
        {
            SceneManager.activeSceneChanged +=
                HandleActiveSceneChanged;
        }

        /// <summary>
        /// 在组件停用时解除运行时关联。
        /// </summary>
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -=
                HandleActiveSceneChanged;
        }

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        public void Initialize(UILayerRoot layerRoot)
        {
            if (layerRoot == null)
            {
                throw new ArgumentNullException(nameof(layerRoot));
            }

            EnsureLuaEnvironment();
            initializeFunction.Call(layerRoot);
        }

        /// <summary>
        /// 执行配置资源资源包相关逻辑。
        /// </summary>
        public void ConfigureResourcePackage(string packageName)
        {
            string normalized =
                string.IsNullOrWhiteSpace(packageName)
                    ? "DefaultPackage"
                    : packageName.Trim();
            if (luaEnv != null &&
                !string.Equals(
                    resourcePackageName,
                    normalized,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "LuaEnv 启动后不能切换 YooAsset Package。");
            }

            resourcePackageName = normalized;
        }

        /// <summary>
        /// 执行 Main.lua。模块返回 table 且包含 Start 方法时自动调用。
        /// </summary>
        public void StartMain(string moduleName = "Main")
        {
            EnsureReady();
            if (mainStarted)
            {
                return;
            }

            string normalized = NormalizeModuleName(moduleName);
            luaEnv.DoString(
                "local main = require('" + normalized + "')\n" +
                "if type(main) == 'table' and " +
                "type(main.Start) == 'function' then\n" +
                "    main.Start()\n" +
                "end\n" +
                "return main",
                "Main.lua");
            mainStarted = true;
            Debug.Log(
                "[Lua] 启动模块执行成功：" + normalized,
                this);
        }

        /// <summary>
        /// 注册预制体。
        /// </summary>
        public string RegisterPrefab(
            GameObject prefab,
            string panelId = null)
        {
            EnsureReady();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            object[] results =
                registerPrefabFunction.Call(prefab, panelId);
            return results != null && results.Length > 0
                ? results[0]?.ToString()
                : prefab.name;
        }

        /// <summary>
        /// 打开面板。
        /// </summary>
        public void OpenPanel(
            string panelId,
            object userData = null)
        {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(panelId))
            {
                throw new ArgumentException(
                    "Panel ID cannot be empty.",
                    nameof(panelId));
            }

            openPanelFunction.Call(panelId, userData);
        }

        /// <summary>
        /// 关闭面板。
        /// </summary>
        public void ClosePanel(
            string panelId,
            bool forceDestroy = false)
        {
            EnsureReady();
            closePanelFunction.Call(panelId, forceDestroy);
        }

        /// <summary>
        /// 执行预加载面板相关逻辑。
        /// </summary>
        public void PreloadPanel(string panelId)
        {
            EnsureReady();
            preloadPanelFunction.Call(panelId);
        }

        /// <summary>
        /// 执行判断是否面板打开相关逻辑。
        /// </summary>
        public bool IsPanelOpen(string panelId)
        {
            EnsureReady();
            object[] results =
                isPanelOpenFunction.Call(panelId);
            return results != null &&
                   results.Length > 0 &&
                   Convert.ToBoolean(results[0]);
        }

        /// <summary>
        /// 提供给 Lua UIManager 的统一 Prefab 加载入口。
        /// Editor 使用 AssetDatabase，Player 使用 YooAsset。
        /// </summary>
        public static void LoadPanelAsync(
            string location,
            Transform parent,
            LuaFunction completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            LuaUIRuntime runtime = Instance;
            runtime.StartCoroutine(
                runtime.LoadPanelRoutine(
                    location,
                    parent,
                    completed));
        }

        /// <summary>
        /// 提供给 Lua UIManager 的实例释放入口。
        /// </summary>
        public static void ReleasePanel(GameObject panelObject)
        {
            if (panelObject == null)
            {
                return;
            }

            if (instance == null)
            {
                Destroy(panelObject);
                return;
            }

            instance.ReleasePanelInternal(panelObject);
        }

        /// <summary>
        /// 通知场景Changed。
        /// </summary>
        public void NotifySceneChanged()
        {
            if (IsInitialized)
            {
                sceneChangedFunction.Call();
            }
        }

        /// <summary>
        /// 创建Backdrop。
        /// </summary>
        public static GameObject CreateBackdrop(
            RectTransform parent,
            string panelId,
            Color color,
            bool closeOnClick)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var backdrop = new GameObject(
                $"[{panelId}.Backdrop]",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LuaUIBackdrop));
            RectTransform rect =
                backdrop.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            UILayerRoot.SetLayerRecursively(
                backdrop,
                parent.gameObject.layer);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            Image image = backdrop.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            backdrop.GetComponent<LuaUIBackdrop>()
                .Configure(panelId, closeOnClick);
            return backdrop;
        }

        /// <summary>
        /// 尝试关闭FromBackdrop，并返回是否成功。
        /// </summary>
        internal static void TryCloseFromBackdrop(
            string panelId)
        {
            if (instance == null || !instance.IsInitialized)
            {
                return;
            }

            instance.ClosePanel(panelId);
        }

        /// <summary>
        /// 处理激活项场景Changed。
        /// </summary>
        private void HandleActiveSceneChanged(
            Scene previous,
            Scene current)
        {
            NotifySceneChanged();
        }

        /// <summary>
        /// 加载面板Routine。
        /// </summary>
        private IEnumerator LoadPanelRoutine(
            string location,
            Transform parent,
            LuaFunction completed)
        {
            string normalizedLocation =
                NormalizeAssetLocation(location);
            GameObject panelInstance = null;
            string errorMessage = null;

#if UNITY_EDITOR
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    normalizedLocation);
            if (prefab == null)
            {
                errorMessage =
                    "AssetDatabase 无法加载 Lua UI Prefab：" +
                    normalizedLocation;
            }
            else
            {
                panelInstance = Instantiate(
                    prefab,
                    parent,
                    false);
            }
#else
            if (!TryGetResourcePackage(
                    out ResourcePackage package,
                    out errorMessage))
            {
                CompletePanelLoad(
                    completed,
                    null,
                    errorMessage);
                yield break;
            }

            GameResourceInstanceHandle instanceHandle =
                GameResourceManager.GetOrCreate(gameObject)
                    .InstantiateAsync(
                        package,
                        normalizedLocation,
                        parent,
                        false,
                        true);
            yield return instanceHandle;
            if (instanceHandle.Status !=
                EOperationStatus.Succeeded)
            {
                errorMessage = instanceHandle.Error;
                instanceHandle.Release();
            }
            else
            {
                panelInstance = instanceHandle.Result;
                if (panelInstance == null)
                {
                    errorMessage =
                        "Prefab 实例化结果为空：" +
                        normalizedLocation;
                    instanceHandle.Release();
                }
                else
                {
                    panelAssetRecords[panelInstance.GetInstanceID()] =
                        new LuaPanelAssetRecord
                        {
                            Instance = panelInstance,
                            Handle = instanceHandle
                        };
                }
            }
#endif

            if (panelInstance != null && parent != null)
            {
                UILayerRoot.SetLayerRecursively(
                    panelInstance,
                    parent.gameObject.layer);
            }

            CompletePanelLoad(
                completed,
                panelInstance,
                errorMessage);
            yield break;
        }

        /// <summary>
        /// 执行Complete面板加载相关逻辑。
        /// </summary>
        private void CompletePanelLoad(
            LuaFunction completed,
            GameObject panelObject,
            string errorMessage)
        {
            try
            {
                completed.Call(panelObject, errorMessage);
            }
            catch (Exception exception)
            {
                if (panelObject != null)
                {
                    ReleasePanelInternal(panelObject);
                }

                Debug.LogException(exception, this);
            }
            finally
            {
                completed.Dispose();
            }
        }

        /// <summary>
        /// 释放面板Internal。
        /// </summary>
        private void ReleasePanelInternal(GameObject panelObject)
        {
            int instanceId = panelObject.GetInstanceID();
            bool releasedByResourceManager = false;
            if (panelAssetRecords.TryGetValue(
                    instanceId,
                    out LuaPanelAssetRecord record))
            {
                panelAssetRecords.Remove(instanceId);
                if (record.Handle != null)
                {
                    record.Handle.Release();
                    releasedByResourceManager = true;
                }
            }

            if (!releasedByResourceManager)
            {
                UnityEngine.Object.Destroy(panelObject);
            }
        }

        /// <summary>
        /// 确保就绪状态。
        /// </summary>
        private void EnsureReady()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "LuaUIRuntime is not initialized. " +
                    "Call Initialize(UILayerRoot) first.");
            }
        }

        /// <summary>
        /// 确保LuaEnvironment。
        /// </summary>
        private void EnsureLuaEnvironment()
        {
            if (IsInitialized)
            {
                return;
            }

            GameLogicXLuaGeneratedRegistration.EnsureRegistered();
            luaEnv = new LuaEnv();
#if UNITY_EDITOR
            luaEnv.Global.Set("UNITY_EDITOR", true);
#else
            luaEnv.Global.Set("UNITY_EDITOR", false);
#endif
            LuaObjectBindProxy.SetLuaEnv(luaEnv);
            luaEnv.AddLoader(LoadLuaModule);
            try
            {
                object[] results = luaEnv.DoString(
                    $"return require('{BootstrapModule}')",
                    "LuaUIBootstrap");
                bootstrap =
                    results != null && results.Length > 0
                        ? results[0] as LuaTable
                        : null;
                if (bootstrap == null)
                {
                    throw new InvalidOperationException(
                        $"Lua module {BootstrapModule} " +
                        "did not return a table.");
                }

                initializeFunction =
                    GetRequiredFunction("Initialize");
                registerPrefabFunction =
                    GetRequiredFunction("RegisterPrefab");
                openPanelFunction =
                    GetRequiredFunction("OpenPanel");
                closePanelFunction =
                    GetRequiredFunction("ClosePanel");
                preloadPanelFunction =
                    GetRequiredFunction("PreloadPanel");
                isPanelOpenFunction =
                    GetRequiredFunction("IsPanelOpen");
                sceneChangedFunction =
                    GetRequiredFunction("OnSceneChanged");
                shutdownFunction =
                    GetRequiredFunction("Shutdown");
            }
            catch
            {
                DisposeLuaEnvironment(false);
                throw;
            }
        }

        /// <summary>
        /// 获取必需项Function。
        /// </summary>
        private LuaFunction GetRequiredFunction(string name)
        {
            LuaFunction function =
                bootstrap.Get<LuaFunction>(name);
            if (function == null)
            {
                throw new MissingMethodException(
                    BootstrapModule,
                    name);
            }

            return function;
        }

        /// <summary>
        /// 加载Lua模块。
        /// </summary>
        private byte[] LoadLuaModule(ref string modulePath)
        {
            string relativePath =
                (modulePath ?? string.Empty)
                .Replace('.', '/')
                .Replace('\\', '/');
            if (!relativePath.EndsWith(
                    ".lua",
                    StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ".lua";
            }

#if UNITY_EDITOR
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(
                projectRoot,
                "Lua",
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                Debug.LogError(
                    "[Lua] 项目 Lua 目录中不存在模块：" +
                    sourcePath);
                return null;
            }

            try
            {
                modulePath = sourcePath.Replace('\\', '/');
                return File.ReadAllBytes(sourcePath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[Lua] 读取模块失败：" + sourcePath +
                    "\n" + exception);
                return null;
            }
#else
            string location =
                LuaAssetRoot + relativePath + ".bytes";
            if (luaModuleHandles.TryGetValue(
                    location,
                    out GameResourceHandle<TextAsset>
                        cachedHandle) &&
                    cachedHandle != null &&
                    cachedHandle.IsValid)
            {
                TextAsset cachedAsset = cachedHandle.Asset;
                if (cachedAsset != null)
                {
                    modulePath = location;
                    return cachedAsset.bytes;
                }
            }

            if (!TryGetResourcePackage(
                    out ResourcePackage package,
                    out string errorMessage))
            {
                Debug.LogError("[Lua] " + errorMessage);
                return null;
            }

            GameResourceHandle<TextAsset> handle =
                GameResourceManager.GetOrCreate(gameObject)
                    .LoadAssetSync<TextAsset>(
                        package,
                        location);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError(
                    "[Lua] YooAsset 加载模块失败：" +
                    location + "\n" + handle.Error);
                handle.Release();
                return null;
            }

            TextAsset luaAsset = handle.Asset;
            if (luaAsset == null)
            {
                handle.Release();
                return null;
            }

            luaModuleHandles[location] = handle;
            modulePath = location;
            return luaAsset.bytes;
#endif
        }

        /// <summary>
        /// 尝试获取资源资源包，并返回是否成功。
        /// </summary>
        private bool TryGetResourcePackage(
            out ResourcePackage package,
            out string errorMessage)
        {
            package = null;
            errorMessage = null;
            if (!YooAssets.IsInitialized)
            {
                errorMessage =
                    "YooAssets 尚未初始化。";
                return false;
            }

            if (!YooAssets.TryGetPackage(
                    resourcePackageName,
                    out package))
            {
                errorMessage =
                    "YooAsset Package 不存在：" +
                    resourcePackageName;
                return false;
            }

            if (package.InitializeStatus !=
                EOperationStatus.Succeeded)
            {
                errorMessage =
                    "YooAsset Package 尚未就绪：" +
                    resourcePackageName;
                package = null;
                return false;
            }

            return true;
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
                    "Lua UI ResPath 不能为空。",
                    nameof(location));
            }

            return location.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// 执行规范化Module名称相关逻辑。
        /// </summary>
        private static string NormalizeModuleName(
            string moduleName)
        {
            string normalized =
                string.IsNullOrWhiteSpace(moduleName)
                    ? "Main"
                    : moduleName.Trim()
                        .Replace('\\', '.')
                        .Replace('/', '.');
            if (normalized.EndsWith(
                    ".lua",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - 4);
            }

            foreach (char character in normalized)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '_' &&
                    character != '.')
                {
                    throw new ArgumentException(
                        "Lua 模块名包含非法字符：" +
                        moduleName,
                        nameof(moduleName));
                }
            }

            return normalized;
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

            DisposeLuaEnvironment(true);
            instance = null;
        }

        /// <summary>
        /// 执行DisposeLuaEnvironment相关逻辑。
        /// </summary>
        private void DisposeLuaEnvironment(bool invokeShutdown)
        {
            if (invokeShutdown && shutdownFunction != null)
            {
                try
                {
                    shutdownFunction.Call();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            DisposeFunction(ref initializeFunction);
            DisposeFunction(ref registerPrefabFunction);
            DisposeFunction(ref openPanelFunction);
            DisposeFunction(ref closePanelFunction);
            DisposeFunction(ref preloadPanelFunction);
            DisposeFunction(ref isPanelOpenFunction);
            DisposeFunction(ref sceneChangedFunction);
            DisposeFunction(ref shutdownFunction);
            bootstrap?.Dispose();
            bootstrap = null;
            ReleaseAllPanelAssets();
            LuaObjectBindProxy.ClearLuaEnv(luaEnv);
            luaEnv?.Dispose();
            luaEnv = null;
            ReleaseAllLuaModuleHandles();
            mainStarted = false;
        }

        /// <summary>
        /// 释放全部面板资源。
        /// </summary>
        private void ReleaseAllPanelAssets()
        {
            foreach (LuaPanelAssetRecord record in
                     panelAssetRecords.Values)
            {
                if (record.Handle != null)
                {
                    record.Handle.Release();
                }
                else if (record.Instance != null)
                {
                    UnityEngine.Object.Destroy(
                        record.Instance);
                }
            }

            panelAssetRecords.Clear();
        }

        /// <summary>
        /// 释放全部Lua模块Handles。
        /// </summary>
        private void ReleaseAllLuaModuleHandles()
        {
            foreach (GameResourceHandle<TextAsset> handle in
                     luaModuleHandles.Values)
            {
                if (handle != null && handle.IsValid)
                {
                    handle.Release();
                }
            }

            luaModuleHandles.Clear();
        }

        /// <summary>
        /// 执行DisposeFunction相关逻辑。
        /// </summary>
        private static void DisposeFunction(
            ref LuaFunction function)
        {
            function?.Dispose();
            function = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LuaUIBackdrop :
        MonoBehaviour,
        IPointerClickHandler
    {
        private string panelId;
        private bool closeOnClick;

        /// <summary>
        /// 配置当前对象。
        /// </summary>
        public void Configure(
            string configuredPanelId,
            bool configuredCloseOnClick)
        {
            panelId = configuredPanelId;
            closeOnClick = configuredCloseOnClick;
        }

        /// <summary>
        /// 响应Pointer点击事件。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (closeOnClick)
            {
                LuaUIRuntime.TryCloseFromBackdrop(panelId);
            }
        }
    }
}
