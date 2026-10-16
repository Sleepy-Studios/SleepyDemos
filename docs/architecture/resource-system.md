# 资源系统设计原则

## 目标

资源系统的目标是让 Core、Hotfix 和 UI 使用统一资源语义，而不是让业务层直接感知 YooAssets 的包、句柄和操作对象。

当前底层实现是 YooAssets，但项目代码应依赖 Core 暴露的资源抽象：

- `ResourceServices`
- `IResourceService`
- `IResourceLoader`
- `IResourceSceneLoader`
- `ResourceLoadResult<T>`

## 为什么隔离 YooAssets

YooAssets 是当前资源框架实现，不是业务层协议。

如果 Hotfix 或 UI 直接持有 `ResourcePackage`、`AssetHandle` 等类型，会带来几个问题：

- 替换资源框架时需要改大量业务代码。
- UI 生命周期和资源句柄生命周期容易散落在页面逻辑里。
- 热更新程序集加载、UI 预制体加载、公共资源加载会形成多套入口。
- 资源地址规范化规则容易在各处复制。

因此资源系统只允许在 Core 资源适配层直接接触 YooAssets。

## 分层边界

`Core.Runtime/Resource` 负责资源抽象和当前实现：

- 服务注册：`ResourceServices`
- 全局服务：`IResourceService`
- 局部 loader：`IResourceLoader`
- YooAssets 适配：`YooAssetResourceService`、`YooAssetResourceLoader`、`YooAssetResourceSystem`

`Core.Runtime/Startup` 负责启动期资源准备：

- `ResourceStartupState`
- `YooAssetInitializeSystem`
- `ResourceDownloadSystem`

`Core.Runtime/UI` 只通过 loader 实例化 View：

- `View.Loader`
- `IResourceLoader.InstantiateAsync(...)`

`Hotfix` 只能使用 Core 资源入口，不直接依赖 YooAssets。

## 关键取舍

资源系统目前保持轻量抽象，没有建立复杂资源域或资源所有权树。

当前规则是：

- 全局资源服务负责初始化、下载、全局共享加载。
- 局部 loader 负责页面或调用方自己的资源生命周期。
- `View` 默认持有自己的 loader，销毁时释放。
- 热更新程序集读取也走资源服务，避免单独绕过资源系统。

场景加载通过 `ResourceServices.CreateSceneLoader()` 创建独立 `IResourceSceneLoader`，场景句柄只暴露标准化地址和 Unity `Scene`。YooAsset `SceneHandle` 只存在于 Core 适配层，加载成功后必须由同一加载器配对卸载。

将来如果需要预加载批次、缓存策略或原生文件加载，继续扩展现有资源抽象，不新增并行入口。

## 独立游戏发布边界

`Assets/Settings/ReleaseProfiles.json` 是发布白名单：`GameId` 关联分组、启动场景、应用标识和有序程序集；`PcAssemblies` 只追加到 Windows64。`Hotfix`、`Demo1` 至 `Demo5` 是六个管理分组，不等于六个 CLR 程序集。每个独立游戏有自己的 DLL，公共代码保留 `Hotfix.dll`，空分组不创建空 asmdef。当前 Demo1/2/3 分别是无人机、夹爆它、纸艺卡丁车，Demo4/5 预留。

拆 DLL 只隔离编译单元。完整边界还包括应用标识、启动目标、程序集加载列表、资源依赖、平台代码宏、远端版本入口与在线首包基线。Core.Editor 的 `ReleaseProfiles.Apply` 统一配置启动目标、程序集、应用标识、宏与 HybridCLR；`ReleaseAssetFilter` 只收集当前游戏和公共资源，打包前 `ValidateDependencies` 递归拒绝流向其它 Demo 的资源或脚本引用。不能依靠 Collector 排除规则悄悄掩盖跨 Demo 的真实依赖。

资源地址保持现有约定。`Codes/Hotfix` 每个 DLL 单独打包，防止公共 DLL 和其它游戏 DLL 合并到同一 Bundle。YooAsset 3.0.6 会缓存过滤规则实例，每次 GUI 或 CI 资源构建都通过 `ConfigureCollectors(config, target)` 刷新当前 profile 与真实 BuildTarget；单个资源判断不重新读取 JSON。PC 诊断资源及 DLL 仅进入 Hub 的 Windows 包；WebGL 排除原生联机公共资源与卡丁车联机页面。

独立游戏的版本目录和 AOT 基线按渠道、GameId、平台隔离。修改卡丁车自己的代码或资源不会进入无人机的 DLL/资源收集集合；修改公共 Core、公共 Hotfix 或共享资源仍可能影响所有消费方。更新平台 SDK、AOT 或公共主包输入时必须重新 Full，不能将另一个游戏的基线用于 Hotfix。具体入口与目录见[热更新模块](../modules/hotfix.md)及[Jenkins 构建模块](../modules/jenkins-build.md)。

Windows 的持久化目录与 PlayerPrefs 还受 productName 影响，不能只区分 ApplicationId。发布配置保留 Hub 的 `SleepyDemos`，独立游戏使用 `SleepyDemos-<GameId>`，因此独立 PC 包使用自己的存档、偏好与默认资源缓存目录，不迁移旧共享目录。TapTap H5 完整网页依赖随平台 ZIP 内置，Pages 只发布当前游戏的 YooAsset 热更资源；H5 基线按 `TapTap/<GameId>/WebGL/H5` 隔离，不消费旧 SDK 小游戏基线或 Player CDN。旧已发布目录保持历史状态，新 H5 使用新的资源版本。

## 与其它模块的关系

- 启动流程：资源系统在 `ResourceStartupState` 阶段初始化和下载。
- 热更新模块：AOT 元数据和热更程序集通过资源服务加载 `TextAsset`。
- UI 运行时：View 通过自己的 loader 实例化预制体。
- 资源命名：资源系统不决定命名规则，命名规则见 `docs/architecture/asset-naming.md`。

## 修改原则

- 改资源底层实现时，优先替换 `IResourceService` 实现和 `ResourceServices.RegisterDefault(...)` 注册点。
- 不要把 YooAssets 类型扩散到 Hotfix 或 UI 页面。
- 不要在业务层直接 new YooAssets loader 或持有 YooAssets handle。
- 改启动期初始化或下载顺序时，同步检查 `docs/architecture/startup-flow.md`。
- 改使用步骤时，更新对应 runbook，不把操作教程塞进模块文档。
