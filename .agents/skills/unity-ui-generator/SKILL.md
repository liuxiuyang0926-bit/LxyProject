---
name: unity-ui-generator
description: 根据截图、本地图片、Figma Frame/Component 节点链接或 UISchema，分析、生成、审查或调试可移植 Unity UGUI Prefab。适用于独立效果图生成包、LxyDemo 项目适配器、FigmaNodeUrl 导入、重复 UI 模板、Sprite 自动匹配、缺失资源占位和生成器维护。
---

# Unity UI 生成器

支持两条明确分离的流水线：轻量模式由 Codex 只输出结构、Unity C# 确定性构建；高精度模式先用现有单轮分析和 Builder 快速创建 Prefab，再让 Codex 通过用户已经配置的 UnityMCP 只审查当前 Prefab、实际 Image/Sprite、层级和渲染结果。UnityMCP 会话不直接写 Prefab，而是返回完整修正版 UISchema；Unity 校验身份、尺寸和效果图哈希后写回，再由同一 Builder 重建。发生修改时最多追加一次重建后验证，总计最多两轮，完整工作流 Token 上限为 300,000。

## 按任务读取参考

- Unity Editor 对本地效果图的后台提示包含“低 Token 模式”时：只读 [compact-blueprint.md](references/compact-blueprint.md)。不要搜索项目资源、读取其他参考、修改文件或调用 Unity。
- Unity Editor 后台提示包含“第一阶段结构分析器”时：提示本身已包含完整输出契约，只使用附件效果图返回无资源路径的结构草案 `schemaJson`；不要读取 Skill/参考、搜索文件、写项目或调用任何 MCP/工具。
- Unity Editor 后台提示包含“第二阶段资源与结构校正器”时：只使用附件效果图、节点专属 Sprite 联系图、原始分辨率节点裁片、manifest 和内嵌草案返回最终 `schemaJson`；候选是证据而不是锁定项，可据其源尺寸、Border 和透明轮廓合并/拆分视觉层、修正 Image/Container 与父子层级。`global-layer-geometry/global-outline-geometry` 是从全库按独立层几何补召回的证据，节点裁片不是资源。不要读取 Skill/参考、搜索项目、调用 MCP 或写项目。
- Unity Editor 后台提示包含“定向差异修复器”时：只修本地审计列出的资源风险及 Prefab Image-only 预览中明确可见的最小差异，只返回提示白名单中的 `targetPath + replacementNodeJson` 节点补丁，不返回完整 UISchema，不重做正确区域，不调用 UnityMCP；不得因预览故意不渲染 Text 而删除或改动文字。补丁替换的是完整子树，除非证据明确要求合并，必须保留现有正确 children；Unity 最多接受两轮且每轮只保留量化改善的版本。
- Unity Editor 后台提示包含“Unity UGUI Prefab 的定向视觉修复器”时：必须使用已连接的 UnityMCP 选择提示指定的项目，只检查指定 Prefab，并至少查看其真实层级、Image Sprite 与渲染/预览结果后再判断。不得枚举完整项目或完整 Sprite 库；每个问题节点只深入检查少量候选。不直接写项目文件，最终返回完整修正版 `schemaJson`，交给 Unity 校验和重建；当前结果已经一致时返回 `changed=false`。
- 只有用户明确要求在 Codex 终端中通过 UnityMCP 手工直建 Prefab 时，才读 [direct-unity-mcp.md](references/direct-unity-mcp.md)、[ui-schema.md](references/ui-schema.md) 与 [project-ui-rules.md](references/project-ui-rules.md)。
- 在 Codex 终端手工创建或审查 Schema 时：读 [ui-schema.md](references/ui-schema.md)。需要生成 Prefab 时再读 [project-ui-rules.md](references/project-ui-rules.md)。
- 修改生成器 C#、资源解析或 Prefab 流水线时：同时读上述两个完整参考，并使用 `unity-project-development`。

## 职责边界

轻量模式的 Codex 只负责：

- 从效果图识别区域、父子层级、控件类型、可见文字，并以原图像素量取矩形；
- 先判断效果图表示完整页面还是覆盖在宿主页面上的模态弹窗；检测到大面积调暗遮罩和独立弹窗前景时，只输出遮罩与弹窗自身子树，遮罩下透出的地图、导航、货币栏、HUD、入口按钮和列表都属于宿主页面，不进入弹窗 UISchema；
- 先基于完整效果图识别全部可见实例并确定几何、资源边界与父子层级，再判断重复内容是运行时数据列表还是设计期固定重复；普通运行时列表在 Schema 中只输出一个无编号模板；若同构颜色/阵营/状态候选的正确保留项依赖项目 Sprite，则用 `runtimeTemplateGroup/runtimeTemplateVariant` 暂存所有候选，交给 C# 匹配后裁成一个；
- 标记纯色块和无法确认的交互。

高精度模式的 Codex 还负责：

- 第一阶段完整量取几何、独立视觉边界与父子所有权，不猜资源路径；第二阶段对照效果图和 C# 逐节点检索的 Sprite 联系图，结合源尺寸、九宫格 Border、透明轮廓、颜色及上下文确认资源并反向校正结构；
- 用 `resourcePolicy` 明确资源结论：效果图与项目 Sprite 均已目视确认时可请求 `Verified + resource`，但 Unity 仅在同一路径也是本地独立高分、高领先、无结构风险赢家时锁定，否则自动降级为 `Candidate`；仍有歧义时使用 `Candidate + resourceCandidates` 有序 Top-N；已确认无纹理纯色且候选均不匹配时才使用 `ColorFallback`；其余使用 `Auto`。第二阶段不写项目；定向修复最多两次且只读。

轻量模式的 Unity C# 负责：

- 当来源为 `FigmaNodeUrl` 时，通过后台 Codex 复用当前已授权的官方 Figma MCP；只请求 `get_metadata`，并用 `download_assets` 获取根节点 export render 的临时 URL，不调用大段代码型 `get_design_context`，不使用 REST 或 Personal Access Token；
- 验证 MCP 返回的精简 UISchema，按 `figma://crop` 节点矩形从 Frame 截图裁切本地 Sprite；不得把临时 URL、OAuth 凭据或 base64 写入仓库；
- 验证本地效果图返回的精简 UISchema，在用户选定的资源总父目录中递归索引 Sprite，并以节点绝对矩形做本地视觉匹配；可辨认的视觉节点应提供与资源名无关的通用 `semantic` 类别供候选分组，匹配失败保留纯色兜底，不创建 `ReferenceGenerated` 裁切资源；
- 下载/导入图片，使用稳定资源路径并注入原图路径、尺寸和内容哈希；同一 Panel、路径、尺寸和图片哈希一致时默认复用已审核 UISchema，不重新调用 AI；
- 推导 `Auto` 锚点，展开 Schema 明确声明的固定设计期重复并验证 Schema；不得在资源匹配前自动删除、合并或改写同构业务模板；
- 扫描 Sprite、匹配资源、创建缺失占位；
- 编辑器窗口每次打开时同步刷新并重建当前资源根索引，Sprite 导入、删除或移动时自动使索引与描述缓存失效；
- 在高置信像素证据下执行资源感知结构校正：从局部可见矩形恢复父级完整背景、相邻区域联合、缺失文字承载背景补建、同构同级视觉的已验证资源补全、艺术字单层/多层 Sprite 转换、父 Sprite 已烘焙文字抑制，以及资源相关运行时模板候选的确定性裁剪；校正必须保持其他节点累计绝对坐标并稳定排序；
- 对后台偶发漏标的运行时模板做严格、确定性的候选标注：只处理同一父级下名称仅一个颜色/阵营变体 token 不同、根类型为 Container/Image、根语义为 Card/Item/Row/Cell/Entry 或明确业务 Panel、至少包含三个节点，且整棵子树类型、顺序、局部矩形与非视觉属性同构的兄弟节点；标注阶段不删除节点，全部候选完成资源匹配后才裁成一个。左右/上下布局 token、Button、Toggle、简单样式状态、结构不同或固定常驻面板永不自动标注；
- 创建组件、计算 ScrollRect Content、保存 Prefab；
- 通过 `UIEffectProjectAdapterRegistry.Active` 创建 Prefab 外壳并刷新项目集成；LxyDemo 的适配器必须继续复用 `CSharpUIGenerator` 的 Canvas、Binder 和脚本流水线，独立包默认使用不依赖业务框架的通用 UGUI 适配器；
- 压缩 Schema，并显示本次 Codex Token。

高精度模式的 Unity C# 额外负责：

- 第一阶段只发送高清效果图并取得结构草案；随后用真实节点矩形调用与最终 Builder 相同的全库像素评分，组合精排、快速外观、源尺寸几何、遗漏整图 Container 探测、已命中目录、稳定文件族变体，以及不依赖草案整块裁剪的全库独立层几何召回，生成节点专属联系图、原始分辨率节点裁片以及精确路径、源尺寸和 Border manifest。候选不得按文件顺序或固定目录前 N 截取；高置信节点仍在 manifest 中但只展示当前最优图，低领先、轮廓敏感、大背景、Container Probe、合并、多层和整图风险节点展示更多局部候选；
- 把本地初步命中的资源与候选交给第二阶段反向校正：候选完整轮廓跨越多个草案 Image 时允许合并；Container 探测命中完整底图时补建/转换 Image；草案 Image 没有独立边界且已烘焙在父图时转为 Container。所有结构改写保持后代累计绝对坐标；
- 保存每个草案节点经完整资源库检索得到的有序 Top-N，并合并回第二阶段结果；`Candidate` 只参与完整像素精排，第一候选获得有限加权，后续候选递减且全库未展示项仍可凭更强视觉证据胜出。视觉评分拒绝候选后不得在构建阶段直接取第一项伪装成成功；
- `Verified` 只有同时满足 AI 请求和 Unity 本地独立高置信证据才作为精确锁定；不一致时保留路径为 `Candidate` 并继续全库验证。`ColorFallback` 明确跳过资源检索，`Auto/Candidate` 继续由 Builder 全库验证；
- 注入参考图路径与哈希、执行通用层级/模板归一并调用项目 Builder。生成后本地汇总未命中、低绝对分、低领先幅度和逐节点 Image-only（含边缘带）差异风险；无风险立即结束，有风险时生成真实 RectTransform/Image/九宫格的 Image-only 预览、原始分辨率风险参考裁切联系图与风险候选联系图，最多启动两次白名单节点补丁。每轮由同一 Builder 重建并重新量化，改善才接受，恶化自动恢复最佳 Schema/Prefab；
- Prefab 生成后的定向修复必须加载 UnityMCP，但只允许访问当前项目实例和目标 Prefab；禁止全项目枚举、无关场景读取和全库资源转储。会话返回完整 UISchema，不直接保存 Prefab；Unity 必须复核 Panel ID、设计尺寸、参考图哈希及 Schema 合法性后才能写回并重建。第一轮发生修改时允许再启动一次验证，最多两轮；使用 `high`，累计达到 235,000 Token 后不得启动新轮次，完整流程不超过 300,000。
- 对“遮罩 + 独立 Popup/Dialog/Modal 前景”的强证据执行模态前景提取：把遮罩和弹窗按完整画布绝对坐标提升到 Schema 根，排除所有被覆盖的宿主页面节点；没有同时满足遮罩、前景、绘制顺序和几何包含关系时不触发。

轻量模式不要让 Codex 枚举资源目录、猜 `Assets/...` 路径、输出默认字段、重复抄写相同节点或直接逐个创建 GameObject。高精度模式不得仅凭文件名猜资源；第二阶段和定向修复只使用 Unity 导出的附件、manifest、草案和审计证据，按照 `Verified/Candidate/ColorFallback/Auto` 提交结论，随后由现有 Builder 完成最终全量本地验证，不得绕开已注册的项目适配器。LxyDemo 中不得绕开其 Canvas/Binder 流水线。

## FigmaNodeUrl

在 `工具/UI工具/根据效果图生成Prefab` 中选择 `FigmaNodeUrl`，粘贴 `/design/`、`/file/` 或 `/proto/` 链接；链接必须包含 `node-id`。使用前只需在 Codex 中连接并授权官方 Figma MCP；Unity 窗口不提供、读取或保存 Personal Access Token。

点击“读取 Figma 节点并生成 Prefab”后，后台 Codex 只代理 MCP 的 `get_metadata`/`download_assets` 读取并返回精简结构与根节点导出地址；截图由 Node HTTPS 下载，C# 完成 Sprite 裁切、UISchema 校验和 Prefab 生成。`download_assets` 只选择根节点的 export render，不使用 raw source image URL。生成资源固定放在：

- `Assets/Editor/UIReferences/<Panel>_Figma.png`：只用于审查；
- `Assets/Editor/UISchemas/<Panel>.json`：可审查中间格式；
- `Assets/GameResources/UIAtlas/FigmaGenerated/<Panel>/`：Prefab 使用的运行时 Sprite。

检查生成日志中的兼容性提示。混合字符样式、旋转文字或同时包含复杂背景与可编辑文字的节点可能需要人工复核；不要静默声称完全等价。

## 视觉分析规则

先识别 `Panel > Header / Content / Footer` 等区域，再放置 Image、Text、Button、Toggle、ToggleGroup 和 ScrollRect。使用能解释效果图的最小层级；不要推断图片没有展示的动画、安全区、Mask 或业务交互。

模态弹窗截图的生成范围只包括弹窗自己的呈现层。大面积半透明 Overlay/Dimmer/Scrim 与其上方独立 Popup/Dialog/Modal 同时出现时，保留遮罩、完整弹窗背景及其 Header/Content/Footer、文字、图标和交互；遮罩下仍能看见的宿主页面背景、地图、导航、货币栏、排行榜、入口按钮、聊天/HUD 等全部排除。若遮罩和弹窗原本被分析为某个宿主容器的 children，将二者提升到 UISchema 根并用累计绝对坐标重写 `x/y`，不得因移除底层界面而移动或缩放弹窗。不要用目标 Prefab 名或具体业务组件名判断；没有大面积遮罩或没有独立模态前景时按普通全屏界面处理。

效果图生成 `UITeHui` 及同类 UI 时，默认排除由运行时代码统一创建的通用组件：

- 不输出名为 `TopTabGroup` 的节点及其整棵子树；顶部通用页签由代码动态生成。
- 不输出位于画布左上角、语义明确为返回的 Button、图标、文字、点击区域或其包装节点；返回组件由通用代码动态生成。
- 只有用户明确要求将上述组件固化进 Prefab 时才保留。过滤后仍以完整效果图为坐标系，其他节点不得向上、向左补位，也不得留下透明占位节点。

交互类型必须按行为语义区分：确认、领取、返回等独立动作使用 `Button`；页签、分类、模式、筛选等同组互斥选项使用 `Toggle`，并放到一个具有可测量矩形的 `ToggleGroup` 父节点下。视觉上明确选中的项写 `isOn: true`；默认组不允许全部取消，只有效果图或需求明确允许时才写 `allowSwitchOff: true`。不要仅因 Figma 节点名包含 Button 或当前帧只有一个选中态，就把互斥页签近似为 Button。

尺寸和位置必须使用提示中的源文件原始宽高作为唯一坐标系，不得使用 Unity 因 `maxTextureSize` 缩小后的导入预览尺寸，不得套用 1080x1920 等常用屏幕尺寸或凭比例粗估。附件预览被缩放时，所有测量值必须按比例换算回源文件坐标。画布原点始终是完整图片最外层左上角，不能裁掉留白或以首个可见控件重新定原点。先量取每个节点相对整张图的绝对边界，再用 `child.x = child.left - parent.left`、`child.y = child.top - parent.top` 转成父节点局部坐标。只有边界可从图中量取的区域才能作为父节点；不能确定原点的语义分组应省略，避免嵌套偏移累积。

输出前递归累加所有祖先的 `x/y` 重建节点绝对矩形，核对主区域边缘、居中/对称关系、等宽高和重复间距。`Auto` 锚点只保留 Schema 的初始矩形，不会纠正错误测量。完整量取和自检见 [compact-blueprint.md](references/compact-blueprint.md) 与 [ui-schema.md](references/ui-schema.md)。

相同视觉结构使用稳定的规范化名称和顺序：不要在 `Plate`、`Field`、`Panel`、`Backdrop` 等近义后缀间随机切换；所有测量矩形按最近整数像素取整，兄弟节点保持从后到前的绘制顺序。只有效果图内容确实变化或用户明确关闭“同图复用现有 Schema”时才重新分析；重新生成 Prefab 本身不得改写 Schema。

运行时数据列表只定义一个模板节点，名称去掉 `01/02/...`，不写 `repeatCount`、`repeatOffset` 或 `variants`。例如效果图中的 `CityCard01~04` 只输出 `CityCard`，`DefenseRow01~04` 只输出 `DefenseRow`；运行时代码负责循环实例化。只有数量固定且全部必须随 Prefab 常驻的设计期内容（例如固定页签或装饰点）才使用 `repeatCount`。

还原效果图、识别独立 Sprite 边界和建立正确父子层级始终优先于模板去重。必须先在完整效果图上分析所有实例，用它们互相校验第一项的真实边界与内部切层；随后仅在最终 Schema 输出阶段省略运行时会动态创建的后续实例。省略实例不得触发重新测量、补位、回流、拉伸、缩放、改名、改父子关系或合并资源承载节点；保留模板必须维持完整效果图中的原始绝对矩形、绘制顺序和完整内部层级，被省略实例所在像素也不得并入弹窗或相邻背景的资源边界。

运行时模板不一定带数字。相邻业务面板仅由颜色、阵营、队伍或状态前缀区分，且类型、尺寸、子树层级和局部矩形同构时，最终 Prefab 只保留一个节点，不使用 `repeatCount` 或 `variants`。如果各实例的独立视觉资源完全一致，Schema 可直接只写第一个；如果正确保留项依赖项目 Sprite，Codex 不搜索资源，而是完整输出候选，在各候选根节点写相同 `runtimeTemplateGroup` 和各自简短稳定的 `runtimeTemplateVariant`（例如 `red/green/blue`）。C# 必须先匹配所有候选，再按变体与已命中资源 token 的一致性、命中资源完整度、视觉分和原始绘制顺序确定性地保留一个。保留项维持自己的原名、原始绝对矩形、绘制顺序和内部视觉，不得把另一实例的颜色或资源套到它的位置。例如红色候选的旗帜与徽标资源证据最完整时保留原位置的 `RedFactionPanel`。不确定是否真正同构时不要标候选组。简单颜色按钮、Toggle、左右固定面板、结构不同的面板以及明确要求全部常驻的节点不适用此规则。

同构 Card/Item/Row/Cell/业务 Panel 必须在第一次 Builder 调用前完成识别与标记。若 Codex 漏写显式组，Builder 内的 C# 严格兜底按上一段条件在内存中补写候选标记，并在首次资源匹配后裁剪；自动兜底不得扩大父背景、重测坐标或改变保留模板的资源与 children。Unity 窗口可在后台任务返回后做快速数量断言；只有本地资源或预览审计产生风险时，最多两次定向修复才可替换白名单中的最小 Schema 节点或必要父子树。每次重建后必须量化验收，未改善时回滚，达到轮次或 Token 上限后停止并明确报告是否通过严格验收。

先判断一个可见区域是独立资源、父资源的一部分，还是多层资源合成。父级 Sprite 已经包含页签底、关闭图标、边框或装饰时，只在其上叠加真正独立的文字/交互，不得再创建同区域的 `RoundTab`、`CloseIcon` 等视觉子节点。旗帜、底章、徽标等边界明确且互相叠放的复合图形应拆成有 sibling 顺序的多个 Image；旗面与中央徽标轮廓可分辨时必须拆成两个 Image，不能把独立徽标当成旗面纹理省略。不要用一个大 Image 同时包住多层外观。

反向检查每个嵌套 Image 是否真的具有独立视觉边界。若某段底色、纹理、转角与父 Sprite 连续，且没有独立描边、接缝、透明轮廓或明显不同纹理，即使该区域上方有标题文字，也不得为了语义分组额外建立 `Plate`、`Backdrop`、`Header` 等 Image；把文字或交互直接挂到已经包含该视觉的父节点。文字排版框、语义名称或局部颜色变化本身都不能证明存在单独 Sprite。

资源边界必须按连续边框、底纹和转角判断，而不是按 `Header`、`Body`、`Content` 等语义名称切割。弹窗外框或底纹连续跨过标题区与内容区时，用一个覆盖完整外框、排在所有内容之前的背景 Image；不得拆成 `DialogHeader`/`DialogBody` 色块。若同一张连续底图横跨直接父节点全宽，但内容只占中间区域，背景 Image 仍按完整资源边界覆盖父级；标题区、内容区等语义分组改用无 Graphic 的 Container 并作为背景 Image 的子节点。只有可见独立接缝、完整转角或不同重复纹理能证明是两张资源时才拆分。卡片背景即使被徽标、文字和面板大面积遮挡，也保留覆盖完整可见边界的底图 Image。

视觉所有权与矩形精度同等重要，禁止为了保持绝对坐标而把一个区域内的背景、标题、标签和值全部平铺成同层节点。背景条、标题牌、字段底、统计格、按钮、页签或局部 Panel 只要有明确可见边界，就作为其内部文字、图标和字段的最近视觉父节点；在多个候选都包含子矩形时选择面积最小、先绘制且语义完整的承载节点。每一条在截图中具有可见边缘、纹理、渐变或色带的字段底、名字条和分数条都必须输出一个 Image，即使纹理很弱或被文字覆盖；同一条上的 Label、Value、Bonus 等文字全部放入该 Image.children，禁止直接平铺到非视觉 Container。子节点的 `x/y` 必须由绝对坐标减去该父节点绝对左上角重新计算。例如 `GuildName` 必须放入 `GuildNamePlate.children`，`LevelLabel` 和 `LevelValue` 必须放入 `LevelField.children`；统计格的 Label/Value 必须归入各自 StatBackground，而不是与所有背景同层。大范围 DimOverlay 不得成为业务面板父节点。输出前除递归复核绝对矩形外，还要逐个复核 Text、Icon 和交互节点的最近视觉父节点。

连续状态文字必须按效果图准确填写内容、颜色、字号、对齐和矩形。只有同一行确实存在不同颜色或运行时字段时才拆成多个 Text；拆分后的矩形不得互相覆盖，并要按字面顺序贴合。例如“布局期 剩余09:40:22 积分目标：20000”中的白色标签与黄色值可分段，但每段必须保持正确颜色和间距。

资源字段通常留空，让 C# 做本地视觉匹配。只有用户或仓库已明确给出真实路径时才填写 `resource`。对角色可辨认的 Image/Button/Toggle 填写简短稳定的通用 `semantic`，例如 `background/panel/header/bar/field/icon/emblem/badge/crest/flag/banner/avatar/portrait/overlay`；它只描述视觉类别，不得猜项目路径、资源文件名或业务数据。`intentionalColor` 只表示最终视觉确实是无纹理、无透明转角、无边框变化的纯色；本地视觉模式仍应先尝试匹配已有 Sprite，只有没有可信候选时才保留该色块。候选的原始尺寸、长宽比和整幅背景覆盖判断必须把 `Sprite.rect` 按 `TextureImporter` 源文件宽高与实际导入纹理宽高的比例还原到源像素；不得直接使用受 `maxTextureSize` 缩小的导入 Rect，否则完整大图会被尺寸过滤误杀。带 Sprite Border 的资源是九宫格候选：必须按节点目标矩形模拟 Sliced 后的外观比较；背景、面板、条和字段等可拉伸表面不能因源图片宽高、面积或长宽比不同而淘汰，但图标、徽标、徽章、旗帜、头像等轮廓敏感图形即使带 Border 也必须保留原始长宽比约束，禁止细长九宫格条竞争近方形徽标。命中后设置 `Image.Type.Sliced`。后绘制的 Image、Button、Toggle 和子控件使用实际矩形作硬遮挡；`Text` 的 RectTransform 只是排版范围，不能把整块背景判为不可见，必须保留背景采样并把字形像素作为软遮挡/离群点鲁棒排除。半透明候选从离屏预览读取后先反预乘 Alpha，再用节点周边采样的底色模拟最终合成；近中性灰白 Sprite 还要评估 `Image.color` 染色并在命中后写回颜色。只有原色候选同时具有高视觉分且与效果图平均颜色接近时，才可在染色蒙版领先不足时优先；若没有颜色接近的强原色候选，仍保留结构更吻合的染色九宫格，不能全局压制着色复用。视觉分接近时可以用节点 `semantic/name` 与资源类别同义词（如 icon/emblem/badge、flag/banner）作小幅决胜加分，但不能用名称覆盖明显相反的像素证据。如果 AI 把一个连续背景误拆成相邻、同宽或同高的 Image，除联合矩形匹配外，C# 还可在两个片段连续、联合区域覆盖父级主要宽度、候选原始尺寸接近父级全宽且整幅参考区域高置信时，将它们还原为一个普通 Sprite 背景并保持所有子节点绝对坐标。明确写了 `mayMerge:true` 时也可考虑其他普通 Sprite 联合，但必须同时满足原始尺寸、长宽比、更高绝对分和正向领先幅度。非视觉 Container 下若遗漏了截图中真实存在的文字承载条，C# 只在九宫格候选对包围文字组的区域达到高绝对分和正向领先时补建 Image，并把同组文字归入其 children。资源缺失只记录日志并显示色块，日志要包含最佳候选、视觉分数和领先幅度，不得向 Prefab 添加 `PlaceholderLabel` 或 `[MISSING]` 文字。可编辑文字继续使用可见的 TMP `Text`，交互节点的 Graphic 不得因存在参考底图而被设成透明。

高精度后台从有限联系图选择的路径仍是候选证据，由导入层转为 `resourceCandidates` 后针对节点进行全库验证，不能因为进入联系图就绕过像素精排。非视觉 Container 下准备补建文字承载背景时，如果同一父级中较早绘制的现有视觉节点已经完整包含该文字组，不得再生成重复背景。

半透明 Sprite 不能把整个目标区域的背景压缩成一个平均色。优先把最近一个已高置信命中的视觉祖先按其目标尺寸渲染，并将该祖先的 Sprite、Image.color、Alpha 与更外层背景逐采样点合成为当前节点的底色；跨过无 Graphic 的 Container 时仍沿用这个最近视觉祖先。没有可用祖先时才从节点四周逐点取样，同级 Image、Text 和交互区域必须从环形样本中排除，不能把相邻字段、按钮或文字颜色当成底色。对带 Alpha 变化且具有原始颜色的候选，最终排序使用逐点合成误差、边缘和去背景后的前景颜色方向/强度，不能再让未经合成的原图平均色覆盖该结论；文字覆盖短名单也要保留一小部分去背景证据。

`intentionalColor` 的不透明无子节点区域只有在效果图内部采样近乎完全均匀、且与声明颜色一致时才作为真正纯色直接保留；这包括纯黑留边或纯色填充。只要存在可见纹理、渐变、边框、透明转角或色带，仍进入 Sprite 匹配。这个判断必须来自像素一致性，不得依赖具体节点名。

若局部 Container 内的两个或多个相邻 Image 从左到右无缝覆盖该 Container，而其联合区域在直接父级的完整矩形上高置信命中一张原尺寸相近的普通 Sprite，应把这些片段连同 Container 一起还原为一个父级完整背景；所有片段原有 children 必须转入该背景，并以累计绝对矩形不变为约束重算局部坐标。该校正必须先处理子层再处理祖先，且只能由连续覆盖、背景语义、候选尺寸/长宽比和整幅截图像素共同触发，不能依赖 `TitlePanel`、`ContentPanel` 等具体名称。

近方形小图形匹配可以把当前节点的通用语义与直接父级的视觉语义组合为有上限的弱证据：例如 `emblem` 位于 `flag` 下时，旗帜类图标可获得额外决胜分；各语义证据可累加但不得跨过像素置信门槛。AI 量取的小图形边界略有裁切时，可在不超出直接父级的若干确定性外扩矩形上重新采样，仅用于寻找更完整的视觉证据，不得凭名称直接赋资源。文字软遮挡的鲁棒离群点剔除必须保护九宫格最外侧采样带，不能把窄边框、单侧色带或端帽当成字形噪声删除。

同一父级下语义、目标宽高和纯文字子树同构的多个字段必须作为一组复核。只有一个已验证候选时，未命中成员仍需在各自截图区域独立过门槛；已经自动命中多个相互冲突的候选时，应把这些候选分别放回组内每个区域评分，按稳定排序选择组均值明显更高的一项，并允许纠正近似但错误的自动赋值。局部像素显著支持不同变体或 Schema 显式指定了资源时不得强制统一。
同构字段的局部复核必须使用与首次匹配相同的祖先逐点底色和同级排除规则；不允许重新退回空遮挡、单一平均底色评分。日志应说明节点最终候选、分数、领先幅度、底色来源，以及冲突组因均值或领先不足而未统一的原因。
`runtimeTemplateGroup` 的候选还必须在裁剪前按相同子树位置复核通用资源：只传播不含任一变体 token、在多个实例区域均通过局部门槛且组均值稳定领先的 Sprite；Header、Flag、Emblem 等已经命中本变体 token 的槽位禁止跨变体覆盖。保留模板的阵营身份仍由变体资源亲和度决定，但不能丢失其他候选已经验证的通用背景、字段底和分数面板。

九宫格不按原始长宽比硬淘汰，但在像素分接近时必须保留“非拉伸厚度”的原始尺寸证据：横向 Bar/Field 以源高度接近目标高度为弱决胜，纵向表面以源宽度为弱决胜，近方形表面保留源长宽比的弱证据；目标宽高小于对应两侧 Border 之和、导致 Unity 挤压边框带时还要额外降权。所有尺寸证据都只能小幅影响排序，不能阻止真实九宫格拉伸。语义文件名加分同样只能是弱决胜，数字命名但像素明显更吻合的 Sprite 不得被包含 `icon/badge/crest` 的普通资源压过。

父级非九宫格 Sprite 的原始尺寸与父节点几乎一致、整幅匹配已经高置信时，如果一个非显式资源的子 Image 只承载文字，且遮掉文字后父 Sprite 对该局部近乎完全复现并明显优于子节点候选，可把漏写 `mayMerge` 视为结构分析遗漏并折叠。该兜底不得用于父级九宫格、尺寸差异明显的面板或包含独立图形的子节点。

Sprite 索引、视觉短名单和同分决胜必须确定化：搜索根、资源路径、子 Sprite 名称及矩形按序排序；快速分相同则按规范化资源路径和子 Sprite 名稳定选择。不得依赖 `AssetDatabase.FindAssets`、字典或文件系统的偶然枚举顺序。

明确标为 `intentionalColor`、覆盖至少四分之一画布、带半透明颜色且语义为 Overlay/Dimmer/Scrim/遮罩的大范围节点直接保留色块，不参与 Sprite 匹配；这类调暗层不能因平均颜色相近而误命中普通面板资源。其他 `intentionalColor` 视觉节点仍先尝试可信 Sprite，以容错 AI 把纹理误判为纯色。

置信判断在通用视觉类别和轮廓约束过滤候选之后进行，不能为了补救类别冲突而全局降低阈值，也不能只使用所有节点共用的固定领先幅度：无遮挡候选、文字软遮挡候选和仅剩局部边缘可见的候选分别使用分段阈值；当第一名具有足够高的绝对颜色/结构/边缘分，并在遮挡后的有效样本中仍稳定领先时，允许比普通节点更小但必须为正的领先幅度，避免把真实第一名误判为缺失。近方形且不含文字的小型图标/徽标必须满足更高的结构与轮廓分，不能让低分纯色横条或普通面板经缩放后冒充图标；没有可信图标时保留色块。匹配父 Sprite 后，仅当文字承载子 Image 明确写了 `mayMerge:true`，C# 才允许继续用父图局部复现证据折叠它并提升后代；字段底、名字条、分数条等具有独立边界的 Image 默认永久保留，不能仅因父节点暂时命中高分候选就删除。该校验不得按具体节点名或资源名写死。

禁止使用“整张效果图作为底图 + 其余组件 Alpha=0”的实现。它只能伪造静态预览，无法证明组件位置、尺寸、文字和 Sprite 正确。旧 Schema 的 `useReferenceImageAsVisual` 仅为兼容读取保留，新生成结果不得设置。

## 生成入口

- Unity 窗口：`工具/UI工具/根据效果图生成Prefab`
  - 本地效果图默认选择“快速生成 + UnityMCP 定向修复”：先按现有单轮流程和 Builder 生成 Prefab，再由 UnityMCP 审查真实生成结果；有修改时写回 Schema、重建并最多再验证一次；
  - “轻量 UISchema（旧流程）”保留低 Token 的纯结构分析与 C# 视觉匹配；
- Figma：窗口来源选择 `FigmaNodeUrl`，直接读取节点并生成 Prefab；
- 选中 Schema：`Assets/UI工具/根据选中的UISchema生成Prefab`
- 压缩旧 Schema：`Assets/UI工具/压缩选中的UISchema（低Token）`
- Editor API：`Lxy.UIEffectGenerator.Editor.UIEffectPrefabBuilder.GenerateFromSchemaPath(...)`

只有 Unity 实际保存 Prefab 后才能声称生成完成。重新生成只拥有 Prefab 根节点下的 `Generated` 子树，并保留手工兄弟节点。

组件范围为 `Container`、`Image`、`Text`、`Button`、`Toggle`、`ToggleGroup` 和 `ScrollRect`。
