using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace LxyDemo.EditorTools
{
    /// <summary>
    /// 固化 DefaultPackage 的首包/启动强更交付策略。YooAsset 的 Group 只负责
    /// 组织资源，真正决定首包复制范围的是 BundledCopyOption + Tag。
    /// </summary>
    [InitializeOnLoad]
    internal static class YooAssetContentPolicy
    {
        private const string PackageName = "DefaultPackage";
        private const string PipelineName =
            "ScriptableBuildPipeline";

        static YooAssetContentPolicy()
        {
            EditorApplication.delayCall +=
                ApplyBuildSettingsSilently;
        }

        [MenuItem("工具/YooAsset/应用首包与全量强更策略", false, 20)]
        private static void ApplyBuildSettings()
        {
            ApplyBuildSettingsSilently();
            Debug.Log(
                "[YooAssetPolicy] 已设置首包复制策略：" +
                "ClearAndCopyByTags / Builtin。构建资源包时只会把 " +
                "Builtin Bundle 和 Manifest 复制到 StreamingAssets。"
            );
        }

        [MenuItem("工具/YooAsset/验证首包与全量强更策略", false, 21)]
        private static void ValidatePolicy()
        {
            ApplyBuildSettingsSilently();

            BundleCollectorSetting setting =
                BundleCollectorSettingData.Setting;
            BundleCollectorPackage package =
                setting.GetPackage(PackageName);
            if (package == null)
            {
                throw new InvalidOperationException(
                    $"找不到 YooAsset Package：{PackageName}");
            }

            ValidateGroup(package, "Builtin",
                "Builtin;Mandatory");
            ValidateGroup(package, "Mandatory", "Mandatory");

            CollectResult result = setting.BeginCollect(
                PackageName,
                false,
                true);
            var mainAssets = result.CollectAssets
                .Where(asset => asset.CollectorType ==
                                ECollectorType.MainAssetCollector)
                .ToArray();
            string[] deliveryTags =
            {
                YooAssetContentTags.Builtin,
                YooAssetContentTags.Mandatory
            };
            var unclassified = mainAssets
                .Where(asset => !asset.AssetTags.Any(
                    tag => deliveryTags.Contains(tag)))
                .Select(asset => asset.AssetInfo.AssetPath)
                .ToArray();
            if (unclassified.Length > 0)
            {
                throw new InvalidOperationException(
                    "存在未划分交付层的主资源：\n" +
                    string.Join("\n", unclassified));
            }

            int builtinCount = CountTag(
                mainAssets,
                YooAssetContentTags.Builtin);
            int mandatoryCount = CountTag(
                mainAssets,
                YooAssetContentTags.Mandatory);
            Debug.Log(
                $"[YooAssetPolicy] 验证通过。主资源：" +
                $"Builtin={builtinCount}，" +
                $"Mandatory={mandatoryCount}（包含 Builtin）。"
            );

            WarnIfBundledArtifactsAreStale(setting);
        }

        private static void ApplyBuildSettingsSilently()
        {
            BundleBuilderSetting.SetPackageBundledCopyOption(
                PackageName,
                PipelineName,
                EBundledCopyOption.ClearAndCopyByTags);
            BundleBuilderSetting.SetPackageBundledCopyParams(
                PackageName,
                PipelineName,
                YooAssetContentTags.Builtin);
        }

        internal static void ApplyBuildSettingsForAutomation()
        {
            ApplyBuildSettingsSilently();
        }

        private static void ValidateGroup(
            BundleCollectorPackage package,
            string groupName,
            string expectedTags)
        {
            BundleCollectorGroup group = package.Groups.FirstOrDefault(
                value => string.Equals(
                    value.GroupName,
                    groupName,
                    StringComparison.Ordinal));
            if (group == null)
            {
                throw new InvalidOperationException(
                    $"缺少 YooAsset Group：{groupName}");
            }

            if (!string.Equals(
                    group.AssetTags,
                    expectedTags,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Group {groupName} 标签应为“{expectedTags}”，" +
                    $"当前为“{group.AssetTags}”。");
            }
        }

        private static int CountTag(
            System.Collections.Generic.IEnumerable<CollectAssetInfo> assets,
            string tag)
        {
            return assets.Count(asset => asset.AssetTags.Contains(tag));
        }

        private static void WarnIfBundledArtifactsAreStale(
            BundleCollectorSetting setting)
        {
            string settingPath = AssetDatabase.GetAssetPath(setting);
            string versionPath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "yoo",
                PackageName,
                $"{PackageName}.version");
            if (!File.Exists(versionPath) ||
                string.IsNullOrEmpty(settingPath))
            {
                Debug.LogWarning(
                    "[YooAssetPolicy] 尚未发现可放入 APK 的 " +
                    "DefaultPackage 产物，请先构建 YooAsset。"
                );
                return;
            }

            string absoluteSettingPath = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    settingPath));
            if (File.GetLastWriteTimeUtc(versionPath) >=
                File.GetLastWriteTimeUtc(absoluteSettingPath))
            {
                return;
            }

            Debug.LogWarning(
                "[YooAssetPolicy] 当前 StreamingAssets 内置产物早于 " +
                "BundleCollectorSetting，仍是旧分层结果。发布 APK 前请用" +
                "新的 Package Version 重新构建 YooAsset；构建后 " +
                "ClearAndCopyByTags 会自动移除非 Builtin Bundle。"
            );
        }
    }
}
