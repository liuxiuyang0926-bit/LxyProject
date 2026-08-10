using System;

namespace Game.Contracts
{
    public enum HotUpdateStartupStage
    {
        None,
        ValidatingEnvironment,
        ReadingVersionState,
        InitializingRuntime,
        Ready,
        Failed
    }

    public readonly struct HotUpdateStartupProgress
    {
        public HotUpdateStartupProgress(
            HotUpdateStartupStage stage,
            float progress,
            string message)
        {
            Stage = stage;
            Progress = Math.Max(0f, Math.Min(1f, progress));
            Message = message ?? string.Empty;
        }

        public HotUpdateStartupStage Stage { get; }
        public float Progress { get; }
        public string Message { get; }
    }

    /// <summary>
    /// AOT 启动层传递给热更程序集的稳定契约。
    /// 不直接暴露 YooAsset 或业务层类型，避免程序集反向依赖。
    /// </summary>
    public sealed class HotUpdateStartupContext
    {
        private readonly Action<HotUpdateStartupProgress>
            progressCallback;

        public HotUpdateStartupContext(
            string packageName,
            string packageVersion,
            string applicationVersion,
            string firstSceneName,
            bool isEditor,
            Action<HotUpdateStartupProgress> progressCallback = null)
        {
            PackageName = packageName ?? string.Empty;
            PackageVersion = packageVersion ?? string.Empty;
            ApplicationVersion = applicationVersion ?? string.Empty;
            FirstSceneName = firstSceneName ?? string.Empty;
            IsEditor = isEditor;
            this.progressCallback = progressCallback;
        }

        public string PackageName { get; }
        public string PackageVersion { get; }
        public string ApplicationVersion { get; }
        public string FirstSceneName { get; }
        public bool IsEditor { get; }

        public HotUpdateStartupStage Stage { get; private set; }
        public float Progress { get; private set; }
        public string Message { get; private set; }
        public string Error { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool Succeeded => IsCompleted &&
                                 string.IsNullOrEmpty(Error);

        public bool IsFirstSceneRuntimeCompleted { get; private set; }
        public string FirstSceneRuntimeError { get; private set; }
        public bool FirstSceneRuntimeSucceeded =>
            IsFirstSceneRuntimeCompleted &&
            string.IsNullOrEmpty(FirstSceneRuntimeError);

        public void Report(
            HotUpdateStartupStage stage,
            float progress,
            string message)
        {
            if (IsCompleted)
            {
                return;
            }

            Stage = stage;
            Progress = Math.Max(0f, Math.Min(1f, progress));
            Message = message ?? string.Empty;
            progressCallback?.Invoke(
                new HotUpdateStartupProgress(
                    Stage,
                    Progress,
                    Message));
        }

        public void Complete(string message = null)
        {
            if (IsCompleted)
            {
                return;
            }

            Stage = HotUpdateStartupStage.Ready;
            Progress = 1f;
            Message = message ?? "热更运行时已就绪";
            Error = null;
            IsCompleted = true;
            progressCallback?.Invoke(
                new HotUpdateStartupProgress(
                    Stage,
                    Progress,
                    Message));
        }

        public void Fail(string error)
        {
            if (IsCompleted)
            {
                return;
            }

            Error = string.IsNullOrWhiteSpace(error)
                ? "热更运行时启动失败"
                : error.Trim();
            Stage = HotUpdateStartupStage.Failed;
            Message = Error;
            IsCompleted = true;
            progressCallback?.Invoke(
                new HotUpdateStartupProgress(
                    Stage,
                    Progress,
                    Message));
        }

        public void CompleteFirstSceneRuntime()
        {
            if (IsFirstSceneRuntimeCompleted)
            {
                return;
            }

            FirstSceneRuntimeError = null;
            IsFirstSceneRuntimeCompleted = true;
        }

        public void FailFirstSceneRuntime(string error)
        {
            if (IsFirstSceneRuntimeCompleted)
            {
                return;
            }

            FirstSceneRuntimeError = string.IsNullOrWhiteSpace(error)
                ? "首场景热更新运行时启动失败"
                : error.Trim();
            IsFirstSceneRuntimeCompleted = true;
        }
    }
}
