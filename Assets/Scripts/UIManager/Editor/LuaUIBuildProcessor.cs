#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            StageLuaFiles();
        }

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
}
#endif
