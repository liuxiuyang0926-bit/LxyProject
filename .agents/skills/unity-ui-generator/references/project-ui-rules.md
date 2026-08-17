# LxyDemo UI 项目规则

## 现有制作流水线

标准制作入口实现在：

`Packages/com.lxy.ui/Editor/UI/CSharpUIGeneratorWindow.cs`

现有菜单命令：

`工具/UI工具/创建Prefab`

效果图编译器由以下文件实现：

- `UIEffectSchema.cs`
- `UIEffectPrefabBuilder.cs`
- `UIEffectPrefabGeneratorWindow.cs`

它必须调用 `CSharpUIGenerator.Generate(...)`，以确保生成的 Prefab 保留项目的 Canvas、GraphicRaycaster、CanvasGroup、ObjectBinder、UICodeBinder、UI 层级配置以及 C#/Lua 生成行为。

## AI 与 C# 分流

Unity Editor 后台 Codex 只输出视觉结构；不得搜索 Sprite 或执行 Prefab 操作。固定 C# 流程负责图片路径/尺寸、`Auto` 锚点、重复节点展开、资源索引、缺失占位、ScrollRect Content、Prefab 保存与 Binder 刷新。

Schema 使用 2.0 精简格式：省略默认字段并以单行 JSON 保存。重复列表行、页签和卡片使用 `repeatCount`、位移和 `variants`，不得为每个实例复制完整子树。旧式 `Name01/Name02` 同构兄弟由压缩器自动合并。

Prefab 生成时继续复用 `CSharpUIGenerator` 的基础组件与脚本流水线。重复模板在 Schema 中复用定义，最终 Prefab 展开为正常 GameObject 层级，便于现有 Binder 和运行时代码使用；不要额外建立一套 Canvas 或 Binder。

默认 Prefab 生成目录：

`Assets/GameResources/Prefabs/UIRes`

默认 UISchema 和参考图目录：

- `Assets/Editor/UISchemas`
- `Assets/Editor/UIReferences`

参考截图只作为 Editor 输入。除非用户明确将其提升为运行时资源，否则不得复制到 `Assets/GameResources`。

## 生成内容的所有权

编译器只拥有 Prefab 根节点下名为 `Generated` 的子节点。重新生成时删除并重建这个子树，同时保留所有手工兄弟节点。

不要手工编辑生成的 C# Base 文件。创建第一版视觉 Prefab 时，不要修改已有运行时逻辑。

## 绑定命名

项目生成器按前缀自动收集组件绑定：

- `btn_`：`Button`
- `img_`：`Image`
- `txt_` 或 `tmp_`：TMP 文本
- `scroll_`：`ScrollRect`
- `rt_`：`RectTransform`

效果图构建器只给需要绑定的节点添加这些前缀。UISchema 2.0 的 `binding: Auto` 仅自动绑定 Button 和 ScrollRect；Image、Text、Container 只有明确写 `binding: Yes` 才进入 Binder。这样可避免视觉稿中的纯装饰组件生成大量无用字段。Schema 名称不得自带前缀；可绑定节点名称必须全局唯一。

按钮辅助 Label 和占位 Label 特意不使用绑定前缀。

## 资源匹配

默认搜索根目录为 `Assets/GameResources`。已知更精确的项目目录时，增加更窄的搜索根；这样可以同时提高速度和匹配质量。

优先使用通过仓库搜索或 `AssetDatabase` 找到的明确 Sprite 路径。Sprite Sheet 使用：

`Assets/.../atlas.png#SubSpriteName`

不要为了让不确定的匹配生效而修改纹理导入设置。不要绑定名称勉强相似的最近资源。解析器使用保守阈值，因此明显的占位色块优于看似合理但错误的美术资源。

## 输入来源边界

Figma 通过官方图片渲染接口支持，Token 保存在仓库之外。链接必须通过 `node-id` 指定节点。

第一版没有集成需要鉴权的蓝湖客户端。只接受本地导出图片或直接返回图片的 URL。绝不能提交蓝湖凭证、浏览器 Cookie、Figma Token 或临时签名 URL。下载后只保存和引用本地 Editor 资源路径。

## 第一版组件范围

支持：

- `Container`
- 通过 `Text` 表示的 `TextMeshProUGUI`
- `Image`
- `Button`
- `ScrollRect`

UISchema 2.0 尚不表示：

- Toggle、Slider、Dropdown、InputField；
- GridLayoutGroup 和自动列表模板；
- 显式 Mask 节点；
- SpriteAtlas Address 选择；
- 动画、过渡和运行时数据绑定；
- 安全区和设备专属变体。

在结果报告中明确列出这些问题，不要静默近似实现。
