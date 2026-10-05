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
- `HotfixAssemblySystem`
- `RuntimeServiceRegisterSystem`
- `HotfixEntrySystem`

`RuntimeServiceRegisterSystem` 注册 `StreamlineRuntime`，不再由某个 Demo 管理 DLSS 生命周期。全局设置默认关闭；用户曾保存启用档位时，公共主相机绑定后应用。Hub、Demo 和已有 Editor 直启共用同一渲染服务与公共画质设置。

## 热更入口

Hotfix 入口位于：
- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`

当前行为：
- 补充扫描当前 Hotfix 程序集的具体 View；Core 在装配阶段已经扫描的程序集直接跳过，每轮只发现一次，名称查找不再遍历 AppDomain
- 运行 `HotfixBootService.RunBootSystems`
- 先通过 `LubanConfigSystem` 完整加载业务配置，再通过 `GlobalDataSystem` / `FluxService` 注册 Hotfix 全局 Flux Data
- 注册 Hotfix World Transition Provider，并等待 `MainMenuView.ShowAsync` 稳定进入
- 仅在主界面导航成功或已稳定存在后销毁启动加载界面；Failed 保留原异常，Canceled 中断启动

当前 Hotfix 启动系统说明见 [Hotfix 启动系统](../modules/hotfix-boot-systems.md)。

## 启动完成后的场景边界

- `AppEntrance` 是常驻 Hub 壳，只在应用冷启动时运行 `CoreEntrance`。
- Hotfix 初始化 `GameSceneNavigator` 后，Demo 统一通过 YooAsset Additive 加载。
- 返回 Hub 只卸载当前 Demo 并恢复 Hub 相机和主菜单，不以 Single 模式重载 `AppEntrance`。
- 启动期 `StartupLoading` 与运行期 `CommonLoadingView` 分属两条生命周期，不互相依赖。

`StartupLoadingView` 属于 Core.Runtime，在资源服务及 Hotfix 就绪前展示。标题、阶段、说明、百分比与大小字段统一使用直接序列化的 `TMP_Text`；Prefab 同时直接引用公共字体、材质及必要回退字库，不能等待资源地址加载或 Hotfix/UIBind 初始化后才补绑定。进度仍来自 `StartupStateMachine` 与各启动系统，View 不用定时器推进假进度。空阶段、说明或大小信息隐藏对应独立文本节点；后续报告非空信息时恢复显示。背景与进度条继续直接引用 `Image`。

运行期 `CommonLoadingView` 保持现有 UIBind/TMP 字段及普通 Core View 导航链，不因视觉统一改变依赖层。修改这两套加载 Prefab 的具体步骤见[接入 Core UI View](../runbooks/create-ui-view.md#维护启动与场景加载界面)。

## Unity Editor Demo 直启旁路

Editor 可在 Demo 场景通过独立 `DemoIslandEditorBootstrap` 补齐最小运行时：读取正式 `HotfixConfig`，初始化 `ResourceServices`/YooAsset 和 `UIManager`，扫描 Hotfix View，幂等运行 `HotfixBootService`，注册世界过渡并建立 Editor 直启导航器。

该旁路不执行 HybridCLR 元数据补充、热更新程序集装配、完整 `HotfixEntry` 或 MainMenuView。Development Build 和 Release 不启用此旁路，仍固定从 AppEntrance 进入。Bootstrap 发现正式 Navigator 已存在时必须立即退出。

## 修改这里时必须注意

- 不要把业务 UI 初始化提前塞进 Core 的低层系统里
- 改状态顺序时，要同步检查资源、程序集和 UI 的前置依赖
- 改 Hotfix 入口时，要同步检查 UIBind 生成代码、预制体地址和主菜单可见性
- 改资源底层实现时，优先替换 `IResourceService` 注册点和实现层，不要把具体资源框架类型扩散到 UI 或 Hotfix
- 调整 Hotfix 启动系统顺序时，必须保持依赖配置的业务初始化位于 `LubanConfigSystem` 之后
- 只要启动链路变化，就必须同步更新本文档

## 排障定位建议

- 启动报错先看 `CoreEntrance` 是否挂载完整
- 资源相关问题先看 `ResourceStartupState`
- 热更程序集加载问题先看 `BeforeHotfixStartupState`
- 主界面不显示先看 `HotfixEntry` 与 `MainMenuView` 注册链路
- 配置加载失败先看 `LubanConfigSystem` 日志中的表名和资源地址，再按 [Luban 配置 runbook](../runbooks/use-luban-config.md) 检查生成物与采集设置

## 独立试玩包入口

HotfixConfig.StartupScene为空时仍显示Hub。独立包可在构建专用配置中指定Hotfix场景目录名称；Core只携带字符串，不引用业务枚举。HotfixEntry完成原资源/Boot初始化后解析目标，经同一GameSceneNavigator加载业务场景，成功后关闭启动Loading，不先闪现MainMenuView。未知目标使启动明确失败，不静默回Hub。原AppEntrance和默认配置不因独立包构建而修改。
