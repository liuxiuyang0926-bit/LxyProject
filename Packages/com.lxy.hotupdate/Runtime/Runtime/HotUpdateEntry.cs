using System;
using System.Collections;
using Game.Contracts;
using LxyDemo.UIFramework;
using UnityEngine;

namespace Game.HotUpdate
{
    /// <summary>
    /// HybridCLR 热更程序集的唯一公开入口。
    /// Loader 会等待本协程完成后才允许加载业务场景。
    /// </summary>
    public static class HotUpdateEntry
    {
        public static IEnumerator Start(
            HotUpdateStartupContext context)
        {
            if (context == null)
            {
                Debug.LogError(
                    "[HotUpdate] 启动上下文为空，终止热更运行时启动。");
                yield break;
            }

            HotUpdateRuntime runtime =
                HotUpdateRuntime.Instance;
            if (runtime == null)
            {
                var runtimeObject = new GameObject(
                    "[HotUpdateRuntime]");
                UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
                runtime =
                    runtimeObject.AddComponent<HotUpdateRuntime>();
            }

            yield return runtime.InitializeAsync(context);
        }

        /// <summary>
        /// 首个业务场景加载完成后由 AOT 壳反射调用。UI 与 Lua 的具体
        /// 启动逻辑保留在热更新程序集，Game.Main 不再引用 Game.UI。
        /// </summary>
        public static IEnumerator StartFirstScene(
            HotUpdateStartupContext context)
        {
            HotUpdateRuntime runtime =
                HotUpdateRuntime.Instance;
            if (runtime == null)
            {
                context?.FailFirstSceneRuntime(
                    "热更新运行时尚未创建");
                yield break;
            }

            yield return runtime.StartFirstSceneAsync(context);
        }
    }

    /// <summary>
    /// 常驻的热更业务运行时。后续配置、存档、网络、SDK 等热更模块
    /// 应从这里继续拆分和注册，不再堆到静态入口里。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class HotUpdateRuntime : MonoBehaviour
    {
        private const string LastApplicationVersionKey =
            "hotupdate.last_application_version";
        private const string LastPackageVersionKey =
            "hotupdate.last_package_version";

        private static HotUpdateRuntime instance;

        public static HotUpdateRuntime Instance => instance;

        public bool IsInitializing { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public string PreviousApplicationVersion { get; private set; }
        public string PreviousPackageVersion { get; private set; }
        public string CurrentApplicationVersion { get; private set; }
        public string CurrentPackageVersion { get; private set; }
        public bool IsFirstLaunchAfterApplicationUpdate { get; private set; }
        public bool IsFirstLaunchAfterResourceUpdate { get; private set; }

        public event Action<bool> ApplicationFocusChanged;
        public event Action<bool> ApplicationPauseChanged;
        public event Action ApplicationQuitting;

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

        public IEnumerator InitializeAsync(
            HotUpdateStartupContext context)
        {
            if (IsReady)
            {
                context.Complete("复用已就绪的热更运行时");
                yield break;
            }

            if (IsInitializing)
            {
                while (IsInitializing)
                {
                    yield return null;
                }

                if (IsReady)
                {
                    context.Complete("复用已就绪的热更运行时");
                }
                else
                {
                    context.Fail(
                        LastError ?? "热更运行时初始化失败");
                }

                yield break;
            }

            IsInitializing = true;
            IsReady = false;
            LastError = null;

            context.Report(
                HotUpdateStartupStage.ValidatingEnvironment,
                0.15f,
                "校验热更运行环境");

            if (!ValidateContext(context, out string error))
            {
                Fail(context, error);
                yield break;
            }

            if (!HotUpdateRuntimeConfig.Apply(out error))
            {
                Fail(context, error);
                yield break;
            }

            // 将初始化开销分散到不同帧，避免以后模块增多后在首帧形成尖峰。
            yield return null;

            context.Report(
                HotUpdateStartupStage.ReadingVersionState,
                0.45f,
                "读取应用与资源版本状态");

            PreviousApplicationVersion =
                PlayerPrefs.GetString(
                    LastApplicationVersionKey,
                    string.Empty);
            PreviousPackageVersion =
                PlayerPrefs.GetString(
                    LastPackageVersionKey,
                    string.Empty);
            CurrentApplicationVersion =
                context.ApplicationVersion;
            CurrentPackageVersion =
                context.PackageVersion;

            IsFirstLaunchAfterApplicationUpdate =
                !string.Equals(
                    PreviousApplicationVersion,
                    CurrentApplicationVersion,
                    StringComparison.Ordinal);
            IsFirstLaunchAfterResourceUpdate =
                !string.Equals(
                    PreviousPackageVersion,
                    CurrentPackageVersion,
                    StringComparison.Ordinal);

            yield return null;

            context.Report(
                HotUpdateStartupStage.InitializingRuntime,
                0.75f,
                "初始化热更生命周期和业务服务容器");

            // 只有所有热更初始化步骤都成功后才提交版本，崩溃或失败时
            // 下一次启动仍会被识别为更新后的首次启动。
            PlayerPrefs.SetString(
                LastApplicationVersionKey,
                CurrentApplicationVersion);
            PlayerPrefs.SetString(
                LastPackageVersionKey,
                CurrentPackageVersion);
            PlayerPrefs.Save();

            IsReady = true;
            IsInitializing = false;
            context.Complete();

            Debug.Log(
                $"[HotUpdate] 运行时启动完成，App=" +
                $"{CurrentApplicationVersion}，Package=" +
                $"{CurrentPackageVersion}，AppUpdated=" +
                $"{IsFirstLaunchAfterApplicationUpdate}，" +
                $"ResourceUpdated={IsFirstLaunchAfterResourceUpdate}",
                this);
        }

        public IEnumerator StartFirstSceneAsync(
            HotUpdateStartupContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (context.IsFirstSceneRuntimeCompleted)
            {
                yield break;
            }

            if (!IsReady)
            {
                context.FailFirstSceneRuntime(
                    LastError ?? "热更新运行时尚未就绪");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup +
                             Mathf.Max(
                                 1f,
                                 HotUpdateRuntimeConfig
                                     .UIStartupTimeoutSeconds);
            while (UIStartup.Instance == null)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    context.FailFirstSceneRuntime(
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
                context.FailFirstSceneRuntime(
                    startup.LastError ??
                    "UIManager 或 XLua 初始化失败。");
                yield break;
            }

            context.CompleteFirstSceneRuntime();
        }

        private static bool ValidateContext(
            HotUpdateStartupContext context,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(context.PackageName))
            {
                error = "Package 名称为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.PackageVersion))
            {
                error = "Package 版本为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.ApplicationVersion))
            {
                error = "应用版本为空";
                return false;
            }

            error = null;
            return true;
        }

        private void Fail(
            HotUpdateStartupContext context,
            string error)
        {
            LastError = error;
            IsInitializing = false;
            IsReady = false;
            context.Fail(error);
            Debug.LogError("[HotUpdate] " + error, this);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplicationFocusChanged?.Invoke(hasFocus);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            ApplicationPauseChanged?.Invoke(pauseStatus);
        }

        private void OnApplicationQuit()
        {
            ApplicationQuitting?.Invoke();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
