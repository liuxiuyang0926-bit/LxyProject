using System;
using System.Collections;
using Game.Contracts;
using Game.Main;
using LxyDemo.SceneManagement;
using LxyDemo.UIFramework;
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
        [Tooltip("进入首场景后等待 UIStartup 完成 UIManager 与 XLua 初始化。")]
        [SerializeField]
        private bool waitForUIAndLua = true;

        [Min(1f)]
        [SerializeField]
        private float uiStartupTimeoutSeconds = 30f;

        public static GameMain Instance => instance;
        public bool IsInitializing { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }
        public BootstrapStage Stage { get; private set; }
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; }

        public event Action<BootstrapSnapshot> StatusChanged;

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

            string packageVersion =
                resourceLauncher.PackageVersion;
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                packageVersion = Application.isEditor
                    ? "Editor"
                    : "Unknown";
            }

            var context = new HotUpdateStartupContext(
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
            yield return hybridCLRLoader.LoadAndStart(context);

            if (!hybridCLRLoader.IsReady)
            {
                Fail(
                    hybridCLRLoader.LastError ??
                    context.Error ??
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

            float deadline = Time.realtimeSinceStartup +
                             Mathf.Max(1f, uiStartupTimeoutSeconds);
            while (UIStartup.Instance == null)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(
                        "等待 UIStartup 超时，请确认首场景中已挂载 " +
                        "UIStartup 组件。");
                    yield break;
                }

                yield return null;
            }

            UIStartup startup = UIStartup.Instance;
            yield return startup.InitializeAsync();
            if (!startup.IsInitialized)
            {
                Fail(
                    startup.LastError ??
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
            Debug.LogError("[GameMain] " + message, this);
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
