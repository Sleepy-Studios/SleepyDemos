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
