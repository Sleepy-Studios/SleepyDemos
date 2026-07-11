# 大型项目 UI 导航与过渡系统设计

## 1. 背景与目标

SleepyDemos 已具备 `UIManager`、`UIStack`、`UICache`、`UIRootManager`、`View`、MvcBind、YooAsset 加载抽象以及固定层 Canvas。当前缺口不是再造一套 UI 框架，而是把现有骨架提升为可以支撑大型项目的确定性运行时：页面切换可等待、可取消、可回滚，UI 与世界/相机过渡有明确边界，快速连续操作不会破坏栈、遮罩、缓存和资源状态。

本设计的目标是：

- 保留 `Core.Runtime` / `Core.Editor` / `Hotfix` 现有边界，不引入并行 UI 框架。
- 提供确定性的异步导航事务，消除 `Forget()` 驱动的幽灵页面和 Show/Close 竞态。
- 明确 Page、Modal、Widget 三种展示语义，分离“渲染层级”和“导航行为”。
- 提供稳定实例、可取消、可立即完成的 UI Transition。
- 在 Core 中定义 World Transition 扩展点，由 Hotfix 实现具体 Camera、场景和角色联动。
- 建立覆盖加载、栈、遮罩、动画、取消和失败回滚的自动化测试。

非目标：

- 不照搬 fishinggameplay 的 `UIManager` 或静态 `CameraAnimationBase`。
- 不在第一轮支持同类型顶级页面多实例；列表项继续由 `ItemView` / `ViewList` 管理。
- 不在没有真实业务用例前实现任意路由图、深链、URL 导航或 UI 状态持久化。
- 不让 Core 知道钓鱼、主城、水族馆等具体场景。

## 2. 总体架构

系统拆成六个职责单元：

1. `UIManager`：面向业务的门面，只负责参数校验、兼容 API 和事件发布。
2. `UINavigationCoordinator`：串行调度导航操作，管理取消、反向操作、事务提交和失败回滚。
3. `UIStack`：只维护已提交的 Page、Modal、Widget 与层级顺序，不加载资源、不播放动画。
4. `View`：拥有资源实例、组件绑定、稳定 Transition 实例和自身生命周期状态。
5. `IUITransition`：只改变 UI 表现；默认实现使用项目已有 DOTween，所有 Tween 必须可 Kill 并落到确定终态。
6. `IUIWorldTransitionProvider`：Core 定义的可选扩展点；Hotfix 根据进入/退出 View 解析世界过渡，具体控制 Camera、场景对象或角色。

调用链为：

```text
Hotfix
  -> UIManager.ShowAsync<T>()
  -> UINavigationCoordinator.Enqueue()
  -> 创建导航事务快照
  -> View.InitAsync()
  -> 退出页面 UI Transition + World Transition
  -> 提交 UIStack / Mask / sibling
  -> 进入页面 UI Transition + World Transition
  -> 发布成功事件并返回 UIOperationResult
```

任何一步失败或被取消，Coordinator 都按照事务快照恢复上一稳定状态。栈中只保存已提交页面，正在加载的页面属于进行中的操作，不提前写入正式栈。

## 3. 核心契约

### 3.1 展示语义

新增 `UIViewMode`：

- `Page`：进入历史栈；默认隐藏前一个 Page；Back 恢复前一个 Page。
- `Modal`：叠加在当前 Page 上；不隐藏 Page；参与遮罩和 Modal 栈。
- `Widget`：不进入返回历史；按类型独立显示和关闭。

`UILayer` 继续只表达渲染顺序。`Pop` 不再自动等价于 Modal，`Decorate` / `Tip` 也不再作为 Widget 的唯一判据。为兼容现有页面，默认映射为：`Base` / `Foreground` → Page，`Pop` → Modal，`Decorate` / `Tip` → Widget；新生成代码必须显式写出 `ViewMode`。

### 3.2 导航动作

第一版提供：

- `ShowAsync<T>()`：根据 `T.ViewMode` 执行 Page Push、Modal Present 或 Widget Show。
- `ReplaceAsync<T>()`：用新 Page 替换当前 Page，不保留被替换页面的历史位置。
- `CloseAsync<T>()`：关闭指定 Modal、Widget 或当前 Page。
- `BackAsync()`：优先关闭最上层 Modal，否则 Pop 当前 Page。
- `PreloadAsync<T>()`：只完成加载和初始化，不改变正式导航栈。
- `CloseAllAsync()`：取消进行中的操作，按 Modal → Widget → Page 顺序无动画关闭并释放。

暂不提供 `BackTo<T>`、同类型多实例和命名栈。真实 Demo 出现跨域导航需求后，再以独立规格扩展，避免复制 fishinggameplay 的历史复杂度。

### 3.3 操作结果

所有异步入口返回 `UniTask<UIOperationResult>`。结果包含：

- `Status`：`Succeeded`、`Canceled`、`Failed`、`Ignored`。
- `View`：成功或被忽略时对应的 View。
- `Exception`：失败原因；成功和取消时为空。
- `OperationId`：单调递增编号，用于日志和测试定位。

资源地址为空、加载返回 null、生命周期回调异常和 Transition 异常均返回 `Failed`，同时记录统一错误日志。调用者取消返回 `Canceled`。重复打开已经稳定显示的同一个单实例 View 返回 `Ignored`，不重复执行 `OnShow`。

保留现有同步外观 `Show<T>()` / `Close<T>()` 一个迁移周期，它们内部调用异步入口并记录未观察失败；新代码和 MvcBind 模板只生成异步用法示例。

## 4. 并发、取消与事务

所有会修改导航状态的操作进入一个 FIFO 队列，保证栈和 Mask 只有一个写入者。

特殊规则：

- 当前正在打开 T 时收到关闭 T：取消当前打开，随后执行关闭；T 不得短暂进入稳定显示态。
- 当前正在关闭 T 时收到打开 T：取消退出 Transition，恢复到显示终态，本次打开返回成功。
- 连续打开不同 Page：保持请求顺序，不允许加载完成顺序决定最终页面。
- `CloseAllAsync()`：取消当前操作、清空待执行队列，再执行无动画清理事务。
- 外部 CancellationToken 只取消对应调用，不清除其它调用。

每次事务开始时保存：Page/Modal/Widget 栈快照、当前 Mask 目标、进入/退出 View 的状态和 sibling。只有加载成功且退出阶段可提交时才修改正式栈。提交后进入动画失败，则直接完成到进入终态并报告失败，不把系统留在半透明或不可交互状态。

## 5. View 生命周期

`ViewState` 从可组合 Flags 改为单值状态：

```text
Created -> Loading -> LoadedHidden -> Entering -> Visible
Visible -> Exiting -> LoadedHidden
LoadedHidden -> Destroying -> Destroyed
任意进行态 -> Faulted（事务负责恢复或销毁）
```

公开生命周期顺序：

```text
OnBeforeLoad
资源加载
InitComponent
IUITransition.Initialize
OnLoaded
OnBeforeEnter
进入 Transition
OnEntered
OnBeforeExit
退出 Transition
OnExited
OnBeforeDestroy
释放实例与 Loader
OnDestroyed
```

`OnEntered` 之后 View 才被视为稳定可交互；`OnBeforeExit` 开始时停止接收业务输入。数据订阅建议在 `OnBeforeEnter` 建立、`OnExited` 释放，长期资源绑定在 `OnLoaded` / `OnBeforeDestroy` 管理。

## 6. UI Transition

以 `IUITransition` 替换当前过于简化的 `IUIAnimation`：

```csharp
public interface IUITransition
{
    void Initialize(Transform root);
    UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);
    UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);
    void CompleteImmediately(UITransitionDirection direction);
    void Dispose();
}
```

`UITransitionContext` 明确包含 EnteringView、ExitingView、NavigationAction、IsAnimated 和 OperationId，不允许 Transition 通过 `UIManager.LastCloseName` 猜测来源页面。

每个 View 在首次加载时调用一次 `CreateUITransition()`，由 View 基类缓存并持有返回实例，销毁时 Dispose。MvcBind 只生成工厂方法覆盖，不能覆盖 `UITransition` 属性并在每次读取时 `new`。

Core 第一版提供：

- `EmptyUITransition`：立即完成。
- `FadeScaleUITransition`：CanvasGroup Alpha + 根节点 Scale，使用 DOTween；Enter/Exit 前 Kill 旧 Tween；取消时落到与新操作一致的终态。

全局 `UIInteractionGate` 在导航过渡期间通过独立最高优先级透明 Graphic 阻止点击，使用引用计数处理嵌套操作。Modal Mask 仍只负责背景显示和关闭行为，二者不能复用。

## 7. World / Camera Transition

Core 不直接引用具体 Camera Controller。Core 只定义：

```csharp
public interface IUIWorldTransitionProvider
{
    IUIWorldTransition Resolve(View view);
}
```

以及与 `IUITransition` 对称的 `IUIWorldTransition`。未注册 Provider 或解析为空时视为立即完成。

Hotfix 提供 `HotfixWorldTransitionProvider`，以后按 View 类型返回具体实现。具体实现可以操作 Camera、Cinemachine、Timeline、场景对象或角色，但必须遵守：

- 不读取 `CurrentUIName` / `LastCloseName` 字符串推断路由，统一使用 `UITransitionContext`。
- 不把业务网络请求、支付逻辑或音频系统直接塞进通用过渡基类。
- 必须支持取消和 `CompleteImmediately`。
- 需要恢复相机状态时，由实例状态或显式快照保存，不使用全局静态 Last/Next。
- World Transition 失败不能破坏 UI 栈；Coordinator 记录错误并将世界状态立即完成到目标端。

第一阶段只实现 Provider、空实现和测试用 Fake，不制作无真实业务需求的相机运动。首个需要 UI 与 3D 场景联动的 Demo 再增加具体 Hotfix Transition。

## 8. 缓存与资源所有权

- 一个顶级 View 类型对应一个缓存实例。
- `PreloadAsync` 创建缓存但不增加导航引用。
- 正式进入栈后增加导航引用；从所有栈移除后减少引用。
- `DestroyOnHide=true` 且引用为 0 时销毁。
- 加载失败必须 Dispose Loader，并从 UICache 移除失败实例。
- `CloseAllAsync` 必须等待正在加载的 View 结束或取消后的资源回收。
- Transition 不持有资源 Loader，也不自行释放 View GameObject。

## 9. 错误与诊断

统一日志前缀 `[UI]`，每条导航日志包含 OperationId、Action、View 类型和状态。新增事件：

- `OperationStarted`
- `OperationCompleted`
- `OperationFailed`
- `ViewEntered`
- `ViewExited`

保留现有 `OnOpen` / `OnClose` 一个迁移周期，并在内部映射到稳定态事件。错误事件只用于观测，不允许监听器改变当前事务。

## 10. 测试策略

EditMode 覆盖纯状态和生成器：

- ViewMode 默认映射。
- UIStack Page/Modal/Widget 行为。
- MvcBind 生成稳定 Transition 字段。
- 操作结果与状态转换规则。

PlayMode 使用 Fake ResourceLoader、Fake Transition、Fake WorldTransition 覆盖：

- Show 成功及完整生命周期顺序。
- 重复 Show 返回 Ignored。
- 加载中 Close 不产生幽灵页面。
- 退出中重新 Show 恢复显示终态。
- 不同 Page 快速 Show 保持请求顺序。
- 加载失败和 Transition 异常回滚。
- Modal Mask 与 InteractionGate 独立工作。
- Back 优先关闭 Modal，再恢复 Page。
- Widget 关闭不改变 Page。
- DestroyOnHide 与 CloseAll 释放资源。

只运行当前任务涉及的精确测试类，不自动扩大到全量 Core.Tests。每一阶段通过对应最小测试后再提交。

## 11. 分阶段交付

1. 导航契约与 UIStack 纯状态模型。
2. View 单值生命周期、稳定 Transition 实例与 MvcBind 修正。
3. UINavigationCoordinator、异步 API、取消和失败回滚。
4. Page/Modal/Widget、Mask、InteractionGate 与默认 UI Transition。
5. World Transition Provider 扩展点及 Hotfix 注册。
6. 兼容 API 收口、诊断、完整模块文档和接入 runbook。

每阶段都必须保持主菜单可以正常进入，且不得要求一次性迁移所有 Hotfix 页面。

## 12. 验收标准

- 所有新业务页面通过可等待 API 打开和关闭。
- 快速 Show/Close/Back 不产生栈、Mask、缓存或交互状态错乱。
- MvcBind 生成的 Transition 在 View 生命周期内保持同一实例。
- UI Transition 与 World Transition 均支持取消和立即完成。
- Core 不依赖 Hotfix；具体 Camera/场景逻辑只存在于 Hotfix。
- 失败加载不会留下缓存实例或 YooAsset 句柄。
- 精确的 EditMode / PlayMode 测试覆盖所有规定竞态。
- `docs/architecture/ui-rendering.md`、`docs/modules/ui-runtime.md` 和 `docs/runbooks/create-ui-view.md` 与实现同步更新。
