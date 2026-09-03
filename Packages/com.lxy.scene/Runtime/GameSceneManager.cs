using System;
using System.Collections;
using Game.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace LxyDemo.SceneManagement
{
    /// <summary>
    /// 常驻场景服务，只负责场景加载、进度与切换事件。
    /// 游戏启动流程由 GameMain 统一编排。
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class GameSceneManager : MonoBehaviour
    {
        private static GameSceneManager instance;

        private float minimumLoadingSeconds = 0.1f;

        [SerializeField]
        [Tooltip("场景切换完成后回收引用计数已经归零的 YooAsset Bundle。")]
        private bool unloadUnusedAssetsAfterSceneChanged = true;

        private Coroutine activeLoadCoroutine;
        private Action<float> activeProgressCallback;
        private Action<bool, string> activeCompletedCallback;

        public static GameSceneManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                var runtimeObject = new GameObject(
                    "[GameSceneManager]")
                {
                    hideFlags = HideFlags.HideInHierarchy |
                                HideFlags.DontSave
                };
                instance =
                    runtimeObject.AddComponent<GameSceneManager>();
                return instance;
            }
        }

        public float MinimumLoadingSeconds
        {
            get => minimumLoadingSeconds;
            set => minimumLoadingSeconds = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 指示当前对象是否正在加载。
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 向调用方提供Target场景名称。
        /// </summary>
        public string TargetSceneName { get; private set; }

        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// 向调用方提供Active场景名称。
        /// </summary>
        public string ActiveSceneName =>
            UnitySceneManager.GetActiveScene().name;

        public event Action<string> SceneLoadStarted;

        public event Action<string, float> SceneLoadProgressChanged;

        public event Action<string> SceneLoadCompleted;

        public event Action<string, string> SceneLoadFailed;

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
            gameObject.hideFlags = HideFlags.HideInHierarchy |
                                   HideFlags.DontSave;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 异步切换到目标场景。同一时间只允许一个加载任务。
        /// </summary>
        public Coroutine LoadSceneAsync(string sceneName)
        {
            string normalizedName = sceneName?.Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                ReportFailure(string.Empty, "场景名称不能为空。");
                return null;
            }

            if (IsLoading)
            {
                Debug.LogWarning(
                    $"[GameSceneManager] 正在加载 {TargetSceneName}，" +
                    $"忽略重复请求：{normalizedName}",
                    this);
                return activeLoadCoroutine;
            }

            if (string.Equals(
                    ActiveSceneName,
                    normalizedName,
                    StringComparison.Ordinal))
            {
                Progress = 1f;
                TargetSceneName = normalizedName;
                return null;
            }

            if (!Application.CanStreamedLevelBeLoaded(normalizedName))
            {
                ReportFailure(
                    normalizedName,
                    $"场景未加入 Build Settings：{normalizedName}");
                return null;
            }

            activeLoadCoroutine =
                StartCoroutine(LoadSceneRoutine(normalizedName));
            return activeLoadCoroutine;
        }

        /// <summary>
        /// Lua 场景系统使用的回调式加载入口。
        /// 真正的场景加载仍由 GameSceneManager 统一执行。
        /// </summary>
        public bool LoadSceneWithCallbacks(
            string sceneName,
            Action<float> progressCallback,
            Action<bool, string> completedCallback)
        {
            string normalizedName = sceneName?.Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                SafeInvokeCompleted(
                    completedCallback,
                    false,
                    "场景名称不能为空。");
                return false;
            }

            if (IsLoading)
            {
                if (!string.Equals(
                        TargetSceneName,
                        normalizedName,
                        StringComparison.Ordinal))
                {
                    SafeInvokeCompleted(
                        completedCallback,
                        false,
                        $"正在加载 {TargetSceneName}，" +
                        $"不能同时加载 {normalizedName}。");
                    return false;
                }

                activeProgressCallback += progressCallback;
                activeCompletedCallback += completedCallback;
                SafeInvokeProgress(progressCallback, Progress);
                return true;
            }

            if (string.Equals(
                    ActiveSceneName,
                    normalizedName,
                    StringComparison.Ordinal))
            {
                SafeInvokeProgress(progressCallback, 1f);
                SafeInvokeCompleted(
                    completedCallback,
                    true,
                    null);
                return true;
            }

            activeProgressCallback = progressCallback;
            activeCompletedCallback = completedCallback;
            Coroutine operation = LoadSceneAsync(normalizedName);
            return operation != null;
        }

        /// <summary>
        /// 加载场景Routine。
        /// </summary>
        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsLoading = true;
            Progress = 0f;
            TargetSceneName = sceneName;
            LastError = null;
            SceneLoadStarted?.Invoke(sceneName);
            NotifyProgress(sceneName, 0f);

            float startTime = Time.realtimeSinceStartup;
            AsyncOperation operation =
                UnitySceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single);
            if (operation == null)
            {
                FinishWithFailure(
                    sceneName,
                    $"Unity 未能创建场景加载任务：{sceneName}");
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                NotifyProgress(
                    sceneName,
                    Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }

            while (Time.realtimeSinceStartup - startTime <
                   minimumLoadingSeconds)
            {
                yield return null;
            }

            NotifyProgress(sceneName, 1f);
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            if (unloadUnusedAssetsAfterSceneChanged &&
                GameResourceManager.Instance != null)
            {
                // 等待旧场景对象的 OnDestroy 完成，确保 UI 已归还句柄。
                yield return null;
                yield return GameResourceManager.Instance
                    .UnloadUnusedAssetsAsync();
            }

            IsLoading = false;
            activeLoadCoroutine = null;
            SceneLoadCompleted?.Invoke(sceneName);
            FinishCallbacks(true, null);
            Debug.Log(
                $"[GameSceneManager] 场景加载完成：{sceneName}",
                this);
        }

        /// <summary>
        /// 通知进度。
        /// </summary>
        private void NotifyProgress(
            string sceneName,
            float progress)
        {
            Progress = Mathf.Clamp01(progress);
            SceneLoadProgressChanged?.Invoke(
                sceneName,
                Progress);
            SafeInvokeProgress(
                activeProgressCallback,
                Progress);
        }

        /// <summary>
        /// 报告Failure。
        /// </summary>
        private void ReportFailure(
            string sceneName,
            string message)
        {
            TargetSceneName = sceneName;
            LastError = message;
            Debug.LogError(
                "[GameSceneManager] " + message,
                this);
            SceneLoadFailed?.Invoke(sceneName, message);
            FinishCallbacks(false, message);
        }

        /// <summary>
        /// 执行FinishCallbacks相关逻辑。
        /// </summary>
        private void FinishCallbacks(
            bool succeeded,
            string errorMessage)
        {
            Action<bool, string> completed =
                activeCompletedCallback;
            activeProgressCallback = null;
            activeCompletedCallback = null;
            SafeInvokeCompleted(
                completed,
                succeeded,
                errorMessage);
        }

        /// <summary>
        /// 执行安全调用进度相关逻辑。
        /// </summary>
        private void SafeInvokeProgress(
            Action<float> callback,
            float progress)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(progress);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// 执行安全调用Completed相关逻辑。
        /// </summary>
        private void SafeInvokeCompleted(
            Action<bool, string> callback,
            bool succeeded,
            string errorMessage)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(succeeded, errorMessage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// 执行FinishWithFailure相关逻辑。
        /// </summary>
        private void FinishWithFailure(
            string sceneName,
            string message)
        {
            IsLoading = false;
            Progress = 0f;
            activeLoadCoroutine = null;
            ReportFailure(sceneName, message);
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
