# UISchema 2.0

`UISchema` 是设计图与 Unity Prefab 之间经过审查的中间表示。Unity 使用 `JsonUtility` 读取它，因此必须使用普通 JSON，字段名严格保持本文档定义，且不能包含注释。

## 根字段

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `version` | string | 新文件使用 `"2.0"`；仍兼容 `"1.0"`。 |
| `name` | string | Panel ID 和默认 Prefab 名称。 |
| `designWidth` | number | 参考图像素宽度。 |
| `designHeight` | number | 参考图像素高度。 |
| `referenceImage` | string | 用于审查/预览的可选项目资源路径。 |
| `children` | array | 顶层 UI 节点。 |

## 节点字段

| 字段 | 类型 | 默认值/用途 |
| --- | --- | --- |
| `name` | string | 具有语义且全局唯一的可绑定名称。 |
| `type` | string | `Container`、`Image`、`Text`、`Button` 或 `ScrollRect`。 |
| `semantic` | string | 资源语义，例如 `gold_icon` 或 `role_avatar`。 |
| `anchor` | string | 默认 `Auto`，由 C# 根据矩形位置推导；也可显式指定命名锚点。 |
| `x`, `y` | number | 相对于直接父节点左上角的位置。 |
| `width`, `height` | number | 设计像素尺寸；必须大于 0。 |
| `text` | string | `Text` 的可见文字或 `Button` 的默认 Label。 |
| `fontSize` | number | TMP 字号。 |
| `alignment` | string | TMP 对齐方式，通常为 `Left`、`Center` 或 `Right`。 |
| `bold` | bool | 是否应用 TMP 粗体样式。 |
| `color` | string | `#FFFFFFFF` 等 HTML 颜色；为空时使用类型默认值。 |
| `resource` | string | 精确 Sprite 资源路径，可选 `#subSpriteName`。 |
| `resourceCandidates` | string[] | 有证据支持的候选文件名。 |
| `intentionalColor` | bool | 只有纯色本身就是最终视觉时才设为 true。 |
| `preserveAspect` | bool | 设置 `Image.preserveAspect`。 |
| `sliced` | bool | 请求 Sliced 模式；仅在匹配 Sprite 存在 Border 时生效。 |
| `raycastTarget` | bool | 是否启用 Graphic Raycast；Button 始终保持可交互。 |
| `scrollDirection` | string | `Vertical`、`Horizontal` 或 `Both`。 |
| `binding` | string | `Auto`、`Yes`、`No`；2.0 默认只自动绑定 Button/ScrollRect。 |
| `repeatCount` | int | 默认 1；重复列表行、页签或卡片的实例数。 |
| `repeatOffsetX/Y` | number | 相邻重复实例的位移。 |
| `repeatNameDigits` | int | 自动名称后缀位数，默认 2。 |
| `variants` | array | 按 1 开始的实例索引覆盖 `text/color/semantic/resource/resourceCandidates`。 |
| `children` | array | 当前节点局部设计坐标空间中的子节点。 |

## 锚点和坐标

支持 `Auto` 和以下显式锚点：

`TopLeft`、`TopCenter`、`TopRight`、`MiddleLeft`、`MiddleCenter`、
`MiddleRight`、`BottomLeft`、`BottomCenter`、`BottomRight`、
`StretchHorizontal`、`StretchVertical`、`StretchAll`。

`x` 和 `y` 始终从父节点左上角开始测量。命名锚点控制响应式附着位置，不改变 JSON 中坐标的记录方式。对于 Stretch 锚点，编译器根据 `x`、`y`、`width` 和 `height` 把矩形转换为边距。

示例：在 1080x1920 的父节点中，一个左上角位于 `(820, 1780)`、尺寸为 220x80 的按钮。默认字段全部省略：

```json
{"name":"Confirm","type":"Button","x":820,"y":1780,"width":220,"height":80,"text":"确定","bold":true,"sliced":true}
```

## 资源和占位行为

解析顺序：

1. `resource` 中的明确资源路径；
2. `resourceCandidates` 中的精确/强匹配；
3. `semantic` 和 `name` 的强匹配；
4. 彩色占位。

占位颜色用于语义诊断：

- 背景：灰色；
- 按钮：绿色；
- 头像/肖像：紫色；
- Icon：黄色；
- 普通图片：蓝色；
- 未知图片类节点：红色。

缺失节点还会显示 `[MISSING]` 和语义名称。如果本来就需要纯色填充，设置 `intentionalColor: true` 并提供 `color`。

`Button` 设置 `text` 后，构建器会添加一个不参与绑定的白色 `Label` 子节点。如果 Label 的矩形、文字颜色或样式需要不同，改为添加显式 `Text` 子节点；不要同时使用两种方式。Button 的 `color` 属于背景 Image，不属于自动 Label。

## 重复模板

重复节点只定义一次。模板及子节点名称不带编号；构建器展开时自动追加 `01`、`02`。子节点可以按实例索引覆盖少量差异：

```json
{"name":"Row","type":"Container","width":900,"height":100,"repeatCount":5,"repeatOffsetY":108,"children":[{"name":"Result","type":"Text","x":300,"width":120,"height":50,"text":"胜利","variants":[{"index":4,"text":"失败","color":"#FF5544FF"},{"index":5,"text":"失败","color":"#FF5544FF"}]}]}
```

生成窗口还会识别旧 Schema 中连续的 `Row01`、`Row02` 等同构节点，自动合并并以精简 JSON 保存。
