# Core UI 运行时

## 目标

Core UI 运行时提供业务界面前置的公共 UI 能力，包括 View 生命周期、UI 栈、Root 构建、遮罩、缓存和基础组件。Hotfix 可以使用这些能力组织页面，但不应在 Hotfix 中再实现一套并行 UI 框架。

## 关键结构

- `View` / `View<T>`：页面基类，负责资源实例化、组件初始化、显示隐藏、动画接入和销毁。
- `UIManager`：统一打开、关闭、返回、预加载、清栈和栈顶查询入口。
- `UIStack`：维护普通界面栈、挂件列表、分层数据和遮罩位置。
- `UICache`：按 View 类型缓存 View 实例。
- `UIRootManager`：构建 `UIRoot`、UI Camera、EventSystem、层级根节点和遮罩。
- `Components/`：公共基础组件，如 `UITab`、`ViewList`、`UIBtnSwitch`、`UIDropdown`、`UIState`。
- `Components/LoopScroll/`：独立循环列表模块，提供 `LoopVerticalScrollRect`、`LoopHorizontalScrollRect`、`LoopGridView`、`LoopStaggeredGridView`。

## 基础组件边界

本模块只承载基础有限 UI 组件：
- `UITab`：有限 Tab，支持静态项和基于 prefab 的动态项，支持 Button/Toggle、UIState 状态驱动（`Normal/Selected`）和拦截回调。文本优先使用 TMP（`TextMeshProUGUI`），可附带图标配置。
- `ViewList`：有限列表，复用已创建 View 项，不包含循环列表或无限滚动。
- `UIBtnSwitch`：按钮开关状态组件，视觉状态优先由 `UIState`（`On/Off`）驱动，必要时兼容旧的 On/Off 节点。
- `UIDropdown`：基于 `UITab` 的基础下拉选择组件。
- `ViewTab`：通过一个 `UITab` 驱动多个 View 或本地 ViewRoot 子节点切换，公共 `Parent/ViewRoot` 用于挂载 View。
- `AccordionTab`：两级手风琴 Tab，一级负责展开/收起，回调使用扁平化叶子索引。
- `AccordionViewTab`：通过 `AccordionTab` 叶子索引驱动多个 View 或本地 ViewRoot 子节点切换。
- `UIState`：序列化状态切换组件，用于 Normal/Selected 等轻量状态；状态项使用固定枚举和强类型组件引用，不使用反射属性名或字符串解析。

明确不在这里混入：
- 业务图片加载器
- 具体 Demo 或页面业务状态

循环列表已经独立收口在 [LoopScroll 运行时](./loop-scroll-runtime.md)，不要把无限滚动逻辑塞回 `ViewList`。

## UIManager 使用规则

- 打开界面优先使用 `UIManager.Show<T>()`。
- 关闭界面使用 `UIManager.Close<T>()` 或 `Back()`。
- 需要提前加载资源时使用 `Preload<T>()`，不要绕过 `View` 生命周期直接实例化 UI 预制体。
- 清空所有界面时使用 `CloseAll()`，它会隐藏并销毁当前缓存中的 View，并清空 UI 栈和缓存。
- `DestroyOnHide` 为 true 且引用计数归零时，View 会在关闭后销毁并释放 loader。
- 快速重复打开同一界面时，`UIManager` 会防止同一类型重复进入异步打开流程。

## 基础组件验证资源

基础 UI 框架验证资源放在：

- `Assets/LoadResources/UI/UIFrameworkValidation/`：验证入口和 `UITab`、`UIBtnSwitch`、`UIDropdown`、`ViewTab` 分页预制体。
- `Assets/Scripts/Hotfix/Module/UIFrameworkValidation/`：验证 View 逻辑和 MvcBind 生成的 Component 代码。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/`：验证页和 `UIFrameworkValidation` 相关构建统一复用的模板仓库，当前流程会优先读取并实例化 `Tab/TabItem.prefab` 与 `Btns/BtnSwitch.prefab`。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Tab/ViewTabVertical.prefab`：`ViewTab` 基础模板，结构为 `ViewTabVertical / Tab / ViewRoot`。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Btns/BtnSwitch.prefab`：`UIBtnSwitch` 基础模板。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Dropdown/Dropdown.prefab`：`UIDropdown` 基础模板。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Accordion/AccordionTab.prefab`：`AccordionTab` 基础模板。
- `Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Accordion/AccordionViewTab.prefab`：`AccordionViewTab` 基础模板。

`ViewTab` 模板和验证页遵循 `UITab + ViewRoot/Parent` 结构：`UITab` 只负责选择，`ViewRoot/Parent` 只负责承载打开出来的 View 或本地 View 节点。`UIDropdown` 的可滚动版本使用外层 `ScrollRect / Viewport / Content` 承载选项，滚动不通过 `UIDropdown.Update()` 或循环轮询实现。

验证入口从 `MainMenuView` 的「基础 UI 验证」按钮打开，后续页面仍通过 `UIManager.Show<T>()` 进入，不绕过 View 生命周期或资源 Loader。当前入口包含 `UITab`、`UIBtnSwitch`、`UIDropdown`、`ViewTab`、`AccordionTab`、`AccordionViewTab` 六个验证页。

## 修改注意

- 不要让 Core UI 依赖 Hotfix。
- 不要让业务页面直接依赖 YooAssets 句柄或包类型。
- 新组件优先放在 `Assets/Scripts/Core/Runtime/Components`。
- 循环列表使用 `Components/LoopScroll/`，`ViewList` 仍只承担有限列表。

## 验证入口

在 Unity Editor 中可以运行：
- `Tools/SleepyDemos/Validate Core Runtime Infrastructure`
- `Tools/SleepyDemos/UI Framework Validation/Validate Generated Prefabs`

该菜单会检查基础组件类型是否存在，并扫描 UI/Startup/HotUpdate/Hotfix 外层代码是否混入资源底层类型、旧项目循环列表实现或 fishinggameplay 业务依赖。
UI Framework Validation 菜单会实例化验证预制体，检查 MvcBind 绑定数量，并覆盖 `UITab`、`UIBtnSwitch`、`UIDropdown`、`ViewTab`、`AccordionTab`、`AccordionViewTab` 的基础交互。

命令行或 CI 可以使用 Unity 参数：
- `-executeMethod Core.Editor.CoreRuntimeInfrastructureValidator.ValidateForBatchMode`
