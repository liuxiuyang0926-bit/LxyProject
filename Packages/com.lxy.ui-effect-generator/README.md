# Lxy UI Effect Generator

面向 Unity 2022.3 的可移植 Editor 包，可以从本地效果图、Figma 节点或
UISchema 2.0 生成可编辑的 UGUI Prefab。Sprite 视觉匹配、九宫格、层级恢复、
运行时模板裁剪和高精度 Codex 单轮分析都位于这个独立包内。

## 依赖

- Unity UI (`com.unity.ugui`)
- TextMeshPro (`com.unity.textmeshpro`)
- 每台制作机单独安装并登录 Codex CLI

默认 `Generic UGUI` 适配器只创建标准 UGUI Prefab，不依赖 ObjectBinder、
XLua、YooAsset 或任何业务 UI 框架。生成器只拥有 Prefab 根节点下的
`Generated` 子树，重新生成会保留其他手工子节点。

安装后通过以下任一菜单打开：

- `工具/UI工具/根据效果图生成Prefab`
- `Tools/UI Tools/Generate Prefab From Design`

安装、迁移和自定义项目适配器见
[Installation](Documentation~/Installation.md)。
