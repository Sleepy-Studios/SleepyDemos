# 使用 Core 基础 UI 组件

## 目标

本 runbook 面向 Hotfix 页面和 Demo 入口，说明 `Core.Runtime` 中基础交互组件、通用 UGUI/TMP 表现组件和全局扩展的选型规则。交互组件默认分帧初始化或异步资源加载；确需立即完成时显式传 `isAsync: false`。

## 通用规则

- 写页面或新增公共 UI 前，先从下方按需求选型，再核对组件源码、基础 Prefab 和现有调用方。只有现有能力不能覆盖实际需求时才扩展；扩展后同步本入口和对应模块文档。
- 每个组件只使用一个主入口：`Init(...)`、`SetData(...)` 或 `SetImage(...)`。
- `Register` 表示追加回调，`Unregister` 表示移除回调，`SetAction` 表示覆盖回调。
- `notify` 控制本次设置是否触发回调；静默回显或初始化默认值时传 `false`。
- `isAsync` 控制初始化和资源加载方式；`false` 为同步，默认 `true` 为异步；不控制 UIManager 导航、动画或网络。
- 图片参数统一传 Sprite 资源路径，不传图集名、不传缩放值。
- 外观制作先读宿主规范与[公共 UI 美术规范](../architecture/ui-art-direction.md)，按下方流程制作和更换风格；基础行为、Demo 外观与页面业务分别维护。

## 组件选型

| 组件 | 适用场景 |
| --- | --- |
| `UITab` | 有限数量 Tab，只负责选中态、文案、图标和点击回调 |
| `AccordionTab` | 两级手风琴 Tab，回调使用扁平化叶子索引 |
| `ViewTab` | 用 `UITab` 驱动多个独立 View；设置、商店等分类页优先使用 `views` 分支，维护各子页的加载、显隐和销毁生命周期 |
| `AccordionViewTab` | 用 `AccordionTab` 叶子索引驱动多个 View 或本地分页对象 |
| `ViewList` | 有限 View 列表，不承担循环滚动 |
| `LoopScrollView` + `ItemView` | 数据条目多、同类条目需要回收复用的列表；通过 `LoopScrollRegisterExtend` 接入，使用稳定数据 Key |
| 原生 `ScrollRect` + LayoutGroup | 数量有限、控件不同的固定表单，例如声音滑条、画质开关；不为少量异构行增加列表复用和数据绑定 |
| `UIDropdown` | 基于 `UITab` 的基础下拉选择，默认文案绑定 TMP_Text |
| `UIBtnSwitch` | 二态按钮逻辑；BtnSwitch1 为轨道内双文字滑块，BtnSwitch2 为圆形滑块，二者均整块点击切换 |
| 原生 `Toggle` + `UIState` | 独立勾选框；Toggle 负责值/回调，UIState 读取 On/Off 并叠加五态，不添加交互状态桥 |
| 原生 `Slider` + `UIState` | 共用 Slider 的数值、拖动与导航，UIState 负责五态反馈；宿主维护范围、步长和事件 |
| `UIState` | 同节点 Selectable 自动驱动五态，Selected/On/Off 按需追加在同一组件；无 Selectable 时手动切换 |
| `UIMenuScope` | 页面/弹窗的焦点导航与返回，复用公共输入模块 |
| `InputBindingPrompt` / `InputCommandButton` | 有效绑定图标与动作文案 / 可点击、可保持的触控命令；复用 Kenney 图标目录 |
| `UISettingRow` | 对现有 Dropdown、Slider、Switch、Button 的设置行引用与回显；不负责分类切换、保存或输入会话 |
| `UIImageLoader` | 按资源路径给 `Image` 加载 Sprite |
| `TMPAutoFitLayoutElement` | TMP 文本按最大宽高自适应 LayoutElement 或自身尺寸 |
| `TMPAutoScrollEnableBehaviour` | 单行 TMP 超宽后横向自动滚动 |
| `TMP_UGUI_Extend` | TMP 渐变、描边、阴影、倾斜复合效果 |
| `ImageExtend` | 原生 Image 的翻转、旋转、水平/垂直渐变、圆角、细描边与轻阴影；不用于 RawImage、TMP 或第三方材质 |
| `UIVertexRectMask2D` | 不改材质的矩形硬裁剪 |
| `PyramidLayoutGroup` | 固定尺寸品字或三角形布局 |
| `FlowLayoutGroup` | 不同尺寸子项按宽度换行或按高度换列 |
| `TMPLinkHandler` | 监听 TMP `<link>` 的 linkId 点击事件 |

## 从需求找到资源和示例

下面路径相对于项目根目录。公共控件入口为 `Assets/Scripts/Core/Runtime/Components/`；输入相关组件在 `Assets/Scripts/Core/Runtime/Input/`。基础资源以 [UI 预制体检查范围](../modules/ui-foundation-assets.md) 为准，不能只看名字相似就另建组件。

| 需求 | 复用入口 / 资源 | 接入依据 |
|---|---|---|
| 分类按钮，仅选中回调 | `UITab`；`UI/Common/_TemplateInstantiatePrefab/Tab/Tab.prefab` | `Init` / `Register`；保留模板自带图标引用 |
| 横向分类按钮 | `UITab`；`UI/Common/_TemplateInstantiatePrefab/Tab/TabHorizontal.prefab` | 继承 Tab 与原嵌套 TabItem；只改横向布局和下划线，调用契约相同 |
| 分类对应独立业务页面 | `ViewTab`；`UI/Common/_TemplateInstantiatePrefab/Tab/ViewTabVertical.prefab` | `Init(views: ...)`，各分类自己的 View、Prefab 和生命周期；不要改为一个脚本手动切全部区域 |
| 大量同类数据条目 | `LoopScrollView`、`LoopScrollItemViewBridge`、`LoopScrollRegisterExtend` | [循环列表接入](use-loop-scroll.md)；现有 `MainMenuView` / `MainMenuDemoItemView` 展示数据绑定与条目复用 |
| 下拉候选、开关、普通按钮 | `UIDropdown`、`UIBtnSwitch`；`_TemplateInstantiatePrefab/Dropdown/Dropdown`、`Btns/BtnSwitch1`、`Btns/BtnSwitch2`、`Btns/CommonButton` | BtnSwitch1 表现二态选择，BtnSwitch2 表现滑块开关；Hub 二态偏好使用 SwitchRow 内的 BtnSwitch2 |
| 固定设置表单行 | `UISettingRow`；`UI/Common/Settings/{Dropdown,Slider,Switch,Action}Row.prefab` | 复用对应基础控件，业务分页绑定独立候选；示例 `Hotfix/Module/Settings/UI` |
| 独立滑条或设置行内的滑条 | 原生 `Slider`；`UI/Common/_TemplateInstantiatePrefab/CommonSlider.prefab` | SliderRow 嵌套同一源；Demo 从该源制作 Variant，保留宿主范围、整数步进、值与回调 |
| 普通水平进度填充 | `UIProgressBar`；`UI/Common/_TemplateInstantiatePrefab/CommonProgressFill.prefab` | 根是白色水平 Filled Image；宿主维护背景、说明和进度，不复制另一套填充行为 |
| 设备按键提示与触控操作 | `InputBindingPrompt`、`InputCommandButton`；`UI/Common/Input/` | [公共输入](use-gameplay-input.md)；有效会话绑定、图标配置与触控释放共用底座 |
| 保存反馈或控件说明 | `CommonTips`、`SimpleTips`；`SingleUIManager` | 本文“两类 Tips”；不要再造 Toast / Tooltip 宿主 |
| 进度、倒计时、超宽文本 | `UIProgressBar`、`UICountdown`、`TMPAutoScrollEnableBehaviour` | 本文相关小节；使用已维护的时间和布局入口 |

表中 `UI/...` 资源路径相对于 `Assets/LoadResources/`。每个 Demo 从基础 Prefab 建立风格 Variant，集中覆盖图片、公共字体组、颜色、主副字号、边框和阴影；页面实例维护业务参数和必要布局。公共 API 和生命周期一致，卡丁车等已有美术风格保持其作者资源。

## 制作与更换控件风格

四 Demo 的完整风格入口统一为 `Demos/<DemoId>/Prefabs/UI/Foundation/`。使用 `Drone`、`Dlss`、`Wall`、`KagitKart` 前缀及用途名，不按字号或哈希找资源；尚未使用及与公共外观相同的控件也有本 Demo Variant。28项公共基础源的映射见[完整风格库](../modules/ui-foundation-assets.md#四-demo-完整风格库)。

组合 Variant 内部也须选择本 Demo 子变体：例如设置 SliderRow 的 Slider、BG 的 Title/关闭按钮、Tab 的 TabItem，以及 Dropdown/折叠页签引用的动态模板。查真实嵌套源与序列化模板引用，保留根矩形、事件、UIBind、InputCommandButton 和 GlyphSlot，不以另造同名对象修补引用。原先 `Parts/` 或 `Widgets/` 的基础输入提示已统一到 Foundation，专属 HUD 部件与成绩行仍保留原职责。

1. 读[公共 UI 美术规范](../architecture/ui-art-direction.md)和该 Demo 的美术规范，确认当前宿主、主要/辅助信息、最长文案和支持画幅；在本手册、基础资源与调用方中查找已有控件。
2. 在 [Game UI Database](https://www.gameuidatabase.com/) 查看对应游戏场景的实际截图，记录参考页面与借鉴结构；用效果图确认本次外观。Loading 启动 v3 与运行期 v2 已确认并装配；启动 v2 仅布局获认可，海岛背景不实施，v1 均已否决。新页面仍须逐项确认外观，不能把未确认的效果图当作批准结果。
3. 在 Project 找到正式公共基础 Prefab，使用 Unity 的 Create → Prefab Variant，在 `Demos/<DemoId>/Prefabs/UI/Foundation/` 保存风格资产；进入 Prefab Mode 修改图片、颜色、主副字号、边框和阴影。保留公共结构、组件与布局行为，UIState 五态的表现引用继续指向对应装饰。
4. 把 Demo Variant 嵌入页面；实例只设置文案/动作/数据、可见性、位置与必要布局。检查 Overrides，逐属性 Revert 重复样式覆盖，让实例继承风格源；先确认业务绑定及布局用途，禁止整树 Revert、Unpack 或删除原节点后创建同名替代。
5. 对已断继承的控件，在页面 Prefab Mode 中先核对其业务字段、事件、UIBind、嵌套引用和必要矩形，再替换为正式 Variant并重绑。通过正式 UIBind 更新受影响的引用，核对没有缺失脚本或外部节点；保留原页面 GUID，不能只复制图片使断继承看起来一致。
6. 开发时更换整套风格，在该 Demo 的 Variant 集中修改外观，或将页面使用源改为同一基础结构的新风格 Variant；检查所有调用方是否仍有旧图片、色值、字号、边框和阴影覆盖。基础源的结构/行为变化则检查四 Demo 及其页面是否继承，不能靠每页补丁修传播。
7. 通过正式 AppEntrance 查看实际页面和真实 Canvas 截图；检查中英长文案、安全区、正常/悬停/聚焦/按下/禁用、开关业务态及启停恢复。编译、测试与视觉结果分开记录，按[测试架构](../architecture/testing.md)选最小相关验证，不因普通样式制作新增永久测试。

继承中的内部 Prefab 实例不能直接按外层实例的方式换源。确需切换子风格时，在 Demo Variant 内保存“移除旧继承实例 + 新增本 Demo 子 Variant”的覆盖，按稳定层级、组件类型和同类型索引映射作者参数及全部业务引用；保留准确字号、布局、文案、事件与附加内容，不 Unpack。不能把原共有 UIState/Graphic 样式整套拷贝到组合内，不能对嵌套源重复套色；共有结构和样式由子 Variant 继续维护，有实际用途的差异按属性保留。替换后通过正式绑定流程核对引用，并临时修改子风格源验证组合中的继承，结束或失败都恢复完整字节与 GUID。页面已使用本 Demo 风格时不重复迁移。

共享面板/焦点边统一维护在 `Assets/LoadResources/UI/Common/Sprites/Surfaces/Surface.png` 和 `FocusBorder.png`，原 Hub 图片已原生移动并保留 GUID。直角宿主使用同目录 `FocusBorderSquare.png`（24×24、Border 3），夹爆它三个按钮风格 Variant 已接入。普通圆角/边框优先使用带 Border 的 Sliced Sprite；特殊需求再使用 ImageExtend。

## 两种按键提示

| 资源 | 阅读与布局 |
|---|---|
| `UI/Common/Input/InputPrompt.prefab` | 图标组在前、动作文字在后；垂直居中、内部靠左；用于左侧或中部提示 |
| `UI/Common/Input/InputPrompt2.prefab` | 继承提示1，文字在前、图标组在后；垂直居中、内部靠右，宽度变化保持右边界 |
| `UI/Common/Input/InputPromptButton.prefab` | 可点击/保持命令 Variant；保持已有 300×44 制作矩形，水平 Fitter 使用 Unconstrained |

两种普通提示用 HorizontalLayoutGroup 提供 preferredWidth，独立放置时 ContentSizeFitter 自动计算总宽；父 LayoutGroup 接管子宽度时公共组件让 Fitter 使用 Unconstrained，避免双重水平尺寸驱动。父页面继续负责位置、对齐和可用区域，不手算字符串宽度或用 Screen.width 调整。

提示2只交换 Caption 与 Glyphs 组的阅读顺序，槽内 Ctrl + K 等实际组合顺序不反转。复用 InputBindingPrompt、GlyphSlot、InputBindingDisplay 与公共字体，绑定有效会话动作并使用 SetCaptionKey/SetCaption；不复制解析代码或固定键名。缺正式图标时有 KeyboardBlank 则显示键帽和键名；缺全部可显示图形或槽不足时显示完整真实绑定文本，提示1为“绑定 文案”，提示2为“文案 绑定”。触控命令的保持/释放仍由 InputCommandButton 负责。

四 Demo 的对应风格资产为 `DlssInputPrompt2`、`DroneInputPrompt2`、`WallInputPrompt2` 及 `KagitKartInputPrompt2`（统一在各自 `Prefabs/UI/Foundation/`），与各自提示1共同维护同一宿主外观。新增页面选择对应 Demo Variant，避免实例再覆盖同一字体、色值和图片。

Hub 使用 `UI/Common/Input/HubInputGlyphCatalog.asset`，键盘名配合公共 FocusBorder 九宫格薄帽；四 Demo 使用原 `InputGlyphCatalog.asset` 与 Kenney 资源，保留宿主配色和原图标。原目录和 Generic 四个箭头仍有有效引用，不能作为废弃资源删除。缺品牌图标的长键名使用带 Border 的空帽横向扩展，保留作者36或24高度；真实图标仍保留比例。组件在切换回真实图标时恢复作者宽度、边距及布局参数，不修改点击矩形或反转组合键。

版本选择以提示自身的阅读和对齐方式为准：右侧卡片内从左开始阅读的说明行仍用提示1。明确需要提示2的使用方是 `MainMenuView/GalleryContent/InputHints` 的 `Navigate`、`Submit`，以及无人机 `TelemetryRoot` 的 `GearPanel/GearOperationPrompt`、`CameraPanel/CameraOperationPrompt`、`ResetPanel/ResetOperationPrompt`。主菜单父提示组使用 MiddleRight，保留原矩形和子顺序；独立提示改为右侧 pivot 时保持替换前的矩形边界，宽度随后向左增长。这些装配须通过原生 Prefab 替换完成，保留页面 GUID、真实动作、文案和 GlyphSlot 引用，并清掉阻断提示2结构的左对齐／顺序覆盖。可点击提示继续保持作者矩形。

继承验收检查真实基础源、四 Demo Variant 及页面实例：基础新增子结构或调整未被风格覆盖的布局字段，应向使用方传播；风格源调整颜色后，同风格使用方应一起更新。公共按钮的 UIState／InteractionFeedback 必须来自公共源。替换风格时替换页面嵌套实例的来源，并核对 ComponentItemIndex、动作、文案和内部槽位引用；Unity 不支持直接替换 Variant 的父级。需要临时修改正式源做验证时，先保存当前所有未提交内容及 `.meta` 的完整备份，结束或失败都恢复并比较完整内容、哈希与 GUID。资源继承证据不能替代正式页面截图、真实输入和 Profiler 采样。

循环列表的判断依据是数据是否需要同类条目复用，不是页面上出现了滚动条。分类通常只有少量 Tab；固定设置表单使用原生布局；动作及绑定数量随宿主变化的控制列表使用循环列表。不要把所有设置项包装成同一个通用数据列表，也不要把大量动作全部实例化在 LayoutGroup 下。

滚动表单中的控件保持点击区域尺寸稳定，交互五态优先修改颜色、边框或独立装饰节点。设置行曾因按下时缩放 Button 根节点导致抬手命中滚动视口、点击丢失，不能直接照搬模板的根节点缩放。普通 ScrollRect 的键盘／手柄焦点显露由 UIMenuScope 处理；循环列表继续使用自己的导航。循环条目中的改键、重置等所有可点击操作都使用 LoopScrollMenuButton 的回收身份保护。

采集新按键等临时输入使用 `UIMenuScope.SetNavigationEnabled(false)` 保留指针、暂停原生 Submit/Navigate；结束或取消后传 `true`，复用 MenuInputScope 的松键门闩恢复导航。不要在业务页手写 EventSystem 导航状态快照或永久排除 Enter／手柄确认键。

固定分类已为图标预留文字区域时，停用模板 Desc 上按文本宽度驱动的 ContentSizeFitter；长译文使用 TMP 原生自动字号和制作侧上下限。否则文字矩形会被重新撑宽，即使开启自动字号也仍可能覆盖图标。不要在业务代码中逐语言计算文字位置。

下拉选项浮层须有独立不透明背景，覆盖选项之间的间距；背景高度随实际选项数量更新。公共 `DropdownRow` 在 `TabView` 使用原生 Image 和纵向 Preferred Size 的 ContentSizeFitter，不按制作模板的初始项数固定背景高度。页面继续通过 UIDropdown 指定同页 popupRoot，不自行创建第二套浮层。

基础 Dropdown 的 Arrow 绑定 Image，使用 `Assets/LoadResources/UI/Common/Sprites/Controls/ChevronDown.png`，不要用 TMP 字符代替图标；UIDropdown 仍负责展开 180° 旋转及收起恢复。更换表现组件后保留原 arrow Transform 引用，不另加旋转脚本。`Sprites/Tips/Arrow.png`仍由定位说明使用。

TabItem 的 ChooseImg 使用全拉伸锚点，跟随页签根矩形；页面覆盖页签尺寸时不要保留模板固定尺寸的选中背景，否则背景会遮挡相邻内容。体验页的分类区域高 62，三个页签各宽 292、高 62。

## 正式调用示例与维护入口

新增页面前先看组件，再看下列实际调用；不要复制整个业务页面作为基础组件。

| 要接入的能力 | 当前项目示例 |
|---|---|
| 分类与独立子页生命周期 | [HubSettingsView](../../Assets/Scripts/Hotfix/Module/Settings/HubSettingsView/View/HubSettingsView.cs) 与 [GraphicsSettingsPanelView](../../Assets/Scripts/Hotfix/Module/Settings/GraphicsSettingsPanelView/View/GraphicsSettingsPanelView.cs) 等独立分页；宿主持有候选和子 View，交给 `ViewTab.Init(views: ...)` 异步加载与切换 |
| 同类绑定条目回收与回显 | [InputSettingsPanelView](../../Assets/Scripts/Hotfix/Module/Settings/InputSettingsPanelView/View/InputSettingsPanelView.cs) 与 [SettingsBindingItemView](../../Assets/Scripts/Hotfix/Module/Settings/SettingsBindingItemView/View/SettingsBindingItemView.cs)，通过 LoopScroll 注册同类绑定条目，隐藏或回收时清理旧动作回调 |
| 固定异构控件的静默回显 | [AudioSettingsPanelView](../../Assets/Scripts/Hotfix/Module/Settings/AudioSettingsPanelView/View/AudioSettingsPanelView.cs) 与 [UISettingRow](../../Assets/Scripts/Core/Runtime/Components/UISettingRow.cs)，使用普通布局及现有控件，替换回调并编辑候选；不由行组件保存玩家设置 |
| 提示图标与一次性动作文案 | [公共输入接入步骤](use-gameplay-input.md)，使用真实会话绑定、`SetCaptionKey` 和正式提示 Prefab；不要拼固定键名 |

公共组件行为变更时，在本手册更新适用条件和调用示例；正式基础资源新增或替换时，在 [基础预制体维护](../modules/ui-foundation-assets.md) 登记路径、引用及验证范围。专属外观只记录在对应 Demo，公共生命周期与输入规则继续引用这里。检查实际资源和调用方后，再决定是否需要提取新组件。

## 常用调用

```csharp
tab.Init(
    desc: labels,
    itemImages: iconPaths,
    initIndex: 0,
    notify: false,
    action: OnTabReady,
    isAsync: true);
tab.Register(OnTabSelected);
```

```csharp
viewTab.Init(
    desc: labels,
    views: views,
    itemImages: iconPaths,
    index: 0,
    enableAnimation: true,
    isAsync: true);
viewTab.Register(OnViewTabSelected);
```

```csharp
dropdown.SetData(
    values: options,
    action: OnDropdownSelected,
    selectedIndex: 0,
    selectedText: null,
    itemImages: optionIconPaths,
    showStateChanged: OnDropdownShowStateChanged,
    isAsync: true);
dropdown.SetSelectedIndex(savedIndex);
```

下拉控件位于 ScrollRect 或异构表单内时，调用 `dropdown.SetPopupRoot(popupRoot)`，把选项临时挂到同一 View 内、裁剪区域外的容器。设置页使用自己的 controls，体验页使用 Menu；容器保留该页 CanvasGroup 和菜单作用域。展开不会改变表单行顺序，收起、禁用或销毁会恢复原父级及布局，后来打开的 Modal 仍在其上方。可容纳的列表按实际容器尺寸夹回边界；超长选项仍由制作侧配置合适尺寸与滚动，不使用全局高排序 Canvas 盖住其它页面。

只禁用 UIDropdown 组件时立即归还选项；祖先启停或销毁时先收起，再延后归还，避免在 Unity 激活遍历中改变层级。原宿主已销毁则一并销毁借出的选项。公共 DropdownRow 的选项锚定控件下边缘、顶部 Pivot，空间足够时向下展开；背景随选项高度变化，列表宽度跟随字段。

页面持续更新复用 `View.BindUpdate`；隐藏时暂停、销毁时停止，等待下一轮更新后再次检查实例存活。场景或 Editor 直接销毁实例也不能继续调用页面制作组件，不在每个业务页面重复维护轮询。

```csharp
switchButton.SetAction(OnSwitchChanged);
switchButton.SetStatus(savedValue, notify: false);
switchButton.Register(OnSwitchAnalytics);
```

公共 SwitchRow 保留行根与 UISettingRow，内部嵌套 BtnSwitch2；Hub 偏好 13 处、体验页 11 处共 24 个使用方复用同一行。迁移内部 Control 只重绑行组件的 control、toggle、onLabel、offLabel，保持页面引用的行根组件不变。减少动态的 Auto/On/Off 继续使用 Dropdown，语言选择继续使用 UITab。旧 BtnSwitch 重命名为 BtnSwitch1 后 GUID 保持不变，已有选择型引用无需改写。

```csharp
imageLoader.SetImage("LoadResources/UI/Icons/IconStart", setNativeSize: true, isAsync: false);
imageLoader.Clear();
```

```csharp
gameObject.Show();
button.Hide();
LayoutElement layout = gameObject.GetOrAddComponent<LayoutElement>();
rectTransform.SetSize(new Vector2(320f, 80f));
rectTransform.SetAnchoredPositionX(24f);
canvasGroup.SetCanvasGroupVisible(false);
```

`Show/Hide` 只操作 GameObject 激活状态。纯透明度变化优先设置 `CanvasGroup.alpha`，它会与子元素自身 alpha 相乘；多层组也会累计衰减。需要同时收起交互和射线时使用 `SetCanvasGroupVisible(false)`，该入口同步 alpha、interactable、blocksRaycasts，不改变激活状态。

直接拖入 `Assets/LoadResources/UI/Common/TMPAutoScroll.prefab`，绑定子节点 `Text` 的 TMP 后赋值即可；无需 ItemView 或 SubViewWithGameObject。自己装配时在 Inspector 绑定 viewport 和 TMP，也可显式初始化：

```csharp
autoScroll.Initialize(viewport, titleText);
autoScroll.SetOptions(TMPAutoScrollOptions.Default);
titleText.text = title;
```

组件会监听 TMP 顶点脏回调，因此业务继续直接写 `titleText.text = title` 也能重新测宽；通过文字顶点与四维 UV 裁剪，不添加独立遮罩。短文本不滚动并恢复原位置与 TMP 格式。配置 `UseUnscaledTime = true` 可在暂停游戏时继续滚动；`Loop = false` 时，`ScrollCompleted` 在末端停留完成后触发，短文本测量完成即触发。修改文本、字号或可用宽度会重新测量，禁用时复位，重新启用时重新开始。

### ImageExtend 图片组合效果

在原生 Image 同节点添加 ImageExtend，在 Inspector 按需展开图片变换、渐变、圆角、描边和阴影。翻转/旋转作用于网格，渐变按变换后坐标采样并与 Image.color 相乘；RectTransform、布局与原生点击矩形保持原有语义。只改变整组透明度时优先 CanvasGroup，不逐个改子 Image 的 alpha。

Inspector 顶部的「中文 | English」切换控制标签、枚举、提示和警告，首次默认中文，本机 EditorPrefs 记住选择并供所有 ImageExtend Inspector 共用；不写入 Prefab，也不跟随游戏语言。独立半径按左上、右上、右下、左下配置。

「边缘羽化 / Edge Feather」仅在圆角启用时生效，运行时通过 `EdgeFeather` 修改，单位与圆角半径一样为 UI 本地单位。默认 0 仅使用 `fwidth` 自动抗锯齿，无需手工适配缩放。正值表示以原轮廓为中心的总透明过渡宽度，内外各半，例如 12 表示向内、向外各 6；有效过渡宽度不会小于当前像素抗锯齿宽度。它不是整图模糊，也不是阴影柔化。Simple／Sliced 的原图外沿随羽化扩展，边缘纹理在自身 Sprite 范围内延伸，九宫格内部保持原位；RectTransform、布局和点击矩形不变。手动羽化不支持 Filled／Tiled、Use Sprite Mesh、Mask 写入节点、旋转／紧密图集或 Mipmap，这些组合会提示并保留原生绘制；羽化为 0 时保留原有支持范围，作为遮罩子节点时仍受 Mask／RectMask2D 裁剪。很大的宽度会降低小图片内部的不透明度，缩放后应检查真实显示效果。描边与阴影沿同一羽化轮廓计算，外扩装饰仍遵循下述支持限制。

Shader 位于 `Assets/LoadResources/Art/Shaders/UIImageExtend.shader`，运行时资源地址为 `LoadResources/Art/Shaders/UIImageExtend`，组件自动开启所属 Canvas 的 TexCoord1/TexCoord2。纯翻转、旋转和渐变不需要该特效材质。组件不创建或管理遮罩；需要遮罩时显式添加原生 Mask 或 RectMask2D，圆角图片效果与遮罩各自配置。

Shader 通过既有资源服务加载，就绪前保持原生图片。已确认的加载失败不在每次网格刷新时重复请求，重新启用组件可重试；缓存同步完成时避开当前 Canvas 图形重建再刷新。透明轮廓 Sprite 的纹理尚未绑定时也保留原生绘制，不能把尚未绑定的图集样本当作轮廓效果验收。

圆角、描边和阴影仅适用于默认 UI 材质；首版不处理 RawImage、TMP、第三方材质及其他网格效果叠加。不支持的配置由 Inspector 给出原因并保留原生 Image 绘制，不靠页面另挂 Outline、Shadow 或旧效果组件补齐。Filled/Tiled 不支持外扩描边和阴影；普通进度通过独立背景/遮罩制作圆角，仍保持白色 Filled 和原填充方向。

描边宽度为 0～8、阴影柔化为 0～16 个 UI 本地单位，仅用于细轮廓和轻阴影。面板模式按图片绘制矩形计算；需要按透明轮廓时启用 Use Sprite Alpha，使用带透明背景的 Simple Sprite、关闭 Use Sprite Mesh，图集关闭 Rotation/Tight Packing/Mipmap。外置 Alpha 纹理保留原生图片显示，Inspector 提示圆角／描边／阴影材质不支持该组合。检查图集边界与相邻图片，不把固定少量采样当作大范围高质量模糊；大阴影使用预制作图片。

描边/阴影 RGB 使用独立颜色，透明度继续跟随图片渐变 alpha 和 CanvasGroup；切换参数、禁用与销毁由组件维护共享派生材质引用和资源 Loader。业务不修改 Graphic.defaultGraphicMaterial 或共享字体材质。渐变按实际网格顶点采样，中间颜色键是否能表现须看真实图片网格与截图，不能仅凭 Inspector 渐变条判断效果。

共享键只包含基础材质、效果 Shader 及实际生效的圆角、羽化、描边、阴影参数；关闭效果的颜色、偏移等不拆分材质，透明轮廓模式和尺寸由顶点传递。不同基础材质（包含原生遮罩状态）保持隔离，最后一个使用者释放后转入闲置 LRU，再次命中时复用；闲置容量集中由 `ImageExtend.IdleMaterialCapacity`（64）控制，活动项不计入容量且不会被 LRU 淘汰。每次新增闲置项即按最久未使用顺序收敛容量，无需每帧扫描。`ClearUnusedMaterials()` 可主动清理闲置项而保留活动项；失效的基础材质／资源依赖在后续缓存访问或释放时移除，播放模式切换、程序集重载、Editor 退出与 SubsystemRegistration 重置全部缓存。缓存和组件共同持有 Shader 加载器，淘汰或最终清理时释放缓存的依赖引用。LRU 用于减少重复创建，不代表减少 Draw Call。Inspector 下方只读诊断显示 CanvasRenderer 当前已提交材质的名称和实例 ID，重建前可能仍是上一帧值；同 ID 表示共享材质，不保证合批，纹理、遮罩、Canvas 和层级仍会影响合批。

`TMP_UGUI_Extend` 的描边应配合 `TextMeshPro/Mobile/Distance Field` 材质使用。项目版本在 Unity 6 Shader 基础上保留了钓鱼项目的外描边字面补偿；重新导入 TMP Essential Resources 后需要确认 `TMP_SDF-Mobile.shader` 没有被覆盖。项目新建 TMP 文本的默认字体由 `Assets/TextMesh Pro/Resources/TMP Settings.asset` 指向 `HarmonyOS_CN.asset`，已有 TMP 组件仍保留各自序列化字体，需要按需批量或手动替换。

`FlowLayoutGroup` 默认按子节点 `LayoutElement` 或 Graphic/TMP 提供的 preferred size 从左到右排列，宽度不足时换行。需要从上到下排满后换列时将 `Start Axis` 改为 `Vertical`；`Spacing.x/y` 分别表示水平/垂直间距。它适合数量有限且尺寸不同的标签、按钮和筛选项；大量动态数据仍应使用带复用机制的列表，不要把全部 Item 常驻在 LayoutGroup 下。

## 常见误用

- 不要再使用 `ItemImageLoader`，公共图片加载组件统一为 `UIImageLoader`。
- 不要给 Tab、Dropdown 或 Accordion 传图片缩放参数；需要布局尺寸时改 prefab 布局。
- 不要把 `Register` 当覆盖回调用；重复打开页面时应成对 `Unregister`，或使用组件提供的覆盖入口。
- 不要在 `ViewList` 初始化回调里依赖外部循环变量；使用 `Action<TView, TData, int>` 的 index 参数。
- `ViewList` 仅适合有限数量的 View 项；大量同类数据条目优先复用已有 `LoopScrollView` 和宿主桥接。
- 不要把 `CanvasGroup.SetCanvasGroupVisible` 当作 GameObject 激活切换，也不要再增加同名 `CanvasGroup.Show/Hide`。
- 不要从 Hotfix 直接复制大型 `UIUtil` 或第三方包的内部扩展到 `Core.Runtime/Extends`。
- `TMPAutoScrollView.cs` 属于钓鱼项目的 ItemView 适配层；当前项目使用独立组件与预制体，直接操作 TMP，不引入这层包装。

## 验证

修改这些组件或调用方式后，运行：

- `Tests.Module.UIViewPrefabConventionTests`
- `Tests.Module.CoreUIComponentMigrationTests`

圆角、渐变、翻转、矩形裁剪和自动滚动还应在真实 Canvas 中做一次 Play Mode 目视检查；当前仓库没有可用的 `UIFrameworkValidation` 页面或菜单，按业务 Prefab 接入后验证即可。

完整步骤见 [运行 Unity 自动化测试](./run-unity-tests.md)。

UIDropdown 浮层接入的直接回归是 `UIComponentLifecyclePlayModeTests.Dropdown_DefaultAsyncCompletesWhileCollapsed` 与 `DropdownPopupEscapesScrollClippingWithoutCoveringLaterModalAndReturnsToOwner`，分别覆盖原异步收起初始化，以及真实射线、原布局归还、busy、禁用/销毁和后续 Modal 覆盖。先运行这两个精确方法，不因局部下拉修改扩到无关组件或项目全量测试。

## 交互状态与业务选中

控件同一 GameObject 只挂一个 UIState，默认配置 Normal/Hover/Focused/Pressed/Disabled；同节点有 Selectable 时自动消费原生 EventSystem，无 Selectable 时由业务手动切换。业务需要的 Selected、On、Off 追加在同一状态表，由 UITab、UIBtnSwitch 等维护业务值并调用 `SetState`，不再挂独立 UIStateInteraction，也不新增状态管理器。

自动控件按 Normal 基底、业务态、交互态顺序应用。Normal 配齐状态退出所需的恢复值，业务态只声明增量，交互态覆盖本次反馈；释放、失焦或解除禁用后恢复仍有效的业务态。`CurrentStateId` 表示业务态，`InteractionState` 表示自动交互态。鼠标悬停和方向导航不能修改 Tab 索引，Submit 表现不会代替 Button 再点击一次。隐藏清除瞬态，不清空 Selected/On；触屏不显示交互强调，Disabled 仍有效，禁用逻辑继续由 Selectable/CanvasGroup 控制。

独立勾选框使用原生 Toggle：业务通过 `isOn` 或 `SetIsOnWithoutNotify` 设置值，同节点 UIState 读取该值并应用已配置的 On/Off，继续叠加五态；不使用 `UIState.SetState` 代替 Toggle 值，也不添加第二套回调桥。BtnSwitch1 和 BtnSwitch2 则均复用 UIBtnSwitch 的整块 Button 点击切换，区别仅为双文字胶囊块或圆形滑块表现。

由 UIState 拥有的属性关闭原生 Transition，避免双写。保留现有 Decoration 或反馈节点的制作引用，用它们表现五态；纯装饰不承担导航或射线。状态变化才写表现，页面不用维护自己的设备监听和五态切换。

TMPAutoFitLayoutElement 在 Inspector 保存设计字号，运行时更改使用 SetDesignFontSize(value)。TMP 自动缩小后的当前 fontSize 不再作为恢复依据；文本变短恢复设计字号，不读取 TMP 私有字段。SimpleTips 的正式资源使用该组件，正文超高由滚动区域承载，不启用超高缩字。CommonTips 正文使用 TMPAutoScroll，保持单行与固定字号。

## 时间、颜色与倒计时

```csharp
long nowMs = TimeUtil.UtcNowMilliseconds;
string date = TimeUtil.FormatTimestamp(nowMs, "MM-dd HH:mm"); // 设备时区
string savedUtc = TimeUtil.ToIso8601(); // 与现有存档一致的 UTC round-trip 字符串
string duration = TimeUtil.FormatSeconds(90061, TimeDisplayFormat.HoursMinutesSeconds); // 25:01:01
long nextRefresh = TimeUtil.GetNextDailyRefreshTime(4); // 刷新小时由业务提供
Color textColor = ColorUtil.GetColor("#FF000080", Color.white);
Color maskColor = ColorUtil.GetColor("#000000", alphaPercent: 65);
string highlighted = ColorUtil.WrapText("警告", ColorUtil.Colors.Common.Warning);
countdown.StartCountdown(nextRefresh, TimeDisplayFormat.AutoWithUnits); // UICountdown 的 TMP 引用在 Prefab 绑定
```

固定宿主源码颜色使用 `Hotfix.UIColors.{Common,Hub,DroneFlight,WallSqueeze,KagitKart}` 的语义字段；框架自身语义色保留 `ColorUtil.Colors.Common`。DLSS 当前固定外观仅由 Variant 保存，不声明未使用的运行时色字段。色板声明统一使用六位大写 HEX，可选 `alphaPercent:`（0～100，支持小数）。显式百分比覆盖 HEX 自带 Alpha；旧调用仍支持 3/4/6/8 位，八位 HEX 保留 Alpha。已确认原浮点 RGB 按最接近的八位量化，不同时重新配色；字节 Alpha 使用足够小数保留精度。已有颜色仅替换透明度仍用 `WithAlpha`（0～1），富文本用 `WrapText`/`ToHex`。Prefab 与 Variant 色值继续在 Inspector 维护，不自动应用源码色板；角色/配置动态颜色和 Shader 打包参数不迁入固定 UI 色板。

时间戳必须使用名字所标注的单位。`FromUnixSeconds` 与 `FromUnixMilliseconds` 不根据数值猜测；`ToDateTime`/`FormatTimestamp` 可以传 `TimeZoneInfo`。墙钟默认系统 UTC，服务器接入可以实现 `ITimeSource` 并调用 `SetTimeSource`，本模块不发起网络校时。测试结束必须恢复 `SetTimeSource(null)`。倒计时完成事件只在正常到期触发，`StopCountdown`、禁用和销毁不会触发；完成回调可以同步重启下一轮。

## 两类 Tips

```csharp
await SingleUIManager.Instance.ShowTipsMessageBarAsync("保存成功", CommonTipsType.Success);
await SingleUIManager.Instance.ShowTipsMessageBarAsync("请检查输入", CommonTipsType.Notice, duration: 3);
await SingleUIManager.Instance.ShowTipsMessageBarAsync("操作失败", CommonTipsType.Warning);
// 三次调用生成三条独立通知；需要清空时调用：
await SingleUIManager.Instance.HideTipsMessageBarsAsync();
await SingleUIManager.Instance.ShowSimpleTipsAsync(button.transform as RectTransform, "这里显示规则说明", "操作规则");
await SingleUIManager.Instance.ShowSimpleTipsAsync(screenPosition, "指定屏幕点的说明",
    options: new SimpleTipsOptions(TooltipDirection.Right, gap: 12, maxWidth: 480));
await SingleUIManager.Instance.HideSimpleTipsAsync();
```

业务入口在 `Hotfix.SingleUIManager.Instance`；Core 使用方可复用 `UITipsStack`、`UITipsPanel`、`UITooltip` 和 `TooltipPlacementUtil`，不得反向依赖 Hotfix。Tips 宿主的显隐经过 UIManager。`ShowSimpleTipsAsync` 的 RectTransform 重载使用矩形中心与边缘，Vector2 重载只接受屏幕像素；不要传 `transform.position` 世界坐标。

SimpleTips 默认上方、间距 12、主体最大宽度 600。短文本收缩背景，长文本换行，超高正文滚动。外部点击由 Blocker 消耗并关闭，返回关闭后恢复原焦点。连续 Show 更新缓存 View 的内容，不重复开关节点；目标移动和屏幕尺寸变化会重定位，目标无效时自动关闭。

可在业务按钮挂 `SimpleTipsTrigger`，Inspector 保存目标、标题、正文与 Click/Hover 模式。Click 接收点击或公共菜单 Submit；Hover 接收悬停及菜单聚焦，离开/失焦或组件禁用时只关闭自己拥有的提示。悬停模式不接管菜单焦点、不阻挡点击。直接调用接口时也可用 `new SimpleTipsOptions(closeOnOutside: false)` 由业务控制关闭。

CommonTips 新消息叠在前面，不覆盖旧消息。收起时可见三层，悬停通知区域（包含消息间隙）展开全部尚未到期的消息；列表过高可滚动。默认每条停留 2 秒，长正文自动滚动到末尾后才开始这段停留。长消息默认首停 1.5 秒、速度 40 UI 像素/秒、末停 2 秒，换行符按空格显示。悬停时暂停消失计时，文字继续滚动，离开后从剩余时间继续；× 只关闭所在消息。区域外仍可点击，显示时不抢焦点，不受 timeScale 影响。三种图标为 Prefab 中的直接 Sprite 引用，不依赖 emoji 字形。

预先取消的 Show 请求返回 Canceled，并保留当前 Tips。CommonTips 的令牌在显示后取消只移除本次消息；`HideTipsMessageBarsAsync()` 清空全部并取消已排队消息。SimpleTips 仍使用单条替换规则：排队期间取消或显示失败时，没有新请求接管则清理失去所有权的旧提示，避免残留 Blocker；已显示后仍允许外部点击或返回关闭。

CommonTips Prefab 的 `ItemTemplate` 必须保持隐藏，直接保存 `UITipsPanel`、CanvasGroup、Details、CloseButton、三种图标与中文字体引用；`MessageRegion` 是唯一交互区域，外层 ScrollRect 与安全区适配由 `UITipsStack` 管理。根节点不添加全屏 Graphic，关闭按钮的 Navigation 使用 None。模板修改走 Prefab 与 UIBind 流程，不在运行时拼装缺失控件。

两类 Tips 的公共外观按[已确认图02](../agent/art/ui-foundation/foundation-v3-panels.png)维护暖白表面和深墨文字。CommonTips 的三种消息不按状态重染整张背景：成功为绿色圆形勾，普通说明为正文墨色圆形信息图标，警告为红色轮廓三角与叹号。SimpleTips 的方向由目标与安全区决定，图中的左向箭头仅为一种示例；浮层阴影与细边不改变 Blocker、Body、Arrow、滚动区或作者遮罩引用。面板嵌套标题左齐与独立页面标题居中的边界见[面板资源契约](../modules/ui-foundation-assets.md#已确认图02的面板与消息契约)。

正式资源位于 `Assets/LoadResources/UI/Common`，背景、箭头和状态图标位于公共 `Sprites/Tips`。修改绑定后通过 UIBind 重新生成，不手改 `*Component.cs`。字体使用公共 HarmonyOS_CN 及其现有回退；箭头与 Body 为同级，Pivot 位于连接边缘；定位完成后不要再次覆写主体位置。尺寸、安全边距和 gap 使用 Canvas 本地单位，不按 Screen.width 手工缩放。

背景与箭头参考钓鱼项目公共资源；当前消息图标使用 `Sprites/Tips/{CircleSuccess,CircleInfo,WarningOutline}.png`，制作源在 `ArtSource/common_ui/`。圆形单色符号用 ColorUtil 或正文色着色；WarningOutline 预着色为 `#BE0423`，保留警告图标的白色 Tint。旧 WarningTriangle 和 Success 图标仍有其他使用方，不因消息图标替换删除。直接维护正式资产，临时装配、导出及预览工具不作为长期菜单保留。

运行 `Tests.Module.CommonUtilityTests`（EditMode）、`Tests.Module.CommonTipsPlayModeTests`（PlayMode）和相关 Prefab 约定测试。实际观感还需检查安全区、屏幕比例、正文滚动、三种图标及返回焦点；相关测试不等于全量回归或 YooAsset Player 构建。

### 初始化完成时机

`UITab` / `AccordionTab` 的 action 在项创建和初始选择后触发，不保证图片或分页 View 已加载。需要读取最终选中态时放入 action；`ViewList` 的 onInit 按项交付。重复 Init 使用新请求，旧任务不能再回写数据；禁用/销毁会停止未完成的初始化或切换。`UIDropdown.SetData` 先收起旧内容，再初始化隐藏选项，避免隐藏动作取消新任务。

```csharp
tab.Init(labels, action: () => RefreshSelection(tab.Index));
imageLoader.SetImage(spriteAddress); // 默认异步，不应在下一行读取最终 sprite
tab.Init(labels, isAsync: false);    // 明确需要立即初始化时使用
```

## 基础预制体与交互表现

接入 Demo 前先验收公共模板，逐项检查组件绑定、中文字体、Sprite 与 Border、布局、遮罩、射线和导航。通用按钮模板为 `Common/_TemplateInstantiatePrefab/Btns/CommonButton.prefab`；Tab、下拉和开关继续使用各自模板。

`UIState` 直接消费 EventSystem 事件，表现状态为 Normal、Hover、Focused、Pressed、Disabled。基础模板统一采用暖白、深墨和朱红；Hover 提亮，Focused 使用明确焦点边框，Pressed 变深或改变装饰，Disabled 降低强调且不可操作。触屏不显示交互强调，但仍显示禁用状态。五态不缩放控件根节点，保持按下与抬手的点击矩形一致。

业务值与交互反馈职责分开，表现统一配置在同一个 UIState；不要再额外挂交互桥或用原生 Transition 修改同一颜色或缩放。Normal 必须恢复所拥有的属性，关闭、失焦及禁用不能留下按压缩放。纯背景需要状态时手动调用，不参与导航；反馈 Graphic 不拦截射线。触控命令保持与释放继续由 InputCommandButton 负责，表现状态不接管命令生命周期。

`UIProgressBar` 只包装同节点 Image 的 `fillAmount` 与颜色，不实现交互或业务缓动。普通填充使用项目公共 `Sprites/White.png`、Filled 和 Image.color；需固定圆角的背景或遮罩使用有 Border 的 Sliced，填充在遮罩内变化。Filled 必须保存非空 Sprite：没有 Sprite 时 UGUI 会绘制普通矩形，忽略填充逻辑。特殊形状和径向填充保留相应 Sprite 与 FillMethod，不强制使用白块。

修改基础模板后运行 `UIFoundationAssetTests` 与 `UIFoundationPlayModeTests`，检查真实鼠标悬停/点击/移出、键鼠与手柄提交、禁用及反复开关；检查 0%、1%、50%、100% 的实际填充网格和不同尺寸下的边框。自动断言不代替视觉检查，必须查看目标 Canvas 的状态截图。

既有模板样式、BtnSwitch2 与 Hub 设置页的定向记录见[基础资源验证](../modules/ui-foundation-assets.md#验证)及[实施记录](../agent/archive/settings-goal-progress.md)，不作为本轮 ImageExtend、提示2或四 Demo 风格传播已验收的证据。复用 `Tests.Module.UnifiedInputPlayModeTests.NestedMenuRestoresParentAndTouchSuppressesInteractionFeedback` 维护业务态叠加和退出恢复；普通外观变化默认不另留永久测试文件。


`UICancelRelay` 配置在按钮、Slider 等可导航控件上；优先转交最近的 UIMenuScope，宿主已经持有 MenuInputScope 的页面则转交最近的父级 ICancelHandler。只有选中控件接收原生 Cancel，不依赖事件自动向上冒泡，不为转发再创建一个输入作用域。

新增普通水平/竖直 Filled 填充须通过 `UIFoundationAssetTests.LinearProgress_UsesWhiteSpriteAndSharedComponent`；检查覆盖全部 LoadResources Prefab，不按节点名称放行。页面拆分同时检查本地绑定、缺失脚本和 HUD 子 Prefab 引用，视觉验收仍须查看实际输入与填充网格。
