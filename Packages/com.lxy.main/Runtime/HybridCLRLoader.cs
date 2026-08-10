using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Contracts;
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

        public const string HotUpdateDllLocation =
            HybridCLRAssemblyManifest.DefaultEntryAssemblyName +
            ".dll";

        private string packageName = DefaultPackageName;
        private HybridCLRAssemblyManifest assemblyManifest;
        private Assembly hotUpdateAssembly;

        public bool IsLoading { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public Assembly HotUpdateAssembly => hotUpdateAssembly;

        /// <summary>
        /// 必须在 YooAsset 清单更新和资源下载完成后调用，且必须在
        /// 任何挂载热更新 MonoBehaviour 的 Prefab/场景加载前完成。
        /// </summary>
        public IEnumerator LoadAndStart()
        {
            string packageVersion = "Editor";

#if !UNITY_EDITOR
            if (YooAssets.IsInitialized &&
                YooAssets.TryGetPackage(
                    packageName,
                    out ResourcePackage package))
            {
                packageVersion = package.GetPackageVersion();
            }
#endif

            var context = new HotUpdateStartupContext(
                packageName,
                string.IsNullOrWhiteSpace(packageVersion)
                    ? "Unknown"
                    : packageVersion,
                Application.version,
                string.Empty,
                Application.isEditor);

            yield return LoadAndStart(context);
        }

        /// <summary>
        /// 加载热更新程序集并等待热更新入口完整启动。
        /// 只有入口协程成功结束后 IsReady 才会置为 true。
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
            }

            hotUpdateAssembly = FindLoadedHotUpdateAssembly(
                assemblyManifest.entryAssemblyName);
#endif

            yield return StartHotUpdateEntry(context);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            if (!context.Succeeded)
            {
                Fail(
                    context.Error ??
                    "热更新入口未报告启动成功。");
                yield break;
            }

            IsReady = true;
            IsLoading = false;
            Debug.Log(
                "[HybridCLR] AOT 元数据、热更新程序集和入口加载完成。",
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

        /// <summary>
        /// 首场景加载后再次进入热更新程序集，由热更新层启动 UI、Lua
        /// 和其他场景级业务。AOT 壳不直接引用这些程序集。
        /// </summary>
        public IEnumerator StartFirstScene(
            HotUpdateStartupContext context)
        {
            if (context == null)
            {
                Fail("首场景热更新启动上下文为空。");
                yield break;
            }

            if (!IsReady || hotUpdateAssembly == null)
            {
                Fail("HybridCLR 热更新运行时尚未就绪。");
                yield break;
            }

            LastError = null;
            yield return InvokeHotUpdateEntryRoutine(
                "StartFirstScene",
                context);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            if (!context.FirstSceneRuntimeSucceeded)
            {
                Fail(
                    context.FirstSceneRuntimeError ??
                    "首场景热更新运行时未报告启动成功。");
            }
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

            AssetHandle handle =
                package.LoadAssetAsync<TextAsset>(location);
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

            TextAsset manifestAsset =
                handle.AssetObject as TextAsset;
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

                AssetHandle handle =
                    package.LoadAssetAsync<TextAsset>(location);

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

                TextAsset dllAsset =
                    handle.AssetObject as TextAsset;

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

                    AssetHandle handle =
                        package.LoadAssetAsync<TextAsset>(location);
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

                    TextAsset textAsset =
                        handle.AssetObject as TextAsset;
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

                if (string.Equals(
                        actualName,
                        assemblyManifest.entryAssemblyName,
                        StringComparison.Ordinal))
                {
                    hotUpdateAssembly = loadedAssembly;
                }

                Debug.Log(
                    $"[HybridCLR] 热更新程序集加载成功：" +
                    loadedAssembly.FullName,
                    this);
            }

            if (hotUpdateAssembly == null)
            {
                Fail(
                    "热更新入口程序集没有加载：" +
                    assemblyManifest.entryAssemblyName);
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

            manifest.entryAssemblyName =
                manifest.entryAssemblyName?.Trim();
            if (!string.Equals(
                    manifest.entryAssemblyName,
                    HybridCLRAssemblyManifest
                        .DefaultEntryAssemblyName,
                    StringComparison.Ordinal))
            {
                error =
                    "HybridCLR 入口程序集必须保持为：" +
                    HybridCLRAssemblyManifest
                        .DefaultEntryAssemblyName;
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
                    true,
                    "热更新",
                    out manifest.hotUpdateDlls,
                    out error))
            {
                return false;
            }

            string entryLocation =
                manifest.entryAssemblyName + ".dll";
            if (!manifest.hotUpdateDlls.Contains(entryLocation))
            {
                error =
                    $"HybridCLR 程序集清单缺少入口 DLL：" +
                    entryLocation;
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

        private IEnumerator StartHotUpdateEntry(
            HotUpdateStartupContext context)
        {
            yield return InvokeHotUpdateEntryRoutine(
                "Start",
                context);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            if (!context.IsCompleted)
            {
                context.Fail("热更新入口结束但未提交启动结果。");
            }
        }

        private IEnumerator InvokeHotUpdateEntryRoutine(
            string methodName,
            HotUpdateStartupContext context)
        {
            const string typeName =
                "Game.HotUpdate.HotUpdateEntry";

            Type entryType = hotUpdateAssembly?.GetType(typeName);
            if (entryType == null)
            {
                Fail($"热更新入口类型不存在：{typeName}");
                yield break;
            }

            MethodInfo entryMethod = entryType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(HotUpdateStartupContext) },
                null);
            if (entryMethod == null)
            {
                Fail(
                    $"热更新入口方法不存在：" +
                    $"{typeName}.{methodName}(" +
                    "HotUpdateStartupContext)");
                yield break;
            }

            object result;
            try
            {
                result = entryMethod.Invoke(
                    null,
                    new object[] { context });
            }
            catch (TargetInvocationException exception)
            {
                Exception innerException =
                    exception.InnerException ?? exception;
                Fail(
                    $"热更新入口 {methodName} 执行失败：" +
                    innerException.Message,
                    innerException);
                yield break;
            }
            catch (Exception exception)
            {
                Fail(
                    $"热更新入口 {methodName} 执行失败：" +
                    exception.Message,
                    exception);
                yield break;
            }

            if (!(result is IEnumerator entryRoutine))
            {
                Fail(
                    $"热更新入口必须返回 IEnumerator：" +
                    $"{typeName}.{methodName}");
                yield break;
            }

            yield return entryRoutine;
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
