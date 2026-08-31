# Unity 后台低 Token 协议

只分析附加效果图，返回 `schemaJson` 和简短中文 `summary`。不要搜索仓库、读取其他参考、修改文件或生成 Prefab。

## 输出

`schemaJson` 必须是单行 UISchema 2.0 JSON：

```json
{"version":"2.0","name":"UIPanel","designWidth":2048,"designHeight":1180,"children":[]}
```

节点仅支持 `Container`、`Image`、`Text`、`Button`、`Toggle`、`ToggleGroup`、`ScrollRect`。坐标以直接父节点左上角为原点。

独立动作（确认、领取、返回等）使用 `Button`。页签、分类、模式和筛选等同组互斥选项使用 `Toggle`，共同放在一个边界可量取的 `ToggleGroup` 父节点下；子节点必须改写成相对该组的局部坐标。视觉上明确选中的项写 `isOn:true`。默认组不允许全部取消，只有设计明确允许时才写 `allowSwitchOff:true`。

## 通用组件过滤

默认不要把运行时代码接管的通用外壳写入 UISchema：

- 跳过名为 `TopTabGroup` 的节点及其全部后代；顶部通用页签由代码动态生成。
- 跳过画布左上角语义明确为返回的完整组件，包括 Button、返回箭头/图标、文字、点击区域及仅用于包装它们的父节点。
- 用户明确要求固化时才保留。过滤组件后仍使用完整原图坐标；其他内容不得补位或重新建立原点，也不要创建透明、空白或同尺寸占位节点。

## 模态弹窗前景范围

先判断截图是完整页面还是覆盖在宿主页面上的模态弹窗。只有同时看到覆盖画布主要区域的半透明 `Overlay/Dimmer/Scrim`，以及绘制在它上方、边界独立的 `Popup/Dialog/Modal` 前景时，才按模态弹窗处理：UISchema 只保留遮罩与弹窗自身的完整视觉子树。遮罩下透出的地图、页面背景、顶部导航、货币栏、HUD、聊天/排行/商城入口、列表和其他宿主页面控件即使清晰可见，也不得输出。

如果遮罩与弹窗被初步放进宿主页面 Container/Image，移除宿主页面后把遮罩和弹窗提升为根节点；递归累计原祖先 `x/y` 后写回完整画布绝对坐标，弹窗不得补位、居中重算、缩放或裁切。弹窗标题、关闭按钮和全部字段必须归入弹窗子树。没有大面积遮罩、没有独立前景，或无法证明是宿主页面叠加时，不执行该过滤。

## 几何还原（最高优先级）

1. **锁定坐标系**：`designWidth/designHeight` 必须与提示中的源文件原始像素宽高完全相同。Unity 导入尺寸和附件预览都可能被缩小；发现预览尺寸不同时，先计算缩放比例，再把所有矩形换算回源文件坐标。不要使用缩放后预览尺寸、常用屏幕模板或混用归一化比例。
2. **保留完整画布原点**：坐标原点始终是源图片最外层左上角 `(0,0)`。透明边、背景留白和顶部空区都属于画布；不得裁边，也不得以首个可见控件作为新原点。
3. **先测绝对矩形**：先在整图上记录可见节点的 `[left, top, right, bottom]`，再计算 `x=left`、`y=top`、`width=right-left`、`height=bottom-top`。先找画布边缘、中线、大背景边缘、对齐线和重复节奏，再测小控件；不要从“大约占屏多少”直接猜矩形。
4. **后建层级**：只将边界能从图中明确量取的区域作为父节点。子节点局部坐标必须由绝对坐标相减得到：`x=childLeft-parentLeft`、`y=childTop-parentTop`；不得把整图坐标直接写入嵌套节点。如果语义分组没有可测量的外框，省略该分组，让子节点挂到最近的可测量父节点。
5. **使用结构约束**：对称节点由同一中线镜像计算；等宽/等高节点复用同一尺寸；重复间距使用相邻左上角之差，不要对每个实例独立目测。先定边距、间距和对齐线，最后才取整。
   对同一结构使用稳定的规范化节点名，不在 `Plate/Field/Panel/Backdrop` 等近义词间随机切换；全部矩形统一按最近整数像素取整，兄弟节点按从后到前的绘制顺序输出。相同输入即使被强制重新分析，也应尽量产生可比较的规范化 Schema。
6. **按控件定边界**：Image/Button/Container 使用主体外边界；只有阴影或发光明显是同一张不可分离的图时才纳入矩形。Text 优先使用背景、对齐线与留白可以证明的排版框；无法推定排版框时再使用可见字形的紧框，不要套用默认文本宽高。
7. **递归自检**：输出前，对每个节点递归累加祖先 `x/y` 重建绝对矩形，并与原图再比一次。至少复核顶部控件、主内容区和侧边详情区三组绝对矩形。核对左/右/上/下边缘、中心、对称、对齐和重复间距。主区域边缘误差目标不超过 `max(4px, 短边*0.5%)`，普通控件不超过 `max(8px, 短边*1%)`。无法达到时保留最有证据的值，并在 `summary` 点名不确定节点。

局部坐标示例：父节点绝对矩形为 `[100,200,900,800]`，子节点为 `[140,260,340,320]`，则父节点尺寸为 `800x600`，子节点必须写 `x:40,y:60,width:200,height:60`。

## 视觉所有权层级（最高优先级）

禁止把效果图中的所有 Image 和 Text 按绝对坐标平铺在同一层。背景条、标题牌、字段底、统计格、按钮、页签和边界清晰的局部 Panel 是视觉承载节点；完全位于其内部的文字、图标和值必须进入它的 `children`。截图中每一条具有可见边缘、纹理、渐变或色带的字段底、名字条、分数条都输出一个 Image，即使其纹理很弱或大部分被字覆盖；同条上的 Label、Value、Bonus 全部属于它，不能直接挂在非视觉 Container。多个先绘制节点都包含同一子矩形时，选择面积最小且语义完整的最近承载节点；全屏 `DimOverlay` 不能成为弹窗或业务内容父节点。建立层级后重新计算局部 `x/y`，递归累加后的绝对矩形必须保持不变。

必须输出类似结构：`{"name":"GuildNamePlate","type":"Image","children":[{"name":"GuildName","type":"Text",...}]}`；字段必须输出类似 `{"name":"LevelField","type":"Image","children":[{"name":"LevelLabel","type":"Text",...},{"name":"LevelValue","type":"Text",...}]}`。FactionTitle 归入 FactionHeader，统计 Label/Value 归入各自 StatBackground。输出前逐个检查每个 Text、Icon、Button、Toggle 的最近视觉父节点；只检查最终绝对坐标而忽略父子关系不算通过。

每个节点只必填：

- `name`、`type`、`width`、`height`
- `x`、`y` 仅在精确为 0 时省略；不得因为位置不确定而省略
- `children` 仅在非空时填写

以下默认字段必须省略：

- `anchor: "Auto"`
- `fontSize: 32`、`alignment: "Center"`
- 空的 `text`、`semantic`、`color`、`resource`、`resourceCandidates`
- 所有值为 `false` 的布尔字段
- `textMode: "Auto"`、空的 `visualKind`
- `scrollDirection: "Vertical"`
- `binding: "Auto"`（仅 Button/Toggle/ScrollRect 自动绑定）
- 空的 `children`、`variants`

先判断视觉属于父图内部、独立 Sprite 还是多层合成。一个父级背景已经包含页签底、边框、关闭图标或装饰时，不得为同一区域再建立 `RoundTab`、`CloseIcon` 等重复视觉节点；只保留独立文字或交互。底章、旗帜、徽标等轮廓可分辨的叠层应按从后到前建立多个 Image。旗面与中央徽标的轮廓、颜色和遮挡关系可分辨时必须拆成两个 Image，不能把独立徽标当成旗面纹理省略。

逐个反查嵌套 Image 的独立性：父 Sprite 内部连续的底色、纹理或转角，不能因为上方有一段标题文字就另建 `Plate`、`Backdrop`、`Header`。只有能看到独立描边、接缝、完整转角、透明轮廓或明显不同纹理时，才建立该 Image；否则把 Text/交互直接挂到父视觉节点。语义名称和 Text 排版框都不是独立资源证据。

`Panel/Section/Content/Header/Footer` 一类带 children 的布局分组若被误写成 Image，C# 可在父 Sprite 命中后遮掉其全部后代，再比较该区域由父图或独立 Sprite 解释的像素分；父图显著更强时只把该节点转回 Container，保留名称、层级、矩形与后代。`Field/Bar/Plate/Badge/Flag/Icon/Tile/Cell/Card/Row` 等独立视觉承载节点不参与该自动转换。

弹窗、卡片等背景的资源边界由连续的边框、底纹和转角决定，不由 `Header`、`Body`、`Content` 等语义分区决定。只要同一外框或底纹连续跨过标题区和内容区，就必须先输出一个覆盖完整外框的背景 `Image`；若资源横跨直接父级全宽，即使内容只在中间，Image 也按完整边界覆盖父级。标题区、内容区等语义分组使用无 Graphic 的 Container 并放入该背景 Image.children，不得把连续底图拆成多个 Image 色块。只有能看到独立接缝、各自完整转角或明显不同的重复纹理时，才拆成多张背景。卡片底纹即使大部分被徽标、文字和数值面板遮住，也要保留一个覆盖其完整可见边界、排在内容之前的背景 `Image`，不能因遮挡多而改成纯色。

输出前专门检查所有左右相邻、顶部和高度相同的 `Image`：如果它们之间没有可见接缝且共享连续外框/底纹，删除按语义切出的多张 Image，改成一个按完整视觉边界量取的背景 Image；原 `Title/Header/Content/Body` 仅在确有分组价值时改为无 Graphic 的 Container，并全部作为该背景的 children。不得先按中间内容宽度建立局部 Container，再把本应延伸到直接父级边缘的背景裁成该 Container 宽度；疑似跨父级完整宽度时给唯一背景写 `mayUseFullCanvasSprite:true`。

连续状态文字要准确保留文字、颜色、字号、对齐和间距。同一行只有在颜色或运行时字段确实不同的情况下才拆成多个 Text；拆分矩形不得重叠，白色标签和黄色数值必须按原图顺序紧邻。

只有纯色本身就是设计时才写 `color` 和 `intentionalColor: true`。资源字段通常留空，由 Unity C# 在用户选择的资源父目录中做本地视觉匹配；不要输出 `reference://crop`，也不要猜资源名或路径。对视觉角色可辨认的 Image/Button/Toggle 填写简短稳定的通用 `semantic`，例如 `background/panel/header/bar/field/icon/emblem/badge/crest/flag/banner/avatar/portrait/overlay`；只描述视觉类别，不猜项目资源名或业务数据，确实无法判断时才省略。匹配失败由 Unity 保留色块并写日志，不生成裁图或 `[MISSING]` 文字。

当结构取决于项目资源时，只输出不带资源名的校正提示：相邻连续区域可能属于同一 Sprite 时给 Image 写 `mayMerge:true`；它也表示授权 C# 在父图局部已包含该视觉时折叠节点，所以具有独立边界的字段底、名字条、分数条不得写 `mayMerge`。背景疑似横跨直接父级或完整画布时写 `mayUseFullCanvasSprite:true`。书法、Logo、描边或发光标题仍先输出 Text 以记录文字和矩形，同时写 `textMode:"ArtText"`、`visualKind:"ArtText"`，疑似双层发光/描边时再写 `mayLayer:true`。排名、短数字或短标签可能已经烘焙进父图时写 `textMode:"PossiblyBaked"`。普通运行时文字省略 `textMode`；只有明确必须始终可编辑且不能被父图吸收时写 `textMode:"Editable"`。Unity C# 只有在本地像素证据达到高置信门槛时才合并区域、补建文字承载背景、转换艺术字或删除烘焙文字。

父级普通 Sprite 若已经连续包含某个文字区域的底色、端帽、纹理或装饰，不要再为该区域建立只承载文字的 Image；文字直接挂到父视觉节点。输出前尤其复核标题页签、状态条内嵌段和完整分数面板的内部标题区。若仍误拆且漏写 `mayMerge`，C# 只会在父 Sprite 原始尺寸与父节点几乎一致、整幅父图已高置信命中、子节点仅含文字、父图对该局部达到近乎完全复现并明显优于独立候选时，把它视为结构漏标并折叠；这不是建立重复 Image 的理由。

禁止设置 `useReferenceImageAsVisual`，也禁止把文本、图片或交互 Graphic 设为透明后只显示整张参考图。普通可编辑文字必须保留为 `Text`；美术字先按上述规则标记，交给本地资源证据校正。组件是否正确要能在关闭背景后独立检查。

从效果图本身不要猜运行时字段。`binding` 只允许 `Auto`、`Yes`、`No`，它是策略而不是字段名；禁止填写 lowerCamelCase 业务标识。只有任务明确要求代码访问某个非交互节点时才写 `binding: "Yes"`；纯装饰节点可写 `binding: "No"`，通常直接省略即可。最终字段名由节点 `name` 和项目统一前缀生成。所有最终同时存在的可绑定节点 `name` 必须全局唯一；不同固定面板中同名的 Value、Label、Button 使用最近父级语义前缀，未绑定静态装饰可以跨父级同名。运行时模板候选可在裁剪前保持同构名称，Builder 只对裁剪后的最终树做确定性冲突修复。

## 重复节点

视觉还原优先于去重。先在完整效果图上分析全部实例，确定第一项的原始绝对矩形、独立 Sprite 边界、绘制顺序和完整父子层级，再区分运行时数据列表与设计期固定重复。省略后续实例只是最终 Schema 的输出投影，不得重新测量、补位、回流、拉伸、缩放、改名、改父子关系或合并保留模板的资源节点；后续实例所在像素不得并入相邻背景边界。

- Card、Row、ListItem、奖励格、排行项、阵容项等由业务数据驱动的列表，Prefab 只生成一个模板。去掉 `01/02/...` 后缀，不写 `repeatCount`、`repeatOffsetX/Y` 或 `variants`。例如 `CityCard01~04` 输出一个 `CityCard`，`DefenseRow01~04` 输出一个 `DefenseRow`；运行时代码自行循环创建。
- 运行时业务模板即使没有数字后缀，最终 Prefab 也只保留一个。相邻 Panel/Card/Row/Item 仅以颜色、阵营、队伍或状态前缀区分，且子树结构、尺寸和局部矩形同构时：独立视觉完全一致可直接只输出第一个；若正确保留项依赖项目 Sprite，则完整输出所有候选，在候选根节点写相同 `runtimeTemplateGroup` 和各自 `runtimeTemplateVariant`（如 `red/green/blue`），不写 `repeatCount`、位移或 `variants`。C# 匹配资源后只保留证据最完整的一项；保留项维持自己的原始矩形和资源层级。无法确认同构时不要标候选组。简单按钮/Toggle、左右固定布局或结构不同的面板不得因此折叠。
- 只有数量固定、必须全部常驻 Prefab 的页签或装饰元素，才使用 `repeatCount`、位移和 `variants`。

同构业务模板不得在资源匹配前删除、合并或重排。无资源歧义时由 Codex 直接省略；有资源歧义时 C# 必须先按 Schema 原样匹配全部标记候选，随后只在同一父节点、同一 `runtimeTemplateGroup` 内按资源证据裁剪，证据相同则稳定保留原始绘制顺序最前项。

同一 `runtimeTemplateGroup` 的候选在裁剪前还会互相校验结构位置相同的通用视觉。颜色/阵营 token 明确对应某个变体的 Header、Flag、Emblem 等资源保持各自证据；不含变体 token 的字段底、名字条、完整分数面板等资源，只有在多个候选区域的局部像素与组均值都通过门槛时才可回填到保留模板。不要为了让后续回填生效而省略候选实例的完整视觉层级。

运行时列表模板示例：

```json
{"name":"DefenseRow","type":"Container","x":10,"y":60,"width":1070,"height":150,"children":[{"name":"RowIndex","type":"Text","width":30,"height":40,"text":"1"}]}
```

只建立少量且边界可测量的语义分组，再放控件。不要推断未展示的动画、Mask、安全区或业务逻辑。文字看不清时用短占位并在 `summary` 说明。
