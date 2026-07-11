# Core UI 运行时

## 目标

Core UI 运行时提供业务界面前置的公共 UI 能力，包括 View 生命周期、UI 栈、Root 构建、遮罩、缓存和基础组件。Hotfix 可以使用这些能力组织页面，但不应在 Hotfix 中再实现一套并行 UI 框架。

## 关键结构

- `View` / `View<T>`：页面基类，负责资源实例化、组件初始化、显示隐藏、动画接入和销毁。
- `UIManager`：统一打开、关闭、返回、预加载、清栈和栈顶查询入口，当前承接 Mask、sibling、View 显示隐藏等表现副作用。
- `UIStack`：只维护已提交的 Page、Modal、Widget 状态与顺序，提供状态快照，并暂时保留同步命名栈兼容状态。
- `UICache`：按 View 类型缓存 View 实例。
- `UIRootManager`：构建 `UIRootCanvas`、透视 UI Camera、EventSystem、固定层级 Canvas 和遮罩。
- `Components/`：公共基础组件，如 `UITab`、`ViewList`、`UIBtnSwitch`、`UIDropdown`、`UIState`。

## Transition 契约边界

- `IUITransition` 定义单个 View 的初始化、进入、退出、立即完成和释放契约；`EmptyUITransition` 是无副作用、立即完成的默认实现。
- `IUIWorldTransition` 定义跨 View 的世界表现过渡，`IUIWorldTransitionProvider` 按 `View` 解析具体实现。业务生成代码只声明 `WorldTransitionKey`，不直接实例化世界过渡。
- `UITransitionContext` 统一携带操作标识、导航动作、进入/退出 View 和是否播放过渡。
- `View` 在资源加载成功后调用一次 `CreateUITransition()`，缓存为该 View 生命周期内稳定的 `UITransition` 实例，并使用 View 根节点调用一次 `Initialize()`。
- `EnterAsync` / `ExitAsync` 始终使用缓存实例。`DestroyAsync` 负责调用一次 `Dispose()`；业务 View 不自行创建、替换或释放 Transition。

## View 生命周期

`ViewState` 是真实单值状态，不再表示可组合标记。主链路如下：

```text
Created -> Loading -> LoadedHidden -> Entering -> Visible
                              ^                    |
                              |---- Exiting <------|

Loading --加载失败或取消--> Faulted
任一可清理状态 -> Destroying -> Destroyed
```

- `LoadAsync` 在调用 `OnBeforeInit()` 前进入 `Loading`。资源加载成功后先进入 `LoadedHidden`，再初始化组件与 Transition；根对象保持 inactive。
- `EnterAsync` 先进入 `Entering`，激活根对象并调用 `OnShow()`；过渡完成后进入 `Visible`。
- `ExitAsync` 先进入 `Exiting`，完成退出过渡后关闭根对象并调用 `OnHide()`；最终回到 `LoadedHidden`。
- `DestroyAsync` 是幂等且可并发等待的唯一清理入口。加载中的销毁会等待同一加载结果，并回收晚到实例；subViews、bindings、Transition、资源实例和 Loader 各自最多清理一次。
- 地址为空、加载结果为 null 或非取消异常会进入 `Faulted` 并释放 Loader。取消会清理已返回的晚到实例并继续抛出 `OperationCanceledException`，由导航协调层决定回滚结果。
- `Init`、`InitAsync`、`InitWithGameObject`、`Show`、`Hide`、`Destroy` 暂时保留为旧调用兼容外观，底层复用同一生命周期和清理路径，不构成第二套状态机。

## 导航状态与表现边界

- `UIStack` 只保存已提交导航状态，不持有 Mask、Button 或 Root，也不调用 `View.Show()` / `View.Hide()`、操作 Transform 或 GameObject。Page、Modal、Widget 集合与快照都只通过不可修改视图对外暴露。
- `UIManager` 当前负责同步兼容入口，以及 Mask、sibling、View 显示隐藏等表现副作用；清栈时由 `UIManager` 同步收口 View、缓存、状态栈和 Mask。
- 当前 `NewStack` / `RemoveStack` 等命名栈能力属于兼容层。后续任务 5 将由导航协调器替换这层同步兼容实现；在此之前不要把表现副作用重新塞回 `UIStack`。

## 基础组件边界

本模块只承载基础有限 UI 组件：
- `UITab`：有限 Tab，支持静态项和基于 prefab 的动态项，支持 Button/Toggle、UIState 状态驱动（`Normal/Selected`）和拦截回调。文本优先使用 TMP（`TextMeshProUGUI`），可附带图标配置。
- `ViewList`：有限列表，复用已创建 View 项，不承担大量数据虚拟化滚动。
- `UIBtnSwitch`：按钮开关状态组件，视觉状态优先由 `UIState`（`On/Off`）驱动，必要时兼容旧的 On/Off 节点。
- `UIDropdown`：基于 `UITab` 的基础下拉选择组件。
- `ViewTab`：通过一个 `UITab` 驱动多个 View 或本地 ViewRoot 子节点切换，公共 `Parent/ViewRoot` 用于挂载 View。
- `AccordionTab`：两级手风琴 Tab，一级负责展开/收起，回调使用扁平化叶子索引。
- `AccordionViewTab`：通过 `AccordionTab` 叶子索引驱动多个 View 或本地 ViewRoot 子节点切换。
- `UIImageLoader`：按 Sprite 资源路径加载图片，支持同步/异步和 `SetNativeSize`。
- `UIState`：序列化状态切换组件，用于 Normal/Selected 等轻量状态；状态项使用固定枚举和强类型组件引用，不使用反射属性名或字符串解析。

明确不在这里混入：
- 业务图片加载器或图集拆分规则
- 具体 Demo 或页面业务状态

## UIManager 使用规则

- 打开界面优先使用 `UIManager.Show<T>()`。
- 关闭界面使用 `UIManager.Close<T>()` 或 `Back()`。
- 需要提前加载资源时使用 `Preload<T>()`，不要绕过 `View` 生命周期直接实例化 UI 预制体。
- 清空所有界面时使用 `CloseAll()`，它会隐藏并销毁当前缓存中的 View，并清空 UI 栈和缓存。
- `DestroyOnHide` 为 true 且引用计数归零时，View 会在关闭后销毁并释放 loader。
- 快速重复打开同一界面时，`UIManager` 会防止同一类型重复进入异步打开流程。

## 渲染结构与生命周期

启动阶段由 `UIInitializeSystem` 调用 `UIManager.InitializeAsync()`，再由 `UIRootManager.BuildUIRoot()` 动态创建持久化 UI 环境：

1. 确认项目存在 UI Layer，并让主相机排除该层。
2. 创建透视 `UICamera`，加入 URP 主相机的 Camera Stack。
3. 创建 `UIRootCanvas`，统一设置 `ScreenSpaceCamera` 和 `1920×1080` 屏幕适配。
4. 创建 `Underground/Base/Foreground/Pop/Decorate/Tip` 六个固定子 Canvas。
5. 在 Pop 层创建遮罩，并确保 EventSystem 存在。

固定层使用 `0/100/150/200/250/300` 的 `sortingOrder`。View 初始化后直接挂到 `Level` 对应的层 Root，通过 sibling 顺序控制同层先后，不再动态添加窗口级 Canvas、Scaler 或 Raycaster。

`BuildUIRoot()` 可以重复调用，但同一运行期只创建一套 Root 和 UI Camera。`CloseAll()` 只清理 View、UI 栈与缓存，不销毁持久化 Root。

完整设计取舍见 [Core UI 渲染设计原则](../architecture/ui-rendering.md)，新增 View 的制作规则见 [接入 Core UI View](../runbooks/create-ui-view.md)。

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

基础组件公共入口统一收口在一个主方法上：
- `UITab`、`AccordionTab`、`ViewTab`、`AccordionViewTab`、`ViewList` 使用 `Init(...)`。
- `UIDropdown` 使用 `SetData(...)`。
- `UIBtnSwitch` 使用 `Set(...)` / `SetStatus(...)`，`SetAction` 为覆盖回调，`Register` 为追加回调。
- `UIImageLoader` 使用 `SetImage(string key, bool setNativeSize = true, bool isAsync = false)`。

所有基础组件默认 `isAsync = false`，即同步初始化或同步资源加载；需要分帧初始化、异步图片加载或异步 View 加载时显式传 `isAsync: true`。图片参数统一为 Sprite 资源路径，不传图集名和缩放值。完整调用规则见 [使用 Core 基础 UI 组件](../runbooks/use-core-ui-components.md)。

## 修改注意

- 不要让 Core UI 依赖 Hotfix。
- 不要让业务页面直接依赖 YooAssets 句柄或包类型。
- 新组件优先放在 `Assets/Scripts/Core/Runtime/Components`。
- `ViewList` 只承担有限列表，不在其中扩展大量数据虚拟化滚动能力。
- View Prefab 根节点不得携带 `Canvas`、`CanvasScaler` 或 `GraphicRaycaster`。
- 只有 Profiler 证明有必要时才增加局部 Sub-Canvas，并保持 `overrideSorting=false`。

## 验证入口

- `Core.Tests.UI.UIViewPrefabConventionTests` 检查公共 View Prefab 根节点 Canvas 三件套。
- `Core.Tests.UI.UIStackTests` 在 Edit Mode 中检查 Page、Modal、Widget、Back、快照恢复和只读状态边界。
- `Core.Tests.UI.MvcBindTransitionGenerationTests` 在 Edit Mode 中检查 MvcBind 生成 Transition 工厂、显式 ViewMode 和 World Transition Key。
- `Core.Tests.UI.UIRootManagerPlayModeTests` 在真实 Play Mode 中检查 Root Canvas、六个固定层、Mask、重复初始化和清栈后的 Mask 状态。
- `Core.Tests.UI.UIViewLifecyclePlayModeTests` 在真实 Play Mode 中检查加载、取消、加载中销毁、稳定 Transition、幂等释放和不可复用缓存替换。
- `Tools/SleepyDemos/UI Framework Validation/Validate Generated Prefabs` 实例化验证 Prefab，覆盖 MvcBind 绑定和基础组件交互。

统一运行方式见 [运行 Unity 自动化测试](../runbooks/run-unity-tests.md)。
