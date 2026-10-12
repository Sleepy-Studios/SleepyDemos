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
| `UIBtnSwitch` | 二态按钮逻辑；BtnSwitch1 为选择型，BtnSwitch2 为滑块开关型 |
| `UIState` | 同节点 Selectable 自动驱动五态，Selected/On/Off 按需追加在同一组件；无 Selectable 时手动切换 |
| `UIMenuScope` | 页面/弹窗的焦点导航与返回，复用公共输入模块 |
| `InputBindingPrompt` / `InputCommandButton` | 有效绑定图标与动作文案 / 可点击、可保持的触控命令；复用 Kenney 图标目录 |
| `UISettingRow` | 对现有 Dropdown、Slider、Switch、Button 的设置行引用与回显；不负责分类切换、保存或输入会话 |
| `UIImageLoader` | 按资源路径给 `Image` 加载 Sprite |
| `TMPAutoFitLayoutElement` | TMP 文本按最大宽高自适应 LayoutElement 或自身尺寸 |
| `TMPAutoScrollEnableBehaviour` | 单行 TMP 超宽后横向自动滚动 |
| `TMP_UGUI_Extend` | TMP 渐变、描边、阴影、倾斜复合效果 |
| `GradientUI` | 任意 UGUI Graphic 的简单水平或垂直渐变 |
| `FlipImage` | 任意 UGUI Graphic 顶点翻转或旋转 |
| `RoundedCorners` | Image 等 Graphic 的四角圆角和可选 Mask |
| `UIVertexRectMask2D` | 不改材质的矩形硬裁剪 |
| `PyramidLayoutGroup` | 固定尺寸品字或三角形布局 |
| `FlowLayoutGroup` | 不同尺寸子项按宽度换行或按高度换列 |
| `TMPLinkHandler` | 监听 TMP `<link>` 的 linkId 点击事件 |

## 从需求找到资源和示例

下面路径相对于项目根目录。公共控件入口为 `Assets/Scripts/Core/Runtime/Components/`；输入相关组件在 `Assets/Scripts/Core/Runtime/Input/`。基础资源以 [UI 预制体检查范围](../modules/ui-foundation-assets.md) 为准，不能只看名字相似就另建组件。

| 需求 | 复用入口 / 资源 | 接入依据 |
|---|---|---|
| 分类按钮，仅选中回调 | `UITab`；`UI/Common/_TemplateInstantiatePrefab/Tab/Tab.prefab` | `Init` / `Register`；保留模板自带图标引用 |
| 分类对应独立业务页面 | `ViewTab`；`UI/Common/_TemplateInstantiatePrefab/Tab/ViewTabVertical.prefab` | `Init(views: ...)`，各分类自己的 View、Prefab 和生命周期；不要改为一个脚本手动切全部区域 |
| 大量同类数据条目 | `LoopScrollView`、`LoopScrollItemViewBridge`、`LoopScrollRegisterExtend` | [循环列表接入](use-loop-scroll.md)；现有 `MainMenuView` / `MainMenuDemoItemView` 展示数据绑定与条目复用 |
| 下拉候选、开关、普通按钮 | `UIDropdown`、`UIBtnSwitch`；`_TemplateInstantiatePrefab/Dropdown/Dropdown`、`Btns/BtnSwitch1`、`Btns/BtnSwitch2`、`Btns/CommonButton` | BtnSwitch1 表现二态选择，BtnSwitch2 表现滑块开关；Hub 二态偏好使用 SwitchRow 内的 BtnSwitch2 |
| 固定设置表单行 | `UISettingRow`；`UI/Common/Settings/{Dropdown,Slider,Switch,Action}Row.prefab` | 复用对应基础控件，业务分页绑定独立候选；示例 `Hotfix/Module/Settings/UI` |
| 设备按键提示与触控操作 | `InputBindingPrompt`、`InputCommandButton`；`UI/Common/Input/` | [公共输入](use-gameplay-input.md)；有效会话绑定、图标配置与触控释放共用底座 |
| 保存反馈或控件说明 | `CommonTips`、`SimpleTips`；`SingleUIManager` | 本文“两类 Tips”；不要再造 Toast / Tooltip 宿主 |
| 进度、倒计时、超宽文本 | `UIProgressBar`、`UICountdown`、`TMPAutoScrollEnableBehaviour` | 本文相关小节；使用已维护的时间和布局入口 |

表中 `UI/...` 资源路径相对于 `Assets/LoadResources/`。每个 Demo 可从基础 Prefab 建立自己的 Variant，覆盖图片、字体组、颜色和布局；公共 API 和生命周期一致，不要求 Demo 外观与 Hub 相同。卡丁车等已有美术风格保持其作者资源。

循环列表的判断依据是数据是否需要同类条目复用，不是页面上出现了滚动条。分类通常只有少量 Tab；固定设置表单使用原生布局；动作及绑定数量随宿主变化的控制列表使用循环列表。不要把所有设置项包装成同一个通用数据列表，也不要把大量动作全部实例化在 LayoutGroup 下。

滚动表单中的控件保持点击区域尺寸稳定，交互五态优先修改颜色、边框或独立装饰节点。设置行曾因按下时缩放 Button 根节点导致抬手命中滚动视口、点击丢失，不能直接照搬模板的根节点缩放。普通 ScrollRect 的键盘／手柄焦点显露由 UIMenuScope 处理；循环列表继续使用自己的导航。循环条目中的改键、重置等所有可点击操作都使用 LoopScrollMenuButton 的回收身份保护。

采集新按键等临时输入使用 `UIMenuScope.SetNavigationEnabled(false)` 保留指针、暂停原生 Submit/Navigate；结束或取消后传 `true`，复用 MenuInputScope 的松键门闩恢复导航。不要在业务页手写 EventSystem 导航状态快照或永久排除 Enter／手柄确认键。

固定分类已为图标预留文字区域时，停用模板 Desc 上按文本宽度驱动的 ContentSizeFitter；长译文使用 TMP 原生自动字号和制作侧上下限。否则文字矩形会被重新撑宽，即使开启自动字号也仍可能覆盖图标。不要在业务代码中逐语言计算文字位置。

下拉选项浮层须有独立不透明背景，覆盖选项之间的间距；背景高度随实际选项数量更新。公共 `DropdownRow` 在 `TabView` 使用原生 Image 和纵向 Preferred Size 的 ContentSizeFitter，不按制作模板的初始项数固定背景高度。页面继续通过 UIDropdown 指定同页 popupRoot，不自行创建第二套浮层。

基础 Dropdown 的 Arrow 绑定 Image，使用 `Assets/LoadResources/UI/Common/Sprites/Tips/Arrow.png`，不要用 TMP 字符代替图标；UIDropdown 仍负责展开 180° 旋转及收起恢复。更换表现组件后保留原 arrow Transform 引用，不另加旋转脚本。

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

`Show/Hide` 只操作 GameObject 激活状态。CanvasGroup 可见性必须使用 `SetCanvasGroupVisible`，避免调用方误以为两者生命周期一致。

直接拖入 `Assets/LoadResources/UI/Common/TMPAutoScroll.prefab`，绑定子节点 `Text` 的 TMP 后赋值即可；无需 ItemView 或 SubViewWithGameObject。自己装配时在 Inspector 绑定 viewport 和 TMP，也可显式初始化：

```csharp
autoScroll.Initialize(viewport, titleText);
autoScroll.SetOptions(TMPAutoScrollOptions.Default);
titleText.text = title;
```

组件会监听 TMP 顶点脏回调，因此业务继续直接写 `titleText.text = title` 也能重新测宽；通过文字顶点与四维 UV 裁剪，不添加独立遮罩。短文本不滚动并恢复原位置与 TMP 格式。配置 `UseUnscaledTime = true` 可在暂停游戏时继续滚动；`Loop = false` 时，`ScrollCompleted` 在末端停留完成后触发，短文本测量完成即触发。修改文本、字号或可用宽度会重新测量，禁用时复位，重新启用时重新开始。

`RoundedCorners` 所需 Shader 位于 `Assets/LoadResources/Art/Shaders/UIRoundedCorners.shader`。挂载后组件自动开启 Canvas 的 TexCoord1/TexCoord2；若启用 `Use As Mask` 会补 `Mask`。Item 列表优先复用相同半径以命中材质缓存。

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
string highlighted = ColorUtil.WrapText("警告", ColorUtil.Colors.Warning);
countdown.StartCountdown(nextRefresh, TimeDisplayFormat.AutoWithUnits); // UICountdown 的 TMP 引用在 Prefab 绑定
```

时间戳必须使用名字所标注的单位。`FromUnixSeconds` 与 `FromUnixMilliseconds` 不根据数值猜测；`ToDateTime`/`FormatTimestamp` 可以传 `TimeZoneInfo`。墙钟默认系统 UTC，服务器接入可以实现 `ITimeSource` 并调用 `SetTimeSource`，本模块不发起网络校时。测试结束必须恢复 `SetTimeSource(null)`。倒计时完成事件只在正常到期触发，`StopCountdown`、禁用和销毁不会触发；完成回调可以同步重启下一轮。

## 两类 Tips

```csharp
var result = await SingleUIManager.Instance.ShowTipsMessageBarAsync("保存成功", CommonTipsType.Success);
if (result.Status == UIOperationStatus.Failed) Debug.LogException(result.Exception);
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

正式资源位于 `Assets/LoadResources/UI/Common`，背景、箭头和状态图标位于公共 `Sprites/Tips`。修改绑定后通过 UIBind 重新生成，不手改 `*Component.cs`。字体使用公共 HarmonyOS_CN 及其现有回退；箭头与 Body 为同级，Pivot 位于连接边缘；定位完成后不要再次覆写主体位置。尺寸、安全边距和 gap 使用 Canvas 本地单位，不按 Screen.width 手工缩放。

背景、箭头、成功勾号和提醒叹号参考钓鱼项目公共资源；单色符号用 ColorUtil 的语义色着色。警告三角为本项目资源，矢量原稿保存在独立美术目录的 `ArtSource/common_ui/WarningTriangle.svg`，对应 `WarningTriangle.png`。直接维护正式资产，临时装配、导出及预览工具不作为长期菜单保留。

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

本次模板样式与 BtnSwitch2 替换后的最终验收仍待主代理汇总，复用 `Tests.Module.UnifiedInputPlayModeTests.NestedMenuRestoresParentAndTouchSuppressesInteractionFeedback` 检查业务态叠加和退出恢复，再运行上述直接受影响的资源与真实输入测试。旧模板的 17 项五态截图及通过记录仅保留为[历史证据](../modules/ui-foundation-assets.md#验证)，不作为本次装配已通过的结论；普通外观变化默认不另留永久测试文件。


`UICancelRelay` 配置在按钮、Slider 等可导航控件上；优先转交最近的 UIMenuScope，宿主已经持有 MenuInputScope 的页面则转交最近的父级 ICancelHandler。只有选中控件接收原生 Cancel，不依赖事件自动向上冒泡，不为转发再创建一个输入作用域。

新增普通水平/竖直 Filled 填充须通过 `UIFoundationAssetTests.LinearProgress_UsesWhiteSpriteAndSharedComponent`；检查覆盖全部 LoadResources Prefab，不按节点名称放行。页面拆分同时检查本地绑定、缺失脚本和 HUD 子 Prefab 引用，视觉验收仍须查看实际输入与填充网格。
