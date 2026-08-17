# YooAsset 首包与全量强更规则

当前项目体量较小，不使用游戏内按需下载。`DefaultPackage` 采用两层资源策略：

| 层级 | Collector 标签 | 进入 APK | 下载时机 |
| --- | --- | --- | --- |
| 内置启动资源 | `Builtin;Mandatory` | 是 | 安装时已有；远端有新版本时仍在启动阶段更新 |
| 启动全量强更资源 | `Mandatory` | 否 | 进入 Login 前必须全部下载成功 |

## 当前资源划分

- `Builtin`：HybridCLR AOT 元数据、`UICanvasRoot.prefab`。
- `Mandatory`：热更新 DLL、程序集清单、全部 Lua、字体、Login/Main UI、自动图集、启动背景配置和图片。

新增 YooAsset 资源必须加入 `Mandatory` Group，保证进入 Login 前已经下载完成。不要恢复一个覆盖整个 `GameResources` 的根目录 Collector，否则可能与已有子目录 Collector 重复收集。

## 构建 APK 内置资源

`Assets/Editor/YooAssetContentPolicy.cs` 会把 ScriptableBuildPipeline 固定为：

- `Bundled Copy Option = ClearAndCopyByTags`
- `Bundled Copy Params = Builtin`

因此每次构建 YooAsset 后，`Assets/StreamingAssets/yoo/DefaultPackage` 只包含 Manifest 和 `Builtin` Bundle。可在 Unity 菜单执行：

- `工具/YooAsset/应用首包与全量强更策略`
- `工具/YooAsset/验证首包与全量强更策略`

发布资源时必须使用新的 Package Version，并把该版本 Manifest 与全部远端 Bundle 一起上传。不要用新内容覆盖已经发布过的同版本文件。

## 启动行为

Player Host 模式加载远端 Manifest 后，创建 `Mandatory` 标签下载器。只有全部 Mandatory Bundle 下载并校验成功、废弃缓存清理完成后，才会加载 HybridCLR 热更新程序集并进入 Login。下载失败时正式包不会降级跳过强更。
