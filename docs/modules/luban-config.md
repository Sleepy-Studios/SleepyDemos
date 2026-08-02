# Luban 配置模块

## 负责什么

该模块负责把 Luban 生成的二进制表在 Hotfix 启动期完整加载，并以 `Cfg.Tables.<Table>` 的稳定入口提供给业务。

## 代码与资源位置

- `Assets/Scripts/Hotfix/Cfgs/LubanConfigService.cs`：幂等初始化、批量加载、错误上下文。
- `Assets/Scripts/Hotfix/Cfgs/TablesPartial.cs`：自动生成的静态 facade。
- `Assets/Scripts/Hotfix/Cfgs/Generated/`：Luban 生成代码。
- `Assets/Scripts/Hotfix/AppDelegate/Boot/Systems/LubanConfigSystem.cs`：Hotfix 启动接入。
- `Assets/LoadResources/Config/Luban/`：运行时二进制表。
- `Assets/Scripts/Core/Editor/Config/`：编辑器生成与校验工具。
- `SleepyConfigs/`：独立 Git 子模块。

## 主链路

```text
HotfixBootService
  -> LubanConfigSystem.RunAsync
  -> LubanConfigService.InitializeAsync
  -> ResourceServices.CreateLoader
  -> IResourceLoader.LoadAssetAsync<TextAsset>
  -> 复制全部 TextAsset.bytes
  -> new GeneratedTables(loader)
  -> Tables.SetInstance
  -> GlobalDataSystem
```

加载进度由 `Tables.LoadProgress` 表示，表数由 `Tables.TableCount` 提供。只有完整成功后 `Tables.IsInitialized` 才为 `true`。

## 失败语义

- 缺少资源、资源类型错误或解析失败时，异常必须包含表名和完整资源地址。
- 任意表失败时不发布半初始化实例，进度复位为 0。
- 失败的初始化任务结束后清除共享状态，后续可以重试。
- 成功后重复调用不重复加载资源。

资源地址固定为：

```text
LoadResources/Config/Luban/<Luban 数据文件名>
```

例如 `example_info.bytes` 的地址是 `LoadResources/Config/Luban/example_info`。

## 扩展新表

新增表只修改 `SleepyConfigs` 中的定义与数据，然后运行完整生成。不要手写对应 C# 类型、静态属性或 bytes。生成器会从真实 `GeneratedTables.cs` 发现新表并更新 `TablesPartial.cs`。

若新表需要业务级二次索引、组合查询或热更新策略，应在 Hotfix 另建面向业务的服务；不要把业务逻辑塞进生成代码或通用加载服务。

## 验证重点

- `SleepyConfigs/validate.ps1` 和 `gen.ps1` 成功。
- Unity 编译无错误。
- `LubanTemplateClassGeneratorTests` 覆盖发现、排序、稳定生成和失败路径。
- `LubanAssetNamingTests` 覆盖 Luban lower_snake_case 文件名例外及扩展名限制。
- `LubanConfigServiceTests` 覆盖加载、查询、重复初始化、初始化前访问和缺资源错误。
- Play Mode 启动日志中，Luban 初始化发生在 Flux 全局数据之前，主界面能够进入。

操作和排障步骤见 [使用 Luban 配置](../runbooks/use-luban-config.md)。
