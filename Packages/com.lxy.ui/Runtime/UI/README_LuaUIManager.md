# Lua UIManager 落地说明

Lua 版本按照 `UIManager_UI_Framework_Analysis.md` 的结构实现，入口在：

```text
Lua/Framework/UI/UIManager.lua
Lua/Framework/UI/Bootstrap.lua
```

## 主要模块

| 文件 | 作用 |
| --- | --- |
| `Framework/Core/DefineClass.lua` | Lua 类、继承、析构与单例 |
| `UI/UIDefine.lua` | CloseType、UILayer、状态机 |
| `UI/UIPanelConfig.lua` | 面板配置及默认规则 |
| `UI/UIStackCoordinator.lua` | 入栈、出栈、去重 |
| `UI/UIBaseLogic.lua` | 绑定、显示、隐藏、释放和生成事件 |
| `UI/UILogicFactory.lua` | require 并创建面板 Logic |
| `UI/UILayerRoot.lua` | 对接 C# `UICanvasRoot` 层级 |
| `UI/UIManager.lua` | 加载、状态机、pending close、预加载、场景恢复 |
| `UI/Bootstrap.lua` | C# 与 Lua 的稳定调用入口 |

`LuaUIRuntime.cs` 管理唯一的 `LuaEnv`，并把 Lua 面板接到场景中的
`UILayerRoot`。`UIStartup` 通过 `Initial Script Type` 选择默认界面的管理器：

- C# Prefab 使用现有 C# `UIManager`。
- 带 `ObjectBinder` 且不带 `UICodeBinder` 的 Prefab 使用
  `LuaUIRuntime` 和 Lua `UIManager`。

## UIDefine 面板配置

Lua Prefab 不保存 UI 管理元数据。所有面板统一在
`Lua/Framework/UI/UIDefine.lua` 配置：

```lua
local UIPanelConfig = require("Framework.UI.UIPanelConfig")

local CloseType = UIDefine.CloseType
local ScreenFitType = UIDefine.ScreenFitType
local OrderLayer = UIDefine.OrderLayer
local require = function(module) return module end

UIDefine.PanelConfig = {
    UILogin = UIPanelConfig.new({
        ResPath = "Assets/GameResources/Prefabs/UIRes/Login/UILogin.prefab",
        ClassName = require("UI.Login.UILogin"),
        ScreenFitType = ScreenFitType.InSafeArea,
        CloseType = CloseType.Destroy,
        IgnoreStack = false,
        OrderLayer = OrderLayer.StackLayer,
    }),
}
```

Lua Bootstrap 初始化时会自动注册 `UIDefine.PanelConfig` 中的全部
面板。注册本地 Prefab 时按配置 key 或 `ResPath` 的文件名查找配置。

## 打开和关闭

```csharp
LuaUIRuntime runtime = LuaUIRuntime.Instance;
runtime.Initialize(layerRoot);
string panelId = runtime.RegisterPrefab(luaPrefab);
runtime.OpenPanel(panelId, userData);
runtime.ClosePanel(panelId);
```

Lua 内部直接使用 `UIManager`：

```lua
local UIManager = require("Framework.UI.UIManager")

UIManager:OpenUIPanel("UILogin", userData)
UIManager:CloseUIPanel("UILogin")
```

`UIManager` 模块会代理到 `LuaUIRuntime` 初始化好的唯一管理器实例。
因此应在 `UIStartup` 完成初始化后调用。原有 `Bootstrap.OpenPanel`
等接口仍然保留，供 C# 桥接和兼容旧代码使用。

`UIPanelConfig` 还支持传入 `LoadAsync(config, parent, complete)` 和
`Release(config, gameObject)`，可对接 YooAsset 或其他资源系统。
异步回调会经过 requestId 校验，过期实例会立即释放。

## 已实现的管理行为

- `panelRecords` 生命周期事实源与显式状态转换。
- `stackRecords` 导航栈、重复入栈处理、关闭栈顶后恢复上一界面。
- 加载期间关闭、连续打开产生的 pending-close 链。
- Destroy / Hide 两种关闭策略。
- 单面板和批量预加载。
- 加载失败回收与上一栈界面恢复。
- 场景切换自动销毁与导航栈保存/恢复。
- Popup 自动关闭、栈顶查询、运行时快照。
- BlurMode 遮罩、射线阻挡与点击遮罩关闭。

## Lua 目录与 YooAsset 热更新

Lua 源码仍然维护在项目根目录 `Lua`。同步工具会把它们转换为
`Assets/GameResources/Lua/**/*.lua.bytes`，该目录已经被
`DefaultPackage` 的 YooAsset Collector 收集：

- Unity 编辑器：`LuaUIRuntime` 通过 AssetDatabase 读取 `.lua.bytes`。
- Player：资源更新完成后，通过 YooAsset 同步读取 Lua TextAsset。
- Lua 模块句柄由 `LuaUIRuntime` 保留，销毁 LuaEnv 时统一 Release。

Player 构建前会自动同步，也可以在构建 YooAsset Package 前手动执行：

```text
工具 > UI工具 > 同步Lua构建资源
工具 > UI工具 > 清理Lua构建资源
```

使用 YooAsset 的 Bundle Builder 时，只有点击 Build 按钮才会先执行
“同步Lua构建资源”。同步成功后继续按钮原有的 Bundle 构建逻辑；
同步失败则阻止本次构建，不会修改或切换 YooAsset 构建管线。

`UIStartup` 在资源准备完成后初始化唯一 LuaEnv，先加载
`Framework.UI.Bootstrap`，再执行 `Main.lua`。Main 返回的 table 中存在
`Start` 方法时会自动调用。

Lua 类型的 Prefab 创建时不会立即创建 Lua 文件，只在根节点挂载
`ObjectBinder`，不会挂载 `UICodeBinder`。需要脚本时，在
ObjectBinder Inspector 点击“生成 Lua 脚本”，生成 Main 与 `_Auto.lua`。
Lua UIManager 使用 `UIDefine.PanelConfig.ClassName` 加载逻辑，并在
绑定 GameObject 时执行 `ObjectBinder.Init(self)`。
