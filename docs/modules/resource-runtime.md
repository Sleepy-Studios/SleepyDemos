# Core 资源运行时

## 负责什么

资源运行时维护 Core 资源抽象和当前 YooAssets 适配实现，为启动流程、热更新加载、UI 框架和 Hotfix 业务提供统一资源入口。

它负责：

- 注册和访问默认资源服务
- 初始化默认资源包
- 下载资源补丁
- 规范化资源地址
- 创建局部 loader
- 同步/异步加载资源和 `TextAsset`
- 同步/异步实例化与释放 GameObject
- Additive 场景加载、真实进度、句柄配对卸载

它不负责：

- 具体 Demo 的资源目录规划
- 资源命名规则本身
- UI 页面展示逻辑
- 热更新程序集装载策略

设计原则见 `docs/architecture/resource-system.md`。

## 代码位置

- `Assets/Scripts/Core/Runtime/Resource/IResourceService.cs`
- `Assets/Scripts/Core/Runtime/Resource/IResourceLoader.cs`
- `Assets/Scripts/Core/Runtime/Resource/IResourceSceneLoader.cs`
- `Assets/Scripts/Core/Runtime/Resource/ResourceServices.cs`
- `Assets/Scripts/Core/Runtime/Resource/ResourceInitializeOptions.cs`
- `Assets/Scripts/Core/Runtime/Resource/ResourceLoadResult.cs`
- `Assets/Scripts/Core/Runtime/Resource/YooAssetResourceService.cs`
- `Assets/Scripts/Core/Runtime/Resource/YooAssetResourceLoader.cs`
- `Assets/Scripts/Core/Runtime/Resource/YooAssetResourceSystem.cs`
- `Assets/Scripts/Core/Runtime/Startup/States/ResourceStartupState.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/YooAssetInitializeSystem.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/ResourceDownloadSystem.cs`

相关调用入口：

- `Assets/Scripts/Core/Runtime/UI/Core/View.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HybridAotAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HotfixAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/RuntimeServiceRegisterSystem.cs`

## 主链路

启动资源链路：

1. `ResourceStartupState`
2. `YooAssetInitializeSystem`
3. `ResourceServices.Default.InitializeAsync(options)`
4. `YooAssetResourceService`
5. `YooAssetResourceSystem.InitializeAsync(...)`
6. `ResourceDownloadSystem`
7. `ResourceServices.Default.DownloadPackageAsync(...)`
8. 进入 `BeforeHotfixStartupState`

资源初始化参数来自启动配置。配置缺失时使用 `ResourceInitializeOptions.Default`。

## 生命周期

`ResourceServices.Default` 是全局资源服务入口，默认实现是 `YooAssetResourceService`。

当前 YooAsset 适配目标版本为 `3.0.5`，只使用 3.x 原生接口，不启用 `YOOASSET_LEGACY_API` 兼容层：

- 包初始化使用 `InitializePackageAsync` 与各运行模式对应的 `*PlayModeOptions`。
- 编辑器模拟构建使用 `EditorSimulateBuildInvoker.Build`。
- 联机模式使用 `IRemoteService`、Builtin 文件系统与 Sandbox 缓存文件系统。
- 资源清单流程为 `RequestPackageVersionAsync` 后调用 `LoadPackageManifestAsync`。
- 下载器通过 `ResourceDownloaderOptions` 创建，监听 `DownloadProgressChanged` 后调用 `StartDownload`。
- 项目始终显式持有 `ResourcePackage`，不依赖静态默认包快捷入口。

`IResourceLoader` 是局部生命周期入口。当前主要由 `View` 持有：

- `View.Loader` 通过 `ResourceServices.CreateLoader()` 创建。
- `View` 销毁时释放自身 loader。
- 某个 loader 实例化出的 GameObject，应由同一个 loader 释放。
- 某个 loader 加载出的资源，应由同一个 loader 释放或随 loader `Dispose` 释放。
- 全局服务加载出的共享资源由服务内部共享 loader 持有。

`IResourceSceneLoader` 是运行期内容场景入口：

- 通过 `ResourceServices.CreateSceneLoader()` 创建。
- 加载成功返回不暴露 YooAsset 类型的 `IResourceSceneHandle`。
- 句柄必须交还给同一个 loader 卸载；卸载成功后不可重复使用。
- Active Scene、相机和业务 UI 不由资源适配层处理，而由 Hotfix 场景导航事务负责。

## 边界规则

- Hotfix 和 UI 业务层不直接依赖 YooAssets。
- 业务层不直接持有 YooAssets 句柄。
- 资源地址进入加载前由资源服务统一规范化。
- 热更新加载程序集时走 `ResourceServices.Default.LoadTextAssetAsync(...)`。
- UI 实例化 View 时走 `IResourceLoader.Instantiate(...)` 或 `IResourceLoader.InstantiateAsync(...)`；基础 UI 组件通过 `isAsync` 决定使用哪条路径。

## 修改这里时注意什么

- 替换资源框架时，优先新增 `IResourceService` / `IResourceLoader` 实现，再改注册点。
- 新增场景加载、原生文件、预加载批次、缓存策略时，优先扩展资源抽象，不新增并行入口。
- 改资源初始化或下载流程时，同步检查 `docs/architecture/startup-flow.md`。
- 改资源系统设计原则时，同步检查 `docs/architecture/resource-system.md`。
- 改资源使用步骤时，同步检查 `docs/runbooks/use-resource-loader.md`。

## 验证重点

- 启动阶段能完成 YooAssets 初始化。
- 无补丁时能进入后续 Hotfix 流程。
- 有补丁时加载界面能显示下载进度。
- UI 能通过 loader 正常实例化。
- 热更新程序集能通过资源服务读取 `TextAsset`。
- `Core.Tests.Resource.ResourceServiceTests` 全部通过。

## 相关文档

- `docs/architecture/resource-system.md`
- `docs/architecture/startup-flow.md`
- `docs/architecture/asset-naming.md`
- `docs/modules/ui-runtime.md`
- `docs/modules/hotfix.md`
- `docs/runbooks/use-resource-loader.md`
- `docs/runbooks/run-unity-tests.md`
