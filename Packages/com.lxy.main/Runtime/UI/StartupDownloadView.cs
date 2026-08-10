using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

namespace Game.Main
{
    /// <summary>
    /// 启动阶段的资源下载界面。该组件属于 AOT 启动壳，确保在
    /// HybridCLR 热更新程序集尚未加载时也能显示下载进度。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupDownloadView : MonoBehaviour
    {
        private const float NoDownloadCompletionDurationSeconds = 3f;

        private Slider progressSlider;

        private TextMeshProUGUI statusText;

        private TextMeshProUGUI progressText;

        private TextMeshProUGUI sizeText;

        private YooAssetLauncher launcher;
        private long lastCurrentBytes;
        private long lastTotalBytes;
        private int lastCurrentCount;
        private int lastTotalCount;
        private bool readyAnimationFinished;

        /// <summary>
        /// 组件由 GameMain 在实例化预制体后动态添加，再按节点名称
        /// 获取引用，预制体不需要挂载脚本或保存字段引用。
        /// </summary>
        public bool Initialize(
            YooAssetLauncher value,
            out string error)
        {
            if (!TryResolveBindings(out error))
            {
                return false;
            }

            Bind(value);
            error = null;
            return true;
        }

        private void Bind(YooAssetLauncher value)
        {
            if (launcher == value)
            {
                return;
            }

            Unbind();
            launcher = value;
            if (launcher != null)
            {
                launcher.StatusChanged += OnStatusChanged;
                launcher.DownloadError += OnDownloadError;
            }

            ResetView();
        }

        private bool TryResolveBindings(out string error)
        {
            progressSlider = FindPathComponent<Slider>(
                "bottom/Slider");
            statusText = FindPathComponent<TextMeshProUGUI>(
                "bottom/StatusText");
            progressText = FindPathComponent<TextMeshProUGUI>(
                "bottom/ProgressText");
            sizeText = FindPathComponent<TextMeshProUGUI>(
                "bottom/SizeText");

            if (progressSlider != null &&
                statusText != null &&
                progressText != null &&
                sizeText != null)
            {
                error = null;
                return true;
            }

            var missing = new System.Collections.Generic.List<string>();
            if (progressSlider == null)
            {
                missing.Add("bottom/Slider(Slider)");
            }

            if (statusText == null)
            {
                missing.Add("bottom/StatusText(TextMeshProUGUI)");
            }

            if (progressText == null)
            {
                missing.Add("bottom/ProgressText(TextMeshProUGUI)");
            }

            if (sizeText == null)
            {
                missing.Add("bottom/SizeText(TextMeshProUGUI)");
            }

            error = "UISlider 缺少动态绑定节点：" +
                    string.Join(", ", missing);
            return false;
        }

        private T FindPathComponent<T>(string relativePath)
            where T : Component
        {
            Transform target = transform.Find(relativePath);
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<T>();
        }

        public void ShowStartingGame(string message)
        {
            SetSliderValue(1f);
            SetText(statusText,
                string.IsNullOrWhiteSpace(message)
                    ? "资源准备完成，正在进入游戏"
                    : message);
            SetText(progressText, "下载进度 100%");
            SetText(sizeText, BuildCompletedSummary());
        }

        /// <summary>
        /// 没有差异资源时也完整展示一次启动进度。实际发生过下载时，
        /// YooAsset 已经提供了真实进度，因此这里直接完成。
        /// </summary>
        public IEnumerator PlayReadyAnimationAsync()
        {
            if (readyAnimationFinished)
            {
                yield break;
            }

            bool hasDownload =
                lastTotalBytes > 0 || lastTotalCount > 0;
            if (hasDownload)
            {
                SetSliderValue(1f);
                SetText(progressText, "下载进度 100%");
                readyAnimationFinished = true;
                yield break;
            }

            SetText(statusText, "当前已是最新资源");
            SetText(sizeText, "游戏初始化中...");
            SetSliderValue(0f);

            float elapsed = 0f;
            while (elapsed < NoDownloadCompletionDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float linearProgress = Mathf.Clamp01(
                    elapsed / NoDownloadCompletionDurationSeconds);
                float displayProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    linearProgress);

                SetSliderValue(displayProgress);
                SetText(progressText,
                    $"启动进度 {displayProgress * 100f:F1}%");
                yield return null;
            }

            SetSliderValue(1f);
            SetText(progressText, "启动进度 100%");
            readyAnimationFinished = true;
        }

        public void ShowFailure(string message)
        {
            SetText(statusText, "资源更新失败");
            SetText(progressText,
                string.IsNullOrWhiteSpace(message)
                    ? "请检查网络后重新启动游戏"
                    : message);

            if (lastTotalBytes > 0 || lastTotalCount > 0)
            {
                SetText(sizeText, BuildDownloadSummary());
            }
            else
            {
                SetText(sizeText, "未能获取远程资源信息");
            }
        }

        private void ResetView()
        {
            lastCurrentBytes = 0;
            lastTotalBytes = 0;
            lastCurrentCount = 0;
            lastTotalCount = 0;
            readyAnimationFinished = false;
            SetSliderValue(0f);
            SetText(statusText, "正在检查资源更新");
            SetText(progressText, "下载进度 0%");
            SetText(sizeText, "正在获取资源信息...");
        }

        private void OnStatusChanged(
            YooAssetLauncher.UpdateSnapshot snapshot)
        {
            lastCurrentBytes = Math.Max(0L,
                snapshot.CurrentDownloadBytes);
            lastTotalBytes = Math.Max(0L,
                snapshot.TotalDownloadBytes);
            lastCurrentCount = Math.Max(0,
                snapshot.CurrentDownloadCount);
            lastTotalCount = Math.Max(0,
                snapshot.TotalDownloadCount);

            bool hasDownload =
                lastTotalBytes > 0 || lastTotalCount > 0;
            float downloadProgress = CalculateDownloadProgress();

            if ((snapshot.Stage ==
                     YooAssetLauncher.UpdateStage.Ready &&
                 hasDownload) ||
                (snapshot.Stage ==
                     YooAssetLauncher.UpdateStage.Downloading &&
                 hasDownload && downloadProgress >= 1f))
            {
                downloadProgress = 1f;
            }

            SetSliderValue(downloadProgress);
            SetText(statusText, GetStatusMessage(snapshot));

            if (hasDownload)
            {
                SetText(progressText,
                    $"下载进度 {downloadProgress * 100f:F1}%");
                SetText(sizeText, BuildDownloadSummary());
            }
            else if (snapshot.Stage ==
                     YooAssetLauncher.UpdateStage.Ready)
            {
                SetSliderValue(0f);
                SetText(progressText, "启动进度 0%");
                SetText(sizeText, "当前没有需要下载的资源");
            }
            else
            {
                SetText(progressText, "下载进度 0%");
                SetText(sizeText, "正在获取资源信息...");
            }
        }

        private void OnDownloadError(DownloadErrorEventArgs args)
        {
            SetText(statusText, "资源下载异常，正在重试");
            SetText(progressText,
                string.IsNullOrWhiteSpace(args.ErrorInfo)
                    ? args.FileName
                    : args.ErrorInfo);
        }

        private string GetStatusMessage(
            YooAssetLauncher.UpdateSnapshot snapshot)
        {
            switch (snapshot.Stage)
            {
                case YooAssetLauncher.UpdateStage.Downloading:
                    return lastTotalBytes > 0 ||
                           lastTotalCount > 0
                        ? "正在下载游戏资源"
                        : "当前已是最新资源";
                case YooAssetLauncher.UpdateStage.Paused:
                    return "资源下载已暂停";
                case YooAssetLauncher.UpdateStage.ClearingCache:
                    return "正在整理资源缓存";
                case YooAssetLauncher.UpdateStage.Ready:
                    return "资源准备完成";
                case YooAssetLauncher.UpdateStage.Cancelled:
                    return "资源下载已取消";
                case YooAssetLauncher.UpdateStage.Failed:
                    return "资源更新失败";
                default:
                    return string.IsNullOrWhiteSpace(snapshot.Message)
                        ? "正在检查资源更新"
                        : snapshot.Message;
            }
        }

        private float CalculateDownloadProgress()
        {
            if (lastTotalBytes > 0)
            {
                return Mathf.Clamp01(
                    (float)((double)lastCurrentBytes /
                            lastTotalBytes));
            }

            if (lastTotalCount > 0)
            {
                return Mathf.Clamp01(
                    (float)lastCurrentCount / lastTotalCount);
            }

            return 0f;
        }

        private string BuildDownloadSummary()
        {
            return $"{FormatBytes(lastCurrentBytes)} / " +
                   $"{FormatBytes(lastTotalBytes)}    " +
                   $"{lastCurrentCount} / {lastTotalCount} 个文件";
        }

        private string BuildCompletedSummary()
        {
            if (lastTotalBytes <= 0 && lastTotalCount <= 0)
            {
                return "当前没有需要下载的资源";
            }

            lastCurrentBytes = lastTotalBytes;
            lastCurrentCount = lastTotalCount;
            return BuildDownloadSummary();
        }

        private void SetSliderValue(float value)
        {
            if (progressSlider != null)
            {
                progressSlider.SetValueWithoutNotify(
                    Mathf.Clamp01(value));
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static string FormatBytes(long bytes)
        {
            const long kb = 1024;
            const long mb = kb * 1024;
            const long gb = mb * 1024;

            if (bytes >= gb)
            {
                return $"{bytes / (double)gb:F2} GB";
            }

            if (bytes >= mb)
            {
                return $"{bytes / (double)mb:F2} MB";
            }

            if (bytes >= kb)
            {
                return $"{bytes / (double)kb:F2} KB";
            }

            return $"{bytes} B";
        }

        private void Unbind()
        {
            if (launcher == null)
            {
                return;
            }

            launcher.StatusChanged -= OnStatusChanged;
            launcher.DownloadError -= OnDownloadError;
            launcher = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
