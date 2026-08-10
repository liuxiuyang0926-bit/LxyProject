using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Main.Editor
{
    public static class HybridCLRAssetSynchronizer
    {
        private const string EntryAssemblyName = "Game.HotUpdate";
        private const string AotAssetDirectory =
            "Assets/GameResources/HybridCLR/AOT";
        private const string HotUpdateAssetDirectory =
            "Assets/GameResources/HybridCLR/HotUpdate";
        private const string RuntimeManifestPath =
            "Assets/GameResources/HybridCLR/" +
            "HybridCLRAssemblyManifest.bytes";

        [MenuItem(
            "工具/HybridCLR/生成全部并同步到 YooAsset",
            priority = 100)]
        public static void GenerateAllAndSync()
        {
            PrebuildCommand.GenerateAll();
            SyncActiveBuildTarget();
        }

        [MenuItem(
            "工具/HybridCLR/同步现有产物到 YooAsset",
            priority = 101)]
        public static void SyncActiveBuildTarget()
        {
            BuildTarget target =
                EditorUserBuildSettings.activeBuildTarget;

            try
            {
                Sync(target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public static void Sync(BuildTarget target)
        {
            IReadOnlyList<string> aotDlls =
                ReadPatchedAotAssemblyList();
            IReadOnlyList<string> hotUpdateDlls =
                GetConfiguredHotUpdateDlls();

            ValidateEntryAssembly(hotUpdateDlls);
            ValidateHotUpdateLoadOrder(hotUpdateDlls);

            string projectDirectory = SettingsUtil.ProjectDir;
            string aotSourceDirectory = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    SettingsUtil
                        .GetAssembliesPostIl2CppStripDir(target)));
            string hotUpdateSourceDirectory = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    SettingsUtil
                        .GetHotUpdateDllsOutputDirByTarget(target)));

            IReadOnlyList<CopyItem> aotCopies = BuildCopyItems(
                aotSourceDirectory,
                AotAssetDirectory,
                aotDlls);
            IReadOnlyList<CopyItem> hotUpdateCopies = BuildCopyItems(
                hotUpdateSourceDirectory,
                HotUpdateAssetDirectory,
                hotUpdateDlls);

            ValidateSources(aotCopies, "AOT 元数据");
            ValidateSources(hotUpdateCopies, "热更新");

            int changedCount = 0;
            changedCount += SynchronizeDirectory(
                AotAssetDirectory,
                aotCopies);
            changedCount += SynchronizeDirectory(
                HotUpdateAssetDirectory,
                hotUpdateCopies);

            if (WriteRuntimeManifest(
                    aotDlls,
                    hotUpdateDlls))
            {
                changedCount++;
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);

            Debug.Log(
                $"[HybridCLR Sync] {target} 同步完成：" +
                $"AOT={aotDlls.Count}，" +
                $"HotUpdate={hotUpdateDlls.Count}，" +
                $"变更={changedCount}。\n" +
                "下一步请重新构建 YooAsset Bundle。");
        }

        private static IReadOnlyList<string>
            ReadPatchedAotAssemblyList()
        {
            string relativePath = SettingsUtil.HybridCLRSettings
                .outputAOTGenericReferenceFile;
            string sourcePath = Path.GetFullPath(
                Path.Combine(Application.dataPath, relativePath));

            if (!File.Exists(sourcePath))
            {
                throw new BuildFailedException(
                    "没有找到 AOTGenericReferences.cs：" +
                    sourcePath + "。请先执行 HybridCLR/Generate/All。");
            }

            string source = File.ReadAllText(sourcePath);
            int fieldIndex = source.IndexOf(
                "PatchedAOTAssemblyList",
                StringComparison.Ordinal);
            int blockStart = fieldIndex < 0
                ? -1
                : source.IndexOf('{', fieldIndex);
            int blockEnd = blockStart < 0
                ? -1
                : source.IndexOf("};", blockStart,
                    StringComparison.Ordinal);

            if (fieldIndex < 0 || blockStart < 0 || blockEnd < 0)
            {
                throw new BuildFailedException(
                    "无法解析 PatchedAOTAssemblyList：" +
                    sourcePath);
            }

            string listBlock = source.Substring(
                blockStart,
                blockEnd - blockStart);

            return Regex.Matches(
                    listBlock,
                    "\\\"(?<name>[^\\\"]+\\.dll)\\\"")
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<string>
            GetConfiguredHotUpdateDlls()
        {
            string entryDll = EntryAssemblyName + ".dll";
            var dlls = SettingsUtil
                .HotUpdateAssemblyFilesExcludePreserved
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .Where(name => !string.Equals(
                    name,
                    entryDll,
                    StringComparison.Ordinal))
                .ToList();

            if (SettingsUtil.HotUpdateAssemblyFilesExcludePreserved
                .Contains(entryDll))
            {
                // 入口最后加载；此前的 DLL 可作为它的热更新依赖。
                dlls.Add(entryDll);
            }

            return dlls;
        }

        private static void ValidateEntryAssembly(
            IReadOnlyList<string> hotUpdateDlls)
        {
            string entryDll = EntryAssemblyName + ".dll";
            if (!hotUpdateDlls.Contains(entryDll))
            {
                throw new BuildFailedException(
                    $"HybridCLR Settings 没有配置入口程序集：" +
                    $"{EntryAssemblyName}。请将其 asmdef 添加到 " +
                    "Hot Update Assembly Definitions。");
            }
        }

        private static void ValidateHotUpdateLoadOrder(
            IReadOnlyList<string> hotUpdateDlls)
        {
            // Assembly.Load 不会替我们修正热更新程序集的依赖顺序。
            // 这里把当前框架的稳定拓扑固化为构建期检查，避免发布后
            // 才出现 FileNotFoundException。
            ValidateDependencyBefore(
                hotUpdateDlls,
                "Game.Common.dll",
                "Game.Lua.dll");
            ValidateDependencyBefore(
                hotUpdateDlls,
                "Game.Lua.dll",
                "Game.UI.dll");
            ValidateDependencyBefore(
                hotUpdateDlls,
                "Game.UI.dll",
                "Assembly-CSharp.dll");
            ValidateDependencyBefore(
                hotUpdateDlls,
                "Game.UI.dll",
                EntryAssemblyName + ".dll");
        }

        private static void ValidateDependencyBefore(
            IReadOnlyList<string> hotUpdateDlls,
            string dependency,
            string consumer)
        {
            int dependencyIndex = FindIndex(
                hotUpdateDlls,
                dependency);
            int consumerIndex = FindIndex(
                hotUpdateDlls,
                consumer);
            if (dependencyIndex < 0 || consumerIndex < 0 ||
                dependencyIndex < consumerIndex)
            {
                return;
            }

            throw new BuildFailedException(
                $"热更新 DLL 加载顺序错误：{dependency} 必须位于 " +
                $"{consumer} 之前。请调整 HybridCLR Settings 中的 " +
                "Hot Update Assemblies 顺序。");
        }

        private static int FindIndex(
            IReadOnlyList<string> values,
            string target)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(
                        values[index],
                        target,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static IReadOnlyList<CopyItem> BuildCopyItems(
            string sourceDirectory,
            string destinationAssetDirectory,
            IReadOnlyList<string> dllNames)
        {
            return dllNames
                .Select(dllName => new CopyItem(
                    Path.Combine(sourceDirectory, dllName),
                    destinationAssetDirectory + "/" +
                    dllName + ".bytes"))
                .ToArray();
        }

        private static void ValidateSources(
            IEnumerable<CopyItem> copies,
            string category)
        {
            string[] missing = copies
                .Where(item => !File.Exists(item.SourcePath))
                .Select(item => item.SourcePath)
                .ToArray();

            if (missing.Length == 0)
            {
                return;
            }

            throw new BuildFailedException(
                $"缺少{category} DLL：\n" +
                string.Join("\n", missing) +
                "\n请先针对当前 BuildTarget 执行 " +
                "HybridCLR/Generate/All。");
        }

        private static int SynchronizeDirectory(
            string destinationAssetDirectory,
            IReadOnlyList<CopyItem> copies)
        {
            string absoluteDirectory = AssetPathToAbsolutePath(
                destinationAssetDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var expectedAssetPaths = new HashSet<string>(
                copies.Select(item => item.DestinationAssetPath),
                StringComparer.OrdinalIgnoreCase);
            int changedCount = 0;

            foreach (string existingPath in Directory.GetFiles(
                         absoluteDirectory,
                         "*.dll.bytes",
                         SearchOption.TopDirectoryOnly))
            {
                string assetPath = AbsolutePathToAssetPath(existingPath);
                if (expectedAssetPaths.Contains(assetPath))
                {
                    continue;
                }

                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    throw new IOException(
                        "无法删除过期 HybridCLR 资源：" + assetPath);
                }

                changedCount++;
            }

            foreach (CopyItem item in copies)
            {
                string destinationPath = AssetPathToAbsolutePath(
                    item.DestinationAssetPath);
                if (FilesEqual(item.SourcePath, destinationPath))
                {
                    continue;
                }

                File.Copy(item.SourcePath, destinationPath, true);
                changedCount++;
            }

            return changedCount;
        }

        private static bool WriteRuntimeManifest(
            IReadOnlyList<string> aotDlls,
            IReadOnlyList<string> hotUpdateDlls)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine(
                $"  \"entryAssemblyName\": " +
                $"\"{Escape(EntryAssemblyName)}\",");
            AppendJsonArray(
                builder,
                "aotMetadataDlls",
                aotDlls,
                true);
            AppendJsonArray(
                builder,
                "hotUpdateDlls",
                hotUpdateDlls,
                false);
            builder.AppendLine("}");

            string absolutePath = AssetPathToAbsolutePath(
                RuntimeManifestPath);
            string content = builder.ToString();

            if (File.Exists(absolutePath) &&
                string.Equals(
                    File.ReadAllText(absolutePath),
                    content,
                    StringComparison.Ordinal))
            {
                return false;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(absolutePath) ??
                throw new InvalidOperationException(
                    "运行时清单目录无效。"));
            File.WriteAllText(
                absolutePath,
                content,
                new UTF8Encoding(false));
            return true;
        }

        private static void AppendJsonArray(
            StringBuilder builder,
            string fieldName,
            IReadOnlyList<string> values,
            bool appendComma)
        {
            builder.AppendLine($"  \"{fieldName}\": [");
            for (int index = 0; index < values.Count; index++)
            {
                builder.AppendLine(
                    $"    \"{Escape(values[index])}\"" +
                    (index < values.Count - 1 ? "," : string.Empty));
            }

            builder.AppendLine(appendComma ? "  ]," : "  ]");
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static bool FilesEqual(
            string firstPath,
            string secondPath)
        {
            if (!File.Exists(secondPath))
            {
                return false;
            }

            var firstInfo = new FileInfo(firstPath);
            var secondInfo = new FileInfo(secondPath);
            if (firstInfo.Length != secondInfo.Length)
            {
                return false;
            }

            const int BufferSize = 81920;
            var firstBuffer = new byte[BufferSize];
            var secondBuffer = new byte[BufferSize];

            using (FileStream first = File.OpenRead(firstPath))
            using (FileStream second = File.OpenRead(secondPath))
            {
                while (true)
                {
                    int firstRead = first.Read(
                        firstBuffer,
                        0,
                        firstBuffer.Length);
                    int secondRead = second.Read(
                        secondBuffer,
                        0,
                        secondBuffer.Length);

                    if (firstRead != secondRead)
                    {
                        return false;
                    }

                    if (firstRead == 0)
                    {
                        return true;
                    }

                    for (int index = 0; index < firstRead; index++)
                    {
                        if (firstBuffer[index] != secondBuffer[index])
                        {
                            return false;
                        }
                    }
                }
            }
        }

        private static string AssetPathToAbsolutePath(
            string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(SettingsUtil.ProjectDir, assetPath));
        }

        private static string AbsolutePathToAssetPath(
            string absolutePath)
        {
            string projectPath = Path.GetFullPath(
                    SettingsUtil.ProjectDir)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(absolutePath);

            if (!fullPath.StartsWith(
                    projectPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "路径不在 Unity 工程内：" + absolutePath);
            }

            return fullPath
                .Substring(projectPath.Length)
                .Replace('\\', '/');
        }

        private readonly struct CopyItem
        {
            public CopyItem(
                string sourcePath,
                string destinationAssetPath)
            {
                SourcePath = sourcePath;
                DestinationAssetPath = destinationAssetPath;
            }

            public string SourcePath { get; }
            public string DestinationAssetPath { get; }
        }
    }
}
