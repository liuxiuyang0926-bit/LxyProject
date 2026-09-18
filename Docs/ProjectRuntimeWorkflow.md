# LxyDemo 启动、热更新与资源工作流

> 本文按当前仓库代码与配置整理，适用于 Unity `2022.3.48f1c1`、YooAsset 3.x 和 HybridCLR。最后核对：2026-09-02。

## 1. 全局链路

项目的稳定启动壳由 `Game.Contracts`、`Game.Resource`、`Game.Main` 组成；它们编入 Player/AOT，**不能直接引用**热更新程序集中的具体业务类型。全部游戏逻辑统一位于 `Game.Logic`，并通过稳定契约反向接入启动壳。

```text
Build Settings: Start
  └─ GameMain（常驻）
       ├─ YooAssetLauncher：配置、版本、清单、Mandatory 下载、缓存清理
       ├─ HybridCLRLoader：AOT 元数据 → 热更新 DLL
       └─ 动态创建 Game.Logic 场景服务：切换到 Login
            └─ 动态创建 Game.Logic / UIStartup（IFirstSceneRuntime）
                 ├─ UICanvasRoot
                 ├─ UIManager（C# UI）
                 └─ LuaUIRuntime（可选）→ Main.lua
```

`Start`、`Login`、`BattleDemo` 已在 Build Settings 中；正常启动进入 `Start`，首个业务场景为 `Login`。`GameMain` 和各项常驻服务使用 `DontDestroyOnLoad`。

## 2. 玩家运行时启动顺序

### 2.1 Start 场景与 GameMain

`GameMain`（`Packages/LxyGame/LxyGame.Main/Runtime/GameMain.cs`）以较早执行顺序创建或取得 `YooAssetLauncher`、`HybridCLRLoader`，并创建一个位于 `Resources/UI/UISlider` 的启动下载界面。随后严格按下面顺序执行；任一阶段失败均停止，显示错误或整包强更页面。

| 顺序 | 阶段 | 责任与完成条件 |
| --- | --- | --- |
| 1 | `UpdatingResources` | `YooAssetLauncher.InitializeAsync` 成功，当前 `DefaultPackage` 的启动资源可用。 |
| 2 | `StartingHotUpdateRuntime` | `HybridCLRLoader.LoadAndStart` 成功，补充元数据和全部配置的热更新 DLL 已加载。 |
| 3 | `LoadingFirstScene` | 从已加载的 `Game.Logic` 创建 `GameSceneManager`，完成 `LoadSceneAsync("Login")` 单场景切换。 |
| 4 | `StartingUIAndLua` | 从已加载的 `Game.Logic` 动态创建 `LxyDemo.UIFramework.UIStartup`，并等待其 `IFirstSceneRuntime.InitializeAsync()` 成功。 |
| 5 | `Ready` | 游戏可进入登录业务流程。 |

关键约束：**在 HybridCLR 阶段完成前，不能加载包含热更新 `MonoBehaviour` 的场景或 Prefab。** 因此 `Login` 保持为内置场景，场景服务和首场景 UI 启动器由反射从 `Game.Logic` 中动态添加，而不是序列化在旧 APK 的场景里。

### 2.2 YooAsset 初始化与更新

`YooAssetLauncher` 的默认包为 `DefaultPackage`，Player 默认使用 `Host` 模式；编辑器固定为 `EditorAssetDatabase`，不会真实下载 Bundle 或用 `Assembly.Load` 加载 DLL。

```text
Player Host 模式
  GameRuntimeConfig.GameConfigUrl
    → GET gameConfig.json（默认 15 秒超时）
    → 校验客户端最低版本（必要时整包强更并停止）
    → 得到 CDN 根目录：{downloadUrl}/{Android|IPhone}/{version}
    → InitializePackageAsync（内置文件系统 + Sandbox 缓存文件系统）
    → RequestPackageVersionAsync
    → LoadPackageManifestAsync(packageVersion)
    → 校验启动 Location
    → 准备启动背景
    → 下载 `Mandatory` 标签的全部差异 Bundle
    → ClearUnusedBundleFiles（失败仅告警）
    → Package 就绪
```

`gameConfig.json` 的外层必须满足 `code == 200`，并含有 `data.version`、`data.downloadUrl`。可选字段包括：

```json
{
  "code": 200,
  "message": "ok",
  "data": {
    "version": "1.0.1",
    "downloadUrl": "https://cdn.example.com/game",
    "minimumAppVersion": "1.0.0",
    "minimumAndroidVersionCode": 0,
    "appDownloadUrl": "https://example.com/download",
    "forceUpdateMessage": "请升级客户端"
  }
}
```

首次请求的种子 URL 在 `GameRuntimeConfig` 中编入 AOT 主包；热更新代码可通过 `TryApplyBootstrapOverride` 覆盖并持久化该 URL/超时配置。正式强更包应保持 `fallbackToOffline = false`，以免在强更或远端配置错误时回退到不兼容的旧内置内容。

**整包强更：** 当 `minimumAppVersion` 或 Android `minimumAndroidVersionCode` 高于当前客户端时，启动停在 `ForceUpdateRequired`，调用方可通过 `TryOpenForceUpdatePage` 打开 `appDownloadUrl`。AOT 壳、Player 设置、引擎/插件 AOT 程序集或补充元数据需求改变时，必须走这条路径并发布新的 APK/IPA。

### 2.3 HybridCLR：元数据与热更新程序集

资源流程成功后，`HybridCLRLoader` 从同一个 `DefaultPackage` 读取 `HybridCLRAssemblyManifest`。运行时清单当前位于：

`Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes`

当前清单的职责与顺序：

1. 对 `aotMetadataDlls` 中每个 DLL，以 `RuntimeApi.LoadMetadataForAOTAssembly(..., HomologousImageMode.SuperSet)` 加载补充元数据。
2. 依清单顺序读取 `hotUpdateDlls` 的 `.bytes` 并执行 `Assembly.Load`。
3. 将已加载程序集交给 `GameMain`，由它寻找 `Game.Logic` 的场景服务和 `UIStartup`。

当前 `HybridCLRSettings.asset` 的热更新程序集顺序为：

```text
Game.Logic
```

`HybridCLRAssetSynchronizer` 会将这个唯一的业务 DLL 写入运行时清单。AOT 元数据清单与热更新 DLL 清单彼此独立；补充元数据不是 AOT 代码替代品。

编辑器内不会重复 `Assembly.Load`，而是读取同一份清单，并确认对应 asmdef 已被 Unity 加载。因此，**Editor 能运行不代表目标 Player 的下载、IL2CPP 或 HybridCLR 产物正确。**

### 2.4 进入 Login、UI 与 Lua

`GameSceneManager` 用 `LoadSceneMode.Single` 加载 `Login`；加载完成且旧场景对象释放后，默认调用 `GameResourceManager.UnloadUnusedAssetsAsync()` 回收引用数为零的内存资源。

随后 `GameMain` 从 `Game.Logic` 中动态创建 `UIStartup`，它会：

1. 复用已就绪的 `YooAssetLauncher`；从 Login 场景直接 Play 时才自行创建一个，方便编辑器调试。
2. 通过 `GameResourceManager.InstantiateAsync` 加载 `Assets/GameResources/Prefabs/UICanvasRoot.prefab`。
3. 初始化 `UILayerRoot`、EventSystem（需要时）与 `UIManager`。
4. 若 `UIRuntimeConfig.EnableLuaRuntime` 为真，创建 `LuaUIRuntime`，配置当前包名，加载 Lua 启动模块并执行 `Main.lua`。

Lua 在 Player 中经由 YooAsset 按 `Assets/GameResources/Lua/{模块路径}.bytes` 读取；编辑器则直接从项目 Lua 目录读取。Lua 与 UI 资源也属于启动前必须可用的 `Mandatory` 内容。

## 3. 资源分层、收集与 Location 规则

收集器配置为 `Assets/BundleCollectorSetting.asset`，唯一包为 `DefaultPackage`。资源分层由标签与首包复制策略共同决定：

| 分层 | 标签 | 当前内容/用途 | 交付方式 |
| --- | --- | --- | --- |
| 首包 | `Builtin;Mandatory` | `HybridCLR/AOT`、`UICanvasRoot.prefab` | 随 APK/IPA 复制到 `StreamingAssets/yoo/DefaultPackage`；亦参与远端版本更新。 |
| 启动强更 | `Mandatory` | 热更新 DLL、程序集清单、Lua、字体、Login/Main UI、启动背景、Battle 等 | Host 启动时全部下载完成后才进入 Login。 |

构建策略固定为 `ScriptableBuildPipeline` + `ClearAndCopyByTags`，复制标签为 `Builtin`。相关菜单：

- `工具/YooAsset/应用首包与全量强更策略`
- `工具/YooAsset/验证首包与全量强更策略`

所有主资源必须带 `Builtin` 或 `Mandatory` 标签。新增资源时请增加**精确的** Collector；不要对 `Assets/GameResources` 新增根 Collector，避免同一资源被重复收集。

多数 HybridCLR 收集器使用 `AddressByFileName`，所以包内文件名必须唯一。例如：

```text
Assets/GameResources/HybridCLR/AOT/mscorlib.dll.bytes
  → Location: mscorlib.dll
Assets/GameResources/HybridCLR/HotUpdate/Game.Logic.dll.bytes
  → Location: Game.Logic.dll
Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes
  → Location: HybridCLRAssemblyManifest
```

代码中的 Location 必须与 Collector 的地址规则一致。启动时目前至少校验 `HybridCLRAssemblyManifest` 这个 Location；清单中后续列出的 DLL 由 `HybridCLRLoader` 逐一校验。

## 4. 业务资源加载与释放

业务代码只应使用 `GameResourceManager`，不要把原始 YooAsset `AssetHandle` 交给业务层长期持有。

```csharp
// 普通资源：调用方持有一份业务引用，使用完成后恰好 Release 一次。
GameResourceHandle<TextAsset> handle = manager.LoadAssetAsync<TextAsset>(location);
yield return handle;
if (handle.Status == EOperationStatus.Succeeded)
{
    Use(handle.Asset);
}
handle.Release();

// Prefab：通过实例句柄统一销毁实例和释放底层资源引用。
GameResourceInstanceHandle instanceHandle = manager.InstantiateAsync(prefabLocation, parent);
yield return instanceHandle;
GameObject instance = instanceHandle.Result;
// 关闭/销毁时：instanceHandle.Release(); 或 manager.ReleaseInstance(instance)
```

资源管理器以 `包名 + 类型 + Location` 合并底层加载，但每次加载都会产生独立的业务引用。`GameResourceInstanceHandle.Release()` 会销毁其 Prefab 实例并归还资源引用；实例被外部销毁时，跟踪器也会归还引用。

三个清理操作不能混用：

| 操作 | 作用 | 不做什么 |
| --- | --- | --- |
| `handle.Release()` | 归还一次业务引用，必要时释放底层资源句柄。 | 不保证立刻卸载 Bundle。 |
| `UnloadUnusedAssetsAsync()` | 释放引用计数为零的内存 Provider/Bundle。 | 不删除下载缓存。 |
| `ClearCacheAsync(ClearUnusedBundleFiles)` | 删除当前有效 Manifest 已不再引用的磁盘 Bundle。 | 不替代句柄释放；不应通过手删 Sandbox 缓存替代它。 |

`GetSnapshots()` 可用于排查仍被持有的 Location 和引用数；切换 Package 前 `GameResourceManager` 会先回收旧包句柄。

## 5. 日常改动与发布操作

### 5.1 按改动类型决定交付物

| 改动 | 必需操作 |
| --- | --- |
| 仅热更新 C# / Lua / YooAsset 资源 | 生成并同步 HybridCLR（若涉及 C#）、构建新的 YooAsset 包版本、上传 CDN；无需新 APK。 |
| AOT 壳、Player 设置、插件 AOT、补充元数据要求 | 除上述 YooAsset 操作外，重新构建并发布 APK/IPA，并在远端配置提高最低客户端版本。 |
| 仅内置首包策略或 Builtin 资源 | 构建新的 YooAsset 后重新构建 APK/IPA；如远端同版本也要替换，则同时发布新不可变 CDN 包版本。 |

### 5.2 热更新发布清单

1. Unity 切换到最终目标平台（Android 或 iPhone）。
2. 修改代码/资源/Collector 后，确认 asmdef 依赖仍满足 AOT 不依赖热更具体类型的约束。
3. 若改动热更新 C#，执行 `工具/HybridCLR/生成全部并同步到 YooAsset`；它会生成 HybridCLR 产物、同步 AOT 元数据与热更新 `.dll.bytes`，并重写程序集清单。
4. 执行 `工具/YooAsset/应用首包与全量强更策略`，再执行验证菜单；处理所有未分类资源、标签或地址冲突。
5. 用 `ScriptableBuildPipeline` 构建 `DefaultPackage`，使用**新的、不可变** Package Version。`ClearAndCopyByTags` 会将 Builtin Bundle 和 Manifest 写入 StreamingAssets。
6. 向 CDN 上传该版本目录下的完整清单/Catalog、hash/version 文件及所有被引用 Bundle；不得用新内容覆盖已发布版本。
7. 上传并校验完整资源后，最后才切换远端 `gameConfig.json` 的 `version`；CDN 指向格式必须是 `{downloadUrl}/{平台}/{version}`。
8. 如果有 AOT 改动，构建并安装新的 APK/IPA，并设置合适的 `minimumAppVersion`/`minimumAndroidVersionCode` 与下载地址。
9. 在真实目标 Player 的 Host 模式完成从冷启动到 Login 的冒烟测试；编辑器测试不能替代该步骤。

## 6. 验收与故障定位

建议每次发布至少验证：远端配置请求、包版本、Manifest、Mandatory 下载、AOT 元数据、每个热更新 DLL、Login、UICanvasRoot、C# UI 和 Lua（启用时）。优先定位第一个失败边界，而不是在后续报错处修补。

| 现象 | 首查位置 |
| --- | --- |
| 请求 `gameConfig.json` 或包版本失败 | 种子/持久化 URL、网络、平台目录、`downloadUrl`、`version`。 |
| 触发强更但无法跳转 | 最低版本规则和有效 HTTP/HTTPS 的 `appDownloadUrl`。 |
| Manifest 成功但启动校验失败 | `HybridCLRAssemblyManifest` Collector、`AddressByFileName` Location、CDN 是否上传了同版本完整 Manifest。 |
| Mandatory 下载失败 | CDN 版本目录是否完整、Bundle/hash 是否对应、版本是否被覆盖。 |
| `LoadMetadataForAOTAssembly` 失败 | 当前目标平台的 HybridCLR AOT 生成产物、同步结果、AOT 清单。 |
| `Assembly.Load` 失败或找不到类型 | HybridCLR Settings 的 DLL 列表/顺序、运行时清单、`.dll.bytes`、程序集依赖。 |
| Editor 正常，Player 失败 | 必查 Host 下载、IL2CPP/HybridCLR 产物、目标平台、首包复制与裁剪。 |
| UI 或 Lua 资源缺失 | `UICanvasRoot`、Lua/UI Collector、Mandatory 标签及调用 Location。 |
| 内存/资源似乎泄漏 | `GameResourceManager.GetSnapshots()`、每个业务 Handle 是否恰好 Release 一次、Prefab 是否经实例句柄释放。 |

## 7. 关键文件索引

| 主题 | 文件 |
| --- | --- |
| 启动编排 | `Packages/LxyGame/LxyGame.Main/Runtime/GameMain.cs` |
| YooAsset 生命周期 | `Packages/LxyGame/LxyGame.Resources/Runtime/YooAssetLauncher.cs` |
| 资源句柄与引用计数 | `Packages/LxyGame/LxyGame.Resources/Runtime/GameResourceManager.cs` |
| HybridCLR 加载 | `Packages/LxyGame/LxyGame.Main/Runtime/HybridCLRLoader.cs` |
| 生成与同步工具 | `Packages/LxyGame/LxyGame.Main/Editor/HybridCLRAssetSynchronizer.cs` |
| 首场景 UI 运行时 | `Packages/LxyGame/LxyGame.Logic/Runtime/UI/Base/UIStartup.cs` |
| 场景切换 | `Packages/LxyGame/LxyGame.Logic/Runtime/Scene/GameSceneManager.cs` |
| AOT/热更共同配置 | `Packages/LxyGame/LxyGame.Contracts/Runtime/GameRuntimeConfig.cs` |
| HybridCLR 实时设置 | `ProjectSettings/HybridCLRSettings.asset` |
| YooAsset Collector | `Assets/BundleCollectorSetting.asset` |
| 首包/全量强更构建策略 | `Assets/Editor/YooAssetContentPolicy.cs` |
