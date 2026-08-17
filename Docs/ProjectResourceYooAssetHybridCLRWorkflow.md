# LxyDemo 资源加载、YooAsset 打包与 HybridCLR 热更新流程

> 最后核对日期：2026-08-14
>
> 适用工程：`LxyDemo`
>
> 当前技术栈：Unity 2022.3.48f1c1、YooAsset 3.0.5、HybridCLR、XLua

本文描述的是当前工程已经落地的真实流程，不是通用示例。后续修改资源分层、程序集列表或发布脚本时，应同步更新本文。

---

## 1. 先看结论

当前工程的启动原则是：

```text
先更新资源 Manifest
    ↓
下载全部 Mandatory 强更资源
    ↓
加载 AOT 补充元数据
    ↓
按依赖顺序 Assembly.Load 热更新 DLL
    ↓
加载 Login 场景
    ↓
初始化 C# UI，并按开关决定是否启动 Lua
```

当前不使用游戏内按需下载。除了极少量 APK 内置资源外，其余被 YooAsset 管理的资源全部属于 `Mandatory`，必须在进入 `Login` 前准备完成。

必须始终保持下面这条产物链一致：

```text
HybridCLRSettings
    ↓ GenerateAll
HybridCLRData + AOTGenericReferences
    ↓ 自定义同步工具
Assets/GameResources/HybridCLR/*.bytes + HybridCLRAssemblyManifest.bytes
    ↓ YooAsset Build
同一个 Package Version 的 Manifest + Bundle
    ↓ 完整上传
CDN/资源服务器
```

只执行其中一部分，会出现 DLL 找不到、Location 无效、Manifest 与 Bundle 不匹配等问题。

---

## 2. 工程中的职责边界

### 2.1 资源与代码目录

```text
Assets/
├─ GameResources/                 YooAsset 收集的游戏资源
│  ├─ HybridCLR/                  AOT 元数据、热更新 DLL、运行时 DLL 清单
│  ├─ Lua/                        Lua 脚本
│  ├─ Fonts/                      字体
│  ├─ Prefabs/                    UI 与其他 Prefab
│  ├─ UIAtlas/                    图集源文件和生成图集
│  ├─ UITexture/                  UI 图片
│  └─ Battle/                     战斗配置
├─ Resources/UI/UISlider.prefab   启动强更界面，必须在 YooAsset 初始化前可用
├─ Scenes/                        Start、Login 等场景
└─ StreamingAssets/yoo/           构建 APK 时复制进去的 YooAsset 内置产物

Packages/
├─ com.lxy.main/                  稳定启动壳、HybridCLRLoader
├─ com.lxy.resource/              YooAssetLauncher、GameResourceManager
├─ com.lxy.contracts/             AOT/热更新边界接口和启动配置
├─ com.lxy.scene/                 场景切换
├─ com.lxy.core/                  通用热更新代码，程序集名为 Game.Common
├─ com.lxy.lua/                   XLua 业务运行时
├─ com.lxy.ui/                    UI 业务运行时
└─ com.lxy.battle/                帧同步战斗运行时
```

### 2.2 当前程序集分类

| 分类 | 当前程序集 | 能否只靠 CDN 替换 |
| --- | --- | --- |
| AOT 启动壳 | `Game.Contracts`、`Game.Resource`、`Game.Scene`、`Game.Main` | 否，修改后需要重新出 APK/IPA |
| 热更新程序集 | `Game.Common`、`Game.Lua`、`Game.UI`、`Assembly-CSharp`、`Game.Battle` | 是，重新生成 DLL 并构建 YooAsset 即可 |
| 引擎/插件 AOT | Unity 模块、HybridCLR.Runtime、YooAsset、XLua.Runtime 等 | 通常否 |
| Editor-only | `*.Editor.dll` | 不进入 Player，也不加入热更新列表 |

注意：

- `Assembly-CSharp.dll` 不是 Unity 引擎自带 DLL，而是 Unity 为没有归入 asmdef 的项目脚本生成的默认用户程序集。
- `Game.Main` 是稳定 AOT 启动壳，负责资源更新和加载热更新 DLL。
- AOT 程序集不能直接引用只存在于热更新层的具体类型。跨层调用应通过 `Game.Contracts`、接口、桥接对象或在热更新 DLL 加载完成后创建的场景/Prefab 完成。
- 热更新程序集可以引用 AOT 程序集；热更新程序集之间也可以引用，但 DLL 加载顺序必须满足依赖顺序。

正确的热更新加载顺序应是：

```text
Game.Common.dll
    ↓
Game.Lua.dll
    ↓
Game.UI.dll
    ↓
Game.Battle.dll
    ↓
Assembly-CSharp.dll
```

自定义同步工具会检查：

```text
Game.Common < Game.Lua < Game.UI < Assembly-CSharp
Game.Battle < Assembly-CSharp
```

---

## 3. 当前 YooAsset 资源分层

配置文件：`Assets/BundleCollectorSetting.asset`

Package：

```text
DefaultPackage
```

当前只有两个有效资源层，没有 `OnDemand`：

| Group | Group Tags | 是否进入 APK | 运行时行为 |
| --- | --- | --- | --- |
| `Builtin` | `Builtin;Mandatory` | 是 | APK 安装时已有；远端版本更新时仍属于 Mandatory 比对范围 |
| `Mandatory` | `Mandatory` | 否 | Host 模式进入 Login 前必须全部下载成功 |

`Builtin` 同时带有 `Mandatory` 的原因：它描述的是两个不同维度。

```text
Builtin   = 这个 Bundle 是否复制进 APK
Mandatory = 这个 Bundle 是否属于启动强更集合
```

所以内置资源不是永远不更新。新资源版本发布后，如果内置 Bundle 内容发生变化，YooAsset 仍会按新 Manifest 下载新 Bundle 到缓存，并优先使用缓存版本。

### 3.1 Builtin 当前包含

| 收集路径 | AddressRule | PackRule | 作用 |
| --- | --- | --- | --- |
| `Assets/GameResources/HybridCLR/AOT` | `AddressByFileName` | `PackDirectory` | AOT 补充元数据，保证 HybridCLR 可启动 |
| `Assets/GameResources/Prefabs/UICanvasRoot.prefab` | `AddressByFileName` | `PackSeparately` | UI 根节点 |

此外，启动下载界面 `Resources/UI/UISlider.prefab` 走 Unity `Resources.Load`，不依赖 YooAsset，因此能在 Manifest 尚未加载时显示。

### 3.2 Mandatory 当前包含

| 收集路径 | 主要内容 |
| --- | --- |
| `Assets/GameResources/HybridCLR/HotUpdate` | 热更新 DLL 的 `.dll.bytes` |
| `Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes` | AOT/热更新 DLL 运行时清单 |
| `Assets/GameResources/Lua` | 全部 Lua 脚本 |
| `Assets/GameResources/Fonts` | 字体资源 |
| `Assets/GameResources/Prefabs/UIRes/Login` | Login UI |
| `Assets/GameResources/Prefabs/UIRes/Main` | Main UI |
| `Assets/GameResources/UIAtlas/UIAtlas_AutoGen` | 自动生成图集 |
| `Assets/GameResources/UITexture/Startup` | 可轮换启动背景 |
| `Assets/GameResources/UITexture/StartupBackgroundConfig.json` | 启动背景配置 |
| `Assets/GameResources/Battle` | 战斗配置 |

新增资源时，应将精确目录或资源加入 `Mandatory` Group。不要再添加一个覆盖整个 `Assets/GameResources` 的根目录 Collector，否则同一资源会被父目录和子目录重复收集，触发：

```text
Collecting asset file already exists
```

### 3.3 Address 与代码 Location

当前大部分 Collector 使用 `AddressByFileName`，代码中的 Location 通常是文件名去掉最后一层 `.bytes` 后的名称。例如：

```text
Assets/GameResources/HybridCLR/AOT/mscorlib.dll.bytes
    → Location: mscorlib.dll

Assets/GameResources/HybridCLR/HotUpdate/Game.UI.dll.bytes
    → Location: Game.UI.dll

Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes
    → Location: HybridCLRAssemblyManifest
```

同一个 Package 内使用 `AddressByFileName` 时必须避免重名资源，否则 Address 会冲突。对于代码中使用完整 Assets 路径加载的资源，必须确认 YooAsset 的 Location 规则确实支持该地址。

---

## 4. 游戏启动时的完整运行流程

入口脚本：

- `Packages/com.lxy.main/Runtime/GameMain.cs`
- `Packages/com.lxy.resource/Runtime/YooAssetLauncher.cs`
- `Packages/com.lxy.main/Runtime/HybridCLRLoader.cs`

### 4.1 总时序

```text
Start 场景
    ↓
GameMain.Awake
    ├─ 动态添加/复用 YooAssetLauncher
    ├─ 动态添加/复用 HybridCLRLoader
    └─ DontDestroyOnLoad
    ↓
Resources.Load("UI/UISlider")
    ↓
实例化 UISlider，并动态 AddComponent<StartupDownloadView>
    ↓
YooAssetLauncher.InitializeAsync
    ↓
准备/下载 Mandatory
    ↓
HybridCLRLoader.LoadAndStart
    ├─ 加载 HybridCLRAssemblyManifest
    ├─ LoadMetadataForAOTAssembly
    └─ Assembly.Load 热更新 DLL
    ↓
GameSceneManager.LoadSceneAsync("Login")
    ↓
等待 IFirstSceneRuntime
    ↓
UIStartup.InitializeAsync
    ├─ 实例化 UICanvasRoot
    ├─ 初始化 UIManager
    └─ 根据 UIRuntimeConfig.EnableLuaRuntime 决定是否 require Main.lua
    ↓
Ready
```

重要约束：任何挂有热更新 MonoBehaviour 的 Prefab 或场景，都必须在 `HybridCLRLoader` 完成后加载。

### 4.2 Editor 模式

在 Unity Editor 中：

- `YooAssetLauncher` 强制使用 `EditorAssetDatabase`。
- `GameResourceManager` 通过 `AssetDatabase.LoadAssetAtPath` 加载资源。
- 不请求服务器、不下载 Bundle。
- 热更新 asmdef 已被 Unity 编辑器加载，因此 `HybridCLRLoader` 不会再次 `Assembly.Load`，只校验生成的运行时清单中列出的程序集是否已经存在。

这意味着 Editor Play 成功不等于真机 Host 流程一定成功。发布前必须至少做一次目标平台 Player 验证。

### 4.3 Player Host 模式

当前 Host 模式顺序如下：

```text
GameRuntimeConfig.GameConfigUrl
    ↓ UnityWebRequest.Get
解析 gameConfig.json
    ↓
拼出 HostServer
    ↓
初始化 BuiltinFileSystem + Sandbox/CacheFileSystem
    ↓
RequestPackageVersionAsync
    ↓
LoadPackageManifestAsync
    ↓
校验 HybridCLRAssemblyManifest Location
    ↓
准备可热更启动背景
    ↓
CreateResourceDownloader(Mandatory)
    ↓
下载并校验所有差异 Bundle
    ↓
ClearCacheAsync(ClearUnusedBundleFiles)
    ↓
HybridCLR 加载
```

当前关键参数：

| 参数 | 当前值 |
| --- | --- |
| Package | `DefaultPackage` |
| 启动强更 | 开启，只下载 `Mandatory` 标签集合 |
| 最大并发下载数 | `10` |
| 单文件失败重试 | `3` |
| 成功后清理废弃 Bundle | 开启 |
| 下载失败降级 Offline | 关闭 |

正式包关闭 `fallbackToOffline` 是合理的：Manifest 或 Mandatory 下载失败时终止启动，避免玩家带着旧代码和新资源进入游戏。

### 4.4 Offline 模式

Offline 模式只使用 `StreamingAssets/yoo/DefaultPackage` 中的内置资源，不连接远端服务器。

由于 APK 只复制 `Builtin` Bundle，而热更新 DLL、Lua 和大部分 UI 属于非内置 Mandatory，当前资源分层并不适合把 Offline 当成完整正式游戏模式。它更适合资源服务器不可用时的受限测试；正式启动仍应使用 Host。

### 4.5 没有差异资源时

`CreateResourceDownloader(Mandatory)` 返回下载数量为 0 后，资源流程直接成功。为了避免进度条瞬间消失，`StartupDownloadView` 会播放 3 秒的 `0 → 1` 平滑启动动画，然后继续加载 HybridCLR 和 Login。

### 4.6 第二次启动与中断续传

- 已经完整下载并校验通过、且仍被新 Manifest 引用的 Bundle 会复用，不会重复下载。
- 下载到一半杀进程后，已经完整完成的 Bundle 会保留并复用。
- 正在下载但尚未完成的单个 Bundle，下次通常会重新请求该 Bundle。
- 临时文件由 YooAsset 下载系统管理；业务代码不要直接删除 Sandbox 缓存目录。
- `ClearUnusedBundleFiles` 只删除当前 Manifest 不再引用的旧 Bundle，不会删除当前有效资源。

---

## 5. 统一资源加载与引用计数

业务层应使用：

```text
GameResourceManager
GameResourceHandle<T>
GameResourceInstanceHandle
```

不要在业务代码中长期直接持有 YooAsset `AssetHandle`。

### 5.1 异步加载普通资源

```csharp
GameResourceHandle<Sprite> handle =
    GameResourceManager.Instance.LoadAssetAsync<Sprite>(location);

yield return handle;

if (handle.Status == EOperationStatus.Succeeded)
{
    Sprite sprite = handle.Asset;
    // 使用 sprite
}
else
{
    Debug.LogError(handle.Error);
}

handle.Release();
```

每次 `LoadAssetAsync/LoadAssetSync` 都会获得一个独立业务引用。即使 Location 相同，也必须对每次返回的 Handle 分别调用一次 `Release()`。

### 5.2 实例化 Prefab

```csharp
GameResourceInstanceHandle instanceHandle =
    GameResourceManager.Instance.InstantiateAsync(
        prefabLocation,
        parent);

yield return instanceHandle;

if (instanceHandle.Status == EOperationStatus.Succeeded)
{
    GameObject instance = instanceHandle.Result;
}

// 销毁实例并归还底层 Prefab 引用
instanceHandle.Release();
```

实例上会动态添加内部 Tracker。如果对象被外部 `Destroy`，Tracker 会通知管理器归还资源引用；但业务代码仍应优先显式释放 Handle，让生命周期清晰可追踪。

### 5.3 内部引用计数规则

资源缓存键由以下三项组成：

```text
PackageName + AssetType + Location
```

相同键只保留一个底层资源条目：

```text
第一次 Load → 创建 YooAsset AssetHandle，ReferenceCount = 1
第二次 Load → 复用条目，ReferenceCount = 2
第一次 Release → ReferenceCount = 1
第二次 Release → ReferenceCount = 0，释放 YooAsset AssetHandle
```

`Release()` 可重复调用，不会重复扣减。

### 5.4 三种“释放”不要混淆

| 操作 | 影响 |
| --- | --- |
| `handle.Release()` | 归还一个业务引用；计数归零时释放底层 AssetHandle |
| `UnloadUnusedAssetsAsync()` | 回收内存中已经没有引用的 Provider/Bundle，不删除磁盘缓存 |
| `ClearCacheAsync(ClearUnusedBundleFiles)` | 删除磁盘中不再被当前 Manifest 使用的旧 Bundle |

场景切换完成后，`GameSceneManager` 会等待旧场景对象执行 `OnDestroy`，然后调用 `GameResourceManager.UnloadUnusedAssetsAsync()`。

排查泄漏时可以查看：

```csharp
GameResourceManager.Instance.`LoadedResourceCount`
GameResourceManager.Instance.TrackedInstanceCount
GameResourceManager.Instance.TotalReferenceCount
GameResourceManager.Instance.GetSnapshots()
```

---

## 6. YooAsset 构建流程

### 6.1 构建前检查

1. 在 Unity Build Settings 中切换到最终目标平台，例如 Android。
2. 确认 `DefaultPackage` 的 Collector 没有重复覆盖同一资源。
3. 执行：

```text
工具/YooAsset/应用首包与全量强更策略
工具/YooAsset/验证首包与全量强更策略
```

策略工具会固定：

```text
Pipeline = ScriptableBuildPipeline
Bundled Copy Option = ClearAndCopyByTags
Bundled Copy Params = Builtin
```

所以构建后复制到 `StreamingAssets` 的只有：

```text
Package Manifest/Catalog
Builtin 标签 Bundle
```

远端发布目录仍必须保留完整 Manifest 和全部 Bundle，不能只上传 Mandatory 或只上传 StreamingAssets 中的文件。

### 6.2 如果本次包含热更新 DLL 变化

必须先执行：

```text
工具/HybridCLR/生成全部并同步到 YooAsset
```

确认 Console 出现类似：

```text
[HybridCLR Sync] Android 同步完成：AOT=N，HotUpdate=M
下一步请重新构建 YooAsset Bundle
```

然后再进入 YooAsset 构建。

### 6.3 构建 Bundle

打开：

```text
YooAsset/Bundle Builder
```

建议确认：

| 设置 | 要求 |
| --- | --- |
| Build Target | 与 HybridCLR 生成目标一致，例如 Android |
| Package | `DefaultPackage` |
| Build Pipeline | `ScriptableBuildPipeline` |
| Package Version | 每次正式发布使用新版本，例如 `112` |
| Build Mode | 正式更新使用完整构建或符合团队增量规范的模式 |
| Builtin Copy | `ClearAndCopyByTags / Builtin` |

构建完成后至少核对两类产物：

```text
完整远端产物：以 Bundle Builder 的 Build Output Root 为准
APK 内置产物：Assets/StreamingAssets/yoo/DefaultPackage
```

当前工程根目录约定了 `Bundles/Android/DefaultPackage` 作为构建输出位置，但最终应以 Bundle Builder 界面配置为准。

### 6.4 Package Version 规则

正式发布必须使用新 Package Version：

```text
111 → 112 → 113
```

不要用新内容覆盖已经发布过的同版本文件。正确做法是：

```text
旧版本目录保持不可变
新内容构建到新版本目录
全部上传完成并校验后
最后切换 gameConfig.json 中的 version
```

这样可以避免客户端先拿到新 Manifest，却下载到一半新一半旧的 Bundle。

### 6.5 服务器目录

`YooAssetLauncher` 根据远程配置拼接：

```text
HostServer = downloadUrl/{Platform}/{version}
```

平台目录当前只支持：

```text
Android
IPhone
```

推荐服务器结构：

```text
{downloadUrl}/
└─ Android/
   ├─ 111/
   │  ├─ DefaultPackage.version
   │  ├─ DefaultPackage_111.bytes
   │  ├─ DefaultPackage_111.hash
   │  └─ *.bundle
   └─ 112/
      ├─ DefaultPackage.version
      ├─ DefaultPackage_112.bytes
      ├─ DefaultPackage_112.hash
      └─ *.bundle
```

远程 `gameConfig.json` 格式：

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "version": "112",
    "downloadUrl": "https://your-domain.example.com/game",
    "minimumAppVersion": "0.2.0",
    "minimumAndroidVersionCode": 2,
    "appDownloadUrl": "https://your-domain.example.com/download/game.apk",
    "forceUpdateMessage": "本次更新包含必要的客户端升级，请安装新版本。"
  }
}
```

最终 Android HostServer 为：

```text
https://your-domain.example.com/game/Android/112
```

建议让 `data.version` 与 YooAsset Package Version 保持一致，避免运维混淆。

`data.version` 不是 APK 版本。`minimumAppVersion` 和 `minimumAndroidVersionCode` 才是 AOT 整包强更门槛：客户端先检查这两个字段，任一不满足便显示不可跳过的整包更新界面，不会继续加载 YooAsset Manifest 或 HybridCLR DLL。详细发布步骤见 [AOTForceUpdate.md](AOTForceUpdate.md)。

### 6.6 当前启动配置地址

稳定 AOT 层必须保留一个“种子配置地址”，因为第一次网络请求发生在热更新 DLL 下载之前。当前地址定义在：

```text
Packages/com.lxy.contracts/Runtime/GameRuntimeConfig.cs
```

当前仍使用 HTTP：

```text
http://47.97.108.193:8080/LoadConfig/gameConfig.json
```

域名和证书就绪后应切换到 HTTPS。热更新层可以调用：

```csharp
GameRuntimeConfig.TryApplyBootstrapOverride(
    newConfigUrl,
    timeoutSeconds,
    persist: true,
    out string error);
```

但是要注意：如果用户从未成功进入过能够写入覆盖配置的热更新版本，下一次启动仍只能依赖 APK 中的种子地址。因此种子地址失效通常仍需要发新包兜底。

---

## 7. HybridCLR（华佗）程序集流程

### 7.1 三个概念必须区分

#### AOT 程序集

随 APK/IPA 由 IL2CPP 编译成本机代码，不能通过下载 DLL 替换。例如当前稳定启动壳 `Game.Main`。

#### 热更新程序集

保留 DLL，由 HybridCLR 在运行时解释/混合执行。当前通过 YooAsset 下载 `.dll.bytes` 后执行 `Assembly.Load(byte[])`。

#### AOT 补充元数据程序集

这些 `.dll.bytes` 不是用来替换 AOT 代码，而是传给：

```csharp
RuntimeApi.LoadMetadataForAOTAssembly(
    dllBytes,
    HomologousImageMode.SuperSet);
```

它们为热更新代码补充 AOT 泛型等元数据能力。AOT 元数据列表和热更新 DLL 列表是两套独立清单。

### 7.2 当前 HybridCLRSettings

位置：

```text
Project Settings/HybridCLR Settings
ProjectSettings/HybridCLRSettings.asset
```

当前 `Hot Update Assemblies` 保存的顺序：

```text
Game.Common
Game.Lua
Game.UI
Assembly-CSharp
Game.Battle
```

这个顺序当前不符合工程同步工具的校验规则，因为 `Game.Battle` 位于 `Assembly-CSharp` 之后。发布前必须调整为：

```text
Game.Common
Game.Lua
Game.UI
Game.Battle
Assembly-CSharp
```

当前 `Patch AOT Assemblies`：

```text
System.Core.dll
System.dll
mscorlib.dll
UnityEngine.UI.dll
UnityEngine.CoreModule.dll
```

当前：

```text
Hot Update Assembly Definitions = 空
Preserve Hot Update Assemblies = 空
External Hot Update Assembly Dirs = 空
```

### 7.3 Hot Update Assembly Definitions 与 Hot Update Assemblies

两者都是热更新程序集输入方式：

- `Hot Update Assembly Definitions`：直接拖入 asmdef 资产。
- `Hot Update Assemblies`：填写不带 `.dll` 后缀的程序集名称。

当前工程统一使用名称列表，因此新增程序集时直接在 `Hot Update Assemblies` 添加即可，不需要同时在两个列表重复配置。

### 7.4 Preserve Hot Update Assemblies

当前 HybridCLR 包会把 Preserve 列表加入 HybridCLR 的完整热更新程序集集合，但本工程自定义同步工具使用的是 `HotUpdateAssemblyFilesExcludePreserved`：

```text
Preserve 中的 DLL 不会写入当前运行时清单
不会被复制到 Assets/GameResources/HybridCLR/HotUpdate
也不会由 HybridCLRLoader 主动 Assembly.Load
```

因此普通业务热更新 DLL 不应该填写到 Preserve。当前保持为空是正确的。只有明确设计“预留但由其他机制提供/加载的程序集”时才使用，并需要同步扩展自定义同步与加载逻辑。

### 7.5 不要手改 AOTGenericReferences

文件：

```text
Assets/HybridCLRGenerate/AOTGenericReferences.cs
```

其中：

```csharp
AOTGenericReferences.PatchedAOTAssemblyList
```

是 HybridCLR 扫描热更新代码后生成的结果，不是人工配置入口。正确流程是：

```text
修改 HybridCLRSettings 或代码
    ↓
执行 HybridCLR GenerateAll
    ↓
重新生成 AOTGenericReferences.cs
    ↓
同步工具读取 PatchedAOTAssemblyList
```

直接手改该列表会在下一次 GenerateAll 时被覆盖，也可能让元数据清单与真实泛型扫描结果不一致。

### 7.6 “生成全部并同步到 YooAsset”具体做了什么

菜单：

```text
工具/HybridCLR/生成全部并同步到 YooAsset
```

内部流程：

```text
HybridCLR PrebuildCommand.GenerateAll
    ├─ 编译当前 BuildTarget 的热更新 DLL
    ├─ 生成/更新 link.xml
    ├─ 生成 AOTGenericReferences.cs
    ├─ 生成 MethodBridge 等 HybridCLR 代码
    └─ 准备裁剪后 AOT DLL
    ↓
HybridCLRAssetSynchronizer.Sync
    ├─ 从 PatchedAOTAssemblyList 读取 AOT 元数据 DLL
    ├─ 从 HybridCLRSettings 读取热更新 DLL，保留配置顺序
    ├─ 校验热更新 DLL 依赖顺序
    ├─ 校验所有源 DLL 存在
    ├─ 复制为 *.dll.bytes
    ├─ 删除目标目录中过期的 *.dll.bytes
    └─ 写入 HybridCLRAssemblyManifest.bytes
```

目标平台以 Unity 当前 `EditorUserBuildSettings.activeBuildTarget` 为准。

### 7.7 生成目录与 YooAsset 目录映射

以 Android 为例：

```text
HybridCLRData/HotUpdateDlls/Android/Game.UI.dll
    ↓ copy
Assets/GameResources/HybridCLR/HotUpdate/Game.UI.dll.bytes

HybridCLRData/AssembliesPostIl2CppStrip/Android/mscorlib.dll
    ↓ copy
Assets/GameResources/HybridCLR/AOT/mscorlib.dll.bytes
```

同步工具同时生成：

```text
Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes
```

示意内容：

```json
{
  "aotMetadataDlls": [
    "System.Core.dll",
    "UnityEngine.CoreModule.dll",
    "mscorlib.dll"
  ],
  "hotUpdateDlls": [
    "Game.Common.dll",
    "Game.Lua.dll",
    "Game.UI.dll",
    "Game.Battle.dll",
    "Assembly-CSharp.dll"
  ]
}
```

实际内容以每次生成结果为准，不要手工维护该 JSON。

### 7.8 运行时加载顺序

Player 中 `HybridCLRLoader` 执行：

```text
通过 GameResourceManager 加载 HybridCLRAssemblyManifest
    ↓
按 aotMetadataDlls 顺序加载 TextAsset
    ↓
RuntimeApi.LoadMetadataForAOTAssembly(..., SuperSet)
    ↓
释放 AOT TextAsset Handle
    ↓
按 hotUpdateDlls 顺序加载 TextAsset
    ↓
Assembly.Load(textAsset.bytes)
    ↓
校验真实 Assembly Name 与配置名称一致
    ↓
释放 DLL TextAsset Handle
```

`Assembly.Load` 会复制 DLL 数据，因此加载成功后可以释放 YooAsset Handle。已经装载进 AppDomain 的程序集不能在本次进程中卸载；发布新 DLL 后需要重启游戏进程才能使用新版本。

---

## 8. 新增一个热更新程序集的标准步骤

假设新增：

```text
Game.Feature.dll
```

### 第一步：创建 asmdef

```text
Packages/com.lxy.feature/Runtime/Game.Feature.asmdef
```

确保程序集依赖方向正确。AOT 壳不要引用它的具体类型。

### 第二步：加入 HybridCLR Settings

在 `Hot Update Assemblies` 添加：

```text
Game.Feature
```

不带 `.dll` 后缀，不要同时重复添加 asmdef 和名称。

### 第三步：安排加载顺序

原则是依赖先加载、使用者后加载：

```text
Dependency.dll
    ↓
Game.Feature.dll
    ↓
Assembly-CSharp.dll
```

如果新增了稳定依赖规则，应同步补充 `HybridCLRAssetSynchronizer.ValidateHotUpdateLoadOrder` 的构建期校验。

### 第四步：重新生成与同步

```text
工具/HybridCLR/生成全部并同步到 YooAsset
```

检查：

```text
HybridCLRData/HotUpdateDlls/{Target}/Game.Feature.dll
Assets/GameResources/HybridCLR/HotUpdate/Game.Feature.dll.bytes
HybridCLRAssemblyManifest.bytes 中包含 Game.Feature.dll
```

### 第五步：重新构建 YooAsset

使用新 Package Version，构建并上传完整远端产物。

### 第六步：切换远程配置

确认服务器文件全部可访问后，再把 `gameConfig.json.data.version` 指向新版本。

### 第七步：真机验证

至少验证：

- 首次安装可以下载并进入 Login。
- 已安装旧资源的设备可以升级。
- 下载中途杀进程后可以恢复。
- 断网不会绕过 Mandatory 强更。
- Console/日志中 DLL 加载顺序正确。

---

## 9. 不同更新类型该执行什么

### 9.1 只修改图片、Prefab、配置或场景资源

```text
修改资源
    ↓
必要时重新生成 SpriteAtlas
    ↓
YooAsset 使用新 Package Version 构建
    ↓
上传完整产物
    ↓
切换 gameConfig version
```

不需要重新生成 HybridCLR DLL，前提是资源没有新增必须由新代码识别的类型或字段逻辑。

### 9.2 只修改 Lua

Lua 位于 `Mandatory`，不需要 HybridCLR GenerateAll：

```text
修改 Lua
    ↓
YooAsset 新版本构建
    ↓
上传并切换版本
```

当前 Lua 默认入口由 `UIRuntimeConfig` 控制：

```csharp
UIRuntimeConfig.EnableLuaRuntime = true;
UIRuntimeConfig.LuaMainModule = "Main";
```

### 9.3 修改已有热更新 C# 程序集

```text
修改 Game.UI/Game.Battle/Assembly-CSharp 等代码
    ↓
工具/HybridCLR/生成全部并同步到 YooAsset
    ↓
YooAsset 新版本构建
    ↓
上传并切换版本
```

### 9.4 新增或删除热更新程序集

除上一流程外，还必须修改 `HybridCLRSettings` 并检查加载顺序。同步工具会删除 `Assets/GameResources/HybridCLR/HotUpdate` 中已经不再配置的旧 DLL。

### 9.5 修改 AOT 启动壳

例如修改：

```text
Game.Main
Game.Resource
Game.Scene
Game.Contracts
```

这些修改不能只通过资源热更新生效，需要：

```text
重新生成 HybridCLR
重新构建 YooAsset
重新构建 APK/IPA
发布新客户端
```

### 9.6 修改 Patch AOT Assemblies

需要重新执行 GenerateAll。若涉及 AOT 本机代码、裁剪结果或主包依赖变化，正式发布应重新构建 Player，不要假设只下发 AOT 元数据 `.bytes` 就等同于替换 AOT 代码。

---

## 10. 当前工程状态审计

截至 2026-08-14，工程里存在一组“配置已更新、生成物尚未更新”的状态：

### 10.1 Game.Battle 尚未同步到当前 DLL 产物

`ProjectSettings/HybridCLRSettings.asset` 当前已经包含：

```text
Game.Battle
```

但当前文件：

```text
Assets/GameResources/HybridCLR/HybridCLRAssemblyManifest.bytes
Assets/GameResources/HybridCLR/HotUpdate
```

仍是旧生成结果，没有 `Game.Battle.dll`。

同时，当前 Settings 把 `Game.Battle` 放在了 `Assembly-CSharp` 后面，会触发同步工具的加载顺序校验。应先把顺序调整为：

```text
Game.Common → Game.Lua → Game.UI → Game.Battle → Assembly-CSharp
```

### 10.2 AOTGenericReferences 和 StreamingAssets 也是旧产物

当前时间关系：

```text
AOTGenericReferences.cs                     2026-08-10
HybridCLRAssemblyManifest.bytes             2026-08-12 16:34
HybridCLRSettings.asset                     2026-08-12 17:52
StreamingAssets DefaultPackage Version 111 2026-08-11
```

这说明设置最后一次修改后，没有完成一轮新的完整生成和 YooAsset 构建。

下一次发布前应执行：

```text
1. 切换到目标 BuildTarget（当前主要是 Android）
2. 工具/HybridCLR/生成全部并同步到 YooAsset
3. 检查 Manifest 中出现 Game.Battle.dll
4. 检查 HotUpdate 目录出现 Game.Battle.dll.bytes
5. 工具/YooAsset/验证首包与全量强更策略
6. YooAsset/Bundle Builder 使用新版本构建
7. 上传完整产物
8. 最后切换 gameConfig.json 的 version
```

当前状态适合继续开发，但不能把旧版本 `111` 的 StreamingAssets/远端产物当作包含 `Game.Battle` 的最终发布资源。

---

## 11. 常见问题与排查

### 11.1 `resolve Hot update dll: XXX failed`

检查：

1. `XXX` 是否是实际存在的程序集名称，而不是文件夹或 namespace。
2. 当前 BuildTarget 是否正确。
3. 是否执行过 HybridCLR GenerateAll。
4. `HybridCLRData/HotUpdateDlls/{Target}/XXX.dll` 是否存在。
5. 外部 DLL 是否配置了正确的 External Hot Update Assembly Dir。

`Assembly-CSharp` 只有在其中确实存在脚本并成功编译时才会生成。

### 11.2 `Location is invalid`

通常表示运行时拿到的 Manifest 不包含该资源：

- Collector 没有收集它。
- Address 与代码 Location 不一致。
- 上传了 Bundle，却没有上传同一次构建的 Manifest。
- 客户端仍在读取旧 Package Version。
- HybridCLR 同步后没有重新构建 YooAsset。

### 11.3 新增 DLL 后 Bundle 中看不到

HybridCLR 生成 DLL不等于 YooAsset 自动收集成功。必须满足：

```text
HybridCLRSettings 已配置
    + GenerateAll 成功
    + 同步得到 *.dll.bytes
    + HybridCLR/HotUpdate Collector 已收集
    + YooAsset 使用新版本重新构建
```

多个 `.dll.bytes` 使用 `PackDirectory` 时可能被打进同一个 Bundle，所以“Bundle 数量没有按 DLL 数量增加”不代表 DLL 没有被打包。应查看 Build Report 或 Manifest 的 Asset Location，而不是只看 Bundle 个数。

### 11.4 `Assembly.Load` 找不到依赖

检查 `HybridCLRAssemblyManifest.bytes` 的 `hotUpdateDlls` 顺序。被依赖 DLL 必须先加载。

### 11.5 YooAsset 重复收集资源

错误示例：

```text
Collector A: Assets/GameResources
Collector B: Assets/GameResources/UITexture
```

父目录已经覆盖子目录，两个 MainAssetCollector 会收集同一资源。删除宽泛的根目录 Collector，保留互不重叠的精确目录。

### 11.6 第二次启动仍重复下载

检查：

- Package Version 是否每次启动都被错误改变。
- 服务器是否覆盖了同版本文件。
- Bundle Hash 是否不稳定。
- Sandbox 是否被应用清理或系统清理。
- 下载是否真正完成并通过校验。

### 11.7 更新后代码没有变化

热更新 DLL 装载后本进程不能卸载。必须完全退出并重启游戏，才能加载新版本 DLL。若仍无变化，核对远端 Manifest、DLL Bundle Hash 和运行时日志中的 Assembly FullName。

### 11.8 AOT 泛型报错或缺少方法桥

不要直接修改 `AOTGenericReferences.cs`。重新执行 GenerateAll，确认热更新程序集已加入 Settings，必要时检查 link.xml、泛型迭代次数和目标平台裁剪结果，然后重新出 Player。

---

## 12. 发布检查清单

### 12.1 首包发布

- [ ] BuildTarget 与最终平台一致。
- [ ] HybridCLR Settings 中 AOT/HotUpdate 分类正确。
- [ ] 热更新 DLL 顺序满足依赖。
- [ ] 执行“生成全部并同步到 YooAsset”。
- [ ] `HybridCLRAssemblyManifest.bytes` 与目录中的 DLL 一致。
- [ ] YooAsset 分层策略验证通过。
- [ ] 使用新 Package Version 构建 DefaultPackage。
- [ ] `StreamingAssets` 只含 Manifest/Catalog 和 Builtin Bundle。
- [ ] 远端目录包含完整 Manifest、Hash 和全部 Bundle。
- [ ] `gameConfig.json` 仍指向已经完整上传的版本。
- [ ] 构建 APK/IPA。
- [ ] 新安装、覆盖安装、断网、弱网和杀进程恢复测试通过。

### 12.2 资源/代码热更新发布

- [ ] 明确本次是否涉及热更新 DLL。
- [ ] 涉及 C# 时先 GenerateAll + Sync。
- [ ] 使用从未发布过的新 Package Version。
- [ ] 构建报告中包含预期资源和 DLL Location。
- [ ] 先上传所有文件，后切换远程 version。
- [ ] 用旧客户端真实升级验证。
- [ ] 检查 Mandatory 下载完成后才进入 Login。
- [ ] 检查废弃 Bundle 清理不影响当前资源。

---

## 13. 最常用菜单速查

```text
工具/HybridCLR/生成全部并同步到 YooAsset
工具/HybridCLR/同步现有产物到 YooAsset

工具/YooAsset/应用首包与全量强更策略
工具/YooAsset/验证首包与全量强更策略

YooAsset/Bundle Collector
YooAsset/Bundle Builder
YooAsset/Bundle Reporter
YooAsset/Bundle Debugger

工具/图集/Generate Sprite Atlas
```

推荐日常热更新发布顺序：

```text
确认 BuildTarget
    ↓
生成图集（如有 UI 图片变化）
    ↓
GenerateAll + Sync（如有热更新 C# 变化）
    ↓
验证 YooAsset 分层
    ↓
Bundle Builder 使用新版本构建
    ↓
检查报告和远端完整性
    ↓
上传全部文件
    ↓
切换 gameConfig version
    ↓
旧客户端升级验证
```

---

## 14. 关键源码索引

| 职责 | 文件 |
| --- | --- |
| 总启动编排 | `Packages/com.lxy.main/Runtime/GameMain.cs` |
| YooAsset 初始化、版本、Manifest、下载 | `Packages/com.lxy.resource/Runtime/YooAssetLauncher.cs` |
| 统一资源加载与引用计数 | `Packages/com.lxy.resource/Runtime/GameResourceManager.cs` |
| 资源标签常量 | `Packages/com.lxy.resource/Runtime/YooAssetContentTags.cs` |
| 首包复制策略与验证 | `Assets/Editor/YooAssetContentPolicy.cs` |
| Bundle Collector 配置 | `Assets/BundleCollectorSetting.asset` |
| HybridCLR 生成与同步 | `Packages/com.lxy.main/Editor/HybridCLRAssetSynchronizer.cs` |
| HybridCLR 运行时加载 | `Packages/com.lxy.main/Runtime/HybridCLRLoader.cs` |
| HybridCLR 配置 | `ProjectSettings/HybridCLRSettings.asset` |
| 热更新 DLL 运行时清单结构 | `Packages/com.lxy.contracts/Runtime/HybridCLRAssemblyManifest.cs` |
| 种子资源配置地址 | `Packages/com.lxy.contracts/Runtime/GameRuntimeConfig.cs` |
| 场景加载与内存回收 | `Packages/com.lxy.scene/Runtime/GameSceneManager.cs` |
| Login UI/Lua 启动 | `Packages/com.lxy.ui/Runtime/UI/UIStartup.cs` |
| Lua 开关 | `Packages/com.lxy.ui/Runtime/UI/UIRuntimeConfig.cs` |
