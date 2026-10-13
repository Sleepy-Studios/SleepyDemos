# 公共 UI Goal 当前进度

## 2026-10-13：回到完整基础效果图确认

用户要求先更新全部 Hub 公共基础 UI 效果图、确认后再还原，并明确先验收01／02再做03。最新三张图、提示词和覆盖表见[美术确认记录](../art/ui-foundation/README.md)。2026-10-13用户已分别回复“01 通过”“02 通过”，认可07:17复拍的正式Prefab原生截图；此后才开始03设置行及长文本装配。此前 StartupLoading v3、运行期 Loading v2 的确认仍保留；图集缩略图不替代它们。

ImageExtend 已移除旧圆角自动 Mask 参数与 Inspector 入口；圆角仅影响本 Image，作者显式原生 Mask/RectMask2D 兼容保留。01／02／03正式原生截图均已获用户视觉认可；设置五个新图标也已按确认设计装配单套SVG，并按用户后续要求直接显示具体配置、不加“默认：”。所有本任务临时Builder/Probe/性能对照资产及meta已清理；最终清理后编译9a3fdc56通过，Console Error=0，场景恢复干净AppEntrance；本轮交付核对已收口，提交状态以git log及工作区回读为准，默认不push。

## 最终交付核对

- 基础控件、24按钮风格、普通Slider/Filled源与四Demo使用方已形成真实继承；基础/风格修改传播及开发时替换的原生配置、对象、绑定证据Complete，所有验证改动已恢复原bytes/meta/GUID。不是运行时换肤框架，也不冒充任意换肤后的像素/真实点击全覆盖。
- ImageExtend组合、遮罩、透明度、Filled回退、真实packed图集边缘与异步释放已通过相关验证；旧组件/Inspector/Shader及四边生成入口清零，未保留双轨实现。两种按键提示复用单一解析与原生自动布局，长绑定/缺图标/右边界、实际HUD设备切换已验。
- 两套已确认Loading、四Demo正式进入/重载/返回、失败/Busy保护及冷启动/Editor直启/Loop返回均有实际相关验证。四Demo宿主风格保留，公共与专属规范、资源入口、字号配色、声音后续质量要求及导航已同步。
- 最新追加验证：冷启动/直启/Loop返回3/3，SVG与具体值最终1/1，中英重建1/1，候选隔离/单次提交1/1，基础资产最终6797815d为5/5，共11/11。此前03相关11/11与各专项证据分阶段记录，未执行全量回归，不将同一重跑重复累加成独立覆盖。
- 已清理前期5Builder+meta以及最后03装配器、原生截图器、正式入口Probe、性能对照临时Prefab+meta；剩余临时路径检索为零，截图/XML/备份只在被忽略的Library。两个原有ProjectSettings与EditorSettings hash不变，未保存或修改AppEntrance资产。
- 性能只报告实际Graphic/顶点/材质与Batches/DrawCalls样本；没有完整GPU透明overdraw时间、任意风格运行像素、Player/IL2CPP或真实手柄/触屏验收。SVG包为3.0预览版，后续升级需复验导入与渲染；不宣称合并组件或矢量必然提速。

### 2026-10-13：03新增设置图标与具体值显示

- 用户确认五图标设计后指定安装Vector Graphics直接用SVG；UPM已安装并实际查询确认 `com.unity.vectorgraphics@3.0.0-preview.7`，manifest/lock仅新增该包。Core.Editor与Tests.PlayMode加入实际编译所需依赖，原生装配和临时验证直接使用SVGImporter/SVGImage；没有包类型反射包装或PNG双份。
- 五份200×200透明SVG在 `UI/Common/Sprites/Settings/`，uGUI导入UISVGImage，正式页显示50×50。一次装配初查因Importer未SetDirty停止且未存页面，修正后保存回读通过；第一次50px截图发现与文字重叠，改原HorizontalLayoutGroup左84/右16及图标居中、ignoreLayout后复拍修正，不靠编译/数值断言冒充外观通过。
- 用户进一步要求去掉“默认：”，当前四项仅显示实际窗口、像素、平台画质名和作者帧率语义。未改sentinel与保存逻辑，未引用的Graphics.Current/Graphics.DefaultValue词条已原生删除。最终临时真实冷入口用例673917c2通过1/1，1920/1280截图为 `Library/UIArt/entry-settings-defaults-svg-20261013-003616-442/`，已查看：无前缀、无图文重叠；原生指针五分类切换、Reset/Refresh候选与偏好不变通过。
- 现有中英文重建与焦点用例057d170e、候选隔离/单次提交e23df167各1/1通过。设置前期运行样本Batches/DrawCalls70，SVG版本样本71；不据不同帧入口推断GPU提速。五图标原生顶点448/301/531/469/443，矢量提供单源维护与缩放便利，非零网格成本。

### 2026-10-13：补齐真实启动、直启和 LoopScroll 返回入口

- 真实冷启动 `AppEntranceColdStartupShowsRealLoadingAndCloses`（030ecfb2）、无人机真实Editor直启后重载/返回（3600c01e）、Showcase Main正式Cancel/同帧重复Cancel返回（56f9ac95），三项独立PlayMode验证3/3通过；不重置生产Singleton伪造入口。四Demo正常往返/重载和已有失败/忙碌保护沿用此前验证。
- 真实相机呈现与帧末截图位于 `Library/UIArt/entry-startup-20261012-235829-786/`、`entry-editor-direct-20261013-000035-031/`、`entry-loop-return-20261013-000206-138/`，报告均Complete并恢复临时输入、时间、画幅和偏好。冷启动实际阶段/进度单调推进并最终销毁；Loop返回先展示返回进度，再交接独立AppEntrance启动进度，不把两实例进度拼成一条。
- 原生空测试宿主已恢复单一、干净AppEntrance，未保存场景。两个无关ProjectSettings与EditorSettings原hash不变。测试使用正式EditorSimulate流程，不代表Player/IL2CPP或真实触屏/手柄验证。

## 早期装配与验证记录（历史）

以下记录保留发生时的状态与问题证据；其中“03仅提示”“尚未调用”“尚需复拍”等旧状态已被顶部当前状态及下方最新验证取代，不作为继续工作的指令。

- 执行顺序固定为01／02正式控件精确还原、截图对照与验收，然后才推进03设置行外观；03当前只处理按键提示。2026-10-13 07:17:33／07:17:50已再次原生复拍01／02，各含1920×1080与1280×720，报告Complete、Console Error=0；正式资源与新公共Slider源参与截图，示例摆放仍为预览夹具。已对照当前确认稿，不能把配置验证、截图完成等同于精确视觉验收通过。
- 新公共Slider／Filled配置传播专项报告`Library/UIArt/common-slider-progress-validation.txt`在07:17:21为Complete：4个源与Variant父链、7个实际实例，公共UIState／UIProgressBar启用状态和卡丁车／无人机风格RGB均能传播，10个相关资产bytes/meta/GUID全部恢复。编译128d157e无错误；此项仅证明配置继承，不扩大为运行时换肤或像素验收。

- 本轮已按01→02优先顺序完成真实Prefab装配复核，03设置行/滚动入口未调用。最新编译4874732c无错误；资源检查578962ce通过5/5；Dropdown异步收口39ee9f43与浮层借出/回夹/Modal/归还602e281a各通过1/1；Tips三种消息77968eb0与长短文本9af4938d各通过1/1。浮层测试初始作者矩形越界22px是正常Clamp，夹具扩大宿主并增加四角在边界内的前置断言，保留原角点/回夹/射线/恢复断言；Tips夹具补真实Shader资源加载，未改产品加载逻辑或弱化行为断言。
- 原生01截图在04:09:55、02在04:10:14复拍，1920×1080与1280×720报告均Complete，截图后Console错误0。720p截图浮层先完成Canvas布局再展开，修正预览初始化导致的错误回夹；02短文本scrollDistance=0，单行消息44.13。Commom_Title已改用公共HarmonyOS_CN、32号居中；02恢复字级/四色展示，WarningOutline使用预着色红色轮廓并保留白Tint。截图在 `Library/UIArt/Captures/ApprovedBoard{01,02}_{1920x1080,1280x720}_alpha1.0.png`；这些是正式资源在PreviewScene中的真实渲染，示例状态/文字/摆放及色板为明确夹具，不等于正式AppEntrance页面验收或新一轮性能结论。

- 按批准图调整全部基础外观，真实 Canvas 对照复核，再检查继承传播与实际交互。
- 最终审查的来源页/Busy缺口已修复：Demo来源使用覆盖且不隐藏的Loading，Wall/Kart导航命令拒绝忙碌，非成功返回复位退出状态；正式编译2a86f0d0无错误，现有WallSqueezeRuntimeTests新增长期公共契约方法DemoNavigationFailureAndBusyPreserveSourceViewsAndCommands在5aee3aa8通过1/1。成功卸载相关回归将在资源收口后定向执行，故障注入不作为真实资源场景成功证据。
- Drone Telemetry 三个 Equipment 提示已归入共享 DroneEquipmentInputPrompt 风格 Variant，解除页面橙色 RGB 覆盖，保留原 Alpha、字号与布局。原生换源/保存造成六个 Telemetry localFileID 引用失效，已按 HEAD、原迁移记录和实际调用方证据精确恢复；其余引用不允许空值，HUD 原 GUID/UIBind 保留。原生依赖读回与实际 HUD 定向测试通过，详见下方最新记录。
- 同步导航文档中的 View 收口/失败恢复描述、Wall 背景映射与 CommonLoading 顶部/底部 scrim。
- 本次临时 DemoFoundationVariantMigration、LoadingArtAssembly、UIArtValidation、LoadingSceneAcceptanceProbe、UIFoundationBaselineTemporary 及其 meta 尚待任务交付前清理；保留无关 ProjectSettings 改动，不 push。
- 新增临时 ApprovedFoundationArtAssembly、ApprovedSettingsArtAssembly、ApprovedPanelArtAssembly、ApprovedTabDropdownArtAssembly 也须清理。03拆为仅提示A与设置/滚动B入口，B须01/02验收后才执行，不自动调用。新增Toggle使用原生Toggle值驱动同节点UIState On/Off，不另加状态桥。
- 01/02三份原生装配报告均Complete；横向Tab为Tab的真实Variant，保留原嵌套模板，不解包；圆角/边框按九宫格比例收细。已修复双文字节点隐藏、Accordion宽度被自身Fitter收窄、正文二次合成粗体与消息条高度；CircleSuccess/CircleInfo保留SVG与PNG正式资源。四Demo按钮的表面/焦点九宫格比例在风格层保留，避免Hub边线变化覆盖其已有形状。
- 状态数组改动暴露了旧嵌套索引覆盖与深层保存丢引用，不能靠删除空条目收口。临时装配先归还精确艺术字段，必要时从已验证来源按唯一相对路径/组件类型恢复参数与引用，再通过SerializedObject逐字段写入并原生记录覆盖；保存前后均检查实际目标。三个无控制器/UIBind/动画用途的旧反馈子UIState已精确删除，根同一UIState+CanvasGroup统一反馈透明度，原反馈节点保留。最新编译f210461e无错误，资源测试88b0ffb7通过5/5，基础真实输入/Toggle/进度ac3a5e8b通过3/3，Accordion宽传播/展开/替换生命周期402fcbe2通过1/1。最新01/02截图仍需在资源修复后复拍，不以保存或测试代替视觉验收。
- 按用户清理授权，已将8组被替代图/提示词原稿核对归档到本机生成目录并从仓库删除，仅留5组当前图。旧Input三套品牌图标无正式引用，已原生删除；四个Generic箭头与LICENSE已迁至 `UI/Common/Sprites/Input/Generic/`，GUID与catalog引用保留。报告 `Library/UIArt/legacy-input-cleanup.txt` 为 Complete。

## 已有验证的边界

### 2026-10-13：01／02通过用户验收，开始03正式装配

- 用户分别回复“01 通过”“02 通过”，对应07:17的1920原生规范板；同批1280截图也已检查。之后才执行03 B入口，未提前推进设置行外观。
- 临时装配器先删除过时的Slider子图形重染，并使用Normal避免公共Bold字体再次合成加粗；03只维护行的标题、值、原因和控件列位置。编译37ff9e45无错误，`approved-settings-art.txt`为Complete，4设置行与TMPAutoScroll的GUID、原引用读回保留。执行前5资产与meta精确备份位于`Library/UIArt/Settings03-Before-20261013-072606`。
- 公共规范补齐字号/行距解释、间距档位、实际控件尺寸/图标比例/边框/阴影样本；四个Demo规范按正式Prefab与运行布局补准确角色字号、字重、制作参数和Variant使用方入口。没有创建字号主题、运行时换肤或动效规范。
- 03原生规范板、真实Hub设置页及长文本定向验证待完成；一次性Probe将额外核对实际Kart音量Slider值域、整数步进和valueChanged保存路径，精确恢复原赛车偏好。现有8/8是装配03之前的结果，不能冒充这轮通过。

#### 03装配后的最新验证

- 03板在07:39:10复拍，1920／1280报告Complete；两个右提示的总宽分别126.45／227.41，右边界都为1856设计单位，组合键内部顺序保留。最后一次TMP强制重建后通过原组件StartScroll及Canvas渲染前回调重新裁剪；没有添加预览Mask或手工切割网格。已检查两幅图片，03用户视觉验收仍待回复。
- 一次性截图工具因Core.Editor缺少InputSystem引用首次编译e757397c失败，已精确移动cs/meta至已有依赖的`Assets/Scripts/Hotfix/Editor/UIArtValidation.cs`，不改变任何正式asmdef；后续f3e5dc94、ef21468f、d9a7d1b0编译无错误。清理时使用新精确路径。
- TMPAutoScroll原三测试最初275c1fc3为0/3，失败均为旧x=0断言与新LayoutGroup起点不符；已改为验证文本矩形恢复到viewport内边距，裁剪断言进一步限制到内边距范围，其余停用、完成次数、可见字形及UV断言保留。810a3fe5为3/3通过，未新增永久测试文件或测试方法。
- 正式AppEntrance探针04df245b为1/1通过：四Demo真实进入／重载／返回，当前03设置页四画幅；真实Kart Music/Sfx分别测试0、1、5、10，对应保存0%、10%、50%、100%，原赛车偏好字符串/存在性精确恢复。截图与完整报告为`Library/UIArt/runtime-20261012-234029/`，已查看1920设置页及Kart50%音量页。
- 设置分页、候选隔离和一次Apply的现有契约c242a810为1/1通过；中英文重建与资源检查继续串行收口。性能样本仍为Graphic6→4、顶点64→112、材质/纹理与批次不变，不声明GPU overdraw或整体提速。
- 四Demo规范与公共规范的本地链接、制作源入口和尾空白检查通过。正式文档修正Dropdown Chevron路径、SwitchRow96×40/控件列锚点、Slider fillRect的Sliced胶囊与UIProgressBar白色Filled的区别；旧PRE-03记录仅作追溯。
- 后续d1b7147a中英文字体/真实页面重建/焦点契约1/1、689f2c19基础资源5/5通过。加上本节此前三组，本轮装配03后的最终相关结果共11/11；没有全量回归或Player/真实设备验收。剩余03用户视觉验收、完整交付审查、临时文件清理与范围内本地提交；当前Goal仍未完成。

### 2026-10-13：普通 Slider／Filled 原生迁移完成

- 上轮同一次迁移于07:01:01自然返回，`common-slider-progress-migration.txt` 最新 Status 为 Complete；没有重发、强杀或重启。完整事务备份位于 `SharedControls-80a9f905e72340548e8a804f41b8b663`，六个原资源的 GUID、原作者字段/矩形/事件与全部有意义引用经过保存前及回读核对。
- 公共 `CommonSlider.prefab`（GUID `b57a1e65e3ab9ed4f984048ce9266453`）从 SliderRow 提取并关联回原子节点；卡丁车 Music/Sfx 同用真实 `Foundation/KagitSlider.prefab` Variant。原生 Slider 值域、整数步进和回调由宿主管理，墨色图形与子布局集中在 Variant，未装配03行标题、数值或父布局。
- 公共 `CommonProgressFill.prefab`（GUID `f4f962eeb1f0538469646ee83aa9ea2f`）从 CommonLoading 提取并关联 CommonLoading/Startup 原填充。Startup 沿用直接 Image.fillAmount，新增 UIProgressBar 无自驱生命周期；无人机通过真实 `Foundation/DroneProgressFill.prefab` Variant 集中暖橙，Telemetry 源及 HUD 所有51绑定回读保留。至此本轮三个普通继承缺口已补齐，继承传播的专项复核另行进行。
- 相关定向测试 `d95b254d` 基础资源5/5、`3b06b476` 卡丁车真实设置/操作说明/返回1/1、`68e63e22` 无人机HUD绑定更新1/1、`445b5ee0` 正式AppEntrance四Demo进入/重载/返回1/1，共8/8；未执行全量回归。真实截图为 `Library/UIArt/runtime-20261012-230650/`，已查看1920设置 PRE03与卡丁车 Loading，场景事务和临时偏好恢复报告完整。
- 原生验证器反复逐引用重扫全树导致明显耗时热点，正在改为一次owner/path索引、组件批量Apply/Dispose与阶段即时报告；不得通过删除引用/作者值断言缩短耗时。现有运行/配置证据不扩大为所有风格的真实换肤点击或GPU透明overdraw验证。
- 公共组件手册、基础资源模块与卡丁车/无人机美术规范已同步新真实源入口。Console Error=0；EditorSettings按原bytes恢复0A411...，两个无关ProjectSettings hash不变，AppEntrance场景无任务修改。03设置行/滚动 B 未执行；完整Goal未完成，未提交或push。

### 2026-10-13：风格传播完成，普通 Slider／Filled 继承继续收口

- 临时装配与风格验证代码编译 `e027cc64` 无错误。原生 `button-style-and-skin-proof.txt` 为 Complete：24个真实按钮风格逐项修改源 RootImage RGB 与 Normal 同目标的颜色，并在各实际使用方核对五态配置；原 Alpha、结构、布局、文案、业务引用与回调保留。各源、页面与基础 Prefab 的 bytes/meta/GUID 全部恢复。
- 四个 Demo 的 Return／Exit／Continue／Resume 普通按钮在内存 Prefab Contents 中原生换为 CommonButton，页面不保存；原组件对象、UIBind 类型/键/回调、文案、矩形与交互参数保留，共同五态来源匹配。这是配置、对象及引用证据，不冒充真实运行中任意控件换肤后的点击或像素验收。
- 普通 Slider／Filled 的原生迁移首次在重复同名装饰节点的契约预检失败，未新增资产；定位改为规范名称＋同名兄弟序号后编译 `e6c87774` 无错误。第二次在公共 Slider 保存回读时，验证器错误把 `m_GameObject.m_FileID` 持久化身份当作者字段比较，已完整回滚。本次只修验证器的对象引用迭代，保留所有有意义的标量、引用、矩形及回调保护；不忽略真正整数配置。
- 迁移尚未成功，不将2公共源／2 Demo Variant 写为正式入口。两个卡丁车音量 Slider 和无人机重置填充的继承缺口仍待 native 保存、回读与运行验证。03 的设置行/滚动 B 入口未执行，未提交、未 push。
- 对象引用迭代修正编译 `4576f3a6` 通过；随后一次菜单在新资产前置检查因 Unity 默认 GUID 查询保留删除资产缓存而早停，旧报告时间未变，按当前 Console 识别并改为 `OnlyExistingAssets`，文件/meta存量保护保留。编译 `5d32a1ae` 通过后，06:48:17 仅发起一次新的迁移；HTTP 60秒观察超时没有作为执行终止证据，也没有重发。
- 当前事务 `Library/UIArt/Transactions/SharedControls-80a9f905e72340548e8a804f41b8b663/` 已有6原资源 current bytes/meta与阶段备份；4新Prefab于06:48:18–19生成，5原使用方已原生保存，HUD 于06:54:43原生保存后载入回读。报告仅 finally 写入，所以 `common-slider-progress-migration.txt` 仍是06:44的旧失败记录，不能作为本次结果。进程83608和同实例 health仍在，当前主线程忙/请求未收口；不干预进程或抢写回滚。代码发现引用验证反复重扫全树的性能热点，需本次收口后缓存owner/path、按组件一次Apply并记录阶段；这不是已证明的Native死循环。

### 2026-10-13：01/02 继承与绑定收口，03 仍仅提示

- Hub 的 Start、Settings、Effects 三个普通按钮已成为 CommonButton 的真实风格 Variant，保留当前 Gallery 表面、颜色、阴影及作者矩形。正式 UIBind 同步生成，原组件对象和回调保留；不以公共朱红外观覆盖当前 Gallery 页面。
- 最新基础行为传播检查 `current-button-inheritance-proof.txt` 覆盖四 Demo 与 Hub 共142个实际按钮，原基础资源 bytes/meta/GUID 完整恢复。它只证明基础行为传播，风格传播和替换仍须单独验证。
- 四个 View 的13处旧节点名绑定已通过正式 UIBind 生成与对应业务成员同步修正；`four-view-uibind-path-repair.txt` 为 Complete，保存前后原组件、回调、作者矩形、引用、View 层级/生命周期契约不变，回读路径漂移为0。随后继承检查无剩余 `BINDING_PATH_DRIFT`。
- 编译 `31e3c425` 无错误；新生成源码经 Editor 自动编译后，DroneFlightUiContractTests `49ed68d3` 4/4、DroneVehicleSelectionPlayModeTests `eedfa3b9` 3/3、正式 AppEntrance 四 Demo 进入/重载/返回探针 `d610d336` 1/1 通过。已有测试仅适配共同子节点的新正式路径，没有删除原失败恢复、设备与绑定断言。
- ImageExtend 在资源服务就绪后的 Shader 加载失败不再反复申请/刷新，重新启用允许重试；共享材质双所有者、最后释放及原材质所有权检查 `5d7ae5b6` 1/1 通过。此现有测试扩展保护共享材质长期生命周期契约，未新增永久测试文件。
- 提示长绑定名按原生 Sliced 空键帽自动宽度，保持无人机36px与 Equipment24px高度；切回真实图标恢复作者配置。BlankKeycap 仅添加32px Border，图片 bytes/GUID 不变。实际提示合同检查 `2f7fa7c9` 1/1；夹具激活真实 Equipment 显示生命周期，未缩短长键名或弱化高度/恢复断言。
- 初次真实图集专项在 EditMode 失败：Editor Pack 与自有单 Atlas Bundle 的 GetSprite 未提供有效 packed/texture 样本；当时没有将其记为“不串色”通过。失败报告保留诊断，各次临时 Atlas/PNG/meta/Bundle/manifest 精确清理，模式与设置 bytes 恢复；后续改在真实 PlayMode 验证，结果见下方。
- 03设置行/滚动文字 B 入口仍未执行；当前未提交、未 push，未执行全量回归、Player/IL2CPP 或真实设备验收。原生展板不等同于用户对还原度的新一次确认。
- 后续编译 `be036cbd` 无错误：Accordion 清理私有方法更名，消除遮蔽 Unity 已弃用成员的警告；ImageExtend 对实际观测到的 Sprite 纹理未绑定状态保留原生绘制，避免空纹理访问。最新正式入口截图目录为 `Library/UIArt/runtime-20261012-221150/`，Console Error=0，AppEntrance 无任务修改；Test Runner 原生重序列化造成的 EditorSettings 漂移已按任务前字节备份恢复，SHA256 为 `0A411B0DB4D2B04C6292227C30342653A9929CAA27A3B8CAF4CC58C2A3B204D9`。
- Demo 基础使用方只读补核：24个实际 Foundation 按钮风格（Drone8/DLSS1/Wall3/Kagit12）有真实来源与使用方；Demo 当前无普通 Tab/Dropdown/UIBtnSwitch/Toggle/Tips 使用，不为目录齐全创建空变体。仍有3个普通继承缺口待处理：KagitKartSettingsView 的 Music/Sfx 两个 Slider、TelemetryRoot 的 ResetProgressFill；径向威胁环为玩法专属保留。公共源应从现有 Slider/水平 Filled 提取并回接原使用方，保持单一实现，不以此提前装配03设置行。风格传播与四个普通按钮内存换肤验证正在准备，未记为通过。
- 图集两阶段夹具编译 `986e9bdb`、`6db30212` 无错误。PlayMode 首次 `d7cc96d5` 失败于夹具误把 `AssetDatabase.Contains` 当成 Editor 来源，按真实 Bundle 返回对象、原临时资产不存在及原路径查询为空修正来源判定；第二次 `855d3dc5` 已取得真正 packed/Rectangle/无旋转/mip1 的近邻样本，像素检查通过，但发现 Shader 缓存同步完成在 Canvas 重建内再次标脏的真实错误，因此测试仍判失败。
- ImageExtend 的 Shader 完成回调在 Canvas 正处于图形重建时延后到下一 Update，再刷新，并保护期间销毁。编译 `b919e6df` 无错误；严格原 packed/同纹理/近邻/矩形/无旋转/mip1 及原像素断言未放宽，重跑 `e4073793` 1/1 通过。1920×1080 和1280×720的源对照与 packed 目标 ROI 均为0品红，真实邻图校准分别14400/6400像素；Mip 拒绝回退为 UI/Default。两分辨率 PNG 已查看。未将 Tight/Rotation 拒绝作为像素验证或将此样本扩大为全配置性能结论。
- 最新共享材质测试 `1013200a` 1/1：现有公共生命周期方法增加真实 Canvas 与 ForceUpdateCanvases，保护重建时同步资源完成以及双所有者释放契约。全部临时 Atlas 资产、Bundle四文件/空目录及跨域路径记录均已清理，运行夹具前后设置原bytes与两保护hash相同；Test Runner 的 EditorSettings 原生重序列化由主代理最终恢复。本轮相关新增/重跑最终通过10/10（页面4+机型3+入口1+图集1+生命周期1），未执行全量回归。

### 最新：01/02优先还原，03仅独立提示

- 本轮正式编译 `50e2c045` 与仅截图夹具更新的 `e854c33b` 无错误。01补完整 Hover 红色细边、Toggle 禁用白勾；02面板标题区改为64高/顶部12，浮层阴影 alpha 0.14，页面标题继续居中、面板标题左对齐。作者根矩形、关闭命中、引用与 GUID 保留。展板辅助文案灰色与横向色板属于明确夹具，复用公共中英字体；不以夹具替代正式资源。
- 设置页五个分类的旧 states 数组索引覆盖与字体覆盖已精确归还公共 TabItem 来源；作者图标保留原 Rect，排除出公共布局并留64左距。实际截图中五个分类均可见、字体24、图标在文字左侧，无额外状态管理器。03的四类设置行与 TMPAutoScroll 装配入口 B 未执行。
- 提示 A 原生报告 `approved-input-prompt-art.txt` 为 Complete。Hub 键位使用现有 Sliced FocusBorder 细线框、最小36及原生自动宽度；12个 Demo 提示保留自己的目录、颜色、字号与图片风格。Equipment 三项共享 Variant，仅集中 RGB；HUD六处真实引用已原生恢复并验证依赖，无旧 localFileID 残留。
- 定向测试：提示自动宽度/顺序/父布局所有权 `4310b577` 1/1；实际无人机 HUD 遥测绑定及设备切换 `c67388db` 1/1；基础资源 `de4a879d` 5/5；临时正式 AppEntrance 探针 `8375a68b` 1/1，四个 Demo 共12次进入/重载/返回成功，有实际加载渲染帧，状态及临时偏好已恢复。此前定向失败恢复/Busy、Loading遮挡和设置关闭五项各1/1通过（b2bd6c0d、070ae3d1、e0ac832b、f40a2db4、b9987af7）。这些是相关范围验证，未执行全量回归。
- 最新01/02原生展板为 `Library/UIArt/Captures/ApprovedBoard{01,02}_{1920x1080,1280x720}_alpha1.0.png`，报告 Complete。正式入口证据 `Library/UIArt/runtime-20261012-211309/`，包含 Hub 与设置页四画幅1920×1080、1280×720、2560×1080、2340×1080截图；设置页明确标 PRE03。截图已查看，Console Error=0；超宽画幅并非真实触屏设备验收。
- 同场景、同矩形、Focused 状态的实际样本为 Graphic 6→4、顶点64→112、材质2→2、纹理3→3、批次/DrawCall 33→33。边框绘制顺序调整消除上一轮新增批次，但不声称 GPU 提速或已测透明 overdraw。
- 完整 Goal 仍在进行；本轮未推进03设置行、未提交、未 push。两个无关 ProjectSettings 哈希未变，AppEntrance 场景无任务改动；临时装配与探针需在完整任务交付前清理。

最新原生截图位于 `Library/UIArt/Captures/`；真实 AppEntrance 四 Demo 进入、重载、返回探针通过，报告 `Library/UIArt/runtime-20261012-162434/report.txt`。最终定向测试批 37/37 通过（`Library/UIArt/final-tests.json`），但成功路径通过不覆盖上述审查发现的失败/Busy缺口，也不证明外观符合新图。

受控继承验证与原始资源恢复均 Complete，19 项 bytes/meta/SHA/GUID 检查通过；证据 `Library/UIArt/prompt-inheritance-proof.txt`。性能同场景样本为 Graphic 6→3、顶点 64→80、批次/DrawCall 33→33，不声称提速。未执行全量回归、Player/IL2CPP 或真实设备验收。
