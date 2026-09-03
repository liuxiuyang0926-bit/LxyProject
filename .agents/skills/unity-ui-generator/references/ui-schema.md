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
| `referenceImageHash` | string | C# 注入的参考图内容指纹，格式为 `sha256:<64位小写十六进制>`；用于同图稳定复用，AI 不填写。 |
| `useReferenceImageAsVisual` | bool | 旧版兼容字段；新 Schema 禁止设置，不能用整图和透明组件代替真实 UI。 |
| `children` | array | 顶层 UI 节点。 |

## 节点字段

| 字段 | 类型 | 默认值/用途 |
| --- | --- | --- |
| `name` | string | 具有语义且全局唯一的可绑定名称。 |
| `type` | string | `Container`、`Image`、`Text`、`Button`、`Toggle`、`ToggleGroup` 或 `ScrollRect`。 |
| `semantic` | string | 与资源名无关的通用视觉类别，例如 `background`、`header`、`field`、`icon`、`emblem`、`badge`、`flag` 或 `avatar`。 |
| `visualKind` | string | 可选的视觉形态提示；目前主要用 `ArtText` 表示书法、Logo、发光标题等可能由 Sprite 表达的美术字。 |
| `textMode` | string | `Auto`、`Editable`、`PossiblyBaked` 或 `ArtText`。默认 `Auto`；用于资源匹配后的文字结构校正。 |
| `anchor` | string | 默认 `Auto`，由 C# 根据矩形位置推导；也可显式指定命名锚点。 |
| `x`, `y` | number | 相对于直接父节点左上角的位置。 |
| `width`, `height` | number | 设计像素尺寸；小于等于 0 时由 C# 归一为 1 像素。 |
| `text` | string | `Text` 的可见文字或 `Button` 的默认 Label。 |
| `fontSize` | number | TMP 字号。 |
| `characterSpacing` | number | TMP 字符间距；默认 0。仅在效果图可量取且字体字面宽度需要校正时使用。 |
| `alignment` | string | TMP 对齐方式，通常为 `Left`、`Center` 或 `Right`。 |
| `bold` | bool | 是否应用 TMP 粗体样式。 |
| `color` | string | `#FFFFFFFF` 等 HTML 颜色；为空时使用类型默认值。 |
| `resource` | string | 精确 Sprite 资源路径，可选 `#subSpriteName`。生成式高精度流程中 `Verified` 只是 AI 的锁定请求；只有 Unity 对同一节点独立得到相同的高分、高领先、无结构风险赢家时才锁定，否则自动转入候选精排。 |
| `resourceCandidates` | string[] | 有证据支持的精确候选路径，按视觉可信度降序排列；视觉模式只把它们作为精排证据。 |
| `resourcePolicy` | string | `Auto`、`Candidate`、`Verified` 或 `ColorFallback`，默认 `Auto`。 |
| `intentionalColor` | bool | 只有纯色本身就是最终视觉时才设为 true。 |
| `preserveAspect` | bool | 设置 `Image.preserveAspect`。 |
| `sliced` | bool | 请求 Sliced 模式；仅在匹配 Sprite 存在 Border 时生效。 |
| `raycastTarget` | bool | 是否启用 Graphic Raycast；Button 始终保持可交互。 |
| `mayMerge` | bool | 当前 Image 可能只是同一完整 Sprite 被误拆后的一个区域，并授权 C# 在父 Sprite 已包含该局部时折叠；具有独立边界的字段底、名字条、分数条不得设置。 |
| `mayLayer` | bool | 当前视觉可能由两张或多张 Sprite 重叠构成。 |
| `mayUseFullCanvasSprite` | bool | 当前背景可能横跨直接父级或完整设计画布，而非仅覆盖 AI 初次量取的内容区域。 |
| `isOn` | bool | `Toggle` 的默认选中状态；仅在默认选中时写 true。 |
| `allowSwitchOff` | bool | `ToggleGroup` 是否允许全部取消；默认 false。 |
| `scrollDirection` | string | `Vertical`、`Horizontal` 或 `Both`。 |
| `binding` | string | `Auto`、`Yes`、`No`；2.0 默认自动绑定 Button、Toggle 和 ScrollRect。 |
| `runtimeTemplateGroup` | string | 可选；同构运行时模板候选的稳定组名。只在正确保留项依赖项目 Sprite 时写在候选根节点。 |
| `runtimeTemplateVariant` | string | 可选；候选的简短稳定外观值，例如 `red`、`green`、`blue`、`selected`。与 `runtimeTemplateGroup` 配套使用。 |
| `repeatCount` | int | 默认 1；重复列表行、页签或卡片的实例数。 |
| `repeatOffsetX/Y` | number | 相邻重复实例的位移。 |
| `repeatNameDigits` | int | 自动名称后缀位数，默认 2。 |
| `variants` | array | 按 1 开始的实例索引覆盖 `text/color/semantic/resource/resourceCandidates`。 |
| `children` | array | 当前节点局部设计坐标空间中的子节点。 |

`binding` 是绑定策略，不是生成字段名。通常省略或写 `Auto`；需要把 Image、Text、Container 等显式暴露给 Binder 时写 `Yes`，明确禁止时写 `No`。禁止把 `playerName`、`settlementCondition` 一类 lowerCamelCase 业务标识写入 `binding`；最终字段名始终由节点 `name` 和项目统一前缀生成。AI 生成结果若把合法 C# 标识符误写进 `binding`，导入器会将其归一为 `Yes` 并记录警告；手写/已保存 Schema 仍严格拒绝非法枚举值。所有最终同时存在的可绑定节点必须具有全局唯一的 `name`；固定区域中重复出现的 `CurrencyValue`、`AddButton` 等应带最近父级语义前缀，未绑定的静态 `Label` 可以在不同父级重复。资源匹配和运行时模板裁剪后的最终树若仍有绑定冲突，Builder 只对后出现的冲突项按最近父级路径确定性改名，不删除节点，也不改变层级、坐标或资源。

## 锚点和坐标

支持 `Auto` 和以下显式锚点：

`TopLeft`、`TopCenter`、`TopRight`、`MiddleLeft`、`MiddleCenter`、
`MiddleRight`、`BottomLeft`、`BottomCenter`、`BottomRight`、
`StretchHorizontal`、`StretchVertical`、`StretchAll`。

`x` 和 `y` 始终从父节点左上角开始测量。命名锚点控制响应式附着位置，不改变 JSON 中坐标的记录方式。对于 Stretch 锚点，编译器根据 `x`、`y`、`width` 和 `height` 把矩形转换为边距。构建器会创建固定为 `designWidth × designHeight` 的设计坐标根，再按比例居中适配项目的 1920×1080 UI 参考分辨率，所有节点始终在同一个设计坐标空间计算。

## 效果图几何量取

- 以效果图源文件原始像素宽高填写 `designWidth/designHeight`，不得使用 Unity `maxTextureSize` 产生的导入尺寸、附件预览缩放尺寸或默认分辨率。
- 原点始终是完整源图最外层左上角；透明边和背景留白不能裁掉或重新归零。
- 先量取节点相对整图的绝对矩形 `[L,T,R,B]`，建立层级后再计算局部矩形：`x=Lchild-Lparent`、`y=Tchild-Tparent`、`width=R-L`、`height=B-T`。
- 只有外边界可从图中测量的区域才适合作为父节点。没有可测量原点的抽象分组会引入累积偏移，应省略。
- 对称、对齐、等宽高和重复节奏应作为几何约束统一计算，不对同组元素分别目测。
- 同一视觉结构使用稳定的规范化名称；矩形统一按最近整数像素取整，兄弟节点按从后到前的绘制顺序保存。不要在近义后缀之间随机改名。
- 输出前递归累加祖先 `x/y` 重建每个节点的绝对矩形，与原图复核边缘、中心和间距。`Auto` 锚点保证构建后的初始矩形等于 Schema 矩形，不会自动修正测量误差。

示例：父节点的绝对矩形是 `[100,200,900,800]`，子节点是 `[140,260,340,320]`。父节点尺寸为 `800x600`，子节点局部矩形为 `x=40,y=60,width=200,height=60`。

视觉层级不是可选的整理步骤。具有明确边界的 Header、Plate、Field、StatBackground、Button、Toggle 或局部 Panel 应拥有完全位于其内部的文字、图标和值；多个候选包含同一节点时选择面积最小且先绘制的最近视觉承载节点。不得把 `GuildNamePlate` 与 `GuildName`、`LevelField` 与 `LevelLabel/LevelValue` 平铺为同一父节点的兄弟。C# 会在生成前按“先绘制 + 完整包含 + 最小面积”对明显的平铺结果做保守归一，但 Schema 输出仍必须直接表达正确 `children`，并保持归一前后的累计绝对矩形不变。全屏 DimOverlay、遮罩和纯过渡层不得被推断为业务内容父节点。

模态弹窗截图只描述弹窗呈现层，不描述被遮罩覆盖的宿主页面。存在覆盖画布主要区域的半透明 Overlay/Dimmer/Scrim，且其上方有边界独立、完整包含于遮罩范围的 Popup/Dialog/Modal 时，根 `children` 只保留遮罩和弹窗前景；地图、导航、货币栏、HUD、入口按钮和列表等遮罩下宿主页面节点全部省略。若二者原先嵌在宿主容器中，提升到根时用祖先累计坐标重写 `x/y`，保持完整画布绝对位置。C# 仅在遮罩面积、同父绘制顺序、几何包含和模态身份同时成立时做保守兜底，不按 Panel 名或业务名强删普通页面。

示例：在 1080x1920 的父节点中，一个左上角位于 `(820, 1780)`、尺寸为 220x80 的按钮。默认字段全部省略：

```json
{"name":"Confirm","type":"Button","x":820,"y":1780,"width":220,"height":80,"text":"确定","bold":true,"sliced":true}
```

## 资源和占位行为

高精度生成结果必须显式区分资源结论。`Verified` 要求 `resource` 是项目中真实存在、并已同时对照效果图和 Sprite 外观确认的精确 `Assets/...` 路径，Builder 将其锁定；`Candidate` 使用有序 `resourceCandidates`，这些路径必须进入该节点的完整像素精排，但不会绕过阈值，第一项仅有有限决胜加权，后续项依次降低；`ColorFallback` 会清空资源字段、强制 `intentionalColor=true` 并跳过 Sprite 匹配，只适用于已经证明无纹理、无边框和无透明转角的纯色；`Auto` 不声明资源结论，继续在完整索引中检索。视觉模式若拒绝全部候选，Prefab 必须保留色块并报告审计风险，禁止在组件创建阶段直接取候选第一项。

候选 Sprite 的尺寸和长宽比必须用 `TextureImporter` 源文件尺寸校正 `Sprite.rect`；源图被 `maxTextureSize` 缩小时，按源纹理宽高与导入纹理宽高的比例还原每个 Sprite Rect，不能把导入后的 2048 像素误当作美术原始宽度。

名称/语义模式的解析顺序：

1. `resource` 中的明确资源路径；
2. `resourceCandidates` 中的精确/强匹配；
3. `semantic` 和 `name` 的强匹配；
4. 彩色占位。

本地效果图的视觉模式不依赖文件名：C# 先按节点绝对矩形取得效果图区域，再把候选 Sprite 渲染为小型描述图比较颜色、结构和边缘。Schema 对可辨认的 Image/Button/Toggle 应给出 `background/panel/header/bar/field/icon/emblem/badge/crest/flag/banner/avatar/portrait/overlay` 等通用 `semantic`，只用于候选类别分组与有限决胜，不得携带猜测的项目路径、文件名或业务值。即使节点被 AI 标成 `intentionalColor`，视觉模式也先尝试可信 Sprite，失败后才保留纯色。背景节点的描述图排除后绘制兄弟 Image 和子控件的硬遮挡；文字排版框不得整块排除，字形污染应由鲁棒离群点处理。半透明候选先反预乘 Alpha，再与节点周边采样的参考底色合成；近中性灰白候选允许拟合 `Image.color` 染色并对大幅变换增加代价。只有原色候选的视觉分和平均颜色都足够接近时，才在染色候选领先不足时优先；没有颜色接近的强原色候选时保留结构更匹配的染色九宫格。节点 `semantic/name` 与资源类别同义词仅在视觉候选已经接近时作有限决胜，类别加分不得挽救像素外观明显错误的候选。带 Sprite Border 的候选必须按节点目标宽高模拟九宫格后再比较；背景、面板、条和字段等可拉伸表面不以源尺寸或原始长宽比淘汰，但 icon/emblem/badge/crest/flag/avatar 等轮廓敏感图形即使带 Border 也保持原始长宽比约束，避免细长条缩放后冒充徽标。命中后自动使用 `Image.Type.Sliced`。相邻且同宽或同高的无资源背景片段，默认只有联合矩形高置信命中带 Border 的 Sprite 才自动合并；若两个普通片段连续、联合区域覆盖父级主要宽度、候选原始尺寸接近父级全宽且整幅参考区域高置信，C# 可把它们还原为一个父级宽背景并保持全部子节点累计绝对坐标。节点明确写 `mayMerge:true` 时可以考虑其他普通 Sprite 联合，但还必须满足原始尺寸和长宽比约束，并使用更高的绝对分与领先幅度。非视觉 Container 下若遗漏了截图中真实存在的文字承载条，C# 可对包围同组 Text 的矩形尝试九宫格候选；只有高绝对分且稳定正向领先时才补建 Image 并将文字归入 children。同级中语义和目标尺寸一致、且只承载文字的多个表面，如果部分节点已经高置信命中同一 Sprite，未命中节点可只在自身参考区域再次验证这些同级已确认候选，通过独立门槛后补全；不能无条件复制。低置信候选不得触发结构改写。

半透明候选的参考底色是与描述图同尺寸的逐点数组，不是单个平均色。首选最近已匹配视觉祖先在当前子矩形内的合成结果；该结果必须包含祖先九宫格缩放、`Image.color`、Sprite Alpha 和祖先自身底色。没有祖先证据时才使用节点外环，并排除所有同级 Graphic/Text 矩形后按相同横向或纵向位置采样。原色半透明候选使用合成颜色、Alpha 结构以及 `效果图像素 - 逐点底色` 的前景方向/强度共同评分，原始未合成颜色不得覆盖去背景结论。同构表面的全成员复核复用同一背景模型。

不透明、无子节点的 `intentionalColor` 仅在效果图内部区域颜色范围极小且均值接近声明色时跳过 Sprite 匹配；渐变、纹理、边框和透明边缘任一存在都不属于纯色例外。视觉日志应记录逐点底色来自已匹配祖先还是排除同级后的外环。

当多个背景 Image 无缝铺满一个局部 Container、而该 Container 只占直接父级的一部分时，C# 可在整幅父级矩形上验证原尺寸相近的普通 Sprite；高置信命中后只保留一个父级完整背景，并把各片段 children 以累计绝对坐标不变的方式迁入。紧凑图标可组合直接父级的通用语义并在父级范围内用固定外扩矩形补采样，但仍须通过图标专用像素门槛。文字软遮挡的离群点剔除保护外侧采样带。同级同语义、近似等宽高、只含文字的表面若已经自动命中不同资源，则对组内候选执行全成员像素评分；只有组均值达到门槛且稳定领先时才纠正自动赋值，显式资源和局部强变体保持不变。

视觉置信门槛在通用类别和轮廓约束过滤候选之后，按无遮挡、文字软遮挡和大面积硬遮挡分段；不得通过全局降低 margin 修补错误类别之间的竞争。第一名绝对颜色/结构/边缘分足够高且在有效样本中仍稳定领先时，不应被所有节点共用的固定 margin 误杀。近方形、无文字的小型图标或徽标额外要求更高的结构/轮廓分，避免低分纯色横条和普通面板缩放后误命中；没有可信候选时保留色块。完成父 Sprite 匹配后，构建器只对明确写了 `mayMerge:true` 的纯文字承载子 Image 继续做父图局部复现校验；通过时才折叠并保持后代累计绝对矩形。另有一种不删除层级的保护：带 children 的 `Panel/Section/Content/Header/Footer` 布局 Image 在遮掉全部后代后，如果父 Sprite 对该局部的像素解释显著优于其独立 Sprite，则只将它转成 Container，并保持名称、矩形与 children 不变。`Field/Bar/Plate/Badge/Flag/Icon/Tile/Cell/Card/Row` 等独立视觉承载节点不参与这种转换。字段底、名字条、分数条默认保留，不能因为父节点误匹配了高分 Sprite 而被删除。该保护不依据具体业务资源名。

视觉模式的资源索引和短名单必须稳定排序。搜索根、资源路径、子 Sprite 名和 Sprite Rect 不允许使用未定义枚举顺序；快速分相同的候选按资源路径和子 Sprite 名固定决胜，因此同一 Schema 与同一资源版本应产生相同命中结果。

九宫格候选仍允许自由拉伸，但接近分之间使用非拉伸轴的源尺寸作为有上限的弱证据：横向表面比较源高度，纵向表面比较源宽度，近方形表面保留源长宽比；目标尺寸小于两侧 Border 之和、会把边框带挤压到重叠时额外降权。语义 token 加分必须小于能够推翻明显像素差异的幅度，不能让包含 `icon/crest/badge` 的文件名压过数字命名但轮廓与纹理更吻合的资源。

父级普通 Sprite 的源尺寸与父节点几乎一致且整幅匹配已验证时，C# 可补救 AI 漏写 `mayMerge`：仅对非显式资源、只含 Text 后代的子 Image，在遮掉文字后父图局部达到近乎完全复现并显著优于子候选时折叠。父级九宫格、源尺寸与父节点差异明显、含独立图形后代或局部证据不足时禁止推断。

半透明大范围调暗层如果已经写明 `intentionalColor`，面积至少覆盖画布四分之一，且 name/semantic 为 Overlay、Dimmer、DimLayer、Scrim 或遮罩，则跳过 Sprite 匹配并保留纯色，避免遮罩误命中普通面板资源。

占位颜色用于语义诊断：

- 背景：灰色；
- 按钮：绿色；
- 头像/肖像：紫色；
- Icon：黄色；
- 普通图片：蓝色；
- 未知图片类节点：红色。

缺失节点只记录到生成日志并保留诊断色块，不得创建 `PlaceholderLabel` 或显示 `[MISSING]` 文字。如果本来就需要纯色填充，设置 `intentionalColor: true` 并提供 `color`。

本地效果图的资源字段通常留空，由视觉模式在所选父目录及全部子目录中匹配现有 Sprite。旧 Schema 中的 `reference://crop` 只作兼容输入并回到视觉匹配或色块兜底；构建器不得创建 `ReferenceGenerated`。可编辑文字必须使用可见的 TMP `Text`，不得使用整图加 Alpha=0 组件的方式。

资源相关结构允许在本地匹配后做一次受证据约束的校正。普通运行时文字保持 `textMode: Auto`；明确必须始终可编辑的文字写 `Editable`。书法、Logo、描边或发光标题先以 Text 记录内容和矩形，并写 `textMode: ArtText`、`visualKind: ArtText`，疑似多层时写 `mayLayer: true`；C# 会在原矩形及小幅外扩矩形上用透明轮廓、结构和原始尺寸共同匹配，达到艺术字专用高置信门槛后才转换为一层或多层 Image。排名、短数字或标签疑似已画进父 Sprite 时写 `PossiblyBaked`；只有父 Sprite 对文字局部也能高置信复现时才抑制 Text。显式 `PossiblyBaked` 与仅凭徽章/排名语义推断的短文字使用不同门槛：前者仍要求稳定的父图局部像素证据，但允许比后者略低；后者保持更保守，避免删除动态文字。连续区域可能被误拆时写 `mayMerge`，疑似整幅背景时写 `mayUseFullCanvasSprite`。这些字段不得携带资源名，也不能绕过像素验证。

`Button` 设置 `text` 后，构建器会添加一个不参与绑定的白色 `Label` 子节点。如果 Label 的矩形、文字颜色或样式需要不同，改为添加显式 `Text` 子节点；不要同时使用两种方式。Button 的 `color` 属于背景 Image，不属于自动 Label。

`Toggle` 的图片、文字与 `Button` 相同，但会生成 Unity `Toggle`，绑定名使用 `tgl_`。互斥选项必须置于 `ToggleGroup` 节点之下；构建器会把该组后代中最近归属于它的 Toggle 自动关联到组，并在未指定 `isOn` 且不允许全部取消时默认选中第一个。嵌套 ToggleGroup 各自管理自己的选项。

```json
{"name":"TypeTabBar","type":"ToggleGroup","x":350,"y":130,"width":960,"height":70,"children":[{"name":"NormalAttack","type":"Toggle","x":20,"y":12,"width":288,"height":42,"text":"普通攻击","isOn":true},{"name":"Ultimate","type":"Toggle","x":354,"y":12,"width":278,"height":42,"text":"终结技"}]}
```

## 重复模板

业务数据驱动的列表只定义一个无编号模板，并保持 `repeatCount` 默认值 1。不要使用 `repeatCount` 复制 Card、Row 或 ListItem；运行时代码负责实例化。例如 `CityCard01~04` 只输出 `CityCard`，`DefenseRow01~04` 只输出 `DefenseRow`。

还原效果图与独立 Sprite 边界优先于节点去重。生成 Schema 前先分析完整效果图中的所有实例，用它们校验第一项的原始绝对矩形、资源切层、绘制顺序与父子关系；省略后续实例时不得重新测量、补位、回流、拉伸、缩放、改名、改父子关系或合并资源承载节点，也不得把后续实例所在像素计入弹窗或相邻背景边界。

没有数字后缀的同构业务面板在最终 Prefab 中也只保留一个模板。相邻 Panel/Card/Row/Item 仅由颜色、阵营、队伍或状态前缀区分，且类型、尺寸、子树层级和局部矩形一致时：如果其独立视觉资源完全一致，Schema 直接只写第一个；如果选择依赖项目 Sprite，Schema 完整保留候选，并在根节点写同一 `runtimeTemplateGroup` 与不同 `runtimeTemplateVariant`，例如红/绿/蓝候选分别写 `FactionPanel` 与 `red/green/blue`。C# 先完成全部候选的视觉资源匹配，再按变体资源一致性、资源完整度、视觉分和原始顺序只留下一个。不得为候选写 `repeatCount`、偏移或 `variants`。不确定是否同构时不要写候选组；简单按钮、Toggle、左右固定面板、结构不同或明确要求全部常驻的节点不能折叠。

生成器可对 Codex 漏标做严格兜底，但只能添加候选标记，不能在匹配前删除：同一父级兄弟根必须是 Container/Image，根名末尾为 Card/Item/Row/Cell/Entry，或为带 Faction/Team/Guild/Player 等明确业务身份的 Panel；名称只允许一个颜色/阵营 token 不同；每棵子树至少三个节点，且节点类型、顺序、局部 x/y/width/height、字体和交互属性逐项同构。Left/Right/Top/Bottom 等布局差异、Button/Toggle、简单视觉状态和固定常驻区域不推断。全部候选完成资源解析后才按普通 `runtimeTemplateGroup` 规则保留一个。

裁剪前，C# 按同构子树的稳定位置汇总通用资源证据。不含任何候选变体 token 的 Sprite 只有在多个候选区域分别通过局部像素门槛、组均值稳定领先时，才可补给未命中成员；已经命中本变体 token 的 Header、Flag、Emblem 等槽位不参加跨变体覆盖。这样保留项维持自身阵营身份，同时继承其他候选已验证的通用字段底和面板资源。

```json
{"name":"RedFactionPanel","type":"Container","runtimeTemplateGroup":"FactionPanel","runtimeTemplateVariant":"red","x":120,"y":300,"width":600,"height":400,"children":[...]}
```

`repeatCount` 仅表示数量固定且必须全部常驻 Prefab 的设计期重复。此时模板及子节点名称不带编号；构建器展开时自动追加 `01`、`02`，子节点可按实例索引覆盖少量差异：

```json
{"name":"Row","type":"Container","width":900,"height":100,"repeatCount":5,"repeatOffsetY":108,"children":[{"name":"Result","type":"Text","x":300,"width":120,"height":50,"text":"胜利","variants":[{"index":4,"text":"失败","color":"#FF5544FF"},{"index":5,"text":"失败","color":"#FF5544FF"}]}]}
```

生成窗口还会识别旧 Schema 中连续的 `Row01`、`Row02` 等同构节点，自动合并并以精简 JSON 保存。
