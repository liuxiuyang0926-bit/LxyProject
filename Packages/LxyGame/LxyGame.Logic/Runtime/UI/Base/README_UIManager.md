# C# UIManager 落地说明

本目录中的 C# 框架按照 `UIManager_UI_Framework_Analysis.md` 落地，核心结构保持一致：

- `panelRecords` 是每个面板生命周期的事实源。
- `stackRecords` 只负责非 `IgnoreStack` 面板的导航顺序。
- `UIPanelState` 是显式状态机，非法迁移会抛出结构化异常。
- `requestId` 防止已关闭或重开的旧异步请求污染新实例。
- `pendingCloseTarget` 保证新栈顶加载完成前旧界面继续显示，避免黑屏或空窗。
- 每个远端面板记录直接持有 YooAsset `AssetHandle`。

Prefab 与 C# Logic 可以通过
`工具 > UI工具 > 创建Prefab`
一键创建，详见 `README_CSharpUIGenerator.md`。

场景中的 `[UIStartup]` 是工程唯一启动入口，按顺序完成：

1. 编辑器切换为 AssetDatabase；Player 初始化 YooAsset Host/Offline。
2. 加载 `Assets/GameResources/Prefabs/UICanvasRoot.prefab`。
3. 初始化 C# UIManager 和 LuaUIRuntime。
4. 执行 `Lua/Main.lua` 的 `Main.Start()`。
5. 打开默认界面。

UICanvasRoot 会解析以下层级：

```text
Bottom#Rtf#Canvas
Stack#Rtf#Canvas
PopUp#Rtf#Canvas
GuideLayer#Rtf#Canvas
TopLayer#Rtf#Canvas
Loading#Rtf#Canvas
Tips#Rtf#Canvas
Debug#Rtf#Canvas
```

生成的 UI Prefab 会把 `UILayer` 保存在 `UICodeBinder`。UIManager
加载实例后优先使用该层级；例如 `UILogin = Stack` 时，最终父节点是
`UICanvasRoot/LayerRoot/Root/Stack#Rtf#Canvas`。

`Demo` 场景的 `[UIStartup]` 默认打开 `UILogin`。编辑器通过
`AssetDatabase.LoadAssetAtPath` 加载 CanvasRoot 和 UI Prefab；Player
通过 YooAsset `ResourcePackage` 加载，并由 `AssetHandle` 管理引用计数。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `UIManager.cs` | 打开、预加载、进出栈、pending close、场景切换、隐藏和销毁 |
| `UIPanelLogic.cs` | 与 Prefab 解耦的 C# 界面逻辑基类 |
| `UIDefinitions.cs` | Panel 配置、层级、状态、错误和诊断结构 |
| `UIStackCoordinator.cs` | 无 Unity 副作用的纯导航栈规则 |
| `UILayerRoot.cs` | Canvas、EventSystem 和八层 UI Root |
| `UIManagerSettings.cs` | 可序列化 Panel 配置表 |
| `UIBackdropService.cs` | 默认遮罩及项目自定义截图模糊扩展点 |
| `YooAssetLauncher.cs` | Player 的版本、清单、下载、Host/Offline 降级资源服务 |
| `UIStartup.cs` | 唯一启动入口，编排 YooAsset、CanvasRoot、UIManager、Lua 和默认界面 |
| `Editor/UIManagerValidationMenu.cs` | Panel ID、YooAsset Package、Location 和 Prefab 类型校验 |

## 1. 配置 YooAsset

在 YooAsset Bundle Collector 中创建资源包并收集 UI Prefab，例如：

```text
Package Name: DefaultPackage
Location: Assets/GameResources/Prefabs/UIRes/UITest.prefab
```

也可以开启 Addressable，为 Prefab 配置简短地址。YooAsset Package 必须在
打开 UI 前完成初始化。UIManager 使用 YooAsset 3.x 原生接口加载和实例化：

```csharp
ResourcePackage package =
    YooAssets.GetPackage(config.PackageName);
AssetHandle handle =
    package.LoadAssetAsync<GameObject>(config.Location);
InstantiateOperation instantiateOperation =
    handle.InstantiateAsync(
        new InstantiateOptions(true, layerRoot, false));
```

面板销毁时会 Destroy 实例并 `AssetHandle.Release()`。YooAsset 根据
`AssetHandle` 引用计数自动管理资源和依赖 Bundle 的卸载。

## 2. 创建 Settings

执行：

`Assets > Create > LxyDemo > UI > UI Manager Settings`

添加 Panel：

```text
Id: UITest
Package Name: DefaultPackage
Location: Assets/GameResources/Prefabs/UIRes/UITest.prefab
Ignore Stack: false
Layer: Auto
Close Type: Destroy
Auto Destroy When Scene Changed: true
```

`Layer = Auto` 时：

- 普通栈界面进入 `StackLayer`。
- `IgnoreStack` 面板进入 `PopupLayer`。

执行 `Tools > UI Manager > Validate All Settings`，可以在构建前检查：

- Panel ID 是否重复。
- Package 是否存在于 YooAsset Bundle Collector。
- Location 是否属于指定 Package。
- Location 对应的资源是否为 GameObject Prefab。

## 3. 启动场景

`UIStartup` 位于 `Game.Logic` 热更新程序集，不能直接挂到 APK 内置的首场景。`GameMain` 会在 HybridCLR 加载完 `Game.Logic.dll` 并进入首场景后，动态创建名为 `[UIStartup]` 的对象。

`YooAssetLauncher` 仍由常驻的 `GameMain` 对象提供，动态创建的 `UIStartup` 会自动复用它。请从项目的启动场景运行；不要直接运行 Login 场景来绕过资源与 HybridCLR 初始化。

编辑器不会初始化 YooAsset，而是直接使用 AssetDatabase；Android/iOS 等
Player 不会引用 AssetDatabase，统一使用 YooAsset。若没有已有
`UILayerRoot`，UIStartup 会加载 UICanvasRoot：

```text
UIRoot
├── BottomLayer
├── StackLayer
├── PopupLayer
├── GuideLayer
├── TopLayer
├── LoadingLayer
├── TipsLayer
└── DebugLayer
```

项目现有 `Demo` 场景已经有 Canvas，也可以在其 `UIRoot` 上添加 `UILayerRoot`，再赋给 `UIManagerBootstrap`。

## 4. 编写界面逻辑

Prefab 不强制挂 UI 基类组件。逻辑由工厂独立创建，然后与 YooAsset 实例绑定：

```csharp
using LxyDemo.UIFramework;
using UnityEngine.UI;

public sealed class UITestLogic : UIPanelLogic
{
    private Button closeButton;

    protected override void OnBind()
    {
        closeButton = GetComponentInChildren<Button>();
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    protected override void OnShow(object userData)
    {
        // 每次显示都在这里刷新数据。
    }

    protected override void OnHide(bool skipAnimation)
    {
        // 播放关闭动画或停止界面逻辑。
    }

    protected override void OnUnbind()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton = null;
        }
    }

    private void OnCloseClicked()
    {
        CloseSelf();
    }
}
```

在业务启动时给 Settings 中的配置替换具体 Logic Factory：

```csharp
UIPanelConfig config = new UIPanelConfig
{
    Id = "UITest",
    PackageName = "DefaultPackage",
    Location = "Assets/GameResources/Prefabs/UIRes/UITest.prefab",
    IgnoreStack = false,
    Layer = UILayer.Auto,
    CloseType = UIPanelCloseType.Destroy
};

UIManager.Instance.Register<UITestLogic>(config);
```

如果只使用 Settings 注册且没有提供 Logic 类型，框架使用 `UIDefaultPanelLogic`，仍可完成加载、显示、关闭和释放。

## 5. 打开和关闭

```csharp
private IEnumerator OpenTest()
{
    UIAsyncOperation<UITestLogic> operation =
        UIManager.Instance.OpenPanelAsync<UITestLogic>(
            "UITest",
            new { title = "Test" });

    yield return operation;
    if (!operation.IsSucceeded)
    {
        Debug.LogException(operation.Exception);
        yield break;
    }

    UITestLogic logic = operation.Result;
}
```

关闭：

```csharp
UIManager.Instance.ClosePanel("UITest");
UIManager.Instance.ClosePanel("UITest", forceDestroy: true);
UIManager.Instance.CloseTopPanel();
```

查询：

```csharp
bool visible = UIManager.Instance.IsPanelOpen("UITest");
bool loaded = UIManager.Instance.IsPanelLoaded("UITest");
bool isTop = UIManager.Instance.IsTopPanel("UITest");
UIPanelState state = UIManager.Instance.GetPanelState("UITest");
UITestLogic logic = UIManager.Instance.GetPanelLogic<UITestLogic>("UITest");
```

## 6. CloseType 与 YooAsset 引用

`UIPanelCloseType.Hide`：

- 调用 `OnHide`。
- GameObject 设为 inactive。
- 保留 Logic、实例和 YooAsset `AssetHandle`。
- 下次打开无需重新加载。

`UIPanelCloseType.Destroy`：

- 调用 `OnHide`、`OnUnbind`、`OnDispose`。
- Destroy GameObject。
- 调用 `AssetHandle.Release()`，YooAsset 资源及依赖引用计数减一。
- 下次打开重新创建 Logic 和加载 Prefab。

因此常驻高频界面可选 `Hide`，大型低频界面建议 `Destroy`。

## 7. 导航栈与异步竞态

打开 A 后打开 B：

```text
stack = [A, B]
A.pendingCloseTarget = B
A 保持可见
B 加载并显示
关闭或隐藏 A
```

关闭栈顶 B：

```text
stack = [A]
B.pendingCloseTarget = A
恢复 A
A 显示成功
关闭或隐藏 B
```

正在加载的面板如果被关闭、重开或被更新的栈顶取代：

- 当前调用方收到 `OperationCancelled`。
- YooAsset Provider 的共享加载不会破坏其他调用方。
- 过期实例通过 `requestId` 检查立即销毁并释放独立 `AssetHandle`。
- 旧加载结果不会突然覆盖当前栈顶。

## 8. 预加载

预加载会完成 Prefab 实例化和 Logic 绑定，然后保持隐藏；即使配置是 `CloseType.Destroy`，预加载结果也会保留，直到显式强制销毁或场景策略回收：

```csharp
var preload = UIManager.Instance.PreloadPanelAsync("UITest");
yield return preload;
```

批量预加载包含每个面板的独立超时：

```csharp
var preload = UIManager.Instance.PreloadPanelsAsync(
    new[] { "UINetConnect", "UITest" },
    timeoutPerPanelSeconds: 20f);
yield return preload;
```

## 9. 弹窗、遮罩和模糊

`IgnoreStack = true` 的面板不会改变主导航栈。`BlurMode` 默认创建半透明、可拦截点击的背景；`BlurCloseOnClick` 决定点击背景是否关闭弹窗。

真正的截图高斯模糊与项目渲染管线有关，可实现 `IUIBackdropService` 并替换：

```csharp
UIManager.Instance.BackdropService = new ProjectBlurBackdropService();
```

## 10. 场景切换和栈恢复

Active Scene 改变时，`AutoDestroyWhenSceneChanged = true` 的面板会被强制销毁并释放 YooAsset `AssetHandle`。

需要保存战斗前的主界面栈：

```csharp
UIManager.Instance.SaveNavigationStack("MainCity");
UIManager.Instance.CloseAllPanels();

// 回城后
var restore =
    UIManager.Instance.RestoreNavigationStackAsync("MainCity");
yield return restore;
```

## 11. 运行时诊断

`GetRuntimeSnapshots()` 返回每个面板的：

- 生命周期状态。
- 是否已加载、是否可见。
- 是否位于导航栈。
- pending close 目标。
- 当前 requestId。

同时可以订阅：

```csharp
UIManager.Instance.PanelStateChanged += OnPanelStateChanged;
UIManager.Instance.PanelLoadFailed += OnPanelLoadFailed;
```

这些信息适合接到项目的开发者控制台，用于排查重复打开、返回栈异常和资源未释放。
