# C# / Lua UI Prefab 与手动脚本生成

创建入口：

```text
工具 > UI工具 > 创建Prefab
```

创建工具中的脚本类型用于写入 Prefab 默认配置：

- `C# 脚本`：创建 C# 类型 Prefab。
- `Lua 脚本`：创建 Lua 类型 Prefab。

两种类型在创建时都只生成 Prefab，不立即生成任何 C# 或 Lua 文件。
Prefab 根节点统一包含：

- `RectTransform`
- `Canvas`
- `GraphicRaycaster`
- `CanvasGroup`
- `ObjectBinder`

不再给新 C# Prefab 添加 `UICodeBinder`。

## ObjectBinder 生成设置

创建工具会填写 `ObjectBinder > UI Script Generation`：

| 配置 | 作用 |
| --- | --- |
| `Script Type` | CSharp / Lua，可在 Prefab 上修改 |
| `Panel Id` | UIManager 使用的界面 ID |
| `Csharp Class Name` | C# 业务 View 类名 |
| `Csharp Namespace` | C# 命名空间 |
| `Csharp Output Folder` | C# Main/Base 输出目录 |
| `Lua Module Name` | Lua require 路径 |
| `Lua Output Folder` | Lua Main/Auto 输出目录 |
| `UI Layer` | C# UIManager 挂载层级；Lua 使用 UIDefine |

这些设置都可以直接在 Prefab 的 ObjectBinder 上修改。

## 手动生成按钮

ObjectBinder Inspector 会根据 `Script Type` 只显示对应类型的按钮：

```text
CSharp：隐藏“目标Lua”，显示“生成 C# 脚本”
Lua：显示“目标Lua”，显示“生成 Lua 脚本”
```

点击按钮才会真正创建或更新脚本。业务主文件存在时不会覆盖；
自动生成文件允许重新生成。点击生成按钮前，会先按节点前缀把
尚未加入的组件收集到 `ObjectBinder.bindValues`。

## C# View + ViewBase

假设 Prefab 名称为 `UIShopView`，点击“生成 C# 脚本”后生成：

```text
UIShopView.cs
UIShopViewBase.cs
```

结构参考 `UIMainLevelView` 与 `UIMainLevelViewBase`：

```csharp
public class UIShopView : UIShopViewBase
{
    protected override void OnShow(object userData)
    {
        _TxtTitle.text = "Shop";
    }

    protected override void OnBtnCloseClick()
    {
        CloseSelf();
    }
}
```

`UIShopViewBase.cs` 是可覆盖的自动生成文件，负责：

- 声明 `_Root`、`_BtnClose`、`_TxtTitle` 等 protected 字段。
- 从 `ObjectBinder.bindValues` 读取强类型组件引用。
- 添加和移除 Button、Toggle、Slider 等默认事件。
- 生成 `OnBtnCloseClick()` 等 protected virtual 回调。
- 在解绑时清空生成字段。

业务 `UIShopView.cs` 仅在不存在时创建，负责：

- `OnInitialize`
- `OnInit`（ObjectBinder 字段绑定完成后）
- `OnShow`
- `OnHide`
- `OnRelease`
- `OnDispose`
- 覆写生成的 UI 事件方法

## Lua Main + Auto

点击“生成 Lua 脚本”后生成：

```text
UIShop.lua
UIShop_Auto.lua
```

Lua 主文件存在时保留，`_Auto.lua` 根据 ObjectBinder 重新生成。
Lua 界面的资源路径、逻辑模块、层级和关闭策略仍统一配置在：

```text
Lua/Framework/UI/UIDefine.lua
```

## ObjectBinder 绑定

`ObjectBinder > Bind Values` 是 C# 和 Lua 的共同绑定数据源。

可以手动添加引用，也可以按节点前缀自动收集：

| 节点前缀 | 目标 |
| --- | --- |
| `btn_` | `Button`，生成 Click |
| `tgl_` / `toggle_` | `Toggle` |
| `sld_` / `slider_` | `Slider` |
| `scrollbar_` | `Scrollbar` |
| `input_` | TMP 或 UGUI InputField |
| `dropdown_` | TMP 或 UGUI Dropdown |
| `img_` | `Image` |
| `raw_` | `RawImage` |
| `txt_` / `tmp_` | TMP Text 或 UGUI Text |
| `scroll_` | `ScrollRect` |
| `cg_` | `CanvasGroup` |
| `rt_` | `RectTransform` |
| `tf_` | `Transform` |
| `go_` | `GameObject` |

C# Base 字段使用参考脚本风格：

```text
btn_Close -> protected Button _BtnClose;
txt_Title -> protected TMP_Text _TxtTitle;
go_Item   -> protected GameObject _GoItem;
```

## 推荐流程

1. 使用创建工具生成 C# 或 Lua 类型 Prefab。
2. 编辑 Prefab 层级和 ObjectBinder 绑定。
3. 在 ObjectBinder 上确认或修改脚本生成配置。
4. 根据 `Script Type` 点击当前显示的脚本生成按钮。
5. 只编辑业务主文件，不手动修改 Base / Auto 文件。
