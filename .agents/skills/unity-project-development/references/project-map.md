# LxyDemo 项目地图

## 基线

- Unity：`2022.3.48f1c1`
- 第一方可复用代码：`Packages/com.lxy.*` 下的嵌入式 UPM 包
- 游戏内容和项目绑定产物：`Assets`
- 详细架构文档：`Packages/com.lxy.main/Documentation~/Architecture.md`

当需要精确确认当前状态时，重新读取 `ProjectSettings/ProjectVersion.txt`、asmdef、包清单和架构文档，不要只依赖本参考文件。

## 包归属

| 包 | 程序集 | 职责 |
| --- | --- | --- |
| `com.lxy.contracts` | `Game.Contracts` | 稳定的 AOT/热更新边界契约与启动配置 |
| `com.lxy.core` | `Game.Common` | 通用工具和状态控制组件 |
| `com.lxy.lua` | `Game.Lua` | 项目 XLua 运行时、绑定和编辑工具 |
| `com.lxy.resource` | `Game.Resource` | YooAsset 初始化、加载、引用计数和更新生命周期 |
| `com.lxy.scene` | `Game.Scene` | 场景加载和切换生命周期 |
| `com.lxy.ui-effect-generator` | `Lxy.UIEffectGenerator.Editor` | 可移植的效果图/Figma/UISchema 转 UGUI Prefab 编辑器核心与通用适配器 |
| `com.lxy.ui` | `Game.UI` | C#/Lua UI 运行时、常规 UI 编辑器工具及 Lxy 效果图生成适配器 |
| `com.lxy.battle` | `Game.Battle` | 确定性战斗逻辑、帧客户端、Unity 表现和战斗编辑器工具 |
| `com.lxy.main` | `Game.Main` | 稳定的 AOT 启动层和 HybridCLR 加载 |

## 依赖方向

稳定的第一方 AOT 壳由 `Game.Contracts`、`Game.Resource`、`Game.Scene` 和 `Game.Main` 组成。`Game.Main` 可以依赖其他 AOT 包。热更新程序集可以依赖 AOT 契约和服务，但 AOT 壳不得引用热更新业务的具体类型。

`Game.Main` 在第一个业务场景之前加载已配置的热更新 DLL，然后通过 `IFirstSceneRuntime` 进入业务启动流程。保持这种依赖倒置，不要增加直接的 `Game.Main -> Game.UI` 依赖。

修改 asmdef 前，检查所有使用方，并确认不会产生反向依赖或循环依赖。

## 位置规则

- 运行时代码放在包的 `Runtime` 目录，纯编辑器代码放在 `Editor`。
- 包专属的编辑器代码放在其仅限 Editor 的 asmdef 中。
- 场景、Prefab、资源配置、生成的 HybridCLR 文件和其他游戏内容放在 `Assets` 下。
- 供应商 XLua 代码保留在 `Assets/XLua`；不要随意修改供应商代码。
- 移动 Unity 资源或脚本时连同已有 `.meta` 文件一起移动，以保留 GUID 引用。

## 改动分流

- 涉及确定性模拟、帧命令、定点数数学、战斗碰撞或战斗状态哈希时，同时使用 `unity-frame-sync`。
- 涉及资源句柄、YooAsset 收集器/构建、启动下载、AOT 元数据、热更新 DLL 或发布时，同时使用 `unity-yooasset-hybridclr`。
- 涉及 UI 运行时或 Binder 时，先检查 `Packages/com.lxy.ui`；涉及效果图、Figma、UISchema、Sprite 视觉匹配或 Prefab 编译器时，先检查 `Packages/com.lxy.ui-effect-generator`，不要另起平行方案。
