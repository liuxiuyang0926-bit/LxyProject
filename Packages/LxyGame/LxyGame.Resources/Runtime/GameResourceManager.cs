using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Resource
{
    public readonly struct GameResourceSnapshot
    {
        /// <summary>
        /// 创建游戏资源快照实例。
        /// </summary>
        internal GameResourceSnapshot(GameResourceEntry entry)
        {
            PackageName = entry.PackageName;
            Location = entry.Location;
            AssetType = entry.AssetType;
            ReferenceCount = entry.ReferenceCount;
            Status = entry.Status;
            Progress = entry.Progress;
        }

        /// <summary>
        /// 向调用方提供资源包名称。
        /// </summary>
        public string PackageName { get; }
        /// <summary>
        /// 向调用方提供位置。
        /// </summary>
        public string Location { get; }
        /// <summary>
        /// 向调用方提供资源类型。
        /// </summary>
        public Type AssetType { get; }
        /// <summary>
        /// 当前引用的数量。
        /// </summary>
        public int ReferenceCount { get; }
        /// <summary>
        /// 向调用方提供状态。
        /// </summary>
        public EOperationStatus Status { get; }
        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress { get; }
    }

    /// <summary>
    /// 项目统一资源服务。业务层只持有 GameResourceHandle 或
    /// GameResourceInstanceHandle，不直接持有 YooAsset AssetHandle。
    /// </summary>
    [DefaultExecutionOrder(-18500)]
    [DisallowMultipleComponent]
    public sealed class GameResourceManager : MonoBehaviour
    {
        private readonly Dictionary<string, GameResourceEntry> entries =
            new Dictionary<string, GameResourceEntry>(
                StringComparer.Ordinal);

        private readonly Dictionary<int, GameResourceInstanceHandle>
            instances =
                new Dictionary<int, GameResourceInstanceHandle>();

        private ResourcePackage defaultPackage;
        private bool isShuttingDown;

        /// <summary>
        /// 向调用方提供实例。
        /// </summary>
        public static GameResourceManager Instance { get; private set; }

        /// <summary>
        /// 向调用方提供Default资源包。
        /// </summary>
        public ResourcePackage DefaultPackage => defaultPackage;

        /// <summary>
        /// 向调用方提供Default资源包名称。
        /// </summary>
        public string DefaultPackageName =>
            defaultPackage != null
                ? defaultPackage.PackageName
                : string.Empty;

        /// <summary>
        /// 当前Loaded资源的数量。
        /// </summary>
        public int LoadedResourceCount => entries.Count;

        /// <summary>
        /// 当前Tracked实例的数量。
        /// </summary>
        public int TrackedInstanceCount => instances.Count;

        public int TotalReferenceCount
        {
            get
            {
                int total = 0;
                foreach (GameResourceEntry entry in entries.Values)
                {
                    total += Mathf.Max(0, entry.ReferenceCount);
                }

                return total;
            }
        }

        /// <summary>
        /// 获取Snapshots。
        /// </summary>
        public IReadOnlyList<GameResourceSnapshot> GetSnapshots()
        {
            var result =
                new List<GameResourceSnapshot>(entries.Count);
            foreach (GameResourceEntry entry in entries.Values)
            {
                result.Add(new GameResourceSnapshot(entry));
            }

            result.Sort((left, right) =>
            {
                int packageCompare = string.Compare(
                    left.PackageName,
                    right.PackageName,
                    StringComparison.Ordinal);
                return packageCompare != 0
                    ? packageCompare
                    : string.Compare(
                        left.Location,
                        right.Location,
                        StringComparison.Ordinal);
            });
            return result;
        }

        /// <summary>
        /// 获取现有实例，或把管理器动态挂到指定宿主对象。
        /// </summary>
        public static GameResourceManager GetOrCreate(
            GameObject host = null)
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameResourceManager existing =
                FindObjectOfType<GameResourceManager>(true);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            if (host == null)
            {
                host = new GameObject("[GameResourceManager]");
            }

            return host.GetComponent<GameResourceManager>() ??
                   host.AddComponent<GameResourceManager>();
        }

        /// <summary>
        /// 初始化组件的运行时状态。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// 绑定默认 ResourcePackage。切换同名 Package 前必须先释放旧句柄，
        /// 因此检测到 Package 实例变化时会统一回收现有资源。
        /// </summary>
        public void SetDefaultPackage(ResourcePackage package)
        {
            if (ReferenceEquals(defaultPackage, package))
            {
                return;
            }

            if (defaultPackage != null && entries.Count > 0)
            {
                Debug.LogWarning(
                    "[GameResourceManager] 默认 Package 已变化，" +
                    "正在释放旧 Package 的资源句柄。",
                    this);
                ReleaseAll();
            }

            defaultPackage = package;
        }

        public GameResourceHandle<T> LoadAssetAsync<T>(
            string location,
            string packageName = null)
            where T : Object
        {
            ResourcePackage package =
                ResolvePackage(packageName, null);
            return LoadAssetInternal<T>(
                package,
                packageName,
                location,
                false);
        }

        public GameResourceHandle<T> LoadAssetAsync<T>(
            ResourcePackage package,
            string location)
            where T : Object
        {
            return LoadAssetInternal<T>(
                package,
                package?.PackageName,
                location,
                false);
        }

        public GameResourceHandle<T> LoadAssetSync<T>(
            string location,
            string packageName = null)
            where T : Object
        {
            ResourcePackage package =
                ResolvePackage(packageName, null);
            return LoadAssetInternal<T>(
                package,
                packageName,
                location,
                true);
        }

        public GameResourceHandle<T> LoadAssetSync<T>(
            ResourcePackage package,
            string location)
            where T : Object
        {
            return LoadAssetInternal<T>(
                package,
                package?.PackageName,
                location,
                true);
        }

        /// <summary>
        /// 执行实例化异步相关逻辑。
        /// </summary>
        public GameResourceInstanceHandle InstantiateAsync(
            string location,
            Transform parent = null,
            bool worldPositionStays = false,
            bool isActive = true,
            string packageName = null)
        {
            ResourcePackage package =
                ResolvePackage(packageName, null);
            return InstantiateInternal(
                package,
                packageName,
                location,
                parent,
                worldPositionStays,
                isActive);
        }

        /// <summary>
        /// 执行实例化异步相关逻辑。
        /// </summary>
        public GameResourceInstanceHandle InstantiateAsync(
            ResourcePackage package,
            string location,
            Transform parent = null,
            bool worldPositionStays = false,
            bool isActive = true)
        {
            return InstantiateInternal(
                package,
                package?.PackageName,
                location,
                parent,
                worldPositionStays,
                isActive);
        }

        /// <summary>
        /// 释放由 InstantiateAsync 创建的实例及其资源引用。
        /// </summary>
        public bool ReleaseInstance(GameObject instance)
        {
            if (instance == null ||
                !instances.TryGetValue(
                    instance.GetInstanceID(),
                    out GameResourceInstanceHandle handle))
            {
                return false;
            }

            ReleaseInstanceHandle(handle, true);
            return true;
        }

        /// <summary>
        /// 释放全部受管实例和底层资源句柄。
        /// 已发放的业务 Handle 会立即失效，后续重复 Release 是安全的。
        /// </summary>
        public void ReleaseAll()
        {
            var instanceHandles =
                new List<GameResourceInstanceHandle>(instances.Values);
            foreach (GameResourceInstanceHandle handle in
                     instanceHandles)
            {
                ReleaseInstanceHandle(handle, true);
            }

            var resourceEntries =
                new List<GameResourceEntry>(entries.Values);
            foreach (GameResourceEntry entry in resourceEntries)
            {
                ForceReleaseEntry(entry);
            }

            instances.Clear();
            entries.Clear();
        }

        /// <summary>
        /// 回收引用计数已经归零的 Provider 和 Bundle。
        /// 该操作只处理运行时内存，不删除磁盘下载缓存。
        /// </summary>
        public IEnumerator UnloadUnusedAssetsAsync(
            string packageName = null)
        {
#if UNITY_EDITOR
            yield return Resources.UnloadUnusedAssets();
#else
            ResourcePackage package =
                ResolvePackage(packageName, null);
            if (package == null)
            {
                yield break;
            }

            UnloadUnusedAssetsOperation operation =
                package.UnloadUnusedAssetsAsync();
            yield return operation;
            if (operation.Status != EOperationStatus.Succeeded)
            {
                Debug.LogWarning(
                    "[GameResourceManager] 卸载未使用资源失败：" +
                    operation.Error,
                    this);
            }
#endif
        }

        /// <summary>
        /// 异步释放全部And卸载Unused资源。
        /// </summary>
        public IEnumerator ReleaseAllAndUnloadUnusedAssetsAsync(
            string packageName = null)
        {
            ReleaseAll();
            yield return UnloadUnusedAssetsAsync(packageName);
        }

        /// <summary>
        /// 执行判断是否EntryAlive相关逻辑。
        /// </summary>
        internal bool IsEntryAlive(GameResourceEntry entry)
        {
            return entry != null &&
                   !entry.IsReleased &&
                   entries.TryGetValue(entry.Key, out GameResourceEntry
                       current) &&
                   ReferenceEquals(current, entry);
        }

        /// <summary>
        /// 执行释放相关逻辑。
        /// </summary>
        internal void Release(GameResourceEntry entry)
        {
            if (!IsEntryAlive(entry))
            {
                return;
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0)
            {
                return;
            }

            if (entry.ReferenceCount < 0)
            {
                Debug.LogError(
                    "[GameResourceManager] 资源引用计数小于零：" +
                    entry.Location,
                    this);
            }

            ForceReleaseEntry(entry);
        }

        /// <summary>
        /// 执行跟踪Instance相关逻辑。
        /// </summary>
        internal void TrackInstance(
            GameResourceInstanceHandle handle,
            GameObject instance)
        {
            if (handle == null || instance == null ||
                handle.IsReleased)
            {
                return;
            }

            int instanceId = instance.GetInstanceID();
            if (instances.TryGetValue(
                    instanceId,
                    out GameResourceInstanceHandle existing) &&
                !ReferenceEquals(existing, handle))
            {
                ReleaseInstanceHandle(existing, false);
            }

            instances[instanceId] = handle;
            handle.SetInstanceId(instanceId);

            GameResourceInstanceTracker tracker =
                instance.GetComponent<GameResourceInstanceTracker>();
            if (tracker == null)
            {
                tracker =
                    instance.AddComponent<GameResourceInstanceTracker>();
            }

            tracker.Bind(handle);
        }

        /// <summary>
        /// 释放InstanceHandle。
        /// </summary>
        internal void ReleaseInstanceHandle(
            GameResourceInstanceHandle handle,
            bool destroyInstance)
        {
            if (handle == null || handle.IsReleased)
            {
                return;
            }

            handle.CancelPendingInstantiation();
            GameObject instance = handle.GetRawInstance();
            int instanceId = handle.InstanceId;
            if (instanceId != 0)
            {
                instances.Remove(instanceId);
            }

            if (instance != null)
            {
                GameResourceInstanceTracker tracker =
                    instance.GetComponent<GameResourceInstanceTracker>();
                tracker?.Unbind(handle);
                if (destroyInstance)
                {
                    Object.Destroy(instance);
                }
            }

            handle.MarkReleased();
        }

        private GameResourceHandle<T> LoadAssetInternal<T>(
            ResourcePackage package,
            string requestedPackageName,
            string location,
            bool synchronous)
            where T : Object
        {
            string normalizedLocation = NormalizeLocation(location);
            string resolvedPackageName = ResolvePackageName(
                package,
                requestedPackageName);
            string key = BuildKey(
                resolvedPackageName,
                normalizedLocation,
                typeof(T));

            if (entries.TryGetValue(
                    key,
                    out GameResourceEntry existing))
            {
                existing.ReferenceCount++;
                if (synchronous)
                {
                    existing.WaitForCompletion();
                }

                return new GameResourceHandle<T>(this, existing);
            }

            var entry = new GameResourceEntry(
                key,
                resolvedPackageName,
                normalizedLocation,
                typeof(T),
                package)
            {
                ReferenceCount = 1
            };
            entries.Add(key, entry);

            try
            {
#if UNITY_EDITOR
                entry.EditorAsset =
                    AssetDatabase.LoadAssetAtPath<T>(
                        normalizedLocation);
                if (entry.EditorAsset == null)
                {
                    entry.EditorError =
                        "AssetDatabase 无法加载资源：" +
                        normalizedLocation;
                }
#else
                if (package == null)
                {
                    entry.FailureError =
                        "YooAsset ResourcePackage 不存在：" +
                        resolvedPackageName;
                }
                else if (package.InitializeStatus !=
                         EOperationStatus.Succeeded)
                {
                    entry.FailureError =
                        "YooAsset ResourcePackage 尚未初始化成功：" +
                        resolvedPackageName;
                }
                else
                {
                    entry.AssetHandle = synchronous
                        ? package.LoadAssetSync<T>(normalizedLocation)
                        : package.LoadAssetAsync<T>(normalizedLocation);
                }
#endif
            }
            catch (Exception exception)
            {
                entry.FailureError = exception.Message;
                Debug.LogException(exception, this);
            }

            return new GameResourceHandle<T>(this, entry);
        }

        /// <summary>
        /// 执行实例化内部相关逻辑。
        /// </summary>
        private GameResourceInstanceHandle InstantiateInternal(
            ResourcePackage package,
            string requestedPackageName,
            string location,
            Transform parent,
            bool worldPositionStays,
            bool isActive)
        {
            GameResourceHandle<GameObject> assetHandle =
                LoadAssetInternal<GameObject>(
                    package,
                    requestedPackageName,
                    location,
                    false);

            GameObject editorInstance = null;
            InstantiateOperation operation = null;
            string error = null;

            try
            {
#if UNITY_EDITOR
                GameObject prefab = assetHandle.Asset;
                if (prefab != null)
                {
                    editorInstance = parent == null
                        ? Instantiate(prefab)
                        : Instantiate(
                            prefab,
                            parent,
                            worldPositionStays);
                    editorInstance.SetActive(isActive);
                }
                else
                {
                    error = assetHandle.Error;
                }
#else
                AssetHandle yooAssetHandle =
                    assetHandle.GetYooAssetHandle();
                if (yooAssetHandle == null)
                {
                    error = assetHandle.Error;
                }
                else
                {
                    InstantiateOptions options = parent == null
                        ? new InstantiateOptions(isActive)
                        : new InstantiateOptions(
                            isActive,
                            parent,
                            worldPositionStays);
                    operation =
                        yooAssetHandle.InstantiateAsync(options);
                }
#endif
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogException(exception, this);
            }

            var result = new GameResourceInstanceHandle(
                this,
                assetHandle,
                operation,
                editorInstance,
                error);
            result.TryFinalize();
            return result;
        }

        /// <summary>
        /// 解析资源包。
        /// </summary>
        private ResourcePackage ResolvePackage(
            string packageName,
            ResourcePackage explicitPackage)
        {
#if UNITY_EDITOR
            return explicitPackage ?? defaultPackage;
#else
            if (explicitPackage != null)
            {
                return explicitPackage;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                return defaultPackage;
            }

            return YooAssets.TryGetPackage(
                packageName.Trim(),
                out ResourcePackage package)
                ? package
                : null;
#endif
        }

        /// <summary>
        /// 解析资源包名称。
        /// </summary>
        private static string ResolvePackageName(
            ResourcePackage package,
            string requestedPackageName)
        {
            if (package != null)
            {
                return package.PackageName;
            }

            return string.IsNullOrWhiteSpace(requestedPackageName)
                ? "EditorAssetDatabase"
                : requestedPackageName.Trim();
        }

        /// <summary>
        /// 执行规范化Location相关逻辑。
        /// </summary>
        private static string NormalizeLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException(
                    "资源 Location 不能为空。",
                    nameof(location));
            }

            return location.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// 构建键。
        /// </summary>
        private static string BuildKey(
            string packageName,
            string location,
            Type assetType)
        {
            return packageName + "|" +
                   assetType.AssemblyQualifiedName + "|" +
                   location;
        }

        /// <summary>
        /// 执行强制释放Entry相关逻辑。
        /// </summary>
        private void ForceReleaseEntry(GameResourceEntry entry)
        {
            if (entry == null || entry.IsReleased)
            {
                return;
            }

            entries.Remove(entry.Key);
            entry.ReferenceCount = 0;
            entry.IsReleased = true;
            if (entry.AssetHandle != null &&
                entry.AssetHandle.IsValid)
            {
                entry.AssetHandle.Release();
            }

            entry.AssetHandle = null;
            entry.EditorAsset = null;
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this || isShuttingDown)
            {
                return;
            }

            isShuttingDown = true;
            ReleaseAll();
            defaultPackage = null;
            Instance = null;
        }
    }

    /// <summary>
    /// 业务资源引用。每次 LoadAsset 都得到一个独立引用，必须 Release。
    /// </summary>
    public sealed class GameResourceHandle<T> :
        IEnumerator,
        IDisposable
        where T : Object
    {
        private GameResourceManager owner;
        private GameResourceEntry entry;
        private bool isReleased;

        /// <summary>
        /// 创建游戏资源Handle实例。
        /// </summary>
        internal GameResourceHandle(
            GameResourceManager manager,
            GameResourceEntry resourceEntry)
        {
            owner = manager;
            entry = resourceEntry;
        }

        /// <summary>
        /// 指示当前对象是否有效。
        /// </summary>
        public bool IsValid =>
            !isReleased &&
            owner != null &&
            owner.IsEntryAlive(entry);

        /// <summary>
        /// 指示当前操作是否已完成。
        /// </summary>
        public bool IsDone => !IsValid || entry.IsDone;

        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress => IsValid ? entry.Progress : 0f;

        /// <summary>
        /// 向调用方提供状态。
        /// </summary>
        public EOperationStatus Status =>
            IsValid ? entry.Status : EOperationStatus.None;

        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string Error => IsValid
            ? entry.Error
            : "资源句柄已经释放。";

        /// <summary>
        /// 当前引用的数量。
        /// </summary>
        public int ReferenceCount => IsValid
            ? entry.ReferenceCount
            : 0;

        /// <summary>
        /// 向调用方提供资源。
        /// </summary>
        public T Asset => IsValid
            ? entry.GetAsset<T>()
            : null;

        /// <summary>
        /// 向调用方提供当前。
        /// </summary>
        public object Current => null;

        /// <summary>
        /// 执行MoveNext相关逻辑。
        /// </summary>
        public bool MoveNext()
        {
            return !IsDone;
        }

        /// <summary>
        /// 执行重置相关逻辑。
        /// </summary>
        public void Reset()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 执行释放相关逻辑。
        /// </summary>
        public void Release()
        {
            if (isReleased)
            {
                return;
            }

            isReleased = true;
            GameResourceManager manager = owner;
            GameResourceEntry resourceEntry = entry;
            owner = null;
            entry = null;
            manager?.Release(resourceEntry);
        }

        /// <summary>
        /// 释放当前实例持有的资源。
        /// </summary>
        public void Dispose()
        {
            Release();
        }

        /// <summary>
        /// 获取Yoo资源Handle。
        /// </summary>
        internal AssetHandle GetYooAssetHandle()
        {
            return IsValid ? entry.AssetHandle : null;
        }
    }

    /// <summary>
    /// Prefab 实例和它所依赖资源引用的统一生命周期句柄。
    /// </summary>
    public sealed class GameResourceInstanceHandle :
        IEnumerator,
        IDisposable
    {
        private GameResourceManager owner;
        private GameResourceHandle<GameObject> assetHandle;
        private InstantiateOperation instantiateOperation;
        private GameObject instance;
        private string localError;
        private bool isFinalized;
        private bool isReleased;
        private int instanceId;

        /// <summary>
        /// 创建游戏资源InstanceHandle实例。
        /// </summary>
        internal GameResourceInstanceHandle(
            GameResourceManager manager,
            GameResourceHandle<GameObject> resourceHandle,
            InstantiateOperation operation,
            GameObject createdInstance,
            string error)
        {
            owner = manager;
            assetHandle = resourceHandle;
            instantiateOperation = operation;
            instance = createdInstance;
            localError = error;
        }

        /// <summary>
        /// 指示资源是否已释放。
        /// </summary>
        public bool IsReleased => isReleased;

        public bool IsDone
        {
            get
            {
                if (isReleased || !string.IsNullOrEmpty(localError))
                {
                    return true;
                }

                if (instantiateOperation != null)
                {
                    return instantiateOperation.IsDone;
                }

                return assetHandle == null || assetHandle.IsDone;
            }
        }

        public float Progress
        {
            get
            {
                if (instantiateOperation != null)
                {
                    return instantiateOperation.Progress;
                }

                return assetHandle?.Progress ?? 0f;
            }
        }

        public EOperationStatus Status
        {
            get
            {
                TryFinalize();
                if (isReleased)
                {
                    return EOperationStatus.None;
                }

                if (!string.IsNullOrEmpty(localError))
                {
                    return EOperationStatus.Failed;
                }

                if (instantiateOperation != null)
                {
                    return instantiateOperation.Status;
                }

                return instance != null
                    ? EOperationStatus.Succeeded
                    : assetHandle?.Status ?? EOperationStatus.None;
            }
        }

        public string Error
        {
            get
            {
                TryFinalize();
                if (!string.IsNullOrEmpty(localError))
                {
                    return localError;
                }

                return instantiateOperation?.Error ??
                       assetHandle?.Error;
            }
        }

        public GameObject Result
        {
            get
            {
                TryFinalize();
                return isReleased ? null : instance;
            }
        }

        /// <summary>
        /// 向调用方提供当前。
        /// </summary>
        public object Current => null;

        internal int InstanceId => instanceId;

        /// <summary>
        /// 执行MoveNext相关逻辑。
        /// </summary>
        public bool MoveNext()
        {
            if (!IsDone)
            {
                return true;
            }

            TryFinalize();
            return false;
        }

        /// <summary>
        /// 执行重置相关逻辑。
        /// </summary>
        public void Reset()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 执行释放相关逻辑。
        /// </summary>
        public void Release()
        {
            if (isReleased)
            {
                return;
            }

            if (owner != null)
            {
                owner.ReleaseInstanceHandle(this, true);
                return;
            }

            GameObject current = GetRawInstance();
            if (current != null)
            {
                Object.Destroy(current);
            }

            MarkReleased();
        }

        /// <summary>
        /// 释放当前实例持有的资源。
        /// </summary>
        public void Dispose()
        {
            Release();
        }

        /// <summary>
        /// 尝试Finalize，并返回是否成功。
        /// </summary>
        internal void TryFinalize()
        {
            if (isFinalized || isReleased || !IsDone)
            {
                return;
            }

            isFinalized = true;
            if (instantiateOperation != null)
            {
                if (instantiateOperation.Status ==
                    EOperationStatus.Succeeded)
                {
                    instance = instantiateOperation.Result;
                }
                else
                {
                    localError = instantiateOperation.Error;
                }
            }

            if (instance != null && owner != null)
            {
                owner.TrackInstance(this, instance);
            }
            else if (string.IsNullOrEmpty(localError))
            {
                localError = assetHandle?.Error ??
                             "Prefab 实例化结果为空。";
            }
        }

        /// <summary>
        /// 取消PendingInstantiation。
        /// </summary>
        internal void CancelPendingInstantiation()
        {
            if (instantiateOperation != null &&
                !instantiateOperation.IsDone)
            {
                instantiateOperation.Cancel();
            }

            if (instance == null &&
                instantiateOperation != null &&
                instantiateOperation.IsDone)
            {
                instance = instantiateOperation.Result;
            }
        }

        /// <summary>
        /// 获取原始值Instance。
        /// </summary>
        internal GameObject GetRawInstance()
        {
            TryFinalize();
            return instance;
        }

        /// <summary>
        /// 设置InstanceId。
        /// </summary>
        internal void SetInstanceId(int value)
        {
            instanceId = value;
        }

        /// <summary>
        /// 通知InstanceDestroyed。
        /// </summary>
        internal void NotifyInstanceDestroyed()
        {
            if (isReleased)
            {
                return;
            }

            if (owner != null)
            {
                owner.ReleaseInstanceHandle(this, false);
            }
            else
            {
                MarkReleased();
            }
        }

        /// <summary>
        /// 执行标记Released相关逻辑。
        /// </summary>
        internal void MarkReleased()
        {
            if (isReleased)
            {
                return;
            }

            isReleased = true;
            instance = null;
            instanceId = 0;
            instantiateOperation = null;
            assetHandle?.Release();
            assetHandle = null;
            owner = null;
        }
    }

    internal sealed class GameResourceEntry
    {
        /// <summary>
        /// 创建游戏资源Entry实例。
        /// </summary>
        public GameResourceEntry(
            string key,
            string packageName,
            string location,
            Type assetType,
            ResourcePackage package)
        {
            Key = key;
            PackageName = packageName;
            Location = location;
            AssetType = assetType;
            Package = package;
        }

        /// <summary>
        /// 公开的键数据。
        /// </summary>
        public readonly string Key;
        /// <summary>
        /// 公开的资源包名称数据。
        /// </summary>
        public readonly string PackageName;
        /// <summary>
        /// 公开的位置数据。
        /// </summary>
        public readonly string Location;
        /// <summary>
        /// 公开的资源类型数据。
        /// </summary>
        public readonly Type AssetType;
        /// <summary>
        /// 公开的资源包数据。
        /// </summary>
        public readonly ResourcePackage Package;

        /// <summary>
        /// 公开的资源句柄数据。
        /// </summary>
        public AssetHandle AssetHandle;
        /// <summary>
        /// 公开的Editor资源数据。
        /// </summary>
        public Object EditorAsset;
        /// <summary>
        /// 公开的Editor错误数据。
        /// </summary>
        public string EditorError;
        /// <summary>
        /// 公开的失败错误数据。
        /// </summary>
        public string FailureError;
        /// <summary>
        /// 公开的引用数量数据。
        /// </summary>
        public int ReferenceCount;
        /// <summary>
        /// 指示是否为已释放。
        /// </summary>
        public bool IsReleased;

        /// <summary>
        /// 指示当前操作是否已完成。
        /// </summary>
        public bool IsDone =>
            IsReleased ||
            !string.IsNullOrEmpty(FailureError) ||
            !string.IsNullOrEmpty(EditorError) ||
            EditorAsset != null ||
            AssetHandle == null ||
            AssetHandle.IsDone;

        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress => AssetHandle != null
            ? AssetHandle.Progress
            : IsDone ? 1f : 0f;

        public EOperationStatus Status
        {
            get
            {
                if (IsReleased)
                {
                    return EOperationStatus.None;
                }

                if (!string.IsNullOrEmpty(FailureError) ||
                    !string.IsNullOrEmpty(EditorError))
                {
                    return EOperationStatus.Failed;
                }

                if (EditorAsset != null)
                {
                    return EOperationStatus.Succeeded;
                }

                return AssetHandle?.Status ?? EOperationStatus.None;
            }
        }

        /// <summary>
        /// 最近一次操作失败的错误信息；未发生错误时为 null。
        /// </summary>
        public string Error =>
            FailureError ??
            EditorError ??
            AssetHandle?.Error;

        public T GetAsset<T>() where T : Object
        {
            if (EditorAsset != null)
            {
                return EditorAsset as T;
            }

            return AssetHandle?.GetAssetObject<T>();
        }

        /// <summary>
        /// 执行等待ForCompletion相关逻辑。
        /// </summary>
        public void WaitForCompletion()
        {
            if (AssetHandle != null && !AssetHandle.IsDone)
            {
                AssetHandle.WaitForAsyncComplete();
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class GameResourceInstanceTracker : MonoBehaviour
    {
        private GameResourceInstanceHandle owner;

        /// <summary>
        /// 执行绑定相关逻辑。
        /// </summary>
        public void Bind(GameResourceInstanceHandle handle)
        {
            owner = handle;
        }

        /// <summary>
        /// 解除已有绑定。
        /// </summary>
        public void Unbind(GameResourceInstanceHandle handle)
        {
            if (ReferenceEquals(owner, handle))
            {
                owner = null;
            }
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        private void OnDestroy()
        {
            GameResourceInstanceHandle handle = owner;
            owner = null;
            handle?.NotifyInstanceDestroyed();
        }
    }
}
