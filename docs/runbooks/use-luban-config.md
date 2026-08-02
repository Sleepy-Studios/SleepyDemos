# 使用 Luban 配置

## 首次克隆

主仓库通过 Git submodule 引用独立的 `SleepyConfigs` 仓库。推荐：

```powershell
git clone --recurse-submodules https://github.com/Sleepy-Studios/SleepyDemos.git
```

已有主仓库则执行：

```powershell
git submodule update --init --recursive
```

生成工具需要 .NET 8 或更高版本。Luban v4.10.2 已固定在子模块中，不需要全局安装 Luban。

## 修改与生成

1. 在 `SleepyConfigs/Datas/` 修改 Excel 数据，在 `Defines/` 修改定义。
2. 在 Unity 中运行 `Tools/SleepyDemos/Luban/仅校验配置`。
3. 校验通过后运行 `Tools/SleepyDemos/Luban/生成客户端配置`。
4. 检查主仓库生成的 C#、bytes、`TablesPartial.cs`，以及子模块中的 JSON。
5. 在 Unity Test Runner 运行本次受影响的 Luban 定向测试，并启动项目验证实际加载。

命令行等价操作：

```powershell
powershell -ExecutionPolicy Bypass -File SleepyConfigs/validate.ps1
powershell -ExecutionPolicy Bypass -File SleepyConfigs/gen.ps1 -ParentProjectRoot $PWD
```

若只想根据已有 `GeneratedTables.cs` 刷新静态访问器，使用 `Tools/SleepyDemos/Luban/重新生成 Tables 访问器`。

## 业务读取

Hotfix 启动完成后直接读取：

```csharp
var info = Cfg.Tables.ExampleInfo.Get(1);
var optional = Cfg.Tables.ExampleInfo.GetOrDefault(999);
var allRows = Cfg.Tables.ExampleInfo.DataList;
```

不要在 `LubanConfigSystem` 之前访问，不要自行从磁盘或 YooAsset 加载 `.bytes`。

## 提交顺序

配置仓库和主仓库是两个 Git 历史：

1. 先在 `SleepyConfigs` 提交并推送定义、数据和 JSON。
2. 再在主仓库提交生成 C#、bytes、代码、文档，以及更新后的 submodule 指针。

这样其他协作者检出主仓库提交时，引用的配置提交一定已经存在于远端。

## 常见问题

### 提示子模块未初始化

执行：

```powershell
git submodule update --init --recursive
```

然后确认 `SleepyConfigs/luban.conf` 与 `SleepyConfigs/gen.ps1` 存在。

### 提示 dotnet 缺失或版本过低

安装 .NET 8 SDK/Runtime，并确认 `dotnet --version` 返回 8 或更高版本。

### 生成失败后旧产物是否损坏

不会。脚本先在临时目录生成和检查，成功后才替换正式目录。阅读 Console 中完整的标准输出、错误输出、命令行和退出码定位原因。

### 运行时提示配置尚未初始化

确认调用发生在 Hotfix 启动完成后，并检查 `LubanConfigSystem` 的日志。不要通过绕过静态保护或自行构造管理器解决。

### 运行时提示资源缺失

检查异常中的表名和资源地址，再确认：

- `Assets/LoadResources/Config/Luban/<name>.bytes` 已生成。
- 资源命名校验没有 Error。
- `Config` 目录仍在 YooAsset Collector 中，地址规则为全路径地址。
- 资源包已经按当前内容重新构建。

### lower_snake_case 为什么不告警

Luban 数据文件名由生成器决定。只有 `Assets/LoadResources/Config/Luban/*.bytes` 和 `.json` 允许跳过 PascalCase 语义告警；非法字符、数字开头、扩展名错误和地址冲突仍会报错。
