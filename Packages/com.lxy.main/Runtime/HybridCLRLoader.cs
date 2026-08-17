using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Contracts;
using Game.Resource;
using HybridCLR;
using UnityEngine;
using YooAsset;

namespace Game.Main
{
    [DisallowMultipleComponent]
    public sealed class HybridCLRLoader : MonoBehaviour
    {
        private const string DefaultPackageName = "DefaultPackage";
        private const string ManifestAssetPath =
            "Assets/GameResources/HybridCLR/" +
            "HybridCLRAssemblyManifest.bytes";

        private string packageName = DefaultPackageName;
        private HybridCLRAssemblyManifest assemblyManifest;
        private readonly List<Assembly> loadedHotUpdateAssemblies =
            new List<Assembly>();

        public bool IsLoading { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public IReadOnlyList<Assembly> LoadedHotUpdateAssemblies =>
            loadedHotUpdateAssemblies;

        /// <summary>
        /// 加载 HybridCLR AOT 补充元数据，以及 HybridCLRSettings 中
        /// 配置的全部热更新程序集。热更新 DLL 列表允许为空。
        /// </summary>
        public IEnumerator LoadAndStart(
            HotUpdateStartupContext context)
        {
            if (context == null)
            {
                Fail("热更新启动上下文为空。");
                yield break;
            }

            if (IsReady)
            {
                context.Complete("复用已就绪的 HybridCLR 运行时");
                yield break;
            }

            if (IsLoading)
            {
                while (IsLoading)
                {
                    yield return null;
                }

                if (IsReady)
                {
                    context.Complete(
                        "复用已就绪的 HybridCLR 运行时");
                }
                else
                {
                    context.Fail(
                        LastError ?? "HybridCLR 加载失败");
                }

                yield break;
            }

            IsLoading = true;
            LastError = null;
            loadedHotUpdateAssemblies.Clear();
            context.Report(
                HotUpdateStartupStage.ValidatingEnvironment,
                0.1f,
                "校验 HybridCLR 运行环境");

#if !UNITY_EDITOR
            if (!YooAssets.IsInitialized)
            {
                Fail("YooAssets 尚未初始化。");
                yield break;
            }

            if (!YooAssets.TryGetPackage(
                    packageName,
                    out ResourcePackage package))
            {
                Fail($"没有找到 YooAsset Package：{packageName}");
                yield break;
            }

            yield return LoadAssemblyManifest(package);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            yield return LoadAotMetadata(package);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            context.Report(
                HotUpdateStartupStage.InitializingRuntime,
                0.6f,
                "加载 HybridCLR 热更新程序集");
            yield return LoadHotUpdateAssemblies(package);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }
#else
            // Editor 中 asmdef 已由 Unity 加载，重复 Assembly.Load
            // 会得到两个同名程序集。这里同时校验生成清单是否完整。
            if (!TryLoadEditorAssemblyManifest(out string manifestError))
            {
                Fail(manifestError);
                yield break;
            }

            foreach (string location in assemblyManifest.hotUpdateDlls)
            {
                string assemblyName =
                    GetAssemblyNameFromLocation(location);
                if (FindLoadedHotUpdateAssembly(assemblyName) == null)
                {
                    Fail(
                        $"Editor 中没有找到热更新程序集：" +
                        $"{assemblyName}。请重新执行生成同步命令。");
                    yield break;
                }

                loadedHotUpdateAssemblies.Add(
                    FindLoadedHotUpdateAssembly(assemblyName));
            }
#endif

            IsReady = true;
            IsLoading = false;
            context.Complete(
                assemblyManifest.hotUpdateDlls.Length == 0
                    ? "HybridCLR 已就绪，当前未配置热更新 DLL"
                    : "HybridCLR 热更新程序集加载完成");
            Debug.Log(
                $"[HybridCLR] AOT 元数据和热更新程序集加载完成，" +
                $"HotUpdate={assemblyManifest.hotUpdateDlls.Length}。",
                this);
        }

        public void ConfigurePackage(string value)
        {
            if (IsLoading || IsReady)
            {
                throw new InvalidOperationException(
                    "HybridCLR 加载开始后不能再修改 Package。");
            }

            packageName = string.IsNullOrWhiteSpace(value)
                ? DefaultPackageName
                : value.Trim();
        }

#if !UNITY_EDITOR
        private IEnumerator LoadAssemblyManifest(
            ResourcePackage package)
        {
            string location =
                HybridCLRAssemblyManifest.ManifestLocation;
            if (!package.IsLocationValid(location))
            {
                Fail(
                    $"当前 Package Manifest " +
                    $"{package.GetPackageVersion()} 不包含 HybridCLR " +
                    $"程序集清单：{location}。请重新同步并构建 " +
                    "YooAsset Bundle。");
                yield break;
            }

            GameResourceHandle<TextAsset> handle =
                GameResourceManager.GetOrCreate(gameObject)
                    .LoadAssetAsync<TextAsset>(package, location);
            yield return handle;

            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error =
                    $"加载 HybridCLR 程序集清单失败：{location}\n" +
                    handle.Error;
                handle.Release();
                Fail(error);
                yield break;
            }

            TextAsset manifestAsset = handle.Asset;
            if (manifestAsset == null)
            {
                handle.Release();
                Fail(
                    $"HybridCLR 程序集清单不是 TextAsset：" +
                    location);
                yield break;
            }

            bool parsed = TryParseAssemblyManifest(
                manifestAsset.text,
                out HybridCLRAssemblyManifest parsedManifest,
                out string errorMessage);
            handle.Release();

            if (!parsed)
            {
                Fail(errorMessage);
                yield break;
            }

            assemblyManifest = parsedManifest;
            Debug.Log(
                $"[HybridCLR] 程序集清单加载成功：" +
                $"AOT={assemblyManifest.aotMetadataDlls.Length}，" +
                $"HotUpdate={assemblyManifest.hotUpdateDlls.Length}",
                this);
        }

        private IEnumerator LoadAotMetadata(ResourcePackage package)
        {
            foreach (string dllName in
                     assemblyManifest.aotMetadataDlls)
            {
                // AddressByFileName：
                // mscorlib.dll.bytes -> mscorlib.dll
                string location = dllName;

                Debug.Log(
                    $"[HybridCLR] 开始加载 AOT 元数据：{location}",
                    this);

                GameResourceHandle<TextAsset> handle =
                    GameResourceManager.GetOrCreate(gameObject)
                        .LoadAssetAsync<TextAsset>(
                            package,
                            location);

                yield return handle;

                if (handle.Status != EOperationStatus.Succeeded)
                {
                    string error =
                        $"加载 AOT 元数据资源失败：{location}\n" +
                        handle.Error;

                    handle.Release();
                    Fail(error);
                    yield break;
                }

                TextAsset dllAsset = handle.Asset;

                if (dllAsset == null)
                {
                    handle.Release();
                    Fail(
                        $"AOT 元数据资源不是 TextAsset：{location}");
                    yield break;
                }

                LoadImageErrorCode result;

                try
                {
                    result =
                        RuntimeApi.LoadMetadataForAOTAssembly(
                            dllAsset.bytes,
                            HomologousImageMode.SuperSet);
                }
                catch (Exception exception)
                {
                    handle.Release();

                    Fail(
                        $"调用 LoadMetadataForAOTAssembly 异常：" +
                        $"{dllName}\n{exception.Message}",
                        exception);

                    yield break;
                }

                handle.Release();

                if (result != LoadImageErrorCode.OK)
                {
                    Fail(
                        $"加载 AOT 元数据失败：" +
                        $"{dllName}，错误码：{result}");

                    yield break;
                }

                Debug.Log(
                    $"[HybridCLR] 加载 AOT 元数据成功：" +
                    $"{dllName}，结果：{result}",
                    this);
            }
        }

        private IEnumerator LoadHotUpdateAssemblies(
            ResourcePackage package)
        {
            foreach (string location in assemblyManifest.hotUpdateDlls)
            {
                string assemblyName =
                    GetAssemblyNameFromLocation(location);
                Assembly loadedAssembly =
                    FindLoadedHotUpdateAssembly(assemblyName);

                if (loadedAssembly == null)
                {
                    if (!package.IsLocationValid(location))
                    {
                        Fail(
                            $"当前 Package Manifest " +
                            $"{package.GetPackageVersion()} 不包含" +
                            $"热更新 DLL Location：{location}。" +
                            "请确认已发布最新 Manifest 及其对应的 " +
                            "HybridCLR Bundle。");
                        yield break;
                    }

                    GameResourceHandle<TextAsset> handle =
                        GameResourceManager.GetOrCreate(gameObject)
                            .LoadAssetAsync<TextAsset>(
                                package,
                                location);
                    yield return handle;

                    if (handle.Status != EOperationStatus.Succeeded)
                    {
                        string error =
                            $"加载热更新 DLL 失败：{location}\n" +
                            handle.Error;
                        handle.Release();
                        Fail(error);
                        yield break;
                    }

                    TextAsset textAsset = handle.Asset;
                    if (textAsset == null)
                    {
                        handle.Release();
                        Fail(
                            $"热更新 DLL 资源不是 TextAsset：" +
                            location);
                        yield break;
                    }

                    try
                    {
                        // Assembly.Load 会复制数据，完成后即可释放 Handle。
                        loadedAssembly = Assembly.Load(textAsset.bytes);
                    }
                    catch (Exception exception)
                    {
                        handle.Release();
                        Fail(
                            $"Assembly.Load 失败：{location}\n" +
                            exception.Message,
                            exception);
                        yield break;
                    }

                    handle.Release();
                }

                string actualName = loadedAssembly.GetName().Name;
                if (!string.Equals(
                        actualName,
                        assemblyName,
                        StringComparison.Ordinal))
                {
                    Fail(
                        $"热更新程序集名称不匹配：配置={assemblyName}，" +
                        $"实际={actualName}。");
                    yield break;
                }

                if (!loadedHotUpdateAssemblies.Contains(
                        loadedAssembly))
                {
                    loadedHotUpdateAssemblies.Add(loadedAssembly);
                }

                Debug.Log(
                    $"[HybridCLR] 热更新程序集加载成功：" +
                    loadedAssembly.FullName,
                    this);
            }

        }
#endif

#if UNITY_EDITOR
        private bool TryLoadEditorAssemblyManifest(
            out string error)
        {
            TextAsset manifestAsset =
                UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ManifestAssetPath);
            if (manifestAsset == null)
            {
                error =
                    "Editor 中没有找到 HybridCLR 程序集清单：" +
                    ManifestAssetPath +
                    "。请执行生成同步命令。";
                return false;
            }

            bool parsed = TryParseAssemblyManifest(
                manifestAsset.text,
                out HybridCLRAssemblyManifest parsedManifest,
                out error);
            if (parsed)
            {
                assemblyManifest = parsedManifest;
            }

            return parsed;
        }
#endif

        private static bool TryParseAssemblyManifest(
            string json,
            out HybridCLRAssemblyManifest manifest,
            out string error)
        {
            manifest = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "HybridCLR 程序集清单内容为空。";
                return false;
            }

            try
            {
                manifest =
                    JsonUtility.FromJson<HybridCLRAssemblyManifest>(
                        json);
            }
            catch (Exception exception)
            {
                error =
                    "HybridCLR 程序集清单解析失败：" +
                    exception.Message;
                return false;
            }

            if (manifest == null)
            {
                error = "HybridCLR 程序集清单解析结果为空。";
                return false;
            }

            if (!TryNormalizeDllList(
                    manifest.aotMetadataDlls,
                    false,
                    "AOT 元数据",
                    out manifest.aotMetadataDlls,
                    out error) ||
                !TryNormalizeDllList(
                    manifest.hotUpdateDlls,
                    false,
                    "热更新",
                    out manifest.hotUpdateDlls,
                    out error))
            {
                return false;
            }

            return true;
        }

        private static bool TryNormalizeDllList(
            IEnumerable<string> source,
            bool requireAtLeastOne,
            string category,
            out string[] normalized,
            out string error)
        {
            normalized = Array.Empty<string>();
            error = null;
            if (source == null)
            {
                if (!requireAtLeastOne)
                {
                    return true;
                }

                error = $"HybridCLR {category} DLL 清单为空。";
                return false;
            }

            var names = new List<string>();
            var uniqueNames = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (string item in source)
            {
                string name = item?.Trim();
                if (string.IsNullOrEmpty(name) ||
                    !name.EndsWith(
                        ".dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        $"HybridCLR {category} DLL 名称无效：" +
                        (item ?? "<null>");
                    return false;
                }

                if (!uniqueNames.Add(name))
                {
                    error =
                        $"HybridCLR {category} DLL 重复：{name}";
                    return false;
                }

                names.Add(name);
            }

            if (requireAtLeastOne && names.Count == 0)
            {
                error = $"HybridCLR {category} DLL 清单为空。";
                return false;
            }

            normalized = names.ToArray();
            return true;
        }

        private static Assembly FindLoadedHotUpdateAssembly(
            string assemblyName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly =>
                    string.Equals(
                        assembly.GetName().Name,
                        assemblyName,
                        StringComparison.Ordinal));
        }

        private static string GetAssemblyNameFromLocation(
            string location)
        {
            const string DllExtension = ".dll";
            return location.EndsWith(
                DllExtension,
                StringComparison.OrdinalIgnoreCase)
                ? location.Substring(
                    0,
                    location.Length - DllExtension.Length)
                : location;
        }

        private void Fail(
            string message,
            Exception exception = null)
        {
            LastError = message;
            IsReady = false;
            IsLoading = false;
            Debug.LogError("[HybridCLR] " + message, this);
            if (exception != null)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
