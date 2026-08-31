# Codex + UnityMCP 完整还原协议

此协议只在用户明确要求从 Codex 终端手工使用 UnityMCP 直建 Prefab 时使用。效果图生成窗口的默认高精度单轮模式不读取本协议，也不把 UnityMCP 挂入视觉分析会话；窗口会直接接收 UISchema，并由当前 Unity Editor 调用 Builder。手工直建的目标是在首次生成前完成效果图、层级和资源分析，让 Unity 实际保存 Prefab，并在第一次 Builder 调用成功后立即结束。

## 权限与边界

- 只在提示给定的 Unity 项目与资源根内读取、搜索和写入。
- 允许写目标 `Assets/Editor/UISchemas/<Panel>.json`，并允许项目现有 Builder 写目标 Prefab 与其正常生成脚本。
- 不修改 `Packages/com.lxy.ui` 生成器、Skill、asmdef、MCP 包或现有业务逻辑。
- 所有 Unity 操作使用 `unity_*` MCP 工具。禁止直接请求 `127.0.0.1` Bridge，也禁止用 Shell 模拟 Unity 资源导入或手写 Prefab YAML。
- 多个 Unity 实例存在时，先列出实例，再按提示中的项目绝对路径和端口选择；之后所有 UnityMCP 调用显式携带该端口。

## 完整流程

1. 完整阅读效果图和提示中的原始像素尺寸，先记录所有可见实例的绝对矩形、绘制顺序、连续背景边界、可编辑文字、独立图形层和最近视觉父节点。
   - Unity 窗口会把超大原图转换成一个最长边 2048 的完整概览和四个高细节象限附件。只使用提示中已经附加的这些标准化图像，不要再用 `view_image` 或其他工具直接打开原始大图；象限只帮助看清细节，所有矩形仍换算回提示中的原始画布尺寸。
2. 读取现有 UISchema/Prefab 仅作为可疑线索；若与效果图冲突，以效果图和项目资源为准。不要因为旧 Schema 存在就跳过分析。
3. 在给定资源根递归搜索纹理、Sprite 与 `.meta`。候选确认同时使用：效果图局部像素、源尺寸、九宫格 Border、Alpha 轮廓、主色/渐变、边缘结构、父子上下文和资源命名。文件名只负责缩小范围，不能独立决定赋值。
4. 对每个可见的 Image/Button/Toggle 建立候选清单。九宫格背景按目标尺寸考虑 Sliced 后外观；图标、徽标、旗帜、头像保持轮廓和长宽比约束。确认资源后在 Schema 写精确 `resource`，Sprite Sheet 使用 `Assets/.../file.png#SubSpriteName`。
5. 建立 UISchema 2.0。连续外框只保留一张背景；独立字段底、名字条、分数条、徽标叠层不能省略；Text/Icon 必须进入最近视觉承载 Image 的 `children`。局部坐标由绝对矩形相减，递归累加后必须回到原图矩形。
6. 先分析全部重复实例，并逐组审计同一父级下的 Card/Item/Row/Cell/业务 Panel。名称只由颜色、阵营、队伍或数据变体区分，且根类型、宽高、子树类型、顺序和局部矩形同构时，只保留一个完整运行时模板；若保留项依赖资源证据，所有候选根写相同 `runtimeTemplateGroup` 和不同 `runtimeTemplateVariant`，让 Builder 在资源解析后裁剪。禁止同构候选既不标组又同时进入 Prefab。保留模板不得因去重被移动、拉伸、改名、改父子关系或合并资源切层；固定同时可见、左右布局、Button/Toggle 或结构不同的面板不去重。
7. 写入目标 Schema 后，使用 UnityMCP 导入它，并通过 `unity_execute_code` 调用 `LxyDemo.UIFramework.Editor.UIEffectPrefabBuilder.GenerateFromSchemaPath(...)`。传入提示中的 `panelId`、`prefabFolder`、脚本设置、`UIEffectResourceMatchMode.VisualSimilarity` 与资源搜索根，保留 `CSharpUIGenerator` 的 Canvas、Binder 和代码流水线。
8. `GenerateFromSchemaPath(...)` 首次正常返回且 `PrefabPath` 等于提示目标后，立即输出成功 JSON 并结束。生成成功后禁止继续调用 UnityMCP：不重新加载 Prefab，不遍历组件，不截图，不再次比对效果图，不重新搜索资源，不检查 Console/编译，不改 Schema，不第二次调用 Builder，也不生成资源或模板审计清单。只有首次调用抛错或未返回目标路径时才输出失败。

## 视觉与资源验收

- 不允许 `useReferenceImageAsVisual`，不允许整张效果图当底图后把真实组件设透明。
- 项目资源存在且能解释效果图时，不接受色块兜底。只有完成穷尽搜索且确无可信资源时，才保留纯色或用原生 Image/Text 组合简单符号。
- 不把自动匹配器的“低置信拒绝”当作资源不存在。完整模式由 Codex 直接检查候选并显式赋值，像素吻合、尺寸/Border/上下文成立时应覆盖保守自动阈值。
- Sprite、Sliced、文字烘焙、透明层、层级和运行时模板的判断全部在写 Schema 和首次 Builder 调用之前完成；不得把这些检查拖到 Prefab 已生成之后。

## 完成报告

首次 `GenerateFromSchemaPath(...)` 正常返回目标 `PrefabPath` 后立即返回 `success: true`、`prefabPath`、`schemaPath` 与一句简短 `summary`。不要返回资源清单、模板清单或剩余差异清单，也不要为填写报告继续读取项目或调用工具。
