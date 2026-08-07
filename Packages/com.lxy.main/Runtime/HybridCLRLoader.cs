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

        public const string HotUpdateDllLocation = "Game.HotUpdate.dll";

        // 每次执行 HybridCLR/Generate/All 后，都应按生成的
        // AOTGenericReferences.PatchedAOTAssemblyList 更新此列表，
        // 并重新复制对应平台的裁剪后 AOT DLL。
        private static readonly IReadOnlyList<string>
            AotMetadataDlls = Array.Empty<string>();

        private string packageName = DefaultPackageName;
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

            yield return LoadAotMetadata(package);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }

            yield return LoadHotUpdateAssembly(package);
            if (!string.IsNullOrEmpty(LastError))
            {
                yield break;
            }
#else
            // Editor 中热更新程序集已经由 Unity 加载，重复
            // Assembly.Load 会得到两个同名程序集。
            hotUpdateAssembly = FindLoadedHotUpdateAssembly();
            if (hotUpdateAssembly == null)
            {
                Fail("Editor 中没有找到 Game.HotUpdate 程序集。");
                yield break;
            }
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

#if !UNITY_EDITOR
        private IEnumerator LoadAotMetadata(ResourcePackage package)
        {
            foreach (string dllName in AotMetadataDlls)
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

        private IEnumerator LoadHotUpdateAssembly(
            ResourcePackage package)
        {
            hotUpdateAssembly = FindLoadedHotUpdateAssembly();
            if (hotUpdateAssembly != null)
            {
                yield break;
            }

            if (!package.IsLocationValid(HotUpdateDllLocation))
            {
                Fail(
                    $"当前 Package Manifest {package.GetPackageVersion()} " +
                    $"不包含热更新 DLL Location：" +
                    $"{HotUpdateDllLocation}。请确认已发布最新 Manifest " +
                    "及其对应的 HybridCLR Bundle。");
                yield break;
            }

            AssetHandle handle =
                package.LoadAssetAsync<TextAsset>(
                    HotUpdateDllLocation);
            yield return handle;

            if (handle.Status != EOperationStatus.Succeeded)
            {
                string error =
                    $"加载热更新 DLL 失败：{HotUpdateDllLocation}\n" +
                    handle.Error;
                handle.Release();
                Fail(error);
                yield break;
            }

            TextAsset textAsset = handle.AssetObject as TextAsset;
            if (textAsset == null)
            {
                handle.Release();
                Fail(
                    $"热更新 DLL 资源不是 TextAsset：" +
                    HotUpdateDllLocation);
                yield break;
            }

            try
            {
                // Assembly.Load 会复制 DLL 数据，加载完成即可释放 Handle。
                hotUpdateAssembly = Assembly.Load(textAsset.bytes);
            }
            catch (Exception exception)
            {
                handle.Release();
                Fail(
                    $"Assembly.Load 失败：{HotUpdateDllLocation}\n" +
                    exception.Message,
                    exception);
                yield break;
            }

            handle.Release();
            Debug.Log(
                $"[HybridCLR] 热更新程序集加载成功：" +
                hotUpdateAssembly.FullName,
                this);
        }
#endif

        private IEnumerator StartHotUpdateEntry(
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

            MethodInfo startMethod = entryType.GetMethod(
                "Start",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(HotUpdateStartupContext) },
                null);
            if (startMethod == null)
            {
                Fail(
                    $"热更新入口方法不存在：" +
                    $"{typeName}.Start(" +
                    "HotUpdateStartupContext)");
                yield break;
            }

            object result;
            try
            {
                result = startMethod.Invoke(
                    null,
                    new object[] { context });
            }
            catch (TargetInvocationException exception)
            {
                Exception innerException =
                    exception.InnerException ?? exception;
                Fail(
                    $"热更新入口执行失败：{innerException.Message}",
                    innerException);
                yield break;
            }
            catch (Exception exception)
            {
                Fail(
                    $"热更新入口执行失败：{exception.Message}",
                    exception);
                yield break;
            }

            if (!(result is IEnumerator startRoutine))
            {
                Fail(
                    $"热更新入口必须返回 IEnumerator：" +
                    $"{typeName}.Start");
                yield break;
            }

            yield return startRoutine;

            if (!context.IsCompleted)
            {
                context.Fail("热更新入口结束但未提交启动结果。");
            }
        }

        private static Assembly FindLoadedHotUpdateAssembly()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly =>
                    assembly.GetName().Name == "Game.HotUpdate");
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
