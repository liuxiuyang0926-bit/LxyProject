---
name: unity-ui-generator
description: 根据截图、本地图片、Figma 节点、蓝湖导出图、图片直链或 UISchema，为 LxyDemo 分析、生成、审查或调试 Unity UI Prefab。适用于效果图转精简 UISchema、重复 UI 模板、Sprite 自动匹配、缺失资源占位，以及项目 UI 生成器维护。
---

# Unity UI 生成器

只把视觉理解交给 Codex；把可重复、可验证的工作交给 Unity C#。

## 按任务读取参考

- Unity Editor 后台提示包含“低 Token 模式”时：只读 [compact-blueprint.md](references/compact-blueprint.md)。不要搜索项目资源、读取其他参考、修改文件或调用 Unity。
- 在 Codex 终端手工创建或审查 Schema 时：读 [ui-schema.md](references/ui-schema.md)。需要生成 Prefab 时再读 [project-ui-rules.md](references/project-ui-rules.md)。
- 修改生成器 C#、资源解析或 Prefab 流水线时：同时读上述两个完整参考，并使用 `unity-project-development`。

## 职责边界

Codex 只负责：

- 从效果图识别区域、父子层级、控件类型、可见文字和大致矩形；
- 判断重复的列表行、页签或卡片，并用一个模板表达；
- 标记纯色块和无法确认的交互。

Unity C# 负责：

- 下载/导入图片，注入原图路径和尺寸；
- 推导 `Auto` 锚点，展开重复模板并验证 Schema；
- 扫描 Sprite、匹配资源、创建缺失占位；
- 创建组件、计算 ScrollRect Content、保存 Prefab；
- 复用 `CSharpUIGenerator` 刷新 Canvas、Binder 和脚本；
- 压缩 Schema，并显示本次 Codex Token。

不要让 Codex 枚举资源目录、猜 `Assets/...` 路径、输出默认字段、重复抄写相同节点或直接逐个创建 GameObject。

## 视觉分析规则

先识别 `Panel > Header / Content / Footer` 等区域，再放置 Image、Text、Button 和 ScrollRect。使用能解释效果图的最小层级；不要推断图片没有展示的动画、安全区、Mask 或业务交互。

重复内容只定义一次。差异文字或颜色使用 `variants`；节点名不手写数字后缀，C# 展开时自动追加。

资源字段通常留空，让 C# 根据节点名和 `semantic` 解析。只有用户或仓库已明确给出真实路径时才填写 `resource`。

## 生成入口

- Unity 窗口：`工具/UI工具/根据效果图生成Prefab`
- 选中 Schema：`Assets/UI工具/根据选中的UISchema生成Prefab`
- 压缩旧 Schema：`Assets/UI工具/压缩选中的UISchema（低Token）`
- Editor API：`UIEffectPrefabBuilder.GenerateFromSchemaPath(...)`

只有 Unity 实际保存 Prefab 后才能声称生成完成。重新生成只拥有 Prefab 根节点下的 `Generated` 子树，并保留手工兄弟节点。

第一版组件范围保持为 `Container`、`Image`、`Text`、`Button` 和 `ScrollRect`。
