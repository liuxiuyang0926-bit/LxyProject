using System;
using System.IO;
using System.Linq;
using Game.Main.Editor;
using LxyDemo.UIFramework.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using PlayerBuildResult = UnityEditor.Build.Reporting.BuildResult;
using PlayerBuildReport = UnityEditor.Build.Reporting.BuildReport;
using YooAssetBuildResult = YooAsset.Editor.BuildResult;

namespace LxyDemo.EditorTools
{
    /// <summary>
    /// Android 发布入口：同步 HybridCLR、构建 YooAsset、再构建 APK。
    /// 输入的版本只用于 PlayerSettings.bundleVersion，作为整包强更版本门槛。
    /// YooAsset PackageVersion 由 Bundle Builder 的 BuildVersion 规则独立生成。
    /// </summary>
    public sealed class OneClickPackageBuilder : EditorWindow
    {
        private const string PackageName = "DefaultPackage";
        private const string PipelineName = "ScriptableBuildPipeline";
        private const string OutputDirectory = "Builds/Android";

        private string packageVersion;

        [MenuItem("工具/一键打包", false, 0)]
        private static void OpenWindow()
        {
            GetWindow<OneClickPackageBuilder>("一键打包");
        }

        private void OnEnable()
        {
            packageVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "1.0.0"
                : PlayerSettings.bundleVersion;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Android 一键打包", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "版本只写入 APK。流程会自动生成 HybridCLR、构建 YooAsset，并输出 APK；" +
                "YooAsset 使用 Bundle Builder 的 BuildVersion 规则，Lua 仅在资源包构建期间临时同步。",
                MessageType.Info);

            packageVersion = EditorGUILayout.TextField("Package Version", packageVersion);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling ||
                                               UnityEditor.BuildPipeline.isBuildingPlayer))
            {
                if (GUILayout.Button("开始打包 APK", GUILayout.Height(32)))
                {
                    BuildAndroidPackage();
                }
            }
        }

        private void BuildAndroidPackage()
        {
            string version = NormalizeVersion(packageVersion);
            if (!Version.TryParse(version, out _))
            {
                EditorUtility.DisplayDialog(
                    "版本无效",
                    "Package Version 必须是数字版本，例如 1.0.0。",
                    "确定");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog(
                    "目标平台错误",
                    "请先将 Unity 切换到 Android 平台后再执行一键打包。",
                    "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "确认一键打包",
                    "将使用版本 " + version +
                    " 生成 HybridCLR、YooAsset Package 和 APK。",
                    "开始",
                    "取消"))
            {
                return;
            }

            try
            {
                ExecuteBuild(version);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "一键打包失败",
                    exception.Message,
                    "确定");
            }
        }

        /// <summary>
        /// 执行不显示确认对话框的完整 Android 打包流程，供 CI 和构建验证调用。
        /// </summary>
        public static string BuildAndroidPackageForAutomation(string version)
        {
            version = NormalizeVersion(version);
            if (!Version.TryParse(version, out _))
            {
                throw new ArgumentException(
                    "Package Version 必须是数字版本，例如 1.0.0。",
                    nameof(version));
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "一键打包只能在 Android 平台执行。");
            }

            return ExecuteBuild(version);
        }

        private static string ExecuteBuild(string version)
        {
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = version;
                EditorUtility.DisplayProgressBar("一键打包", "生成 HybridCLR…", 0.1f);
                HybridCLRAssetSynchronizer.GenerateAllAndSync();

                EditorUtility.DisplayProgressBar("一键打包", "构建 YooAsset…", 0.35f);
                BuildYooAssetPackage();

                EditorUtility.DisplayProgressBar("一键打包", "构建 Android APK…", 0.75f);
                string apkPath = BuildApk(version);
                Debug.Log(
                    "[OneClickPackage] 打包完成。Package=" + version +
                    "，APK=" + apkPath);
                EditorUtility.RevealInFinder(apkPath);
                return apkPath;
            }
            catch
            {
                PlayerSettings.bundleVersion = previousVersion;
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                LuaUIBuildProcessor.CleanupStagedLuaFiles();
            }
        }

        private static void BuildYooAssetPackage()
        {
            YooAssetContentPolicy.ApplyBuildSettingsForAutomation();
            LuaUIBuildProcessor.StageLuaFiles();
            try
            {
                var parameters = new ScriptableBuildParameters
                {
                    BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                    BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
                    BuildPipeline = PipelineName,
                    BuildBundleType = (int)EBundleType.AssetBundle,
                    BuildTarget = BuildTarget.Android,
                    PackageName = PackageName,
                    PackageVersion = CreateYooAssetBuildVersion(),
                    EnableSharePackRule = true,
                    VerifyBuildingResult = true,
                    FileNameStyle = BundleBuilderSetting.GetPackageFileNameStyle(
                        PackageName,
                        PipelineName),
                    BundledCopyOption = BundleBuilderSetting.GetPackageBundledCopyOption(
                        PackageName,
                        PipelineName),
                    BundledCopyParams = BundleBuilderSetting.GetPackageBundledCopyParams(
                        PackageName,
                        PipelineName),
                    CompressOption = BundleBuilderSetting.GetPackageCompressOption(
                        PackageName,
                        PipelineName),
                    ClearBuildCacheFiles = BundleBuilderSetting.GetPackageClearBuildCache(
                        PackageName,
                        PipelineName),
                    UseAssetDependencyDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(
                        PackageName,
                        PipelineName),
                    BundleEncryptor = CreateBuildService<IBundleEncryptor>(
                        BundleBuilderSetting.GetPackageBundleEncryptorClassName(
                            PackageName,
                            PipelineName)),
                    ManifestEncryptor = CreateBuildService<IManifestEncryptor>(
                        BundleBuilderSetting.GetPackageManifestEncryptorClassName(
                            PackageName,
                            PipelineName)),
                    ManifestDecryptor = CreateBuildService<IManifestDecryptor>(
                        BundleBuilderSetting.GetPackageManifestDecryptorClassName(
                            PackageName,
                            PipelineName)),
                    BuiltinShadersBundleName = GetBuiltinShaderBundleName(),
                };

                YooAssetBuildResult result =
                    new ScriptableBuildPipeline().Run(parameters, true);
                if (!result.Success)
                {
                    throw new BuildFailedException(
                        "YooAsset 构建失败：" + result.ErrorInfo);
                }
            }
            finally
            {
                LuaUIBuildProcessor.CleanupStagedLuaFiles();
            }
        }

        private static string BuildApk(string version)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("Build Settings 中没有启用的场景。");
            }

            string directory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                OutputDirectory));
            Directory.CreateDirectory(directory);
            string productName = SanitizeFileName(PlayerSettings.productName);
            string outputPath = Path.Combine(
                directory,
                productName + "_" + version + ".apk");

            bool originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                PlayerBuildReport report = UnityEditor.BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = outputPath,
                        target = BuildTarget.Android,
                        options = BuildOptions.None,
                    });
                if (report.summary.result != PlayerBuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "APK 构建失败：" + report.summary.result);
                }
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
            }

            return outputPath;
        }

        private static string GetBuiltinShaderBundleName()
        {
            bool uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
            BundlePackRuleResult ruleResult =
                DefaultBundlePackRule.CreateShadersPackRuleResult();
            return ruleResult.GetBundleName(PackageName, uniqueBundleName);
        }

        private static string CreateYooAssetBuildVersion()
        {
            int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        private static T CreateBuildService<T>(string className)
            where T : class
        {
            Type serviceType = EditorAssemblyUtility
                .GetAssignableTypes(typeof(T))
                .FirstOrDefault(type => type.FullName == className);
            if (serviceType == null)
            {
                Debug.LogWarning(
                    "[OneClickPackage] 未找到 YooAsset 构建服务：" +
                    className);
                return null;
            }

            return Activator.CreateInstance(serviceType) as T;
        }

        private static string NormalizeVersion(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "LxyDemo" : value;
        }
    }
}
