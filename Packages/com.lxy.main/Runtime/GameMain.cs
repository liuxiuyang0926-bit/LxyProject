using System;
using System.Collections;
using System.Reflection;
using Game.Contracts;
using Game.Main;
using LxyDemo.SceneManagement;
using UnityEngine;

namespace LxyDemo
{
    /// <summary>
    /// 游戏唯一启动编排器。严格保证资源清单和差异资源完成后才加载
    /// HybridCLR 程序集，程序集加载完成后才允许进入业务场景并启动 UI。
    /// </summary>
    [DefaultExecutionOrder(-19000)]
    [DisallowMultipleComponent]
    public class GameMain : MonoBehaviour
    {
        /// <summary>
        /// 启动下载界面的 Resources 加载路径；资源更新期间用于显示进度与错误。
        /// </summary>
        private const string StartupDownloadViewResourcePath =
            "UI/UISlider";

        /// <summary>
        /// 是否在资源和热更新运行时就绪后自动进入首个业务场景。
        /// </summary>
        private const bool LoadFirstSceneOnStart = true;

        /// <summary>
        /// 自动启动时要加载的首个业务场景名称。
        /// </summary>
        private const string FirstSceneName = "Login";

        /// <summary>
        /// 首场景 UI 启动类型位于热更新程序集。不能直接序列化在
        /// APK 内置场景中，必须在 HybridCLR 完成后动态创建。
        /// </summary>
        private const string FirstSceneRuntimeAssemblyName =
            "Game.UI";

        private const string FirstSceneRuntimeTypeName =
            "LxyDemo.UIFramework.UIStartup";

        private const string FirstSceneRuntimeObjectName =
            "[UIStartup]";

        /// <summary>
        /// 是否在首个业务场景加载完成后等待其热更新运行时初始化。
        /// </summary>
        private const bool InitializeFirstSceneRuntime = true;

        /// <summary>
        /// 等待首个业务场景运行时完成初始化的最长时间，单位为秒。
        /// </summary>
        private const float FirstSceneRuntimeTimeoutSeconds = 30f;

        public enum BootstrapStage
        {
            None,
            UpdatingResources,
            StartingHotUpdateRuntime,
            LoadingFirstScene,
            StartingUIAndLua,
            ForceUpdateRequired,
            Ready,
            Failed
        }

        public readonly struct BootstrapSnapshot
        {
            /// <summary>
            /// 创建Bootstrap快照实例。
            /// </summary>
            public BootstrapSnapshot(
                BootstrapStage stage,
                float progress,
                string message)
            {
                Stage = stage;
                Progress = Mathf.Clamp01(progress);
                Message = message ?? string.Empty;
            }

            /// <summary>
            /// 当前操作所处的执行阶段。
            /// </summary>
            public BootstrapStage Stage { get; }
            /// <summary>
            /// 当前操作的归一化进度，取值范围为 0 到 1。
            /// </summary>
            public float Progress { get; }
            /// <summary>
            /// 当前操作的状态说明文本。
            /// </summary>
            public string Message { get; }
        }

        private static GameMain instance;
        private YooAssetLauncher resourceLauncher;
        private HybridCLRLoader hybridCLRLoader;

        /// <summary>
        /// 向调用方提供实例。
        /// </summary>
        public static GameMain Instance => instance;

        /// <summary>
        /// 指示当前对象是否正在初始化。
        /// </summary>
        public bool IsInitializing { get; private set; }

        /// <summary>
        /// 指示当前对象是否已初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 最近一次启动失败的错误信息；启动成功且未发生错误时为 null。
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// 当前操作所处的执行阶段。
        /// </summary>
        public BootstrapStage Stage { get; private set; }

        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 当前操作的状态说明文本。
        /// </summary>
        public string StatusMessage { get; private set; }

        public event Action<BootstrapSnapshot> StatusChanged;

        private HotUpdateStartupContext hotUpdateContext;
        private StartupDownloadView startupDownloadView;

        /// <summary>
        /// 初始化组件的运行时状态。
        /// </summary>
        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            resourceLauncher =
                YooAssetLauncher.Instance ??
                GetComponent<YooAssetLauncher>() ??
                gameObject.AddComponent<YooAssetLauncher>();
            hybridCLRLoader =
                GetComponent<HybridCLRLoader>() ??
                gameObject.AddComponent<HybridCLRLoader>();
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 启动组件的运行流程。
        /// </summary>
        protected virtual IEnumerator Start()
        {
            IsInitializing = true;
            IsInitialized = false;
            LastError = null;
            if (!TryCreateStartupDownloadView(out string viewError))
            {
                Fail(viewError);
                yield break;
            }

            string targetScene = FirstSceneName;
            if (LoadFirstSceneOnStart &&
                string.IsNullOrEmpty(targetScene))
            {
                Fail("首个业务场景名称不能为空。");
                yield break;
            }

            yield return InitializeGameAsync();
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            if (LoadFirstSceneOnStart)
            {
                startupDownloadView?.ShowStartingGame(
                    $"正在进入 {targetScene}");
                yield return LoadFirstSceneAsync(targetScene);
                if (!string.IsNullOrEmpty(LastError))
                {
                    yield break;
                }

                if (InitializeFirstSceneRuntime)
                {
                    yield return WaitForFirstSceneRuntimeAsync();
                    if (!string.IsNullOrEmpty(LastError))
                    {
                        yield break;
                    }
                }
            }

            IsInitialized = true;
            IsInitializing = false;
            SetStage(
                BootstrapStage.Ready,
                1f,
                "游戏启动完成");
            Debug.Log(
                "[GameMain] 资源、热更新代码、业务场景和 UI 运行时" +
                "启动完成。",
                this);
        }

        /// <summary>
        /// 完成资源更新和 HybridCLR 热更运行时启动。任何挂有热更新
        /// MonoBehaviour 的资源都只能在此协程成功结束后加载。
        /// </summary>
        protected virtual IEnumerator InitializeGameAsync()
        {
            if (resourceLauncher == null)
            {
                Fail("缺少 YooAssetLauncher 组件。");
                yield break;
            }

            if (hybridCLRLoader == null)
            {
                Fail("缺少 HybridCLRLoader 组件。");
                yield break;
            }

            SetStage(
                BootstrapStage.UpdatingResources,
                0.01f,
                "检查资源更新");
            resourceLauncher.StatusChanged +=
                OnResourceStatusChanged;
            yield return resourceLauncher.InitializeAsync(
                startupDownloadView.PrepareBackgroundsAsync);
            resourceLauncher.StatusChanged -=
                OnResourceStatusChanged;

            if (!resourceLauncher.IsReady)
            {
                if (resourceLauncher.IsForceUpdateRequired)
                {
                    RequireForceUpdate();
                    yield break;
                }

                Fail(
                    resourceLauncher.LastError ??
                    "YooAsset 资源流程失败。");
                yield break;
            }

            if (startupDownloadView != null)
            {
                yield return startupDownloadView
                    .PlayReadyAnimationAsync();
            }

            string packageVersion =
                resourceLauncher.PackageVersion;
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                packageVersion = Application.isEditor
                    ? "Editor"
                    : "Unknown";
            }

            hotUpdateContext = new HotUpdateStartupContext(
                resourceLauncher.PackageName,
                packageVersion,
                string.IsNullOrWhiteSpace(Application.version)
                    ? "Unknown"
                    : Application.version,
                FirstSceneName,
                Application.isEditor,
                OnHotUpdateStatusChanged);

            SetStage(
                BootstrapStage.StartingHotUpdateRuntime,
                0.56f,
                "加载 HybridCLR 热更新运行时");
            if (!hybridCLRLoader.IsLoading &&
                !hybridCLRLoader.IsReady)
            {
                hybridCLRLoader.ConfigurePackage(
                    resourceLauncher.PackageName);
            }
            yield return hybridCLRLoader.LoadAndStart(
                hotUpdateContext);

            if (!hybridCLRLoader.IsReady)
            {
                Fail(
                    hybridCLRLoader.LastError ??
                    hotUpdateContext.Error ??
                    "HybridCLR 热更新启动失败。");
            }
        }

        /// <summary>
        /// 异步加载首个场景。
        /// </summary>
        private IEnumerator LoadFirstSceneAsync(string targetScene)
        {
            GameSceneManager manager =
                GameSceneManager.Instance;
            if (manager == null)
            {
                Fail("无法创建 GameSceneManager。");
                yield break;
            }

            SetStage(
                BootstrapStage.LoadingFirstScene,
                0.75f,
                $"加载业务场景：{targetScene}");
            manager.SceneLoadProgressChanged +=
                OnSceneLoadProgressChanged;

            Debug.Log(
                $"[GameMain] 请求进入首个业务场景：{targetScene}",
                this);
            Coroutine operation =
                manager.LoadSceneAsync(targetScene);
            if (operation != null)
            {
                yield return operation;
            }

            manager.SceneLoadProgressChanged -=
                OnSceneLoadProgressChanged;

            if (!string.IsNullOrEmpty(manager.LastError))
            {
                Fail(manager.LastError);
                yield break;
            }

            if (!string.Equals(
                    manager.ActiveSceneName,
                    targetScene,
                    StringComparison.Ordinal))
            {
                Fail(
                    $"业务场景加载结果不一致，期望 {targetScene}，" +
                    $"实际 {manager.ActiveSceneName}。");
                yield break;
            }

            if (!TryCreateFirstSceneRuntime(out string runtimeError))
            {
                Fail(runtimeError);
            }
        }

        /// <summary>
        /// 从已加载的热更新程序集动态创建首场景运行时。
        /// Login 是 APK 内置场景，若把 Game.UI 的 MonoBehaviour 直接
        /// 序列化到场景中，远端 DLL 的字段变更会和旧 APK 的场景数据
        /// 产生序列化布局冲突。
        /// </summary>
        private bool TryCreateFirstSceneRuntime(out string error)
        {
            error = null;
            if (FirstSceneRuntimeBridge.Current != null)
            {
                return true;
            }

            if (hybridCLRLoader == null || !hybridCLRLoader.IsReady)
            {
                error = "HybridCLR 尚未就绪，不能创建首场景 UI 运行时。";
                return false;
            }

            Assembly runtimeAssembly = null;
            foreach (Assembly assembly in
                     hybridCLRLoader.LoadedHotUpdateAssemblies)
            {
                if (string.Equals(
                        assembly.GetName().Name,
                        FirstSceneRuntimeAssemblyName,
                        StringComparison.Ordinal))
                {
                    runtimeAssembly = assembly;
                    break;
                }
            }

            if (runtimeAssembly == null)
            {
                error = "未加载首场景 UI 热更新程序集：" +
                        FirstSceneRuntimeAssemblyName;
                return false;
            }

            Type runtimeType = runtimeAssembly.GetType(
                FirstSceneRuntimeTypeName);
            if (runtimeType == null)
            {
                error = "热更新程序集缺少首场景 UI 类型：" +
                        FirstSceneRuntimeTypeName;
                return false;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(runtimeType))
            {
                error = "首场景 UI 类型不是 MonoBehaviour：" +
                        FirstSceneRuntimeTypeName;
                return false;
            }

            if (!typeof(IFirstSceneRuntime).IsAssignableFrom(
                    runtimeType))
            {
                error = "首场景 UI 类型未实现 IFirstSceneRuntime：" +
                        FirstSceneRuntimeTypeName;
                return false;
            }

            var runtimeObject = new GameObject(
                FirstSceneRuntimeObjectName);
            runtimeObject.AddComponent(runtimeType);

            if (FirstSceneRuntimeBridge.Current == null)
            {
                Destroy(runtimeObject);
                error = "首场景 UI 热更新组件未注册 IFirstSceneRuntime：" +
                        FirstSceneRuntimeTypeName;
                return false;
            }

            Debug.Log(
                "[GameMain] 已动态创建首场景 UI 运行时：" +
                FirstSceneRuntimeTypeName,
                runtimeObject);
            return true;
        }

        /// <summary>
        /// 执行等待For首个场景运行时异步相关逻辑。
        /// </summary>
        private IEnumerator WaitForFirstSceneRuntimeAsync()
        {
            SetStage(
                BootstrapStage.StartingUIAndLua,
                0.92f,
                "初始化首场景 UI 运行时");

            float deadline = Time.realtimeSinceStartup +
                             FirstSceneRuntimeTimeoutSeconds;
            while (FirstSceneRuntimeBridge.Current == null)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(
                        "等待首场景 UI 运行时超时，请确认场景中已" +
                        "创建实现 IFirstSceneRuntime 的启动组件。");
                    yield break;
                }

                yield return null;
            }

            IFirstSceneRuntime runtime =
                FirstSceneRuntimeBridge.Current;
            yield return runtime.InitializeAsync();
            if (!runtime.IsInitialized)
            {
                Fail(
                    string.IsNullOrWhiteSpace(runtime.LastError)
                        ? "首场景 UI 运行时初始化失败。"
                        : runtime.LastError);
                yield break;
            }

            hotUpdateContext?.CompleteFirstSceneRuntime();

            // UISlider 必须跨过 Start -> Login 的 Single 场景切换；
            // 等 Login UI 至少绘制一帧后再隐藏，避免旧场景销毁与
            // UICanvasRoot 出现之间露出相机的黑色清屏。
            yield return null;
            startupDownloadView?.HideAfterStartup();

            SetStage(
                BootstrapStage.StartingUIAndLua,
                0.99f,
                "首场景 UI 运行时已就绪");
        }

        /// <summary>
        /// 响应资源状态Changed事件。
        /// </summary>
        private void OnResourceStatusChanged(
            YooAssetLauncher.UpdateSnapshot snapshot)
        {
            SetStage(
                BootstrapStage.UpdatingResources,
                Mathf.Lerp(0.01f, 0.55f, snapshot.Progress),
                snapshot.Message);
        }

        /// <summary>
        /// 响应热更新Update状态Changed事件。
        /// </summary>
        private void OnHotUpdateStatusChanged(
            HotUpdateStartupProgress progress)
        {
            SetStage(
                BootstrapStage.StartingHotUpdateRuntime,
                Mathf.Lerp(0.56f, 0.74f, progress.Progress),
                progress.Message);
        }

        /// <summary>
        /// 响应场景加载进度Changed事件。
        /// </summary>
        private void OnSceneLoadProgressChanged(
            string sceneName,
            float progress)
        {
            SetStage(
                BootstrapStage.LoadingFirstScene,
                Mathf.Lerp(0.75f, 0.91f, progress),
                $"加载业务场景：{sceneName}");
        }

        /// <summary>
        /// 设置阶段。
        /// </summary>
        private void SetStage(
            BootstrapStage stage,
            float progress,
            string message)
        {
            Stage = stage;
            Progress = Mathf.Clamp01(progress);
            StatusMessage = message ?? string.Empty;

            if (stage != BootstrapStage.UpdatingResources &&
                stage != BootstrapStage.ForceUpdateRequired &&
                stage != BootstrapStage.Failed)
            {
                startupDownloadView?.ShowStartingGame(
                    StatusMessage);
            }

            try
            {
                StatusChanged?.Invoke(
                    new BootstrapSnapshot(
                        Stage,
                        Progress,
                        StatusMessage));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// 执行标记失败相关逻辑。
        /// </summary>
        private void Fail(string message)
        {
            LastError = message;
            IsInitialized = false;
            IsInitializing = false;
            SetStage(
                BootstrapStage.Failed,
                Progress,
                message);
            startupDownloadView?.ShowFailure(message);
            Debug.LogError("[GameMain] " + message, this);
        }

        /// <summary>
        /// 执行要求强制更新相关逻辑。
        /// </summary>
        private void RequireForceUpdate()
        {
            string message =
                string.IsNullOrWhiteSpace(
                    resourceLauncher.ForceUpdateMessage)
                    ? "当前客户端需要安装新版本。"
                    : resourceLauncher.ForceUpdateMessage;
            LastError = message;
            IsInitialized = false;
            IsInitializing = false;
            SetStage(
                BootstrapStage.ForceUpdateRequired,
                Progress,
                message);
            startupDownloadView?.ShowForceUpdate(
                message,
                resourceLauncher.CurrentAppVersion,
                resourceLauncher.MinimumAppVersion,
                resourceLauncher.CurrentAndroidVersionCode,
                resourceLauncher.MinimumAndroidVersionCode);
            Debug.LogWarning(
                "[GameMain] 已停止启动，等待客户端整包更新。",
                this);
        }

        /// <summary>
        /// 尝试创建启动下载视图，并返回是否成功。
        /// </summary>
        private bool TryCreateStartupDownloadView(out string error)
        {
            if (startupDownloadView != null)
            {
                error = null;
                return true;
            }

            GameObject template =
                Resources.Load<GameObject>(
                    StartupDownloadViewResourcePath);
            if (template == null)
            {
                error =
                    "无法加载启动下载界面：Resources/" +
                    StartupDownloadViewResourcePath;
                return false;
            }

            GameObject viewObject = Instantiate(template);
            viewObject.name = "UISlider";
            viewObject.transform.SetParent(transform, false);
            viewObject.transform.localScale = Vector3.one;

            if (viewObject.TryGetComponent(
                    out StartupDownloadView mountedView))
            {
                Destroy(viewObject);
                error =
                    "UISlider 预制体不应预挂 StartupDownloadView，" +
                    "该组件必须由启动代码动态添加。";
                return false;
            }

            startupDownloadView =
                viewObject.AddComponent<StartupDownloadView>();
            if (!startupDownloadView.Initialize(
                    resourceLauncher,
                    out error))
            {
                startupDownloadView = null;
                Destroy(viewObject);
                return false;
            }

            Debug.Log(
                "[GameMain] 已动态加载启动下载界面并添加控制器：" +
                "Resources/" +
                StartupDownloadViewResourcePath,
                startupDownloadView);
            return true;
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
