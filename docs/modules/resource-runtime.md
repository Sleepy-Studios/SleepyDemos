# Core 资源运行时

## 目标

资源运行时负责给 Core 和 Hotfix 提供统一资源语义。上层代码不直接依赖 YooAssets 的 `ResourcePackage`、`AssetHandle` 或操作类型，而是通过 Core 的资源服务和局部 loader 访问资源。

## 当前结构

- `IResourceService`：全局资源服务契约，负责初始化、下载、地址规范化、创建 loader、加载全局资源。
- `ResourceServices`：默认资源服务注册点。当前默认实现是 `YooAssetResourceService`，未来替换底层实现时优先从这里切换。
- `IResourceLoader`：局部生命周期 loader，通常由 `View` 或具体调用方持有，负责加载资源、实例化对象、释放实例和释放本 loader 持有的资源。
- `YooAssetResourceService`：YooAssets 适配层。
- `YooAssetResourceLoader`：YooAssets 局部 loader 实现，内部维护资源句柄和实例到句柄的映射。
- `YooAssetResourceSystem`：YooAssets 初始化、清单刷新、下载和底层加载的实现细节。

## 生命周期规则

- `View.Loader` 默认通过 `ResourceServices.CreateLoader()` 创建，不直接 new 某个具体资源框架实现。
- `IResourceLoader` 的所有权属于创建它的对象；`View.Destroy()` 会释放 View 内部 loader。
- 通过 loader 实例化出的 GameObject 应由同一个 loader 的 `ReleaseInstance` 释放。
- 通过 loader 加载出的资源应由同一个 loader 的 `ReleaseAsset` 或 `Dispose` 释放。
- 全局资源服务加载出的共享资源由服务内部共享 loader 持有，需要显式释放时调用 `ResourceServices.Default.ReleaseAsset(asset)`。

## 地址与错误

- 上层传入地址后统一经过 `IResourceService.NormalizeAddress` 标准化。
- 加载结果可使用 `ResourceLoadResult<T>` 表示成功、地址和错误信息。
- 初始化失败、资源缺失、下载失败时，底层实现负责输出日志；启动系统根据初始化结果决定是否中断。

## 扩展点

当前最小闭环覆盖：
- 初始化资源系统
- 下载资源包
- 加载资源
- 加载 `TextAsset`
- 实例化 GameObject
- 释放实例和资源句柄
- SpriteAtlas 运行时请求

将来如果要支持场景加载、原生文件、预加载批次、缓存策略或其它资源框架，应优先扩展 `IResourceService` / `IResourceLoader`，避免让业务层直接感知具体实现。

## 验证入口

在 Unity Editor 中可以运行：
- `Tools/SleepyDemos/Validate Core Runtime Infrastructure`

该菜单会检查默认资源服务、loader 创建、地址规范化、外层 YooAssets 具体类型引用和相关文档入口。

命令行或 CI 可以使用 Unity 参数：
- `-executeMethod Core.Editor.CoreRuntimeInfrastructureValidator.ValidateForBatchMode`
