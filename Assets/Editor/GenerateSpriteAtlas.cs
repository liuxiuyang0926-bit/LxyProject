using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace LxyDemo.EditorTools
{
    /// <summary>
    /// 按源目录层级自动生成 SpriteAtlas。
    /// 每个包含至少两张有效 Sprite 的目录生成一份图集。
    /// </summary>
    internal static class GenerateSpriteAtlas
    {
        private const int mMaxAtlasItemWidth = 300;
        private const int mMaxAtlasItemHeight = 300;

        private const string mAtlasSrcFolder =
            "GameResources/UIAtlas/AtlasScr";

        private const string mAtlasFolder =
            "GameResources/UIAtlas/UIAtlas_AutoGen";

        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".jpg",
                ".jpeg"
            };

        [MenuItem("工具/图集/Generate Sprite Atlas", false, 2)]
        private static void Execute()
        {
            string sourceAssetFolder =
                NormalizeAssetPath("Assets/" + mAtlasSrcFolder);
            string outputAssetFolder =
                NormalizeAssetPath("Assets/" + mAtlasFolder);

            if (!AssetDatabase.IsValidFolder(sourceAssetFolder))
            {
                string message =
                    $"图集源目录不存在：{sourceAssetFolder}";
                Debug.LogError("[SpriteAtlas] " + message);
                EditorUtility.DisplayDialog(
                    "Generate Sprite Atlas",
                    message,
                    "确定");
                return;
            }

            EnsureAssetFolder(outputAssetFolder);

            string sourceAbsoluteFolder =
                AssetPathToAbsolutePath(sourceAssetFolder);
            List<AtlasGroup> groups;
            try
            {
                groups = CollectAtlasGroups(
                    sourceAbsoluteFolder,
                    sourceAssetFolder);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Generate Sprite Atlas",
                    exception.Message,
                    "确定");
                return;
            }

            var generatedPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            bool cancelled = false;

            try
            {
                for (int index = 0; index < groups.Count; index++)
                {
                    AtlasGroup group = groups[index];
                    if (!Application.isBatchMode &&
                        EditorUtility.DisplayCancelableProgressBar(
                            "Generate Sprite Atlas",
                            $"[{index + 1}/{groups.Count}] " +
                            group.AtlasName,
                            groups.Count == 0
                                ? 1f
                                : index / (float)groups.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    GenerateAtlas(
                        group,
                        outputAssetFolder,
                        generatedPaths);
                }

                if (cancelled)
                {
                    Debug.LogWarning(
                        "[SpriteAtlas] 用户取消生成，" +
                        "已保留未处理的旧图集。");
                    return;
                }

                DeleteStaleAtlases(
                    outputAssetFolder,
                    generatedPaths);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[SpriteAtlas] 生成完成：" +
                    $"{generatedPaths.Count} 个图集，" +
                    $"输出目录 {outputAssetFolder}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Generate Sprite Atlas",
                    "生成失败：\n" + exception.Message,
                    "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<AtlasGroup> CollectAtlasGroups(
            string sourceAbsoluteFolder,
            string sourceAssetFolder)
        {
            string normalizedRoot =
                NormalizeFileSystemPath(sourceAbsoluteFolder)
                    .TrimEnd('/');
            string rootPrefix = normalizedRoot + "/";

            var directories = new List<string> { normalizedRoot };
            directories.AddRange(
                Directory.GetDirectories(
                        normalizedRoot,
                        "*",
                        SearchOption.AllDirectories)
                    .Select(NormalizeFileSystemPath)
                    .OrderBy(path => path, StringComparer.Ordinal));

            var atlasNameOwners = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var groups = new List<AtlasGroup>();

            foreach (string directory in directories)
            {
                string relativeFolder =
                    string.Equals(
                        directory,
                        normalizedRoot,
                        StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : directory.Substring(rootPrefix.Length);
                string atlasName = BuildAtlasName(
                    relativeFolder,
                    Path.GetFileName(normalizedRoot));

                if (atlasNameOwners.TryGetValue(
                        atlasName,
                        out string owner))
                {
                    throw new InvalidOperationException(
                        $"图集名称重复：{atlasName}\n" +
                        $"目录一：{owner}\n目录二：{directory}");
                }

                atlasNameOwners.Add(atlasName, directory);

                string assetFolder = string.IsNullOrEmpty(relativeFolder)
                    ? sourceAssetFolder
                    : NormalizeAssetPath(
                        sourceAssetFolder + "/" + relativeFolder);
                string[] texturePaths =
                    Directory.GetFiles(
                            directory,
                            "*",
                            SearchOption.TopDirectoryOnly)
                        .Where(path => SupportedExtensions.Contains(
                            Path.GetExtension(path)))
                        .Select(AbsolutePathToAssetPath)
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();

                groups.Add(new AtlasGroup(
                    atlasName,
                    assetFolder,
                    texturePaths));
            }

            return groups;
        }

        private static void GenerateAtlas(
            AtlasGroup group,
            string outputAssetFolder,
            HashSet<string> generatedPaths)
        {
            var packables = new List<Object>();
            foreach (string texturePath in group.TexturePaths)
            {
                var importer = AssetImporter.GetAtPath(texturePath)
                    as TextureImporter;
                if (importer == null ||
                    importer.textureType != TextureImporterType.Sprite)
                {
                    Debug.LogWarning(
                        $"[SpriteAtlas] 跳过非 Sprite 纹理：" +
                        texturePath);
                    continue;
                }

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        texturePath);
                if (texture == null)
                {
                    Debug.LogWarning(
                        $"[SpriteAtlas] 无法加载纹理：{texturePath}");
                    continue;
                }

                if (texture.width > mMaxAtlasItemWidth ||
                    texture.height > mMaxAtlasItemHeight)
                {
                    Debug.Log(
                        $"[SpriteAtlas] 跳过超尺寸纹理：" +
                        $"{texturePath} " +
                        $"({texture.width}x{texture.height})");
                    continue;
                }

                packables.Add(texture);
            }

            if (packables.Count <= 0)
            {
                Debug.Log(
                    $"[SpriteAtlas] 跳过 {group.AssetFolder}：" +
                    $"有效 Sprite 数量为 {packables.Count}，" +
                    "至少需要 1 张。");
                return;
            }

            string atlasPath = NormalizeAssetPath(
                $"{outputAssetFolder}/{group.AtlasName}.spriteatlas");
            SpriteAtlas atlas =
                AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }
            else
            {
                Object[] oldPackables = atlas.GetPackables();
                if (oldPackables.Length > 0)
                {
                    atlas.Remove(oldPackables);
                }
            }

            SetUpAtlasInfo(atlas, true, 2048);
            atlas.Add(packables.ToArray());
            EditorUtility.SetDirty(atlas);
            generatedPaths.Add(atlasPath);

            Debug.Log(
                $"[SpriteAtlas] {group.AtlasName}：" +
                $"{packables.Count} 张 Sprite");
        }

        private static void DeleteStaleAtlases(
            string outputAssetFolder,
            HashSet<string> generatedPaths)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:SpriteAtlas",
                new[] { outputAssetFolder });
            foreach (string guid in guids)
            {
                string atlasPath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (generatedPaths.Contains(atlasPath))
                {
                    continue;
                }

                Debug.Log(
                    $"[SpriteAtlas] 删除失效图集：{atlasPath}");
                AssetDatabase.DeleteAsset(atlasPath);
            }
        }

        private static void SetUpAtlasInfo(
            SpriteAtlas atlas,
            bool isIncludeInBuild,
            int maxSpriteAtlasSize = 1024)
        {
            atlas.SetIncludeInBuild(isIncludeInBuild);
            atlas.SetPackingSettings(
                new SpriteAtlasPackingSettings
                {
                    blockOffset = 1,
                    enableRotation = false,
                    enableTightPacking = false,
                    padding = 4,
                    enableAlphaDilation = true
                });

            atlas.SetTextureSettings(
                new SpriteAtlasTextureSettings
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear
                });

            atlas.SetPlatformSettings(
                new TextureImporterPlatformSettings
                {
                    name = "DefaultTexturePlatform",
                    overridden = false,
                    maxTextureSize = maxSpriteAtlasSize,
                    format = TextureImporterFormat.Automatic,
                    crunchedCompression = false,
                    textureCompression =
                        TextureImporterCompression.Compressed,
                    compressionQuality = 50
                });
        }

        private static string BuildAtlasName(
            string relativeFolder,
            string rootFolderName)
        {
            string suffix = string.IsNullOrWhiteSpace(relativeFolder)
                ? rootFolderName
                : relativeFolder.Replace('/', '_');
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                suffix = suffix.Replace(invalidChar, '_');
            }

            return "Atlas_" + suffix;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = NormalizeAssetPath(assetFolder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = NormalizeAssetPath(
                Path.GetDirectoryName(normalized) ?? "Assets");
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(
                parent,
                Path.GetFileName(normalized));
        }

        private static string AssetPathToAbsolutePath(
            string assetPath)
        {
            string relative = NormalizeAssetPath(assetPath)
                .Substring("Assets/".Length);
            return NormalizeFileSystemPath(
                Path.GetFullPath(
                    Path.Combine(Application.dataPath, relative)));
        }

        private static string AbsolutePathToAssetPath(
            string absolutePath)
        {
            string normalizedPath =
                NormalizeFileSystemPath(absolutePath);
            string normalizedDataPath =
                NormalizeFileSystemPath(Application.dataPath)
                    .TrimEnd('/');
            if (!normalizedPath.StartsWith(
                    normalizedDataPath + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "纹理路径不在 Assets 目录中：" +
                    normalizedPath);
            }

            return "Assets" +
                   normalizedPath.Substring(normalizedDataPath.Length);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static string NormalizeFileSystemPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private sealed class AtlasGroup
        {
            public AtlasGroup(
                string atlasName,
                string assetFolder,
                string[] texturePaths)
            {
                AtlasName = atlasName;
                AssetFolder = assetFolder;
                TexturePaths = texturePaths;
            }

            public string AtlasName { get; }
            public string AssetFolder { get; }
            public string[] TexturePaths { get; }
        }
    }
}
