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

## 基础组件边界

本模块只承载基础有限 UI 组件：
- `UITab`：有限 Tab，支持静态项和基于 prefab 的有限动态项，支持 Button/Toggle、选择状态和拦截回调。
- `ViewList`：有限列表，复用已创建 View 项，不包含循环列表或无限滚动。
- `UIBtnSwitch`：按钮开关状态组件。
- `UIDropdown`：基于 `UITab` 的基础下拉选择组件。
- `UIState`：序列化状态切换组件，用于 Normal/Selected 等轻量状态；状态项使用固定枚举和强类型组件引用，不使用反射属性名或字符串解析。

明确不在这里混入：
- 循环列表
- 无限滚动
- 复杂滚动复用池
- 业务图片加载器
- 具体 Demo 或页面业务状态

## UIManager 使用规则

- 打开界面优先使用 `UIManager.Show<T>()`。
- 关闭界面使用 `UIManager.Close<T>()` 或 `Back()`。
- 需要提前加载资源时使用 `Preload<T>()`，不要绕过 `View` 生命周期直接实例化 UI 预制体。
- 清空所有界面时使用 `CloseAll()`，它会隐藏并销毁当前缓存中的 View，并清空 UI 栈和缓存。
- `DestroyOnHide` 为 true 且引用计数归零时，View 会在关闭后销毁并释放 loader。
- 快速重复打开同一界面时，`UIManager` 会防止同一类型重复进入异步打开流程。

## 修改注意

- 不要让 Core UI 依赖 Hotfix。
- 不要让业务页面直接依赖 YooAssets 句柄或包类型。
- 新组件优先放在 `Assets/Scripts/Core/Runtime/Components`。
- 如果要引入循环列表，应单独设计模块和文档，不要塞进 `ViewList`。

## 验证入口

在 Unity Editor 中可以运行：
- `Tools/SleepyDemos/Validate Core Runtime Infrastructure`

该菜单会检查基础组件类型是否存在，并扫描 UI/Startup/HotUpdate/Hotfix 外层代码是否混入资源底层类型、循环列表或 fishinggameplay 业务依赖。

命令行或 CI 可以使用 Unity 参数：
- `-executeMethod Core.Editor.CoreRuntimeInfrastructureValidator.ValidateForBatchMode`
