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
    public sealed class LuaUIBuildProcessor :
        IPreprocessBuildWithReport
    {
        private const string TargetAssetRoot =
            "Assets/GameResources/Lua";

        /// <summary>
        /// 向调用方提供callbackOrder。
        /// </summary>
        public int callbackOrder => -1000;

        /// <summary>
        /// 响应Preprocess构建事件。
        /// </summary>
        public void OnPreprocessBuild(
            UnityEditor.Build.Reporting.BuildReport report)
        {
            StageLuaFiles();
        }

        /// <summary>
        /// 执行阶段Lua文件相关逻辑。
        /// </summary>
        [MenuItem(
            "工具/UI工具/同步Lua构建资源",
            priority = 30)]
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
        /// 执行清理StagedLua文件相关逻辑。
        /// </summary>
        [MenuItem(
            "工具/UI工具/清理Lua构建资源",
            priority = 31)]
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
    /// 监听 YooAsset Bundle Builder 的 Build 按钮。
    /// 只有用户实际点击按钮时才同步 Lua，成功后继续 YooAsset 原构建逻辑。
    /// </summary>
    [InitializeOnLoad]
    internal static class YooAssetBundleBuilderClickHook
    {
        private const string RemovedPipelineName =
            "ScriptableBuildPipelineWithLua";

        private static readonly HashSet<Button> HookedButtons =
            new HashSet<Button>();

        /// <summary>
        /// 创建YooAssetBundleBuilderClickHook实例。
        /// </summary>
        static YooAssetBundleBuilderClickHook()
        {
            RestoreRemovedPipeline();
            EditorApplication.update += BindBuildButtons;
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

                buildButton.RegisterCallback<PointerUpEvent>(
                    OnBuildButtonPointerUp,
                    TrickleDown.TrickleDown);
                buildButton.RegisterCallback<NavigationSubmitEvent>(
                    OnBuildButtonSubmit,
                    TrickleDown.TrickleDown);
            }
        }

        /// <summary>
        /// 响应构建按钮PointerUp事件。
        /// </summary>
        private static void OnBuildButtonPointerUp(
            PointerUpEvent evt)
        {
            if (evt.button == 0)
            {
                SynchronizeLuaBeforeBuild(evt);
            }
        }

        /// <summary>
        /// 响应构建按钮Submit事件。
        /// </summary>
        private static void OnBuildButtonSubmit(
            NavigationSubmitEvent evt)
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
    }
}
#endif
