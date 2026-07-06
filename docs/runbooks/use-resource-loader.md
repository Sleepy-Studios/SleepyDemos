# 使用资源 Loader

## 适用场景

当 UI、Hotfix 或 Core 运行时代码需要加载资源、实例化预制体或释放资源时，使用本 runbook。

如果你是在修改资源系统设计或底层实现，先看：

- `docs/architecture/resource-system.md`
- `docs/modules/resource-runtime.md`

## 前置条件

- 启动流程已经完成 `ResourceStartupState`。
- `ResourceServices.Default.IsInitialized` 为 true。
- 资源地址符合 `docs/architecture/asset-naming.md` 中的命名规则。

## 在 View 中实例化资源

View 内优先使用自身 `Loader`：

```csharp
var instance = await Loader.InstantiateAsync(address, parent);
```

需要同步实例化时：

```csharp
var instance = Loader.Instantiate(address, parent);
```

需要保持世界坐标时：

```csharp
var instance = await Loader.InstantiateAsync(address, parent, true);
```

View 销毁时会释放自身 loader。通过该 loader 实例化的对象，应由同一个 loader 管理。

## 加载普通资源

在持有 loader 的对象中加载资源：

```csharp
var sprite = await Loader.LoadAssetAsync<Sprite>(address);
```

需要同步加载时：

```csharp
var sprite = Loader.LoadAsset<Sprite>(address);
```

使用完单个资源后可以释放：

```csharp
Loader.ReleaseAsset(sprite);
```

如果资源生命周期跟随 loader，可以在持有者销毁时统一 `Dispose` loader。

## 加载全局共享资源

跨系统共享资源可以通过全局资源服务加载：

```csharp
var result = await ResourceServices.Default.LoadAssetAsync<SpriteAtlas>(atlasName);
if (!result.Success)
{
    Debug.LogWarning(result.Error);
    return;
}

var atlas = result.Asset;
```

需要同步加载全局共享资源时：

```csharp
var result = ResourceServices.Default.LoadAsset<Sprite>(address);
```

全局服务加载出的共享资源需要显式释放时：

```csharp
ResourceServices.Default.ReleaseAsset(atlas);
```

## 加载 TextAsset

热更新程序集或配置文本使用：

```csharp
var result = await ResourceServices.Default.LoadTextAssetAsync(address);
if (!result.Success)
{
    Debug.LogWarning(result.Error);
    return;
}

var bytes = result.Asset.bytes;
```

## 不要这样做

- 不要在 Hotfix 或 UI 业务层直接引用 YooAssets 类型。
- 不要直接 new `YooAssetResourceLoader`。
- 不要跨 loader 释放资源或实例。
- 不要绕过 `ResourceServices.Default.NormalizeAddress(...)` 自己拼接底层 location。
- 不要在资源系统未初始化前依赖同步加载；正常流程应先完成 `ResourceStartupState`。

## 常见问题

### 资源加载失败

先检查：

- 地址是否符合资源命名规则。
- 资源是否在 `Assets/LoadResources` 的预期目录下。
- 启动时 YooAssets 是否初始化成功。
- 当前运行模式和包名是否匹配启动配置。

### UI 预制体实例化失败

先检查：

- View 的 `Address` 是否为空。
- 预制体地址是否能被资源系统规范化。
- 资源包是否已经初始化和下载完成。

### 释放后资源仍然占用

先确认资源由哪个 loader 加载：

- 局部 loader 加载的资源由同一个 loader 释放。
- 全局服务加载的资源由 `ResourceServices.Default.ReleaseAsset(...)` 释放。
- View 生命周期资源优先跟随 View 自身 loader 释放。

## 验证方式

Unity Editor 中运行：

- `Tools/SleepyDemos/Validate Core Runtime Infrastructure`

手动验证：

- 启动项目能完成资源初始化。
- 主界面能正常实例化。
- 加载不存在地址时能看到明确日志。
- 关闭或销毁 View 后没有明显资源句柄残留。
