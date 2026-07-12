# Core UI 渲染设计原则

## 目标

Core UI 需要在一套业务 UI 系统中同时支持普通平面界面和绕 X/Y 轴旋转的透视界面，并让两者继续参与统一的层级、遮罩和生命周期管理。

## 为什么这样设计

业务 UI 统一使用独立透视 `UICamera` 和 `ScreenSpaceCamera`。普通 View 保持零旋转时表现为平面 UI，需要透视效果时旋转 View 或内部视觉节点即可，不需要建立另一套 UI 管理器。

Canvas 使用「总 Canvas + 固定层级子 Canvas」结构：

```text
UIRootCanvas
├── UndergroundLayer Canvas (0)
├── BaseLayer Canvas (100)
├── ForegroundLayer Canvas (150)
├── PopLayer Canvas (200)
├── DecorateLayer Canvas (250)
└── TipLayer Canvas (300)
    ├── TipContent（Tip View 挂载根）
    └── InteractionGate（透明全屏射线拦截，固定在内容根之后）
```

总 Canvas 统一 Camera 和屏幕适配参数。固定子 Canvas 是业务渲染与重建边界，避免一个动态界面使全部业务 UI 进入同一重建域。Canvas 之间不能合批，因此不继续细分到每个 View。

## 分层边界

- `UIRootCanvas` 只负责 `RenderMode`、`UICamera`、`CanvasScaler` 和 Shader Channels，不直接放业务 Graphic。
- 固定子 Canvas 使用 `overrideSorting=true` 和稳定 `sortingOrder`，层内 View 继续用 sibling 顺序排列。
- `Underground` 是纯展示层，不参与 Graphic Raycast；其余层默认允许交互。
- View Prefab 根节点不携带 `Canvas`、`CanvasScaler` 或 `GraphicRaycaster`。
- `StartupLoading.prefab` 在业务 UI 初始化前显示，继续使用独立 Overlay Canvas。
- 真正处于 3D 场景中的 World Space UI 不进入 Core UI 业务层级。

## 透视 UI 规则

- 整个界面需要倾斜时，可以直接旋转 View 根 `RectTransform`。
- 遮罩或全屏点击区域需要保持平面时，在 View 内增加普通 `PerspectiveRoot`，只旋转该节点下的视觉内容。
- 框架不提供强制的 `PerspectiveRoot` 组件或第二套 View 类型。

## 关键取舍

- 固定层级 Canvas 会增加少量固定 Draw Call，但可以隔离层级间的 Canvas 重建。
- View 默认不使用独立 Canvas，避免窗口级排序参数漂移和 Canvas 数量持续增长。
- 只有 Profiler 证明某个高频动画需要进一步隔离时，才允许在 View 内增加局部 Sub-Canvas。
- 局部 Sub-Canvas 默认保持 `overrideSorting=false`，继承所属固定层的排序，不能借此跨越其它业务层。

## 与其它模块的关系

- `UIManager` 负责 View 生命周期、缓存和栈，不负责配置渲染环境。
- `UIRootManager` 是 Canvas、UI Camera、EventSystem、固定层 Root 和遮罩的唯一装配入口。
- Core 只定义 `IUIWorldTransition` / `IUIWorldTransitionProvider` 并编排事务，不实现具体 Camera 或场景移动。Hotfix 通过 `UIManager.RegisterWorldTransitionProvider(...)` 注册业务解析器；未注册或解析结果为 null 时使用无行为实现。
- UI 表现统一使用 `IUITransition`，Camera、场景与 Timeline 联动统一使用 `IUIWorldTransition`。旧 `IUIAnimation` / `ICameraAnimation` 契约和运行时调用链已经移除，不再存在并行的动画生命周期。
- 每个导航事务在开始执行时快照 Provider，并对实际进入、退出的每个 View 最多解析一次。退出阶段的 UI / World Transition 并行完成后才提交栈；提交后进入阶段的 World / UI Transition 并行完成，两个阶段不得交错。
- 非动画导航会让 UI / World Transition 同时立即完成到目标方向；失败或取消回滚时，已尝试的 World Transition 复用本事务解析出的同一实例恢复到事务前方向。世界补偿异常只记录，不覆盖导航主异常。
- Modal Mask 与 `InteractionGate` 是两套独立设施：Mask 只从 `UIStack.TopModal` 刷新父节点、sibling、缩放和关闭交互，Page/Widget 不得直接显示或隐藏 Mask；Gate 固定在 Tip 层的 `TipContent` 之后，只在导航执行期间切换透明 Image 的 `raycastTarget`。两者不得复用对象，也不得互相修改颜色、透明度、父节点或交互状态。
- 未显式覆盖 Transition 的 View 默认使用 `FadeScaleUITransition`：进入从透明、`0.95` 倍缩放恢复到完全可见，退出反向收口。它只修改 View 根节点的 `CanvasGroup.alpha` 与 `Transform.localScale`，不负责 Mask、层级或业务显隐。
- Hotfix 只选择 `UILayer`、制作 View 内容并提供具体 World Transition 工厂，不直接修改 Root Canvas 或由 Core 虚构通用 Camera 移动。首版 `HotfixWorldTransitionProvider` 没有默认注册项，因此没有业务配置时不会移动相机。

## 修改原则

- 新增或调整业务层级时，同时更新 `UILayer`、`UIRootManager` 层级配置、PlayMode 测试和本文件。
- 修改参考分辨率、Camera FOV 或层级排序后，必须在常用宽高比下验证布局、射线和透视效果。
- 不通过给每个 View 补 Canvas 来解决局部排序问题；先检查所属层级和 sibling 顺序。

