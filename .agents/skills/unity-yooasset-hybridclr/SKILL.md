---
name: unity-yooasset-hybridclr
description: 实现、审查、调试、构建或发布 LxyDemo 的 YooAsset 3.0.5 与 HybridCLR 流水线。适用于资源加载或引用计数、BundleCollector 设置、Builtin/Mandatory 标签、清单、下载、缓存清理、AOT 元数据、热更新程序集列表或加载顺序、DLL 生成与同步、Player 启动、整包强制更新以及 CDN 资源发布。
---

# Unity YooAsset 与 HybridCLR

把源设置、生成的 DLL/元数据资源、YooAsset 清单、Bundle 和已发布版本作为一条完整流水线保持一致。修改或诊断前先阅读 [references/release-pipeline.md](references/release-pipeline.md)。

## 任务分流

- 普通运行时资源加载和实例化使用 `GameResourceManager` 及其业务句柄。
- 修改初始化、远端版本、Mandatory 下载或缓存生命周期前，先沿 `GameMain` 启动流程追踪 `YooAssetLauncher`。
- 修改 AOT 元数据或热更新 DLL 加载时，将 `HybridCLRLoader`、程序集清单契约、设置和同步工具视为同一个边界共同修改。
- 通过 `BundleCollectorSetting.asset` 和现有策略工具修改收集器/标签或首包复制策略；避免重叠收集器。
- 将 AOT 启动层改动和可下载热更新改动视为不同发布类型。

## 必须保持的约束

1. 除非有意重新设计发布架构，否则将 `Game.Contracts`、`Game.Resource`、`Game.Scene` 和 `Game.Main` 保留在稳定 AOT 壳中。
2. 禁止 AOT 壳直接引用热更新具体类型。通过 `IFirstSceneRuntime` 等稳定契约跨越边界。
3. 在加载包含热更新 MonoBehaviour 的场景或 Prefab 前，先加载 AOT 补充元数据和所有必需的热更新 DLL。
4. 保持符合依赖关系的热更新程序集顺序，并使用当前 asmdef 和 `HybridCLRSettings` 验证。
5. 所有启动必需的可下载内容保持 `Mandatory` 标签。复制进 APK 的 Bundle 使用 `Builtin`；`Builtin` 资源也必须同时保持 `Mandatory`，以便远端更新替换它们。
6. 不要为整个 `Assets/GameResources` 增加根收集器，它会与项目的精确收集器重叠。
7. 确认代码中的 Location 与收集器地址规则一致。使用 `AddressByFileName` 时，整个包内文件名必须唯一。
8. 每个 `GameResourceManager` 加载或实例化句柄都必须恰好释放一次。业务代码不得持有原始 YooAsset 句柄。
9. 当生成器或构建操作能够重新创建产物时，不要手工修改 HybridCLR 生成输出、清单、Bundle Catalog 或缓存内容。

## 改动与发布流程

1. 检查工作区、目标平台、当前 HybridCLR 设置、收集器设置、包版本和受影响 asmdef。
2. 判断改动属于 AOT、纯热更新、纯资源还是组合改动。AOT 改动必须重新构建 Player。
3. 热更新改动先生成并同步 HybridCLR 产物，再构建 YooAsset。
4. 应用并验证 Builtin/Mandatory 策略，然后使用新的不可变包版本为同一目标构建 `DefaultPackage`。
5. 分别验证 Editor 行为和目标 Player 的 Host-mode 流程；Editor 不会执行真实 DLL 加载和远端 Bundle 下载。
6. 在把远端配置切换到新版本前，先发布完整的版本化清单/Catalog 和所有被引用的 Bundle。

## 按阶段诊断

按以下顺序定位第一个失败边界：远端配置和整包版本门槛、包版本、清单、必需 Location、Mandatory 下载器、缓存清理、AOT 元数据、热更新 DLL 加载、第一个业务场景、UI/Lua 启动。报告失败阶段、源设置、生成产物和正确的重新构建步骤。

最终结果中说明该改动是否需要生成 HybridCLR、重新构建 YooAsset、创建新的远端包版本、上传 CDN 或生成新的 APK/IPA。
