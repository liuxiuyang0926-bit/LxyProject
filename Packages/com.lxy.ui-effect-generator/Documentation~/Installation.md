# 安装与项目适配

## 安装独立包

将整个 `com.lxy.ui-effect-generator` 目录复制到目标项目的 `Packages`，
并在目标项目 `Packages/manifest.json` 中加入：

```json
"com.lxy.ui-effect-generator": "file:com.lxy.ui-effect-generator"
```

也可以在 Unity Package Manager 中选择 `Add package from disk`，指向该目录
的 `package.json`。如果包保存在 Git 仓库子目录，可使用带
`?path=Packages/com.lxy.ui-effect-generator` 的 Git UPM 地址。

目标项目需要 Unity 2022.3、UGUI 和 TextMeshPro。高精度/轻量 AI 分析还
要求制作机能够执行 `codex --version`，并已完成 Codex CLI 登录。默认高精度
模式不依赖 UnityMCP。目标工程可以不是 Git 仓库；包调用 `codex exec` 时会
使用官方 `--skip-git-repo-check` 参数，同时继续保持只读沙箱。

## 通用项目默认行为

没有注册项目适配器时，生成器自动使用 `Generic UGUI`：

- Prefab 输出到 `Assets/GeneratedUI/Prefabs`；
- UISchema 输出到 `Assets/Editor/UIEffectGenerator/Schemas`；
- 参考图输出到 `Assets/Editor/UIEffectGenerator/References`；
- Figma 裁切 Sprite 输出到 `Assets/GeneratedUI/FigmaSprites`；
- Sprite 默认从 `Assets` 及全部子目录扫描；
- Prefab 根包含 `RectTransform`、`Canvas`、`GraphicRaycaster` 和
  `CanvasGroup`；
- 不生成业务脚本，不挂载项目专属 Binder。

这些目录都可以在生成窗口中修改。资源总目录应尽量选择实际 UI Sprite 的
共同父目录，以减少扫描时间并提高候选质量。

## 接入项目自己的 UI 框架

项目可以在自己的 Editor 程序集中实现
`Lxy.UIEffectGenerator.Editor.IUIEffectProjectAdapter`，并在
`InitializeOnLoad` 初始化时注册：

```csharp
using Lxy.UIEffectGenerator.Editor;
using UnityEditor;

[InitializeOnLoad]
internal static class MyUIEffectAdapterRegistration
{
    private static readonly IUIEffectProjectAdapter Adapter =
        new MyUIEffectProjectAdapter();

    static MyUIEffectAdapterRegistration()
    {
        UIEffectProjectAdapterRegistry.Register(Adapter, 100);
    }
}
```

适配器只负责：

1. 创建或加载项目标准 Prefab 外壳；
2. 在替换旧 `Generated` 前移除项目 Binder 中指向旧节点的引用；
3. 新树创建后刷新 Binder、脚本元数据或项目层级配置。

效果图分析、UISchema 校验、Sprite 匹配、组件创建和 Prefab 内容校验仍由
独立包负责。适配器不应重新实现或二次调用视觉匹配。

LxyDemo 的参考实现位于：

`Packages/com.lxy.ui/Editor/UI/UIEffectLxyProjectAdapter.cs`

它继续调用原有 `CSharpUIGenerator`，因此 LxyDemo 中的 ObjectBinder、
UICodeBinder、C#/Lua 配置和默认目录保持不变。

## Editor API

```csharp
using Lxy.UIEffectGenerator.Editor;

UIEffectPrefabGenerationResult result =
    UIEffectPrefabBuilder.GenerateFromSchemaPath(
        "Assets/Editor/UIEffectGenerator/Schemas/UIExample.json",
        new UIEffectPrefabGenerationOptions
        {
            panelId = "UIExample",
            prefabFolder = "Assets/GeneratedUI/Prefabs",
            resourceMatchMode =
                UIEffectResourceMatchMode.VisualSimilarity,
            resourceSearchRoots = new[]
            {
                "Assets/Game/UI/Sprites",
            },
        },
        true);
```

## 打包发布

独立包目录本身就是标准 UPM 包。发布前至少验证：

- 新建的普通 Unity 2022.3 工程只安装此包即可编译；
- 菜单能打开且显示 `通用 UGUI` 适配器；
- 纯色 UISchema 能生成 Prefab，根节点没有 Missing Script；
- 选择项目 Sprite 根后，视觉模式能正确匹配普通和九宫格 Sprite；
- 重新生成只替换 `Generated`，手工兄弟节点保持不变。
