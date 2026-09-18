#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace LxyDemo.UIFramework.Editor
{
    /// <summary>
    /// 将项目根目录 Lua 文件同步为 GameResources 下的 TextAsset。
    /// 编辑器由 AssetDatabase 加载，Player 由 YooAsset 加载。
    /// </summary>
    public static class LuaUIBuildProcessor
    {
        private const string TargetAssetRoot =
            "Assets/GameResources/Lua";

        /// <summary>
        /// 仅在构建 YooAsset Bundle 前临时同步 Lua 文件。
        /// </summary>
        public static void StageLuaFiles()
        {
            CleanupStagedLuaFiles();

            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string sourceRoot =
                Path.Combine(projectRoot, "Lua");
            if (!Directory.Exists(sourceRoot))
            {
                throw new BuildFailedException(
                    $"Lua 根目录不存在：{sourceRoot}");
            }

            string targetRoot =
                Path.Combine(projectRoot, TargetAssetRoot);
            Directory.CreateDirectory(targetRoot);
            int copiedCount = 0;
            foreach (string sourcePath in Directory.GetFiles(
                         sourceRoot,
                         "*.lua",
                         SearchOption.AllDirectories))
            {
                string relativePath =
                    sourcePath.Substring(sourceRoot.Length)
                        .TrimStart(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
                string targetPath = Path.Combine(
                    targetRoot,
                    relativePath + ".bytes");
                string targetFolder =
                    Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }
                File.Copy(sourcePath, targetPath, true);
                copiedCount++;
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"[Lua UI Build] 已同步 {copiedCount} 个 Lua 热更文件到 " +
                TargetAssetRoot + "。");
        }

        /// <summary>
        /// 删除临时同步到 GameResources 的 Lua 构建输入。
        /// </summary>
        public static void CleanupStagedLuaFiles()
        {
            if (AssetDatabase.IsValidFolder(TargetAssetRoot))
            {
                AssetDatabase.DeleteAsset(TargetAssetRoot);
                AssetDatabase.Refresh();
                return;
            }

            string absolutePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                TargetAssetRoot));
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }
        }
    }

    /// <summary>
    /// 监听 YooAsset Bundle Builder 的 Build 按钮。Lua 只在当前 Bundle
    /// 构建期间存在于 GameResources，构建完成（包括失败）后自动清理。
    /// </summary>
    [InitializeOnLoad]
    internal static class YooAssetBundleBuilderClickHook
    {
        private const string RemovedPipelineName =
            "ScriptableBuildPipelineWithLua";

        private static readonly HashSet<Button> HookedButtons =
            new HashSet<Button>();
        private static bool cleanupScheduled;

        /// <summary>
        /// 创建YooAssetBundleBuilderClickHook实例。
        /// </summary>
        static YooAssetBundleBuilderClickHook()
        {
            RestoreRemovedPipeline();
            EditorApplication.delayCall += CleanupStaleLuaFiles;
            EditorApplication.update += BindBuildButtons;
        }

        private static void CleanupStaleLuaFiles()
        {
            try
            {
                LuaUIBuildProcessor.CleanupStagedLuaFiles();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 执行RestoreRemovedPipeline相关逻辑。
        /// </summary>
        private static void RestoreRemovedPipeline()
        {
            if (!BundleCollectorSettingData.HasSettingAsset())
            {
                return;
            }

            string standardPipeline =
                EBuildPipeline.ScriptableBuildPipeline.ToString();
            foreach (BundleCollectorPackage package in
                     BundleCollectorSettingData.Setting.Packages)
            {
                string currentPipeline =
                    BundleBuilderSetting.GetPackageBuildPipeline(
                        package.PackageName);
                if (currentPipeline == RemovedPipelineName)
                {
                    BundleBuilderSetting.SetPackageBuildPipeline(
                        package.PackageName,
                        standardPipeline);
                }
            }
        }

        /// <summary>
        /// 执行绑定构建Buttons相关逻辑。
        /// </summary>
        private static void BindBuildButtons()
        {
            BundleBuilderWindow[] windows =
                Resources.FindObjectsOfTypeAll<BundleBuilderWindow>();
            foreach (BundleBuilderWindow window in windows)
            {
                Button buildButton =
                    window.rootVisualElement.Q<Button>("Build");
                if (buildButton == null ||
                    !HookedButtons.Add(buildButton))
                {
                    continue;
                }

                buildButton.RegisterCallback<ClickEvent>(
                    OnBuildButtonClick,
                    TrickleDown.TrickleDown);
            }
        }

        /// <summary>
        /// 响应构建按钮点击事件。
        /// </summary>
        private static void OnBuildButtonClick(ClickEvent evt)
        {
            SynchronizeLuaBeforeBuild(evt);
        }

        /// <summary>
        /// 执行同步LuaBefore构建相关逻辑。
        /// </summary>
        private static void SynchronizeLuaBeforeBuild(EventBase evt)
        {
            try
            {
                Debug.Log(
                    "[Lua UI Build] 点击 YooAsset Build，" +
                    "开始同步 Lua 资源。"
                );
                LuaUIBuildProcessor.StageLuaFiles();
                ScheduleCleanupAfterBundleBuild();
            }
            catch (Exception exception)
            {
                evt.StopImmediatePropagation();
                Debug.LogError(
                    "[Lua UI Build] Lua 资源同步失败，" +
                    "已阻止本次 YooAsset 构建。\n" +
                    exception
                );
            }
        }

        private static void ScheduleCleanupAfterBundleBuild()
        {
            if (cleanupScheduled)
            {
                return;
            }

            cleanupScheduled = true;
            // Bundle Builder itself queues its synchronous ExecuteBuild with
            // EditorApplication.delayCall. Queue once more so cleanup runs after
            // that build delegate, while still running when the build throws.
            EditorApplication.delayCall += QueueLuaCleanup;
        }

        private static void QueueLuaCleanup()
        {
            EditorApplication.delayCall += CleanupLuaAfterBundleBuild;
        }

        private static void CleanupLuaAfterBundleBuild()
        {
            cleanupScheduled = false;
            try
            {
                LuaUIBuildProcessor.CleanupStagedLuaFiles();
                Debug.Log(
                    "[Lua UI Build] YooAsset Bundle 构建结束，" +
                    "已清理 GameResources/Lua 临时资源。"
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif
