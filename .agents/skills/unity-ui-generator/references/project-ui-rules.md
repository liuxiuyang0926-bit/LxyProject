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

本地效果图有两种互不混用的模式。

轻量 UISchema 模式下，后台 Codex 只输出视觉结构；不得搜索 Sprite 或执行 Prefab 操作。固定 C# 流程负责图片路径/尺寸、`Auto` 锚点、重复节点展开、资源索引、缺失占位、ScrollRect Content、Prefab 保存与 Binder 刷新。该模式只读取效果图并输出受 JSON Schema 约束的精简结构，保持项目只读。

高精度单轮模式下，Unity C# 先稳定扫描用户指定资源根，为有限数量 Sprite 生成视觉联系图，并输出页/行/列到精确资源路径、源尺寸与 Border 的 manifest。后台 Codex 保持项目只读且显式禁用 Unity/Figma MCP，只使用高清效果图、联系图和内嵌规则，在一次响应中输出完整 UISchema。只有联系图视觉证据成立时才显式写 `resource`；其余节点留空资源并提供通用 `semantic`，由 Builder 对完整资源根继续执行本地视觉匹配。

高精度 Schema 返回后，Unity C# 注入参考图路径与内容哈希，执行通用视觉层级和运行时模板归一，再由当前 Editor 直接调用现有 `UIEffectPrefabBuilder.Generate(...)`。Builder 继续负责全量资源匹配、Canvas/Binder、保存和确定性结构校验；首次调用结束后不启动 AI 复验或第二次 Builder。UnityMCP 仅保留给用户明确要求的手工终端直建流程，不进入默认效果图分析会话。

C# 使用 Schema 矩形原值设置 RectTransform；节点宽或高小于等于 0 时先归一为 1 像素。效果图尺寸必须通过 TextureImporter 的源文件尺寸取得，不能使用受 `maxTextureSize` 影响的 `Texture2D.width/height`。Prefab 的 `Generated` 使用固定设计尺寸，并整体等比居中适配项目 1920×1080 参考分辨率，不能直接 Stretch 到尚未挂载运行时 Canvas 的零尺寸 Prefab 根节点。`Auto` 锚点只选择响应式附着点，不会识别效果图或修正其他错误坐标。因此后台 Codex 必须使用原图像素坐标量取边界，先记录绝对矩形，再转成父节点局部坐标并递归复核。

两种模式下后台 Codex 都必须输出视觉所有权，而不是把背景与其文字平铺为同层绝对坐标：Header、Plate、Field、StatBackground、Button、Toggle 和边界明确的局部 Panel 拥有内部文字、图标和值。C# 在解析本地效果图 Schema 后还要按先绘制顺序、完整矩形包含和最小承载面积做保守层级归一，并在改父节点时保持累计绝对矩形不变；全屏 DimOverlay/遮罩不得成为业务内容父节点。该归一是 AI 偶发遗漏的保护网，不能替代提示和 Skill 直接要求正确 `children`。

视觉所有权不等于为每个语义区都创建 Image。父 Sprite 内部连续的底色、纹理和转角如果没有独立描边、接缝、透明轮廓或明显不同纹理，不能仅因为其上有标题文字就另建 Plate/Backdrop/Header；文字应直接属于父视觉节点。反之，字段底、名字条、分数条只要存在可见边缘、纹理、渐变或色带，就必须作为同条 Label/Value/Bonus 的 Image 父节点。完成资源匹配后，C# 只对 Schema 明确写了 `mayMerge:true` 的纯文字承载 Image 做父图局部复现校验并允许折叠；普通独立视觉节点不得因父 Sprite 暂时获得更高局部分而被删除。带 children 的 `Panel/Section/Content/Header/Footer` 布局 Image 是另一种保留层级的校正：遮掉后代后父 Sprite 对该局部的像素解释显著更强时，仅转为 Container；`Field/Bar/Plate/Badge/Flag/Icon/Tile/Cell/Card/Row` 不参与该转换。

`FigmaNodeUrl` 是例外：后台 Codex 只作为已授权 Figma MCP 的代理，读取 `get_metadata`，并用一次 `download_assets` 获取根节点 export render 的临时 URL，同时返回精简 UISchema；不要调用会返回大段代码的 `get_design_context`。C# 校验 Schema，将不能原生表达的纯视觉子树按矩形从根节点导出图裁切为 Sprite。提示必须禁止项目搜索、文件操作、REST、base64、raw source image 和额外资源下载，以压低 Token 与 MCP 调用次数。

Schema 使用 2.0 精简格式：省略默认字段并以单行 JSON 保存。业务数据驱动的 Card、Row、ListItem 列表在最终 Schema 中只输出一个无编号模板，`repeatCount` 保持 1，由运行时代码循环实例化。只有固定常驻的页签或装饰重复才使用 `repeatCount`、位移和 `variants`，不得复制完整子树。

同构运行时模板只能在完整效果图的几何、资源边界和层级分析之后省略。无资源歧义时后台 Codex 可直接只写一个；正确保留项依赖项目 Sprite 时，Schema 必须完整输出候选，并以相同 `runtimeTemplateGroup`、不同 `runtimeTemplateVariant` 标记根节点。C# 先匹配所有候选，再按变体 token 与已命中资源的一致性、命中资源完整度、视觉分和原始绘制顺序确定性地裁成一个。保留模板必须维持自己的原名、原始绝对矩形、绘制顺序与完整资源承载层级；裁剪其他实例不得导致补位、回流、缩放、改名、重设父子关系或扩大相邻背景。视觉还原优先，无法确认同构时不标候选组。

C# 不得在资源匹配前根据颜色/阵营前缀或子树相似度自动删除、合并或重排业务节点。后台漏标的唯一兜底是“只标注、不删除”的严格候选推断：候选必须位于同一父级，根类型为 Container/Image，根名属于 Card/Item/Row/Cell/Entry 或带明确 Faction/Team/Guild/Player 等业务身份的 Panel，至少有三个节点，名称仅一个颜色/阵营 token 不同，并且整棵子树类型、顺序、局部矩形与非视觉属性一致。左右/上下布局、Button、Toggle、简单样式状态、结构不同和固定常驻面板必须排除。只有资源匹配完成后，Prefab Builder 才可在同一 `runtimeTemplateGroup` 内裁剪候选；显式标组或通过上述严格规则标组之外的节点永不参与。旧 Schema 中带连续数字后缀的固定设计期重复压缩仍是独立能力，不能用于猜测无编号业务模板。

Builder 在首次生成的内存 Schema 中执行严格模板推断与裁剪。完整模式完成回调可在内存中重建同一候选分组并快速验证 Prefab 中至多存在一个根节点，但不得写回 Schema、不得再次调用 Builder，也不要求后台 Codex输出模板审计清单。

本地效果图使用稳定导入路径，不用 `GenerateUniqueAssetPath` 为同一 Panel 重复创建新参考图。C# 为参考图计算 SHA-256 并写入 `referenceImageHash`；当 Panel ID、规范化 `referenceImage`、源尺寸和哈希与现有 Schema 一致时，生成窗口默认直接复用该 Schema。只有图片内容或身份变化，或用户显式关闭“同图复用现有 Schema”时才启动新的后台分析；分析期间图片哈希改变时拒绝写入过期结果。

效果图生成窗口每次启用时必须同步刷新 AssetDatabase 并立即重建当前资源根的 Sprite 索引，不得只清空缓存等待下次生成。Sprite 源图片导入、删除或移动时，AssetPostprocessor 必须自动使资源索引和视觉描述缓存失效，避免窗口长时间打开时继续使用旧资源列表。

Prefab 生成时继续复用 `CSharpUIGenerator` 的基础组件与脚本流水线。运行时列表模板在 Prefab 中只生成一个 GameObject，交给业务循环克隆；固定设计重复才展开为多个 GameObject。不要额外建立一套 Canvas 或 Binder。

保存后必须重新加载 Prefab，校验 `Generated` 顶层数量、递归对象数量和 UI 组件数量与展开后的 Schema 一致；校验失败不得报告成功。普通空 Schema 应停止生成，Figma 空 Schema 则从根节点导出图创建一个覆盖设计尺寸的 `FrameVisual` crop 兜底，避免遗留只有 Canvas/Binder 的空壳 Prefab。

默认 Prefab 生成目录：

`Assets/GameResources/Prefabs/UIRes`

默认 UISchema 和参考图目录：

- `Assets/Editor/UISchemas`
- `Assets/Editor/UIReferences`

参考截图只作为 Editor 输入。除非用户明确将其提升为运行时资源，否则不得复制到 `Assets/GameResources`。

本地效果图从用户选择的资源总父目录递归匹配已有 Sprite，不创建 `ReferenceGenerated`。匹配失败只保留色块并写入缺失日志；不得生成 `PlaceholderLabel`。可编辑文字和交互组件保持真实、可见、可独立检查。禁止整图底图加透明交互层；旧 `useReferenceImageAsVisual` 只兼容读取，不得由后台 AI 生成。

效果图若是叠加在现有页面上的模态弹窗，Prefab 只拥有弹窗呈现层：保留大面积调暗遮罩与其上方独立 Popup/Dialog/Modal 的完整子树，排除遮罩下透出的宿主页面背景、地图、导航、货币栏、HUD、入口按钮和列表。遮罩与弹窗从宿主容器提升到 Schema 根时必须保持累计绝对坐标。Builder 仅在大面积遮罩、同父后绘制前景、几何包含和模态身份形成强证据时自动提取；证据不足时保持原树，避免误删普通全屏 UI。

## 生成内容的所有权

编译器只拥有 Prefab 根节点下名为 `Generated` 的子节点。重新生成时删除并重建这个子树，同时保留所有手工兄弟节点。

不要手工编辑生成的 C# Base 文件。创建第一版视觉 Prefab 时，不要修改已有运行时逻辑。

## 绑定命名

项目生成器按前缀自动收集组件绑定：

- `btn_`：`Button`
- `tgl_` 或 `toggle_`：`Toggle`
- `img_`：`Image`
- `txt_` 或 `tmp_`：TMP 文本
- `scroll_`：`ScrollRect`
- `rt_`：`RectTransform`

效果图构建器只给需要绑定的节点添加这些前缀。UISchema 2.0 的 `binding: Auto` 仅自动绑定 Button、Toggle 和 ScrollRect；Image、Text、Container 只有明确写 `binding: Yes` 才进入 Binder。ToggleGroup 默认只负责结构和互斥关系，运行时通过组内 Toggle 操作。这样可避免视觉稿中的纯装饰组件生成大量无用字段。Schema 名称不得自带前缀；可绑定节点名称必须全局唯一。

`binding` 只能是 `Auto`、`Yes` 或 `No`，不能填写业务字段名。业务身份由节点 `name` 表达，生成字段由 Binder 前缀规则确定。AI 导入层可把明显误写成 C# 标识符的 `binding` 归一为 `Yes` 并告警，以免一次视觉分析因安全可修复的字段串位而作废；项目中手写或已保存的 UISchema 继续使用严格校验。所有最终常驻的可绑定节点名必须全局唯一；不同固定面板里相同的 Value、Label 或 Button 要加最近父级语义前缀。普通未绑定装饰节点可以跨父级同名。Builder 在资源匹配和运行时模板候选裁剪后复核最终树，只对剩余冲突的后出现绑定节点按最近父级路径稳定改名，不得通过关闭绑定、删除视觉节点或合并固定控件来消除冲突。

按钮辅助 Label 和占位 Label 特意不使用绑定前缀。

## 资源匹配

所有候选的尺寸、长宽比和完整背景覆盖判断使用美术源像素：通过 `TextureImporter` 源宽高与导入 `Texture2D` 宽高计算缩放，再校正 `Sprite.rect`；不得直接使用受 `maxTextureSize` 缩小的导入 Rect。

默认搜索根目录为 `Assets/GameResources`。已知更精确的项目目录时，增加更窄的搜索根；这样可以同时提高速度和匹配质量。

优先使用通过仓库搜索或 `AssetDatabase` 找到的明确 Sprite 路径。Sprite Sheet 使用：

`Assets/.../atlas.png#SubSpriteName`

不要为了让不确定的匹配生效而修改纹理导入设置。不要绑定名称勉强相似的最近资源。解析器使用保守阈值，因此明显的占位色块优于看似合理但错误的美术资源。

后台分析应为角色可辨认的 Image/Button/Toggle 提供与资源名无关的通用 `semantic`，例如 background/panel/header/bar/field/icon/emblem/badge/crest/flag/banner/avatar/portrait/overlay；它只用于候选分组和有限决胜，不得猜路径、文件名或业务值。候选 Sprite 存在 Border 时，按目标 RectTransform 宽高模拟九宫格渲染后再做视觉评分，并在命中后设置 `Image.Type.Sliced`。背景、面板、条和字段等可拉伸表面不按源尺寸或原始长宽比直接淘汰；图标、徽标、徽章、旗帜、头像等轮廓敏感图形即使带 Border 也必须保留原始长宽比约束，禁止细长条参与近方形图形的最终竞争。视觉模式对 `intentionalColor` 节点也先尝试已有 Sprite，只有没有可信候选时才保留纯色。后绘制兄弟 Image 和子控件按实际矩形从背景采样中排除；Text 的 RectTransform 不代表整块像素不透明，文字覆盖背景必须保留采样并鲁棒排除实际字形离群点。半透明 Sprite 的预览颜色必须先反预乘 Alpha，并结合节点周边参考底色比较最终合成结果；近中性灰白 Sprite 可以拟合 `Image.color`，命中时必须把染色值写回 Schema/Prefab，同时对偏离白色较大的染色增加变换代价。只有原色候选同时满足高视觉分和接近的平均颜色，才可在染色蒙版领先不足时优先；没有颜色接近的强原色候选时，结构更匹配的染色九宫格继续有效。像素候选足够接近时，允许用节点 `semantic/name` 与资源类别同义词作有上限的决胜加分，但名称不能覆盖明显相反的视觉证据。AI 将连续背景误拆成相邻同宽或同高 Image 时，默认只允许在联合矩形高置信命中带 Border 的 Sprite 后自动合并；只有 Schema 明确写 `mayMerge:true`，普通 Sprite 又同时满足原始尺寸、长宽比、更高绝对分和正向领先幅度时才允许结构改写。匹配失败日志应报告最佳候选、分数和领先幅度，便于区分阈值拒绝与资源根目录错误。父资源已经包含的页签底、图标和装饰不得再次生成为重叠子 Image。

半透明九宫格的底色必须逐采样点重建。优先从最近的已验证视觉祖先取得当前子矩形的实际合成颜色，并让该上下文穿过普通 Container；无祖先时才采样外环，且外环必须排除同级 Image、Text 和交互区域。最终分数同时比较合成颜色/边缘、候选 Alpha 结构及去背景前景色方向与强度；文字覆盖的快速短名单保留去背景提示，原始 Sprite 平均色不得推翻逐点合成结果。同构字段发生资源冲突时也使用同一背景归一化后再计算局部和组均值。

真正的纯色节点要求 `intentionalColor`、不透明、无子节点，并且效果图内部像素近乎均匀且接近声明颜色；符合时保留色块并跳过 Sprite 候选，避免纯黑留边误命中透明面板。判断不得写死业务节点名或资源名。成功日志记录节点、资源、得分、领先幅度和背景来源；冲突组未纠正时记录均值或领先不足。

通用语义决胜可以组合当前节点和直接父级的类别，并允许独立同义证据在固定上限内累加；它只能调整已经接近的候选排序。紧凑图标初始边界疑似裁切时，可在直接父级内对若干固定外扩矩形重新取样，仍使用图标专用高门槛。文字离群点剔除不得移除描述图最外侧采样带，以保留窄边框、单侧色带和九宫格端帽等决定性证据。

语义 token 加分只能充当弱决胜；数字命名的资源不能仅因缺少 `icon/crest/badge` 等英文词而失去已经明显更强的像素优势。九宫格接近分额外保留非拉伸轴的源尺寸证据：横向表面比较源高度，纵向表面比较源宽度，近方形表面保留源长宽比；目标尺寸小于对应 Border 总厚度、会挤压边框带时额外降权。所有尺寸证据都以有上限的小幅惩罚而不是硬过滤参与排序。

匹配置信度必须先经过通用视觉类别和轮廓约束过滤，再按无遮挡、文字软遮挡和大面积遮挡分别判定，综合第一名绝对分与领先幅度；不能用全局降低阈值修补类别冲突，也不能用一个固定 margin 拒绝所有候选。遮挡后的有效样本仍给出高绝对颜色/结构/边缘分且第一名保持正向稳定领先时，可以使用更小的分段 margin，以免已有正确资源退回色块；像素分不足或并列不稳定时仍必须保守拒绝。近方形、无文字的小型图标/徽标使用更高的结构与轮廓门槛，禁止低分纯色条或普通面板仅靠缩放和平均颜色通过。

资源索引和候选决胜不得依赖 `AssetDatabase.FindAssets`、字典或文件系统的枚举顺序。搜索根先规范化、去重并排序；候选按资源路径、子 Sprite 名和 Sprite Rect 稳定排序；视觉快速分相同的短名单继续用资源路径和子 Sprite 名作固定决胜。相同参考图、Schema、资源根和资源版本必须选择相同 Sprite。

资源匹配前允许进行一次通用、像素证据驱动的结构校正：带 `mayUseFullCanvasSprite` 或具有明确背景语义的局部容器，可以在候选原始尺寸接近直接父级、长宽比一致且整幅参考区域高置信匹配时扩展为完整背景，同时保持其他子节点的累计绝对坐标；两个连续普通背景片段的联合区域覆盖父级主要宽度、候选原始尺寸接近父级全宽且整幅参考区域高置信时，可直接还原为一个完整背景；其他 `mayMerge` 相邻区域只有联合矩形高置信命中时才能合并。非视觉 Container 下遗漏的文字承载条，只在包围同组 Text 的九宫格候选具有高绝对分与稳定正向领先时补建并建立 children。同级中语义、尺寸及纯文字子树同构的表面，未命中节点可以仅在自身参考区域重新验证同级已命中的候选，通过后补全资源，禁止无像素验证地复制。`ArtText` 可以在原矩形与小幅外扩矩形的透明轮廓和结构高置信匹配后由 Text 转成 Image，并在两张透明 Sprite 合成比分别匹配显著更好时输出稳定的前后两层；`PossiblyBaked` 或徽章内短文字只有父 Sprite 对文字局部也能高置信复现时才删除。所有校正与候选排序都必须确定化，失败时保持原结构或色块。

结构校正按子层到祖先执行。局部 Container 内两个或多个 Image 若同高、相邻且无缝覆盖局部宽度，可直接把其完整父级矩形作为普通 Sprite 候选区域；只有原始尺寸、长宽比和整幅像素分都通过时，才扩展 Container、保留一个背景 Image，并将所有片段的 children 以绝对位置不变的方式归入该背景。对同级同构文字表面，C# 还要检测已经自动命中的资源冲突：将组内出现的候选逐一在所有成员区域评分，以确定性组均值和正向领先选择一致候选；显式资源或局部显著不同的变体不覆盖。

父级非九宫格 Sprite 的原始尺寸与父节点几乎一致且整幅匹配已经验证时，允许对 AI 漏写 `mayMerge` 做严格补救：子 Image 必须没有显式资源、只承载文字，遮掉文字后父图局部近乎完全复现并显著优于子候选。父级九宫格、尺寸差异明显或子树含独立图形时不执行。`runtimeTemplateGroup` 在最终裁剪前还要按同构子树位置复核通用 Sprite；只传播不含任一变体 token、每个实例局部分与组均值都过门槛的资源，已命中本变体 token 的 Header/Flag/Emblem 槽位保持独立。

大范围半透明调暗层是纯色规则的确定性例外：节点已写 `intentionalColor`，面积至少为画布四分之一，颜色 Alpha 小于 0.98，且 name/semantic 明确包含 Overlay、Dimmer、DimLayer、Scrim 或遮罩时，直接保留色块并跳过视觉候选；不得把弹窗遮罩匹配成同色背景 Sprite。

## 输入来源边界

Figma 链接必须通过 `node-id` 指定 Frame 或 Component。使用前在 Codex 中连接并授权官方 Figma MCP；Unity 不得读取 Codex OAuth 状态，不得提供 Personal Access Token 字段，也不得调用 Figma REST。MCP 返回的临时截图 URL 只允许立即下载到本地参考图，不能写入 UISchema 或仓库。

后台 CLI 必须配置名为 `figma` 的官方远程 MCP，地址为 `https://mcp.figma.com/mcp`，并在 CLI 中完成 OAuth。Unity 在发起 AI 请求前通过 `mcp get figma` 做只读预检；结构化响应还必须明确返回 MCP 成功状态。未配置、未授权或工具未进入后台会话时立即停止，不得让 AI 用空字段或占位文本伪装 `schemaJson`/`referenceImageUrl` 成功。

Figma 参考图保存到 `Assets/Editor/UIReferences`。Prefab 使用的节点图片保存到 `Assets/GameResources/UIAtlas/FigmaGenerated/<Panel>`，UISchema 只引用这些本地资源，不保留远程 URL。远程 HTTP 图片不再是支持的输入来源；蓝湖等平台请先导出本地 PNG/JPG，再使用 `LocalImage`。

Figma 转换规则：

- 使用 `absoluteBoundingBox` 换算相对父节点的左上角坐标；
- `TEXT` 转为 `Text`，保留文字、主颜色、字号、粗体和对齐；
- 具有滚动方向，或裁切且子节点越界的 Frame 转为 `ScrollRect`；
- 名称和结构明确表示确认、领取、返回、搜索或添加等独立动作的节点转为 `Button`；
- 页签、分类、模式、筛选等同组互斥选择项转为 `Toggle`，并保留或建立共同的 `ToggleGroup` 父节点；可见默认选中项设置 `isOn: true`，默认不允许全部取消；
- 单一无描边纯色矩形使用原生 `Image.color`；其余不含可编辑子节点的视觉子树使用 `figma://crop` 标记，由 C# 从整张 Frame 截图裁切为 PNG；
- 本地效果图只输出可测量的结构和矩形，资源字段通常留空，由 C# 使用源文件原始尺寸和节点绝对矩形匹配项目 Sprite；
- AI 漏标 crop 时，C# 可将无资源、无纯色、无子节点且不与兄弟节点重叠的独立 `Image` 自动标为 crop；与其他兄弟重叠的背景、边框和选中态不得自动裁切，避免把相邻文字或控件重复烘焙进 Sprite；
- 隐藏、零透明度、零尺寸节点跳过；保持原始子节点顺序作为 UGUI sibling 顺序。

## 第一版组件范围

支持：

- `Container`
- 通过 `Text` 表示的 `TextMeshProUGUI`
- `Image`
- `Button`
- `Toggle`
- `ToggleGroup`
- `ScrollRect`

UISchema 2.0 尚不表示：

- Slider、Dropdown、InputField；
- GridLayoutGroup 和自动列表模板；
- 显式 Mask 节点；
- SpriteAtlas Address 选择；
- 动画、过渡和运行时数据绑定；
- 安全区和设备专属变体。

在结果报告中明确列出这些问题，不要静默近似实现。
