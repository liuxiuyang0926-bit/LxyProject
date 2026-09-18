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
        /// <summary>
        /// 创建HotUpdate启动Progress实例。
        /// </summary>
        public HotUpdateStartupProgress(
            HotUpdateStartupStage stage,
            float progress,
            string message)
        {
            Stage = stage;
            Progress = Math.Max(0f, Math.Min(1f, progress));
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// 当前操作所处的执行阶段。
        /// </summary>
        public HotUpdateStartupStage Stage { get; }
        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; }
        /// <summary>
        /// 当前操作的状态说明文本。
        /// </summary>
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

        /// <summary>
        /// 创建HotUpdate启动上下文实例。
        /// </summary>
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

        /// <summary>
        /// 向调用方提供资源包名称。
        /// </summary>
        public string PackageName { get; }
        /// <summary>
        /// 向调用方提供资源包版本。
        /// </summary>
        public string PackageVersion { get; }
        /// <summary>
        /// 向调用方提供Application版本。
        /// </summary>
        public string ApplicationVersion { get; }
        /// <summary>
        /// 向调用方提供First场景名称。
        /// </summary>
        public string FirstSceneName { get; }
        /// <summary>
        /// 指示Editor是否成立。
        /// </summary>
        public bool IsEditor { get; }

        /// <summary>
        /// 当前操作所处的执行阶段。
        /// </summary>
        public HotUpdateStartupStage Stage { get; private set; }
        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; private set; }
        /// <summary>
        /// 当前操作的状态说明文本。
        /// </summary>
        public string Message { get; private set; }
        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string Error { get; private set; }
        /// <summary>
        /// 指示当前操作是否已完成。
        /// </summary>
        public bool IsCompleted { get; private set; }
        /// <summary>
        /// 指示当前操作是否成功完成。
        /// </summary>
        public bool Succeeded => IsCompleted &&
                                 string.IsNullOrEmpty(Error);

        /// <summary>
        /// 指示First场景运行时Completed是否成立。
        /// </summary>
        public bool IsFirstSceneRuntimeCompleted { get; private set; }
        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string FirstSceneRuntimeError { get; private set; }
        /// <summary>
        /// 指示首场景运行时是否成功初始化。
        /// </summary>
        public bool FirstSceneRuntimeSucceeded =>
            IsFirstSceneRuntimeCompleted &&
            string.IsNullOrEmpty(FirstSceneRuntimeError);

        /// <summary>
        /// 执行报告相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行Complete相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行标记失败相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行Complete首个场景运行时相关逻辑。
        /// </summary>
        public void CompleteFirstSceneRuntime()
        {
            if (IsFirstSceneRuntimeCompleted)
            {
                return;
            }

            FirstSceneRuntimeError = null;
            IsFirstSceneRuntimeCompleted = true;
        }

        /// <summary>
        /// 执行标记失败首个场景运行时相关逻辑。
        /// </summary>
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
