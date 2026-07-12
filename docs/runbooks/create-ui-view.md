# 接入 Core UI View

## 适用场景

用于在 Hotfix 或 Demo 中新增业务 View，或整理已有 UI Prefab，使其进入 Core UI 的固定层级 Canvas。

## 前置条件

- View 资源位于 `Assets/LoadResources/UI` 或对应 Demo 的可加载资源目录。
- View 代码继承 `Core.Runtime.View` 或其泛型版本。
- 新业务通过 `await UIManager.ShowAsync<T>()`、`await CloseAsync<T>()` 管理生命周期；`Preload<T>()` 也会进入同一导航队列。

## 制作 Prefab

1. 使用全屏或内容尺寸明确的 `RectTransform` 作为 Prefab 根节点。
2. 根节点使用 UI Layer。
3. 不要在根节点添加以下组件：
   - `Canvas`
   - `CanvasScaler`
   - `GraphicRaycaster`
4. 打开现有 MvcBind 工具并选择正确的 `UILayer`。
5. 显式选择 `ViewMode`：主页面用 `Page`，弹窗用 `Modal`，常驻挂件或 HUD 用 `Widget`。不要只依赖 `UILayer` 推导。
6. 选择该 View 使用的 `UI Transition` 类型。未生成显式覆盖时，框架默认使用 `FadeScaleUITransition`；不需要视觉过渡时显式选择 `EmptyUITransition`，需要其它表现时选择自定义实现。
7. 只有需要世界表现过渡时才填写 `World Transition Key`。该字段不会在生成代码中实例化世界过渡；首版 Hotfix Provider 按 View 精确类型解析，Key 保留给后续业务 Provider 自定义路由，不要把实现类型名写入 Key。
8. 完成组件和回调绑定后生成 View 与 Component 代码。生成的 Component 会显式覆盖 `ViewMode`，并通过 `CreateUITransition()` 工厂创建 UI Transition。

## Transition 生命周期

生成的 `CreateUITransition()` 只负责返回该 View 使用的 Transition 类型：

```csharp
protected override IUITransition CreateUITransition()
{
    return new ExampleUITransition();
}
```

资源加载成功后，`View` 会缓存这一个实例，并使用 View 根节点调用一次 `Initialize()`。进入和退出都复用该实例，销毁 View 时由 `View.DestroyAsync()` 调用一次 `Dispose()`。

业务 View 必须遵守以下规则：

- 不在 `OnShow()`、`OnHide()` 或其它业务回调中重复 `new` Transition。
- 不自行调用 Transition 的 `Initialize()` 或 `Dispose()`。
- 不缓存第二份 Transition 引用；需要扩展时只覆写 `CreateUITransition()`。
- 不自行创建、保存或 Kill DOTween tween；取消、立即完成和销毁统一交给 Transition 实例处理。
- 加载或过渡收到取消时，让 `OperationCanceledException` 继续返回框架层，不在业务 View 中吞掉。
- 旧 `Show()` / `Hide()` 调用暂时仍可使用；它们是生命周期兼容外观，不代表业务侧拥有 Transition。

## 接入 World / Camera 过渡

Core 不提供虚假的通用 Camera 移动。需要页面切换联动相机、场景状态或 Timeline 时，在 Hotfix 启动阶段注册 `HotfixWorldTransitionProvider`，再按 View 精确类型注册工厂：

```csharp
var provider = new HotfixWorldTransitionProvider();
provider.Register<ExampleView>(() => new ExampleWorldTransition());
UIManager.Instance.RegisterWorldTransitionProvider(provider);
```

工厂规则：

- 每次调用必须返回该导航事务独立使用的 `IUIWorldTransition` 实例；不要返回跨事务共享的可变动画对象。
- 重复注册同一 View 类型会替换旧工厂；传入 null 工厂会立即抛出参数异常。
- 注册、移除和解析都发生在 Unity 主线程。工厂异常会作为本次导航 Failed 的原始异常传播。
- 未注册类型、工厂主动返回 null，或通过 `RegisterWorldTransitionProvider(null)` 清除 Provider 时，Core 使用无行为过渡。
- `EnterAsync` / `ExitAsync` 必须观察传入的取消令牌；`CompleteImmediately` 必须能把世界表现同步到指定确定方向。
- 不要在 `OnShow` / `OnHide` 中重复启动相机过渡。UI 与 World Transition 由同一导航事务等待，失败和取消时由框架回滚。

固定层的用途如下：

| 层级 | 用途 |
| --- | --- |
| `Underground` | 业务 UI 下方的纯展示内容，不接收 UI 射线 |
| `Base` | 主页面和常规全屏界面 |
| `Foreground` | Base 上方的前景内容 |
| `Pop` | 弹窗及其遮罩 |
| `Decorate` | 挂件、HUD 和装饰性浮层 |
| `Tip` | 最高优先级提示和临时交互 |

## 制作透视效果

整个 View 都需要透视旋转时，直接旋转根 `RectTransform`：

```csharp
view.transform.localRotation = Quaternion.Euler(10f, -20f, 0f);
```

只需要旋转弹窗主体时，在 Prefab 内增加普通 `PerspectiveRoot`：

```text
ViewRoot
├── Blocker
└── PerspectiveRoot
    └── Panel
```

旋转 `PerspectiveRoot`，让全屏遮罩、适配根节点和点击区域保持平面。`PerspectiveRoot` 只是命名约定，不需要添加框架组件。

## 局部 Sub-Canvas

不要为了独立排序默认添加 Sub-Canvas。只有 Unity Profiler 显示某个持续动画使整个固定层频繁重建时，才在对应视觉节点增加局部 Canvas。

局部 Canvas 必须遵守：

- 不添加 `CanvasScaler`。
- 默认 `overrideSorting=false`，继承所属固定层排序。
- 只覆盖高频变化的视觉子树。
- 修改前后对比 Canvas Build Batch、CPU 和 Draw Call，确认优化有效。

## 常见问题

### View 显示顺序不对

先检查 `UILayer` 和同层 sibling 顺序。不要给 View 根节点添加 Canvas 或自行修改全局 `sortingOrder`。

### 绕 X/Y 轴旋转没有透视

确认 View 由 Core UI 打开，并位于 `UIRootCanvas` 的固定子 Canvas 下。独立 Overlay Canvas 不经过透视 `UICamera`。

### 点击区域跟着主体倾斜

将全屏 Blocker 留在 View 根节点，只把需要倾斜的主体放进 `PerspectiveRoot`。

## 验证方式

业务调用应检查异步导航结果：

```csharp
var result = await UIManager.Instance.ShowAsync<ExampleView>();
if (result.Status == UIOperationStatus.Failed)
{
    Debug.LogException(result.Exception);
}

await UIManager.Instance.CloseAsync<ExampleView>();
```

需要让同层旧 View 保持可见时，显式传入：

```csharp
await UIManager.Instance.ShowAsync<ExampleView>(
    new UIShowOptions(animated: true, hidePrevious: false));
```

- `Succeeded` 表示事务已提交；`Ignored` 表示同一单实例已稳定处于目标位置。
- `Canceled` 表示调用方取消、反向操作抢占，或目标已不存在，不应当作错误日志。
- `Failed` 表示加载、Hook 或过渡异常；框架会回滚正式栈并清理 Faulted View。
- `OnBeforeOpen` 多个订阅者按注册顺序串行等待；前一个失败后不会调用后续订阅者。
- 旧 `ICameraAnimation` / `IUIAnimation` 仍由导航事务等待执行；不要在 `OnShow` / `OnHide` 中再次手动调用，避免重复动画。
- `Show<T>()` / `Close<T>()` 同步外观仅用于旧代码迁移，新业务不要依赖其 fire-and-forget 完成时机。
- CloseAll 并发窗口内同步 `Show<T>()` 及数据泛型重载会安全返回 null，但 Show operation 仍排在 CloseAll 后执行；新业务必须优先 `await ShowAsync<T>()`。
- 数据泛型兼容入口的 `SetData` 会随导航 operation 按 FIFO 应用；不要在调用 Show/Preload 前自行修改缓存 View 的数据。
- `CloseAllAsync()` 即使返回 Failed 也会完成全量清理；其 `Exception` 可能是包含多个 View 销毁异常的 `AggregateException`。
- CloseAll 在执行期间收到取消也会完成全量清理，最终返回 Canceled；不要把 Canceled 理解为“没有执行清理”。
- 空 Cache 上的 `CloseAllAsync()` 返回 `Succeeded` 且 `View == null`，调用方应按 `Status` 判断，不要把空 View 当作失败。

1. 运行 `Core.Tests.UI.UIViewPrefabConventionTests`，确认 Prefab 根节点规则通过。
2. 修改 View 生命周期或 Transition 时，运行 `Core.Tests.UI.UIViewLifecyclePlayModeTests`。
3. 接入 World / Camera 过渡时，运行 `Core.Tests.UI.UIWorldTransitionPlayModeTests` 和 `Core.Tests.UI.UIManagerNavigationPlayModeTests`。
4. 运行 `Core.Tests.UI.UIRootManagerPlayModeTests`。
5. 从 `AppEntrance` 进入主界面，验证显示、点击、关闭和返回。
6. 临时旋转 View 或 `PerspectiveRoot` 的 X/Y 轴，确认透视效果和射线区域符合预期。
7. 在 `16:9`、超宽和窄屏 Game View 下检查布局。
