# Core UI 运行时

## 目标

Core UI 运行时提供业务界面前置的公共 UI 能力，包括 View 生命周期、UI 栈、Root 构建、遮罩、缓存和基础组件。Hotfix 可以使用这些能力组织页面，但不应在 Hotfix 中再实现一套并行 UI 框架。

## 关键结构

- `View` / `View<T>`：页面基类，负责资源实例化、组件初始化、显示隐藏、动画接入和销毁。
- `UIManager`：导航事务 owner，统一执行加载、过渡、Mask、sibling、缓存清理和事件提交。
- `UINavigationCoordinator`：纯 C# FIFO 单写入协调器，只管理 operation、取消、队列与结果完成，不访问 Unity 场景或 Singleton。
- `UIStack`：只维护已提交的 Page、Modal、Widget 状态与顺序，并提供事务快照与恢复。
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
- 同一 View 的并发 `LoadAsync` 只启动一次底层加载。每个调用者独立响应自己的取消令牌；取消单个 waiter 不影响其它 waiter，全部 waiter 取消或 View 开始销毁时才取消内部加载生命周期并回收晚到实例。
- `EnterAsync` 先进入 `Entering`，激活根对象并调用 `OnShow()`；过渡完成后进入 `Visible`。
- `ExitAsync` 先进入 `Exiting`，完成退出过渡后关闭根对象并调用 `OnHide()`；最终回到 `LoadedHidden`。
- Enter/Exit 的非取消生命周期异常会进入 `Faulted` 并原样传播；取消仍保留当前进行态，由后续导航 Coordinator 决定完成到进入端还是退出端。
- `DestroyAsync` 是幂等且可并发等待的唯一清理入口。加载中的销毁会等待同一加载结果，并回收晚到实例；subViews、bindings、Transition、资源实例和 Loader 各自最多清理一次。
- 同步 `Init` / `InitWithGameObject` 失败时会启动唯一的 owned-resource cleanup operation；后续 `DestroyAsync` 等待同一 operation，不会并发重复枚举或释放资源。
- `AddSubView()` 只接受无环的生命周期所有权关系：拒绝持有自身，也拒绝直接或间接祖先环；重复添加同一合法 child 保持幂等。
- 地址为空、加载结果为 null 或非取消异常会进入 `Faulted` 并释放 Loader。取消会清理已返回的晚到实例并继续抛出 `OperationCanceledException`，由导航协调层决定回滚结果。
- `Init`、`InitAsync`、`InitWithGameObject`、`Show`、`Hide`、`Destroy` 暂时保留为旧调用兼容外观，底层复用同一生命周期和清理路径，不构成第二套状态机。

## 导航状态与表现边界

- `UIStack` 只保存已提交导航状态，不持有 Mask、Button 或 Root，也不调用 `View.Show()` / `View.Hide()`、操作 Transform 或 GameObject。Page、Modal、Widget 集合与快照都只通过不可修改视图对外暴露。
- `UICache` 只会为 `Destroyed` 的旧实例创建替代实例。`Faulted` View 必须由 `UIManager` 事务先移栈、等待 `DestroyAsync`，再从 Cache 移除；Cache 不持有 Stack，也不自行等待销毁。
- Coordinator 使用单一状态锁保护 queue、current、current CTS、Pump 与 Dispose 状态；锁内只接纳已在外部准备好的同步 Show candidate，不调用 Cache、View 构造或任何外部委托。取消、TCS 完成、registration 释放和异步执行也都在锁外，executor 总是在 Unity 主线程串行运行。
- Coordinator 保证不同目标操作严格 FIFO；同类型 Show/Replace 与 Close 反向操作会取消 current，但反向操作仍按队列顺序执行。pending 调用方取消会立即返回 Canceled，不等待队首，也不产生 View 副作用。
- `CloseAllAsync` 会原子取消 current 和调用时的 pending，再作为后续唯一清理操作执行。单个 View 销毁异常不会中断其它 View，最终仍清空 Cache、Stack、Mask 和名称，并通过 Failed + `AggregateException` 返回清理异常。CloseAll 在执行中被调用方或后一个 CloseAll 取消时仍完成全量收口，但结果优先返回 Canceled。
- 没有 View 可清理的 `CloseAllAsync` 是幂等成功，返回 `Succeeded` 且 `View == null`；这是成功结果允许空 View 的唯一 action。
- 加载成功前不修改正式栈。Show/Replace/Close/Back 同时捕获 `UIStackSnapshot` 与表现快照；取消或异常时精确恢复 View parent、sibling、active、稳定状态、Reference、Mask 字段及名称。事务中进入 Faulted 的目标即使原本在栈内也会移栈、销毁并移出 Cache。
- 栈提交、目标 Enter、露出 View 与表现调整属于可逆事务；这些步骤成功后即越过 commit point。随后旧 View/关闭 View 的 Destroy 是 post-commit best-effort cleanup：异常统一记录并始终按实例移出 Cache，不回滚或复活 Destroyed View，核心导航结果保持 Succeeded。
- `OnBehind`、`OnOpen`、`OnClose` 只在核心事务步骤全部成功后发布；单个订阅者异常会统一记录，但不回滚已提交导航，也不阻止其它订阅者。
- `OnBeforeOpen` 的多播订阅者按注册顺序逐个等待；任一订阅者失败会保留原异常、停止后续订阅者并回滚本次导航。
- 旧 `NewStack` / `RemoveStack` 命名栈兼容实现已移除；命名栈不属于当前导航模型。

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

- 新业务优先等待 `ShowAsync<T>()`、`ReplaceAsync<T>()`、`CloseAsync<T>()`、`BackAsync()`、`CloseAllAsync()`，并检查 `UIOperationResult.Status`；Failed 时读取 `Exception`。
- Push Page 会在新页面加载完成后退出旧 Page 并入栈；Replace 首版只支持 Page，成功后移除并按 `DestroyOnHide` 清理旧 Page。
- `ShowAsync<T>(new UIShowOptions(animated, hidePrevious: false))` 与旧同步 `Show<T>(hidePrevious: false)` 会保留同层上一 View 的 Visible 状态；关闭新 View 时不会再次 Enter 已经 Visible 的旧 View。默认 `HidePrevious` 为 true。
- 导航事务统一兼容旧 `ICameraAnimation` / `IUIAnimation`：进入按 Camera Show → `EnterAsync` → UI Show，退出按 Camera Hide → UI Hide → `ExitAsync` 串行等待。每个旧动画阶段在调用前记为已尝试；失败或取消时先恢复表现快照，再按相反顺序 best-effort 补偿，补偿异常只记录且不覆盖原始结果。
- Back 优先关闭 TopModal，再关闭 CurrentPage；露出的旧 Modal/Page 会恢复 Visible。
- 重复显示已经稳定处于顶部且 Visible 的同一单实例返回 Ignored，不重复 Hook、Transition 或引用计数。
- `Show<T>()`、`Close<T>()`、`Back()` 和 `CloseAll()` 仅为迁移期兼容包装；fire-and-forget 路径统一观察 Failed.Exception 并写入错误日志。
- 旧同步 `Show<T>()` 只在 Unity 主线程锁外准备 candidate，再由 Coordinator 原子接纳；后台线程调用不会构造 View 或访问 Unity 对象，只排队并返回 null，实际实例由 executor 回到主线程后创建。
- 更早的同类型 Close 或任意 Back、Replace、CloseAll 处于 current/pending 时，同步 `Show<T>()` 及数据泛型重载不会接纳可能被销毁的 candidate，而是返回 null 并继续把 Show 排在 FIFO 后执行；执行时会清理 Faulted/Destroying/Destroyed 旧目标并取得可用实例。已发现 CloseAll barrier 时还会跳过 candidate 构造快速路径。
- 数据泛型 Show/Preload 把 `SetData` 作为 operation 配置载荷，在轮到该 operation 时才应用，避免后一次调用提前覆盖前一次仍在加载的 View 数据。
- `Preload<T>()` 也进入同一 FIFO 队列，不与导航事务并发修改 View 状态；成功后保持 `LoadedHidden` 且不修改正式栈。
- Replace 收到 Modal 或 Widget 时返回 Failed；只有本次 operation 新建的目标才会销毁并移出 Cache，已显示或已预加载的既有实例、栈、Mask 和名称保持不变。

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
- `Core.Tests.UI.UIViewLifecyclePlayModeTests` 在真实 Play Mode 中检查加载、独立 waiter 取消、加载中销毁、稳定 Transition、幂等释放、subView 无环约束和 Destroyed 缓存替换。
- `Tools/SleepyDemos/UI Framework Validation/Validate Generated Prefabs` 实例化验证 Prefab，覆盖 MvcBind 绑定和基础组件交互。

统一运行方式见 [运行 Unity 自动化测试](../runbooks/run-unity-tests.md)。
