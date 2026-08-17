#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LxyDemo.UIFramework.Editor
{
    internal sealed class UIEffectCodexRequest
    {
        public string codexCommand = "codex";
        public string projectRoot = string.Empty;
        public string imagePath = string.Empty;
        public string prompt = string.Empty;
        public string outputSchemaPath = string.Empty;
        public string outputPath = string.Empty;
    }

    internal sealed class UIEffectCodexRunResult
    {
        public bool succeeded;
        public bool canceled;
        public int exitCode;
        public string outputJson = string.Empty;
        public string error = string.Empty;
        public UIEffectCodexUsage usage;
    }

    [Serializable]
    internal sealed class UIEffectCodexUsage
    {
        public long input_tokens;
        public long cached_input_tokens;
        public long output_tokens;
        public long reasoning_output_tokens;

        public long TotalTokens => input_tokens + output_tokens;
    }

    /// <summary>
    /// Runs the official Codex non-interactive command without opening a
    /// terminal window. The agent receives read-only access; Unity remains
    /// responsible for validating and writing the returned UISchema.
    /// </summary>
    internal sealed class UIEffectCodexRunner : IDisposable
    {
        private const int MaximumErrorLength = 32768;

        private readonly ConcurrentQueue<string> progressMessages =
            new ConcurrentQueue<string>();
        private readonly object stateLock = new object();
        private readonly StringBuilder errorOutput = new StringBuilder();

        private Process process;
        private UIEffectCodexRequest request;
        private UIEffectCodexRunResult completedResult;
        private int isRunning;
        private int completionAvailable;
        private int cancelRequested;
        private bool disposed;
        private UIEffectCodexUsage usage;

        [Serializable]
        private sealed class CodexEvent
        {
            public string type;
            public UIEffectCodexUsage usage;
        }

        public bool IsRunning =>
            Interlocked.CompareExchange(ref isRunning, 0, 0) == 1;

        public void Start(UIEffectCodexRequest runRequest)
        {
            if (runRequest == null)
            {
                throw new ArgumentNullException(nameof(runRequest));
            }

            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(UIEffectCodexRunner));
            }

            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "已有 Codex UI 分析任务正在运行。");
            }

            ValidateRequest(runRequest);
            string executable = ResolveExecutable(
                runRequest.codexCommand);
            Directory.CreateDirectory(
                Path.GetDirectoryName(runRequest.outputSchemaPath));
            Directory.CreateDirectory(
                Path.GetDirectoryName(runRequest.outputPath));

            request = runRequest;
            completedResult = null;
            usage = null;
            errorOutput.Clear();
            while (progressMessages.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref completionAvailable, 0);
            Interlocked.Exchange(ref cancelRequested, 0);
            Interlocked.Exchange(ref isRunning, 1);

            ProcessStartInfo startInfo = CreateStartInfo(
                executable,
                runRequest);
            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            process.OutputDataReceived += HandleOutputDataReceived;
            process.ErrorDataReceived += HandleErrorDataReceived;
            process.Exited += HandleProcessExited;

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "无法启动 Codex 后台进程。");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.StandardInput.Write(runRequest.prompt);
                process.StandardInput.Close();
                progressMessages.Enqueue(
                    "Codex 已在后台启动，正在读取 Skill 和效果图…");
            }
            catch
            {
                Interlocked.Exchange(ref isRunning, 0);
                DisposeProcess();
                throw;
            }
        }

        public bool TryDequeueProgress(out string message)
        {
            return progressMessages.TryDequeue(out message);
        }

        public bool TryTakeResult(out UIEffectCodexRunResult result)
        {
            result = null;
            if (Interlocked.CompareExchange(
                    ref completionAvailable,
                    0,
                    1) != 1)
            {
                return false;
            }

            lock (stateLock)
            {
                result = completedResult;
                completedResult = null;
            }

            DisposeProcess();
            return true;
        }

        public void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            Interlocked.Exchange(ref cancelRequested, 1);
            progressMessages.Enqueue("正在取消 Codex 分析任务…");
            Process current = process;
            if (current == null)
            {
                return;
            }

            try
            {
                if (current.HasExited)
                {
                    return;
                }

#if UNITY_EDITOR_WIN
                var killInfo = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/PID {current.Id} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (Process killer = Process.Start(killInfo))
                {
                    killer?.WaitForExit(3000);
                }
#else
                current.Kill();
#endif
            }
            catch (Exception exception)
            {
                AppendError(exception.Message);
                try
                {
                    if (!current.HasExited)
                    {
                        current.Kill();
                    }
                }
                catch
                {
                    // The process may already have exited between checks.
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Cancel();
            DisposeProcess();
        }

        internal static string ResolveExecutable(string command)
        {
            string value = (command ?? string.Empty)
                .Trim()
                .Trim('"');
            if (value.Length == 0 ||
                value.IndexOfAny(new[] { '\r', '\n', '|', '&', '<', '>' }) >= 0)
            {
                throw new InvalidOperationException(
                    "Codex 命令无效。请填写 codex 或 Codex 可执行文件路径。");
            }

            if (File.Exists(value))
            {
                return Path.GetFullPath(value);
            }

            if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new FileNotFoundException(
                    "找不到 Codex 命令。",
                    value);
            }

            string pathValue =
                Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] extensions = IsWindows()
                ? new[] { ".exe", ".cmd", ".bat", ".ps1", string.Empty }
                : new[] { string.Empty };
            foreach (string folder in pathValue.Split(Path.PathSeparator))
            {
                string trimmedFolder = folder.Trim().Trim('"');
                if (trimmedFolder.Length == 0)
                {
                    continue;
                }

                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(
                        trimmedFolder,
                        value + extension);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }

            throw new FileNotFoundException(
                "找不到 Codex 命令。请确认已安装 Codex CLI，" +
                "或在窗口中填写 codex.cmd/codex.exe 的完整路径。",
                value);
        }

        private static ProcessStartInfo CreateStartInfo(
            string executable,
            UIEffectCodexRequest runRequest)
        {
            string arguments = string.Join(
                " ",
                "exec",
                "--json",
                "--ephemeral",
                "--sandbox",
                "read-only",
                "--cd",
                QuoteArgument(runRequest.projectRoot),
                "--image",
                QuoteArgument(runRequest.imagePath),
                "--output-schema",
                QuoteArgument(runRequest.outputSchemaPath),
                "--output-last-message",
                QuoteArgument(runRequest.outputPath),
                "-");

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = runRequest.projectRoot,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };

            if (string.Equals(
                    Path.GetExtension(executable),
                    ".ps1",
                    StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = IsWindows()
                    ? "powershell.exe"
                    : "pwsh";
                startInfo.Arguments = string.Join(
                    " ",
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    QuoteArgument(executable),
                    arguments);
            }

            return startInfo;
        }

        private void HandleOutputDataReceived(
            object sender,
            DataReceivedEventArgs eventArgs)
        {
            string line = eventArgs.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            string progress = GetProgressMessage(line);
            TryCaptureUsage(line);
            if (!string.IsNullOrWhiteSpace(progress))
            {
                progressMessages.Enqueue(progress);
            }
        }

        private void HandleErrorDataReceived(
            object sender,
            DataReceivedEventArgs eventArgs)
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                AppendError(eventArgs.Data);
            }
        }

        private void HandleProcessExited(object sender, EventArgs eventArgs)
        {
            int exitCode = -1;
            try
            {
                process?.WaitForExit();
                if (process != null)
                {
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception exception)
            {
                AppendError(exception.Message);
            }

            bool wasCanceled = Interlocked.CompareExchange(
                                   ref cancelRequested,
                                   0,
                                   0) == 1;
            string outputJson = string.Empty;
            if (!wasCanceled &&
                request != null &&
                File.Exists(request.outputPath))
            {
                try
                {
                    outputJson = File.ReadAllText(
                        request.outputPath,
                        Encoding.UTF8);
                }
                catch (Exception exception)
                {
                    AppendError(exception.Message);
                }
            }

            string error;
            lock (stateLock)
            {
                error = errorOutput.ToString().Trim();
                completedResult = new UIEffectCodexRunResult
                {
                    succeeded =
                        !wasCanceled &&
                        exitCode == 0 &&
                        !string.IsNullOrWhiteSpace(outputJson),
                    canceled = wasCanceled,
                    exitCode = exitCode,
                    outputJson = outputJson,
                    error = error,
                    usage = usage,
                };
            }

            Interlocked.Exchange(ref isRunning, 0);
            Interlocked.Exchange(ref completionAvailable, 1);
        }

        private void AppendError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (stateLock)
            {
                if (errorOutput.Length >= MaximumErrorLength)
                {
                    return;
                }

                int remaining = MaximumErrorLength - errorOutput.Length;
                string value = message.Length <= remaining
                    ? message
                    : message.Substring(0, remaining);
                errorOutput.AppendLine(value);
            }
        }

        private static string GetProgressMessage(string jsonLine)
        {
            if (jsonLine.IndexOf(
                    "\"type\":\"thread.started\"",
                    StringComparison.Ordinal) >= 0)
            {
                return "Codex 会话已建立，正在加载 unity-ui-generator…";
            }

            if (jsonLine.IndexOf(
                    "\"type\":\"turn.started\"",
                    StringComparison.Ordinal) >= 0)
            {
                return "正在分析效果图和项目 UI 资源…";
            }

            if (jsonLine.IndexOf(
                    "\"type\":\"item.started\"",
                    StringComparison.Ordinal) >= 0 &&
                jsonLine.IndexOf(
                    "command_execution",
                    StringComparison.Ordinal) >= 0)
            {
                return "正在检查项目资源和 UI 规范…";
            }

            if (jsonLine.IndexOf(
                    "\"type\":\"turn.completed\"",
                    StringComparison.Ordinal) >= 0)
            {
                return "AI 分析完成，正在验证 UISchema…";
            }

            if (jsonLine.IndexOf(
                    "\"type\":\"turn.failed\"",
                    StringComparison.Ordinal) >= 0 ||
                jsonLine.IndexOf(
                    "\"type\":\"error\"",
                    StringComparison.Ordinal) >= 0)
            {
                return "Codex 分析失败，正在收集错误信息…";
            }

            return string.Empty;
        }

        private void TryCaptureUsage(string jsonLine)
        {
            if (jsonLine.IndexOf(
                    "\"type\":\"turn.completed\"",
                    StringComparison.Ordinal) < 0)
            {
                return;
            }

            try
            {
                CodexEvent codexEvent =
                    JsonUtility.FromJson<CodexEvent>(jsonLine);
                if (codexEvent?.usage != null)
                {
                    usage = codexEvent.usage;
                }
            }
            catch
            {
                // Usage is diagnostic only; never fail Prefab generation
                // because a future Codex event shape changes.
            }
        }

        private static void ValidateRequest(UIEffectCodexRequest value)
        {
            if (string.IsNullOrWhiteSpace(value.projectRoot) ||
                !Directory.Exists(value.projectRoot))
            {
                throw new DirectoryNotFoundException(
                    "找不到 Unity 项目根目录。");
            }

            if (string.IsNullOrWhiteSpace(value.imagePath) ||
                !File.Exists(value.imagePath))
            {
                throw new FileNotFoundException(
                    "找不到待分析的效果图。",
                    value.imagePath);
            }

            if (string.IsNullOrWhiteSpace(value.prompt))
            {
                throw new InvalidOperationException(
                    "Codex 分析提示不能为空。");
            }

            if (string.IsNullOrWhiteSpace(value.outputSchemaPath) ||
                string.IsNullOrWhiteSpace(value.outputPath))
            {
                throw new InvalidOperationException(
                    "Codex 输出路径不能为空。");
            }
        }

        private static string QuoteArgument(string value)
        {
            value ??= string.Empty;
            if (value.Length > 0 &&
                value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) < 0)
            {
                return value;
            }

            var builder = new StringBuilder();
            builder.Append('"');
            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                backslashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private static bool IsWindows()
        {
            return Path.DirectorySeparatorChar == '\\';
        }

        private void DisposeProcess()
        {
            Process current = process;
            process = null;
            if (current == null)
            {
                return;
            }

            current.OutputDataReceived -= HandleOutputDataReceived;
            current.ErrorDataReceived -= HandleErrorDataReceived;
            current.Exited -= HandleProcessExited;
            current.Dispose();
        }
    }
}
#endif
