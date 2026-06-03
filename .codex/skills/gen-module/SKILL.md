---
name: gen-module
description: "生成或修改 SleepyDemos Hotfix Flux 模块的 Action/Data/Handler C# 文件。创建新业务模块、补本地 Action、补 Data 状态、补 Handler 处理逻辑，或用户提到 Action/Data/Handler 三件套时使用。"
argument-hint: "[ModuleName]"
---

# Hotfix Flux 模块生成器

本技能从钓鱼项目迁入，但已按 SleepyDemos 当前架构调整。生成前必须先读取当前项目源码，不允许沿用钓鱼项目的网络接口或命名空间。

## 当前项目事实

- Flux 底座源码：`Assets/Scripts/Core/Runtime/Flux/`
- `IAction`、`IData`、`IHandler`、`HandlerBase<TAction,TState>`、`GlobalData` 均位于 `Core.Runtime` 命名空间。
- Hotfix 业务代码位于 `Assets/Scripts/Hotfix`，当前命名空间使用 `Hotfix`。
- `HandlerBase` 当前抽象方法是 `protected abstract void Reduce(TAction action)`。
- `HandlerBase` 当前支持钓鱼项目同款命令式网络写法：`SendMsg(command, request)` + `[MessageHandler(command, MessageHandler.State.Success/Error)]`。
- 网络 Success / Error handler 执行完成后，框架会自动 `ApplyState()`。
- `HandlerBase` 也保留泛型 `SendMsg<TRequest,TResponse>(request)` 和回调重载，供不走命令路由的临时场景使用。

## 交互流程

### Step 1：确认模块名与落点

- `$ARGUMENTS` 非空则直接用作 `{ModuleName}`，要求 PascalCase，例如 `GravityWell`。
- `$ARGUMENTS` 为空则询问用户模块名。
- 新模块默认落点：

```text
Assets/Scripts/Hotfix/Module/{ModuleName}/_Actions/{ModuleName}Action.cs
Assets/Scripts/Hotfix/Module/{ModuleName}/_Data/{ModuleName}Data.cs
Assets/Scripts/Hotfix/Module/{ModuleName}/_Reducers/{ModuleName}Handler.cs
```

检查上述三个文件是否已存在：

- 不存在：创建新模块骨架。
- 部分存在：只补缺失文件，修改已有文件前先读取现有内容。
- 全部存在：询问是新增 Action、删除 Action，还是修改状态/处理逻辑。

### Step 2：确认动作和状态

如果用户没有提供清晰的 Action / Data / Handler 需求，先让用户补充：

```text
请说明这个模块需要哪些 Action，以及每个 Action 会修改哪些 Data 状态。
如果涉及网络请求，也请说明现有 Request/Response 类型和实际发送方式。
```

如果用户粘贴的是协议列表，可以按钓鱼项目模式生成 `const string Cmd`、`SendMsg(cmd, request)` 和 `[MessageHandler]` 回包方法。但必须先确认 Request / Response 类型在当前项目存在，不能臆造协议类型。

### Step 3：生成并自检

生成后检查：

1. 命名空间是否为 `Hotfix`。
2. 是否引用 `Core.Runtime`。
3. `Data` 是否实现 `IData.ClearData()`。
4. `Data.Handlers` 是否注册了对应 Handler。
5. `Handler` 是否 override `Reduce({ModuleName}Action action)`。
6. 纯本地修改状态后是否调用 `ApplyState()`；网络 `[MessageHandler]` 回包里是否避免重复调用。
7. 没有臆造不存在的网络接口、Attribute、Req/Res 类型或业务字段。

## 基础模板

### Action 模板

```csharp
using Core.Runtime;

namespace Hotfix
{
    public class {ModuleName}Action : IAction
    {
    }

    public sealed class {ModuleName}SetEnabledAction : {ModuleName}Action
    {
        public bool Enabled { get; }

        public {ModuleName}SetEnabledAction(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
```

规则：

- 基类放在文件最前面。
- 子类命名保留模块名前缀，避免跨模块重名。
- 字段优先用只读属性；如项目已有同模块风格，则跟随现有风格。
- 注释只写业务含义，不塞入未确认的协议细节。

### Data 模板

```csharp
using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix
{
    public class {ModuleName}Data : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new {ModuleName}Handler()
        };

        public bool Enabled { get; set; }

        public void ClearData()
        {
            Enabled = false;
        }
    }
}
```

规则：

- `Handlers` 必须返回包含 `{ModuleName}Handler` 的列表。
- 每个状态字段都要在 `ClearData()` 中恢复到明确初始值。
- 复杂查询或转换逻辑可以放在 `Data` 中，但不要把 UI 控制逻辑塞进去。

### Handler 模板

```csharp
using Core.Runtime;

namespace Hotfix
{
    public class {ModuleName}Handler : HandlerBase<{ModuleName}Action, {ModuleName}Data>
    {
        public const string {ModuleName}GetInfo = "{moduleName}/getInfo";

        protected override void Reduce({ModuleName}Action action)
        {
            switch (action)
            {
                case {ModuleName}SetEnabledAction setEnabled:
                    State.Enabled = setEnabled.Enabled;
                    ApplyState();
                    break;

                case {ModuleName}GetInfoAction getInfo:
                    SendMsg({ModuleName}GetInfo, getInfo.Request);
                    break;
            }
        }

        [MessageHandler({ModuleName}GetInfo, MessageHandler.State.Success)]
        void On{ModuleName}GetInfo({ModuleName}InfoRes response)
        {
            State.Info = response;
        }
    }
}
```

规则：

- 纯本地修改 `State` 后必须手动调用 `ApplyState()`。
- 网络请求回包通过 `[MessageHandler]` 修改 `State`，框架会在 Success / Error 方法执行完后自动 `ApplyState()`。
- `[MessageHandler]` 方法里不要重复调用 `ApplyState()`。
- `SendMsg(command, request)` 前必须确认命令、Request / Response 类型已存在，且当前 `INetworkService` 已被注册。
- 如确实需要不走命令路由的异步请求，可使用泛型 `SendMsg<TRequest,TResponse>` 重载，但必须先确认调用方期望。

## 修改已有模块

新增 Action：

1. 读取现有 Action / Data / Handler。
2. 在 Action 文件追加子类。
3. 如需新状态，在 Data 中追加字段并更新 `ClearData()`。
4. 在 Handler 的 switch 中追加 case。

删除 Action：

1. 读取现有三个文件。
2. 移除 Action 子类。
3. 移除 Handler case。
4. 如果 Data 字段只服务该 Action，确认后再删除字段和 `ClearData()` 语句。

修改 Action 参数：

1. 更新 Action 子类属性和构造函数。
2. 更新 Handler case 中读取参数的逻辑。
3. 如果状态结构变化，同步更新 Data 和 `ClearData()`。

## 异常处理

- 模块名、协议、字段含义不明确：先向用户确认。
- 目标文件存在但结构不符合模板：先读取并按现有风格修改，不强行重排。
- 涉及网络请求但接口不明确：停止生成网络代码，只生成已确认的本地 Action / Data / Handler 骨架，或向用户确认现有网络 API。
