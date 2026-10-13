# 基础 UI 预制体维护

基础组件位于 Core.Runtime，模板位于公共 UI/Common。独立页面由 Hotfix View 使用它们，基础组件不持有 Demo 规则。接入前逐项检查字体、Sprite、Border、绑定、布局、遮罩、射线与导航；使用方式见 [基础 UI 组件](../runbooks/use-core-ui-components.md)，表现与制作流程见[公共美术规范](../architecture/ui-art-direction.md)。

## 源资产与使用方

公共基础维护结构、行为、UIBind 引用和布局能力；四 Demo 使用各自风格 Variant 集中维护图片、颜色、主副字号、边框与阴影；页面实例只覆盖业务参数和必要布局。保留 Variant 源连接，不用控件复制品替代继承。基础改结构后逐个检查四 Demo，风格改表现后检查其全部页面使用方；清理重复覆盖、断继承和无效覆盖前确认业务字段、事件及布局用途。

共享面板/焦点边已通过 Unity 原生 MoveAsset 移入 `UI/Common/Sprites/Surfaces/{Surface,FocusBorder}.png`（相对 LoadResources），保留原 GUID。夹爆它的 Menu/Pause/Retry Foundation 按钮 Variant 使用同目录 `FocusBorderSquare.png`：24×24、四边 Border 3、Sliced、关闭 Fill Center 与射线，保留直角黑框风格。换风格与嵌套使用方检查见[制作步骤](../runbooks/use-core-ui-components.md#制作与更换控件风格)。

四 Demo 的按钮结构现已关联公共 CommonButton → Demo Foundation Variant → 页面实例。原生收口报告 `Library/DemoFoundationMigration/linked-appearance.txt` 记录 139 处控件使用方（含嵌套部件使用方）、20 个 Selected 控件，以及各 3 张 VehicleCard、ModeCard、ClassCard。实例外观覆盖按字段归还风格源，保留业务文字、事件、UIBind、矩形和命中语义。32 个无显示用途的透明反馈 Image 已核对引用、状态、动画和材质后原生清理；CanvasGroup 与完整 FocusBorder 保留。保存与引用核对完成不代表最新正式入口交互、视觉或性能验收通过。

## 四 Demo 完整风格库

每个 Demo 的统一入口为 `Assets/LoadResources/Demos/<DemoId>/Prefabs/UI/Foundation/`；前缀分别为 `Drone`、`Dlss`、`Wall`、`KagitKart`。下表后缀拼接前缀即为正式控件资产名，例如 `DroneSlider.prefab`、`DlssDropdownRow.prefab`。暂未使用或目前与公共外观相同的控件也保留本 Demo Variant，方便开发时从一个目录换整套表现；不据此创建新页面、公共设置入口或运行时主题。

基础源共28项，路径相对于 `Assets/LoadResources/UI/Common/`。全局 `CommonLoading.prefab` 仍由原 Loading 流程维护，排除在 Demo Foundation 库外。

| 公共基础源 | Foundation 后缀 | 数量 |
|---|---|---|
| `_TemplateInstantiatePrefab/Btns/CommonButton` | `Button` | 1 |
| `_TemplateInstantiatePrefab/Btns/{BtnSwitch1,BtnSwitch2,Toggle}` | `BtnSwitch1`、`BtnSwitch2`、`Toggle` | 3 |
| `_TemplateInstantiatePrefab/Tab/{TabItem,Tab,TabHorizontal,ViewTabVertical,AccordionTab,AccordionViewTab}` | 与公共控件同名 | 6 |
| `_TemplateInstantiatePrefab/Dropdown/Dropdown` | `Dropdown` | 1 |
| `_TemplateInstantiatePrefab/CommonSlider`、`CommonProgressFill` | `Slider`、`ProgressFill` | 2 |
| `_TemplateInstantiatePrefab/Bg/{BG_Gift,BG_Large,BG_Middle,BG_SideTab}` | `BgGift`、`BgLarge`、`BgMiddle`、`BgSideTab` | 4 |
| `_TemplateInstantiatePrefab/Commom_Title` | `Title` | 1 |
| `Settings/{DropdownRow,SliderRow,SwitchRow,ActionRow}` | 与公共控件同名 | 4 |
| `Input/{InputPrompt,InputPrompt2,InputPromptButton}` | 与公共控件同名 | 3 |
| `CommonTips`、`SimpleTips` | `CommonTips`、`SimpleTips` | 2 |
| `TMPAutoScroll` | `TMPAutoScroll` | 1 |

已有语义风格继续作为额外入口维护：无人机的 Debug/Back/Close/Return/Start、VehicleCard、TouchCommand/HudCommand，夹爆它的 Menu/Pause/Retry，卡丁车的 Menu/ResultsAction/Choice/ModeCard、Light/Medium/HeavyClassCard、Character/Track/ReturnToHub、HudCommand/ItemCommand。前缀加用途名，不把字号或生成哈希写入文件名。原样保存精确字号、RGBA、纸纹、硬影、专属内容与事件；不同用途不因为字号接近而合并。

13个已有输入提示（含无人机装备提示）统一从 `Parts/`、`Widgets/` 移入 Foundation，原 `.meta` 与 GUID 保留；专属 OperationPanel、TelemetryRoot、TouchControls 和 ResultRow 留在各自部件目录。`DroneProgressFill` 与改名后的 `KagitKartSlider` 继续继承原公共源并保留作者风格。

组合的外层和内部须同时收口：设置行引用本 Demo 的按钮/开关/滑条/下拉，面板引用本 Demo Title 与关闭按钮，Tab/Dropdown/折叠项及其动态模板引用本 Demo 子变体，CommonTips 的自动滚动也引用本 Demo TMPAutoScroll。检查实际嵌套源和序列化模板字段，不只检查外层文件的 Variant 类型。页面实例保留真实文案、动作、组件引用、值域、状态和必要布局；公共结构 → Demo 风格 → 页面实例的继承不能以裸复制控件代替。

内部继承实例换源以 Variant 的移除/新增覆盖完成；原作者参数和引用按稳定层级/组件类型映射，同名兄弟按原顺序一一对应，共有 UIState 与 Graphic 样式继续继承子 Variant，不整套复制状态表或在组合内重复套色。公共源里原有的裸子控件保持原样，在 Demo Variant 中按明确角色路径切换为本 Demo 的 Button、Tab 或 TabItem 子 Variant，保留原布局、文案、事件和绑定。公共组合新增外层节点仍向 Demo 组合传播；已切换子源的分支改从对应公共 Button/Tab/TabItem 经 Demo 子 Variant 继承，不再继承原裸分支内部的结构改动。迁移不重写公共源的 local ID 或依赖资产结构记录。临时创建在独立 PreviewScene 内完成，不使用户活跃场景变脏。

本轮原生迁移在 `Library/DemoFoundationLibrary/<timestamp>/` 保存受影响 Prefab/`.meta` 的备份、公共源清单以及 MOVE/CREATE/NEST/TEMPLATE/GUID/COVER 报告。临时 Builder 只用于这次迁移，资产和引用核对后删除，不作为长期编辑器入口。原生保存/引用核对与正式入口视觉、交互、真实设备及 Player 构建分别报告，不由迁移成功推断全部验收通过。

2026-10-13 定向验收：四套库共 137 个正式控件 Variant，112 个默认基础源链完整；公共结构传播 20 个实际实例、Demo 样式传播 12 个实际页面实例通过。正式 AppEntrance 的四 Demo 进入／返回及图片提示已检查，临时探针与迁移脚本在交付前清理；详细结果和未验证项见[实施记录](../agent/archive/ui-maintenance-2026-10-13.md)。

## 逐项检查范围

| 预制体 | 维护重点 |
| --- | --- |
| CommonButton | 新增可直接复用的按钮模板，五态反馈与取消转发 |
| CommonSlider | 从 SliderRow 的原 Slider 提取，SliderRow 继续嵌套同一源；原生 Slider 与一个 UIState，保护 Fill/Handle/Target 引用、值域、整数步进及回调 |
| CommonProgressFill | 最小水平 Filled Image 与 UIProgressBar，不自驱进度；CommonLoading、StartupLoading 与 Drone 进度使用同源，轨道及说明留在宿主 |
| BtnSwitch1 | 双文字胶囊轨道，两项始终可读，选中块显示在左右位置；与 BtnSwitch2 同样整块点击切换。保留 GUID `1840a1e3ffbf3114dbc14437350022cc`、UIBtnSwitch 与同一 UIState 的业务态/五态，不增加左右分区点击逻辑 |
| Toggle | 独立原生勾选框；原生 Toggle 维护值与回调，同节点 UIState 根据该值应用 On/Off 与五态，不使用 UIBtnSwitch 替代复选框 |
| BtnSwitch2 | 滑块开关型；On/Off 位置与语义色、独立状态文案、交互五态及禁用复位 |
| TabItem | TMP 文案、同一 UIState 的业务 Selected 与五态、无重复反馈层 |
| Tab | 模板项和选中对象绑定，移除容器的无效交互 |
| TabHorizontal | `Tab.prefab` 的真实 Variant，保留原嵌套 TabItem 来源与同一 UITab 解析/选择逻辑；只覆盖横向布局及下划线表现 |
| ViewTabVertical | 嵌套 Tab、分页布局、滚动遮罩和绑定 |
| AccordionTab | 根项/叶项 TMP 文案、展开与选中、嵌套交互覆盖；组/子组只自适应高度，宽度由父级布局分配，避免缩回模板宽度 |
| AccordionViewTab | 嵌套折叠项、分页绑定与滚动区域 |
| Dropdown | 默认文案保持 TMP_Text；Arrow 使用 `Sprites/Controls/ChevronDown.png`，保留展开 180° 旋转；定位说明继续使用原 `Sprites/Tips/Arrow.png` |
| Commom_Title | 关闭按钮五态、中文字体、装饰层射线 |
| BG_Gift | 标题/关闭控件绑定，装饰节点无交互 |
| BG_Large | 关闭控件与尺寸，纯背景无导航 |
| BG_Middle | 关闭控件、字体、图片 Border 与布局 |
| BG_SideTab | 嵌套 Tab/关闭按钮覆盖及反馈层去重 |
| CommonLoading | 白色 Filled 显示真实进度；全屏背景 Simple + EnvelopeParent，独立信息层与顶部/底部渐变 scrim，保持 Base Page |
| CommonTips | 原模板/图标/滚动绑定，关闭按钮五态及射线 |
| SimpleTips | Blocker、返回作用域、定位和遮罩绑定 |
| TMPAutoScroll | 文本/viewport/字体绑定，保留原滚动实现 |

同名嵌套控件也需检查实际覆盖。不要只修改源模板而留下覆盖旧状态的实例；不要删除嵌套源节点后创建第二个同名反馈层。

### 已确认图02的面板与消息契约

面板内标题区高64、顶部距12，分隔线落在顶部76；这是面板嵌套布局，通用页面标题仍独立居中。浮层阴影使用深墨 alpha 0.14、偏移 `(0,-6)`、柔化8；普通表面和消息条不统一加阴影。关闭按钮的命中矩形保留。

外观目标为[图02 v3](../agent/art/ui-foundation/foundation-v3-panels.png)。四个 BG 面板与两类 Tips 使用暖白 `#FCFBF5`、深墨 `#1B292B`、细边 `#B9B8AF`，朱红 `#F23E29` 只用于焦点与选中重点。面板和定位说明具有轻柔阴影，消息条保持平面；共享 Sliced Surface 承载表面，同节点 ImageExtend 提供必要细描边与浮层阴影，不将效果图裁成整张面板图片。

仅独立 `Commom_Title` 页面标题居中。BG_Large、BG_Middle、BG_Gift 的嵌套标题仍左对齐；BG_SideTab 的标题和水平分隔线位于右侧内容区，左侧栏使用独立竖分隔线。嵌套实例只精确覆盖这些布局差异，不修改页面标题源或按钮源。BG_Middle 保持对 BG_Large 的 Variant 继承，SideTab 保持嵌套大面板及分页组件；礼品面板旧花纹不显示，原节点及引用保留。面板作者根 Rect、UIBind、关闭控件、内容挂载点和遮罩不因外观装配重建。

CommonTips 的三种消息共享模板暖白背景，运行时 `SetMessage` 只切换图标语义，不重染背景或边框；成功为绿色圆形勾，普通说明为深墨圆形信息标志，警告为红色轮廓三角与叹号。直接引用 `Sprites/Tips/{CircleSuccess,CircleInfo,WarningOutline}.png`；警告图片预着色为 `#BE0423`，保留现有白色 Tint 契约，原 WarningTriangle 与白色 Success 勾继续供其他使用方使用。SVG 制作源在 `ArtSource/common_ui/`。标题与正文均为真实 TMP，正文24号；图标28、Details 上下各8，当前公共字体单行测量高度约44.13，长消息仍按既有滚动契约测量，不固定为单行高度。关闭按钮保留原单条关闭行为和40命中尺寸。ItemTemplate 仍为隐藏模板，CanvasGroup、Details、单行自动滚动及外层消息列表引用保留。

SimpleTips 的 Body、Arrow、Blocker、返回作用域及滚动区保留原组件；箭头由现有方向算法定位和旋转，不固定为效果图中的左向示例。可选标题隐藏、正文换行与超高滚动继续由既有布局能力处理。ImageExtend 不创建或接管 Mask；无引用的旧 Outline 与新细描边不能重复叠加，作者 Mask/RectMask2D 保留。

上述为批准后的装配契约。原生保存报告与 Canvas 预览不能替代真实三种 `SetMessage`、定位方向、长文滚动和关闭后的运行验收；未产生实际结果前不声明外观还原通过。

`_TemplateInstantiatePrefab` 基础模板统一使用暖白、深墨和朱红，按钮五态保留根节点尺寸与缩放，使用颜色、焦点边和透明度区分状态；滑块自身的局部反馈不改变按钮点击矩形。外观制作保存不等于交互或视觉验收通过，仍需检查真实输入和嵌套使用方。

公共设置 `DropdownRow` 的选项容器保持独立不透明背景与纵向自适应高度，覆盖真实选项和间距，避免滚动表单的字段透入浮层。使用同页 popupRoot 时，继续检查完整选项的边界及后续 Modal 的排序。

选项容器锚定控件下边缘、顶部 Pivot，宽度跟随字段，空间足够时从字段下方向下展开；不要让自适应高度向上撑入分类 Tab。组件禁用即时归还，整树启停或销毁延后归还，制作矩形与顺序必须完整恢复。

公共设置与输入另有以下正式基础 Prefab，路径相对于 `Assets/LoadResources/`：

| 预制体 | 维护边界 |
|---|---|
| `UI/Common/Settings/DropdownRow`、`SliderRow`、`SwitchRow`、`ActionRow` | `UISettingRow` 引用原 Dropdown、Slider、Switch、Button；不承担分类切换或保存。滚动表单保持点击区域稳定 |
| `UI/Common/Input/InputPrompt` | 图标组在前、文案在后，垂直居中靠左；原生 preferredWidth/自动宽度，从真实会话绑定读取按键 |
| `UI/Common/Input/InputPrompt2` | 提示1的 Variant；文案在前、图标组在后，垂直居中靠右，总宽变化保持右边界；不反转组合键槽内部顺序 |
| `UI/Common/Input/InputPromptButton` | 提示1的可点击 Variant；保留 300×44 制作矩形及水平 Unconstrained，复用 InputCommandButton 的离散/保持输入与释放语义 |

SwitchRow 内部 Control 嵌套 BtnSwitch2，行根 GUID 和 UISettingRow 组件保持不变；Hub 偏好 13 处及体验页 11 处共 24 个调用继续复用它。图03装配后的 Control 制作尺寸为96×40，anchorMin/anchorMax均为`(0.28,0.6)`，在行的共同控件列摆放，不用纵向拉伸增加滑块高度。替换内部控件时重绑 control、toggle、onLabel、offLabel；页面生成字段及体验子组件只引用行根，不因 Control 的本地 fileID 改变而重建页面绑定。语言选项及 Auto/On/Off 三态减少动态不属于该迁移。

四个 Demo 从公共提示创建自己的 Prompt / Prompt2 / PromptButton 风格 Variant，样式覆盖负责外观，公共组件负责真实绑定、组合顺序与 fallback；卡丁车保留纸艺资源。四份 Prompt2 已保存，实际页面接入与视觉结果按本轮验收记录确认，不能仅凭资产存在宣称使用方已全部更新。公共设置只在 Hub 开放，不制作 Demo 设置宿主。分类子页、固定表单与循环绑定列表的选择及调用案例统一维护在[基础组件手册](../runbooks/use-core-ui-components.md#从需求找到资源和示例)。

## 行为和生命周期

Hub 与 Demo 输入提示的目录分别为 `HubInputGlyphCatalog.asset` 与原 `InputGlyphCatalog.asset`。前者复用 FocusBorder 薄帽，后者保留 Kenney 与 Demo 原艺术表现；共享的空帽 PNG 原内容和 GUID 保留，Border32支持长名称九宫格扩展。真实图标、36/24高度和作者布局参数的恢复由同一 InputBindingPrompt 维护。

每个控件只有一个 UIState，默认状态为 Normal、Hover、Focused、Pressed、Disabled。同节点 Selectable 自动驱动五态；无 Selectable 的业务或装饰节点手动切换，不挂独立交互桥或全局状态管理器。Tab/开关继续持有业务值，所需 Selected、On、Off 追加在同一状态表。

自动控件按 Normal 基底、业务态、交互态顺序应用。Normal 保存所拥有属性的恢复值，Hover 提亮并显示细边，Focused 使用明确焦点边，Pressed 变深或改变装饰，Disabled 降低强调且关闭反馈层。状态退出恢复仍有效的业务表现；隐藏清除按下、悬停与 Submit 瞬态，不清空业务值。滚动表单保持点击矩形稳定，不统一缩放按钮根节点。触屏允许真实指针按住的 Pressed，抑制 Hover、Focused 与 Submit 瞬态，禁用态仍须清楚可见。

保留现有 Decoration、纸卡等节点及强类型引用，关闭由 UIState 拥有属性的原生 Transition。迁移通过 Unity 原生 Prefab 操作合并状态与重绑引用，保留源资产 GUID、Variant 继承和 Demo 的外观覆盖；不要按状态数组位置搬迁业务定义。状态反馈 Graphic 不拦截射线，触控命令的保持、释放和隐藏清理由 InputCommandButton 负责。

UIState 在 Inspector 验证时清空查找缓存；更改状态定义后不会继续查到旧状态。Dropdown 默认文案及 Accordion 文案使用 TMP，不能只换 Prefab 的文字组件而不修复读取方。

UIProgressBar 包装同节点 Filled Image 的数值和颜色。白色 Sprite 为真实填充纹理，圆角由独立 Sliced 背景/遮罩负责；保留特殊形状的填充纹理和方向。不得将没有 Sprite 的普通矩形当作 Filled 进度。

原生 Image 的图片组合效果统一使用 ImageExtend；Shader 为 `Art/Shaders/UIImageExtend.shader`，资源地址为 `LoadResources/Art/Shaders/UIImageExtend`。普通表面优先 Sliced 图片，特效只在确有用途的节点启用。组件保持 Rect、填充与点击矩形契约，默认 UI 材质、透明图集及 Filled 外扩限制见[手册](../runbooks/use-core-ui-components.md#imageextend-图片组合效果)，不继续维护旧图片效果的双轨接入。

ImageExtend 不自动添加或管理 Mask；圆角效果只处理自身图片。作者所需的原生 Mask/RectMask2D 独立维护，不能因移除旧自动遮罩字段而删除已有作者遮罩。

## 验证

### 既有阶段记录

历史证据保存于 Library/UIRefactor，原生 XML 按 jobId 单独保留。旧双组件结构的资源检查包含所有 17 个模板；当时通过记录为资源 2/2、基础交互/填充 2/2、组件生命周期 10/10、提示组件 21/21。资源复核1de7c6fc为2/2通过，真实输入与实际填充网格f882abfd为2/2通过；当时五态截图已逐张查看，禁用态无强调边框、按下态有缩小与变深。截图夹具配置独立背景相机，避免无相机场景下的残帧；未执行全量测试。这些记录不证明本轮单 UIState 合并后的结构已通过。

既有模板样式与开关替换阶段的直接相关资源、真实输入及 Canvas 预览证据如下，不证明本轮 ImageExtend、Prompt2 与四 Demo 继承传播已经全部验收。复用 `Tests.Module.UnifiedInputPlayModeTests.NestedMenuRestoresParentAndTouchSuppressesInteractionFeedback` 维护单组件 Selected/On 与交互叠加、禁用及启停恢复；测试结果与视觉结果分别记录，不为普通外观装配另留永久测试文件。

当前定向证据：基础资源最新复验 5/5（247574ad）；CommonButton 新原生 XML 1/1（b635b2c7），五态五张实际截图已查看。滑块开关初次 d1dff24b 为 0/1，临时验证禁用后未重新选择焦点；仅修验证准备后复验 1/1（fe841386）。真实 Canvas 首次预览 1/1（b6df198a）及 Dropdown 浮层生命周期 1/1（1b93e3a0）通过，以 fresh 原生 XML 为准，REST 恢复错误分别记录。首次预览发现的 SwitchRow 高度、两个 ViewTab 蓝底及隐藏 UITab 制作模板已修正；最新模板、SwitchRow 与 Arrow 开合预览 1/1（cd7c66db），主代理已查看全部实际图。PreviewRenderUtility 先前生成的 14 张空白图无效，不作为证据。Hub Cancel 后续真实点击 1/1（9c41ec3d），新版中英三画幅实际切页/取消/返回 1/1（59b4065c），关闭按钮三画幅真实点击 1/1（6a0ba70d），不再保留此前的待修状态；没有运行全量测试。

状态截图使用真实 Canvas 与实际输入，检查默认、悬停、聚焦、按下、禁用；数值断言不能代替截图检查。公共模板验收完成后才能迁移各 Demo。

本次样式装配工具 `FoundationUiStylesMigration.cs` 及 meta、两项临时开关/模板预览用例和 Hub 命中诊断已清理。原有基础交互测试只将按下时的根缩放断言更新为 `Vector3.one`；未新增永久测试或运行时组件。实际截图和原生 XML 保留在 Library，空白离屏预览产物已删除。四 Demo 作者资产最终复核 1978 项哈希未变，仅删除四份已空设置目录的 meta；正式入口场景哈希未变。

## Demo 进度填充

普通线性进度使用 `Sprites/White.png` 的Filled与`UIProgressBar.SetValue/SetColor`；有形状的轨道和圆角遮罩继续使用带Border的Sliced Sprite。交互Slider由自身改变`fillRect`宽度，公共`CommonSlider/Fill Area/Fill`使用`CapsuleSurface`、Sliced与朱红染色；它继承已批准图01的轨道/圆柄样式，不按UIProgressBar的Filled契约改写。径向或明确需要纹理形状的Filled保留实际Sprite及FillMethod。

无人机重置进度和公共加载进度使用公共 UIProgressBar。既有序列化 Image 引用保留，初始化时缓存同节点 UIProgressBar。

定向验证：线性填充资源 1/1、无人机 HUD 绑定 1/1。公共 Filled 网格测试覆盖精确 0%、1%、50%、100%。


最终资源契约 59bfe436（4/4）通过：检查全部普通水平/竖直 Filled 的白 Sprite、UIProgressBar 和射线设置，不再按节点名称筛选；同时核查 Demo 页面绑定无外部节点、无缺失脚本以及无人机 HUD 子 Prefab 引用。一次性基础 UI/进度/页面迁移工具及其 meta 已删除，正式组件、保存资源和 Test Runner 是后续维护入口。
