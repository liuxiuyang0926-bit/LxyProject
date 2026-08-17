# Unity 后台低 Token 协议

只分析附加效果图，返回 `schemaJson` 和简短中文 `summary`。不要搜索仓库、读取其他参考、修改文件或生成 Prefab。

## 输出

`schemaJson` 必须是单行 UISchema 2.0 JSON：

```json
{"version":"2.0","name":"UIPanel","designWidth":2048,"designHeight":1180,"children":[]}
```

节点仅支持 `Container`、`Image`、`Text`、`Button`、`ScrollRect`。坐标以直接父节点左上角为原点。

每个节点只必填：

- `name`、`type`、`width`、`height`
- `x`、`y` 仅在非 0 时填写
- `children` 仅在非空时填写

以下默认字段必须省略：

- `anchor: "Auto"`
- `fontSize: 32`、`alignment: "Center"`
- 空的 `text`、`semantic`、`color`、`resource`、`resourceCandidates`
- 所有值为 `false` 的布尔字段
- `scrollDirection: "Vertical"`
- `binding: "Auto"`（仅 Button/ScrollRect 自动绑定）
- 空的 `children`、`variants`

只有纯色本身就是设计时才写 `color` 和 `intentionalColor: true`。资源字段通常留空，由 Unity C# 根据节点名解析。

从效果图本身不要猜运行时字段。只有任务明确要求代码访问某个非交互节点时才写 `binding: "Yes"`；纯装饰节点可写 `binding: "No"`，通常直接省略即可。

## 重复节点

相同列表行、页签或卡片只定义一次。用：

- `repeatCount`：实例数；
- `repeatOffsetX`、`repeatOffsetY`：相邻实例位移；
- `variants`：仅记录个别实例的文字、颜色、语义或资源差异。

模板及其所有子节点名称不要加 `01`、`02`；Unity 展开时自动追加两位编号。子节点的 `variants.index` 使用从 1 开始的实例序号。

```json
{"name":"ReportRow","type":"Container","x":14,"y":11,"width":1027,"height":115,"repeatCount":5,"repeatOffsetY":123,"children":[{"name":"Result","type":"Text","x":337,"y":19,"width":125,"height":55,"text":"攻占","color":"#FF9D28FF","variants":[{"index":4,"text":"失守","color":"#8A939AFF"},{"index":5,"text":"失守","color":"#8A939AFF"}]}]}
```

先建立少量语义分组，再放控件。不要推断未展示的动画、Mask、安全区或业务逻辑。文字看不清时用短占位并在 `summary` 说明。
