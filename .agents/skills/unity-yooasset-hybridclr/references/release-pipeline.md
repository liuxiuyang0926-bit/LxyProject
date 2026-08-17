# YooAsset 与 HybridCLR 发布流水线

## 事实来源

- 完整项目流程：`Docs/ProjectResourceYooAssetHybridCLRWorkflow.md`
- 资源分层：`Docs/YooAssetResourceLayers.md`
- AOT 整包更新：`Docs/AOTForceUpdate.md`
- 架构：`Packages/com.lxy.main/Documentation~/Architecture.md`
- HybridCLR 设置：`ProjectSettings/HybridCLRSettings.asset`
- 收集器设置：`Assets/BundleCollectorSetting.asset`
- 运行时入口：`GameMain`、`YooAssetLauncher` 和 `HybridCLRLoader`
- 生成资源同步工具：`Packages/com.lxy.main/Editor/HybridCLRAssetSynchronizer.cs`

流水线发生改动后，应重新读取这些实时文件，不要假设本参考文档仍然代表当前状态。

## 运行时顺序

```text
Start 场景 / GameMain
  -> 获取启动配置并执行整包版本门槛检查
  -> 初始化 DefaultPackage
  -> 请求包版本
  -> 加载清单并验证启动所需 Location
  -> 下载并校验全部 Mandatory Bundle
  -> 清理当前清单不再使用的 Bundle
  -> 加载 AOT 补充元数据
  -> 按依赖顺序 Assembly.Load 热更新 DLL
  -> 加载 Login
  -> 初始化 IFirstSceneRuntime、C# UI 和可选 Lua 运行时
```

在 DLL 加载步骤完成前，不得加载包含热更新 MonoBehaviour 的场景或 Prefab。

## 程序集边界

当前稳定 AOT 壳：

- `Game.Contracts`
- `Game.Resource`
- `Game.Scene`
- `Game.Main`

当前预期的热更新加载顺序：

1. `Game.Common`
2. `Game.Lua`
3. `Game.UI`
4. `Game.Battle`
5. `Assembly-CSharp`

以 `ProjectSettings/HybridCLRSettings.asset` 为实时事实来源。同步工具会验证依赖顺序。不得加入仅限 Editor 的程序集。

AOT 元数据列表和热更新 DLL 列表相互独立。补充元数据用于提供泛型/运行时元数据支持，不能替代 AOT 代码。

## 资源分层

包：`DefaultPackage`

| 标签 | 含义 |
| --- | --- |
| `Builtin` | 将 Bundle 复制到 Player 的 `StreamingAssets/yoo/DefaultPackage` 下 |
| `Mandatory` | 进入 Login 前确保 Bundle 为当前版本 |

`Builtin` 分组使用 `Builtin;Mandatory`。Mandatory 分组在初次安装时位于远端。当前项目策略不使用游戏过程中的按需下载。

为新内容增加精确收集器。不要在 `Assets/GameResources` 上增加根收集器，否则同一资源可能被收集两次。

大多数 HybridCLR 收集器使用 `AddressByFileName`：

- `AOT/mscorlib.dll.bytes` -> `mscorlib.dll`
- `HotUpdate/Game.UI.dll.bytes` -> `Game.UI.dll`
- `HybridCLRAssemblyManifest.bytes` -> `HybridCLRAssemblyManifest`

保持包内 Address 唯一，并按照收集器规则验证代码中的 Location。

## 业务资源所有权

通过 `GameResourceManager`、`GameResourceHandle<T>` 和 `GameResourceInstanceHandle` 加载。每个返回的业务句柄恰好释放一次。区分：

- `handle.Release()`：释放一份业务引用，并在适当时机释放底层资源句柄。
- `UnloadUnusedAssetsAsync()`：释放未被引用的内存 Provider/Bundle。
- `ClearCacheAsync(ClearUnusedBundleFiles)`：删除当前活动清单不再引用的磁盘 Bundle。

诊断引用或版本问题时，不要手工删除 Sandbox 缓存。

## 构建与发布顺序

热更新代码改动的发布步骤：

1. 把 Unity 切换到最终目标平台。
2. 运行 `工具/HybridCLR/生成全部并同步到 YooAsset`。
3. 确认生成的 AOT 元数据、热更新 `.dll.bytes` 和 `HybridCLRAssemblyManifest.bytes` 与设置一致。
4. 运行 `工具/YooAsset/应用首包与全量强更策略`。
5. 运行 `工具/YooAsset/验证首包与全量强更策略`。
6. 使用 `ScriptableBuildPipeline`、`ClearAndCopyByTags` 和复制标签 `Builtin` 构建 `DefaultPackage`。
7. 使用新的包版本；不得用不同内容覆盖已经发布的版本。
8. 上传完整清单/Catalog 和所有被引用的 Bundle。
9. 上传并校验完成后，最后再切换远端 `gameConfig.json` 版本。
10. 使用目标 Player 以 Host-mode 完成直至 Login 的冒烟测试。

如果 AOT 壳、引擎/插件 AOT 程序集、Player 设置或补充元数据需求发生变化，除了所需资源版本外，还必须生成新的 APK/IPA。

## 失败现象映射

| 现象 | 优先检查 |
| --- | --- |
| 请求包版本失败 | 启动 URL、平台路径、远端配置、网络连通性 |
| 清单加载成功但启动校验失败 | 必需 Location、收集器地址规则、已上传清单 |
| Mandatory 下载失败 | 完整版本是否上传、Bundle 哈希、不可变版本目录 |
| AOT 元数据加载失败 | 补充 AOT 设置、目标平台、已生成和同步的元数据 bytes |
| DLL 缺失或 `Assembly.Load` 失败 | 热更新列表/顺序、生成清单、同步的 `.dll.bytes`、依赖关系 |
| Editor 成功但 Player 失败 | Host-mode 下载、IL2CPP/HybridCLR 产物、目标平台构建、裁剪 |
| 资源看似泄漏 | 业务句柄所有权和 `GameResourceManager` 快照 |
