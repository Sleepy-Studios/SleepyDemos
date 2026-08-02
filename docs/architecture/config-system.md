# 配置系统设计

## 目标与边界

项目使用 Luban 生成强类型客户端配置。配置定义、策划数据和固定版本工具独立放在 `SleepyConfigs` Git 子模块；Unity 主仓库只保存运行时所需的生成 C#、二进制资源和接入代码。

职责划分：

- `SleepyConfigs/`：表定义、Excel 数据、Luban v4.10.2、生成与校验脚本、可审查 JSON。
- `Assets/Scripts/Hotfix/Cfgs/Generated/`：Luban 自动生成的 C# 类型和表管理器，属于 Hotfix 业务层。
- `Assets/LoadResources/Config/Luban/`：客户端运行时加载的 `.bytes` 资源。
- `Assets/Scripts/Core/Editor/Config/`：生成、校验和静态访问器生成工具，只在编辑器中运行。

`Core.Runtime` 不感知 Luban，也不反向依赖 Hotfix。Hotfix 通过 Core 的 `ResourceServices`、`IResourceService` 和 `IResourceLoader` 加载配置，不把 YooAsset 句柄或包类型扩散到业务层。

## 生成数据流

```text
SleepyConfigs/Datas + Defines
          |
          v
SleepyConfigs/Tools/Luban v4.10.2
          |
          +--> Assets/Scripts/Hotfix/Cfgs/Generated       C#
          +--> Assets/LoadResources/Config/Luban         bytes
          +--> SleepyConfigs/GenerateDatas/Json           JSON
```

`gen.ps1` 先生成到临时目录，并检查 `GeneratedTables.ExampleInfo`、`example_info.bytes` 和 `example_info.json` 等后置条件；只有所有步骤成功后才替换正式输出，避免失败生成破坏上一份可用产物。

生成目录中的 C# 和 bytes 随主仓库提交，JSON 随配置仓库提交。因此构建机和只运行 Unity 的协作者不需要每次启动都执行 Luban。

## 运行时生命周期

Hotfix 启动顺序为：

1. `LubanConfigSystem`
2. `GlobalDataSystem`
3. 主界面导航

`LubanConfigService.InitializeAsync()` 会先通过一个 Loader 加载全部表的 `TextAsset.bytes`，复制数据后构造 `GeneratedTables`。只有全部表成功解析，才一次性发布给 `Cfg.Tables`；Loader 在构造完成后释放。并发或重复初始化共享同一任务，失败后允许重试。

业务代码统一使用：

```csharp
var row = Cfg.Tables.ExampleInfo.Get(1);
```

禁止业务代码自行创建 `GeneratedTables`、直接拼接资源地址或持有资源 Loader。访问发生在初始化完成前时，静态入口会抛出包含启动系统和资源目录的明确异常。

## 静态访问器生成策略

Luban 原生生成的 `GeneratedTables` 是实例管理器。项目的 `LubanTemplateClassGenerator` 从实际生成源码中解析表属性和对应数据文件名，按序生成 `TablesPartial.cs`，提供 `Tables.<Table>` 访问方式。

不要手改 `TablesPartial.cs`。每次表结构变化后由完整生成菜单自动更新，或单独运行“重新生成 Tables 访问器”。如果 Luban 上游生成格式变化导致解析不到属性或 loader 映射，生成器应立即失败，而不是猜测文件名。

## 版本与升级

- Luban 工具固定为 `v4.10.2`，二进制和来源校验写在 `SleepyConfigs/Tools/Luban/README.md`。
- Unity 运行时包固定为 `com.code-philosophy.luban` 的 `v1.2.0` 标签。
- 升级时必须先在独立配置仓库验证生成，再检查生成管理器结构、静态访问器、运行时反序列化和定向测试；不要只替换工具目录。

具体操作见 [使用 Luban 配置](../runbooks/use-luban-config.md)，实现维护见 [Luban 配置模块](../modules/luban-config.md)。
