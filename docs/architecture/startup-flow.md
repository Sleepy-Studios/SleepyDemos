# 启动与热更流程

## 入口

当前运行时入口在：
- `Assets/Scripts/Core/Runtime/Startup/CoreEntrance.cs`

它的职责很简单：
- 挂载为启动场景入口
- 构造 `StartupPipeline`
- 驱动整个启动状态机

## 主流程

当前 `StartupPipeline` 固定注册三个状态：

1. `PrepareStartupState`
2. `ResourceStartupState`
3. `BeforeHotfixStartupState`

### 阶段 1：Prepare

职责：
- 运行启动准备系统
- 建立后续流程需要的上下文

当前系统：
- `PrepareRuntimeSystem`

### 阶段 2：Resource

职责：
- 通过 Core 资源服务初始化当前资源实现
- 执行资源下载或资源准备

当前系统：
- `YooAssetInitializeSystem`
- `ResourceDownloadSystem`

当前底层实现仍是 YooAssets，但启动系统通过 `ResourceServices.Default` 访问资源服务；上层不应直接依赖 YooAssets 的包、句柄或操作类型。

### 阶段 3：BeforeHotfix

职责：
- 初始化 UI
- 加载 HybridCLR 元数据
- 装配热更程序集
- 注册运行时服务
- 切入 Hotfix 业务入口

当前系统：
- `UIInitializeSystem`
- `HybridMetadataSystem`
- `HotUpdateAssemblySystem`
- `RuntimeServiceRegisterSystem`
- `HotfixEntrySystem`

## 热更入口

Hotfix 入口位于：
- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`

当前行为：
- 扫描 Hotfix 程序集内的 View 类型
- 运行 `HotfixBootService.RunBootSystems`
- 先通过 `LubanConfigSystem` 完整加载业务配置，再通过 `GlobalDataSystem` / `FluxService` 注册 Hotfix 全局 Flux Data
- 注册 Hotfix World Transition Provider，并等待 `MainMenuView.ShowAsync` 稳定进入
- 仅在主界面导航成功或已稳定存在后销毁启动加载界面；Failed 保留原异常，Canceled 中断启动

当前 Hotfix 启动系统说明见 [Hotfix 启动系统](../modules/hotfix-boot-systems.md)。

## 修改这里时必须注意

- 不要把业务 UI 初始化提前塞进 Core 的低层系统里
- 改状态顺序时，要同步检查资源、程序集和 UI 的前置依赖
- 改 Hotfix 入口时，要同步检查 MvcBind 生成代码、预制体地址和主菜单可见性
- 改资源底层实现时，优先替换 `IResourceService` 注册点和实现层，不要把具体资源框架类型扩散到 UI 或 Hotfix
- 调整 Hotfix 启动系统顺序时，必须保持依赖配置的业务初始化位于 `LubanConfigSystem` 之后
- 只要启动链路变化，就必须同步更新本文档

## 排障定位建议

- 启动报错先看 `CoreEntrance` 是否挂载完整
- 资源相关问题先看 `ResourceStartupState`
- 热更程序集加载问题先看 `BeforeHotfixStartupState`
- 主界面不显示先看 `HotfixEntry` 与 `MainMenuView` 注册链路
- 配置加载失败先看 `LubanConfigSystem` 日志中的表名和资源地址，再按 [Luban 配置 runbook](../runbooks/use-luban-config.md) 检查生成物与采集设置
