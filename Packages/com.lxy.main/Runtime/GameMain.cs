using System;
using System.Collections;
using Game.Contracts;
using Game.Main;
using LxyDemo.SceneManagement;
using UnityEngine;

namespace LxyDemo
{
    /// <summary>
    /// 游戏唯一启动编排器。严格保证资源清单和差异资源完成后才加载
    /// HybridCLR 程序集，热更入口完成后才允许加载业务场景及 XLua。
    /// </summary>
    [DefaultExecutionOrder(-19000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(YooAssetLauncher))]
    [RequireComponent(typeof(HybridCLRLoader))]
    public class GameMain : MonoBehaviour
    {
        private const string StartupDownloadViewResourcePath =
            "UI/UISlider";

        public enum BootstrapStage
        {
            None,
            UpdatingResources,
            StartingHotUpdateRuntime,
            LoadingFirstScene,
            StartingUIAndLua,
            Ready,
            Failed
        }

        public readonly struct BootstrapSnapshot
        {
            public BootstrapSnapshot(
                BootstrapStage stage,
                float progress,
                string message)
            {
                Stage = stage;
                Progress = Mathf.Clamp01(progress);
                Message = message ?? string.Empty;
            }

            public BootstrapStage Stage { get; }
            public float Progress { get; }
            public string Message { get; }
        }

        private static GameMain instance;

        [Header("启动服务")]
        [SerializeField]
        private YooAssetLauncher resourceLauncher;

        [SerializeField]
        private HybridCLRLoader hybridCLRLoader;

        [Header("首个业务场景")]
        [SerializeField]
        private bool loadFirstSceneOnStart = true;

        [SerializeField]
        private string firstSceneName = "Login";

        [Header("业务运行时")]
        [Tooltip("进入首场景后调用热更新入口启动场景级 UI 与 Lua。")]
        [SerializeField]
        private bool waitForUIAndLua = true;

        public static GameMain Instance => instance;
        public bool IsInitializing { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }
        public BootstrapStage Stage { get; private set; }
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; }

        public event Action<BootstrapSnapshot> StatusChanged;

        private HotUpdateStartupContext hotUpdateContext;
        private StartupDownloadView startupDownloadView;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            resourceLauncher ??=
                GetComponent<YooAssetLauncher>();
            hybridCLRLoader ??=
                GetComponent<HybridCLRLoader>();
            DontDestroyOnLoad(gameObject);
        }

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

            string targetScene = firstSceneName?.Trim();
            if (loadFirstSceneOnStart &&
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

            if (loadFirstSceneOnStart)
            {
                startupDownloadView?.ShowStartingGame(
                    $"正在进入 {targetScene}");
                yield return LoadFirstSceneAsync(targetScene);
                if (!string.IsNullOrEmpty(LastError))
                {
                    yield break;
                }

                if (waitForUIAndLua)
                {
                    yield return WaitForUIAndLuaAsync();
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
                "[GameMain] 资源、热更新代码、业务场景和 Lua " +
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
            yield return resourceLauncher.InitializeAsync();
            resourceLauncher.StatusChanged -=
                OnResourceStatusChanged;

            if (!resourceLauncher.IsReady)
            {
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
                firstSceneName?.Trim(),
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
            }
        }

        private IEnumerator WaitForUIAndLuaAsync()
        {
            SetStage(
                BootstrapStage.StartingUIAndLua,
                0.92f,
                "初始化 UIManager 与 XLua");

            if (hotUpdateContext == null)
            {
                Fail("缺少热更新启动上下文。");
                yield break;
            }

            yield return hybridCLRLoader.StartFirstScene(
                hotUpdateContext);
            if (!string.IsNullOrEmpty(hybridCLRLoader.LastError) ||
                !hotUpdateContext.FirstSceneRuntimeSucceeded)
            {
                Fail(
                    hybridCLRLoader.LastError ??
                    hotUpdateContext.FirstSceneRuntimeError ??
                    "UIManager 或 XLua 初始化失败。");
                yield break;
            }

            SetStage(
                BootstrapStage.StartingUIAndLua,
                0.99f,
                "UIManager 与 XLua 已就绪");
        }

        private void OnResourceStatusChanged(
            YooAssetLauncher.UpdateSnapshot snapshot)
        {
            SetStage(
                BootstrapStage.UpdatingResources,
                Mathf.Lerp(0.01f, 0.55f, snapshot.Progress),
                snapshot.Message);
        }

        private void OnHotUpdateStatusChanged(
            HotUpdateStartupProgress progress)
        {
            SetStage(
                BootstrapStage.StartingHotUpdateRuntime,
                Mathf.Lerp(0.56f, 0.74f, progress.Progress),
                progress.Message);
        }

        private void OnSceneLoadProgressChanged(
            string sceneName,
            float progress)
        {
            SetStage(
                BootstrapStage.LoadingFirstScene,
                Mathf.Lerp(0.75f, 0.91f, progress),
                $"加载业务场景：{sceneName}");
        }

        private void SetStage(
            BootstrapStage stage,
            float progress,
            string message)
        {
            Stage = stage;
            Progress = Mathf.Clamp01(progress);
            StatusMessage = message ?? string.Empty;

            if (stage != BootstrapStage.UpdatingResources &&
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

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
