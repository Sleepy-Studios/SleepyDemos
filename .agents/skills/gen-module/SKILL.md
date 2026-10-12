---
name: gen-module
description: "生成或修改 SleepyDemos Hotfix Flux 三件套；覆盖本地业务、Demo 状态及已有网络协议。"
argument-hint: "[ModuleName]"
---

# Hotfix Flux 模块生成与整理

先读目标三件套及全部直接调用方，再按当前需求修改。命名、排版和注释以 [项目 C# 规范](../../../docs/architecture/documentation-rules.md) 为准，格式由项目根 `.editorconfig` 约束。

## 入口与落点

- 从需求和源码确定模块、动作、状态与同步结果；只有实质歧义才询问，不因为文件已存在重复确认。
- 普通业务放 `Assets/Scripts/Hotfix/Module/<ModuleName>`。新模块可使用 `_Actions`、`_Data`、`_Reducers`；已有模块保留自己的目录。
- Demo 放 `Assets/Scripts/Hotfix/Demos/<DemoName>`，沿用其命名空间与职责布局；不为套模板迁移目录或一律改成 `Hotfix`。
- 底座在 `Assets/Scripts/Core/Runtime/Flux`，使用 `Core.Runtime` 的 `IAction`、`IData`、`IHandler`、`HandlerBase<TAction, TData>` 和 `GlobalData`。
- Handler 实现 `protected override void Reduce(TAction action)`，不复制钓鱼的 `Execute`、`Game.Main` 或专属异步 API。

## Action

- 基类在前，具体动作保留模块名前缀。只有一个动作时可直接实现 `IAction`，不用额外基类。
- 参数使用 **PascalCase 公开字段 + 构造函数**；参数名用 camelCase。内部来源、版本字段也用 PascalCase，保留必要访问限制。
- 每个业务动作写简短中文 `///`；带参公开构造函数写完整 XML，说明索引范围、默认值、来源及调用副作用。
- Action 只表示请求和结果，不内置静态 `Send`，不在构造时派发，不执行存档或导航。
- 同步结果用 `public ... Result { get; internal set; }` 等具名属性。调用方构造动作、`GlobalData.Dispatch(action)` 后读取结果，未处理时的结果须明确。

## Data

- `Handlers` 为初始化一次的稳定列表；场景依赖在构造时注入。
- 保存同一份真源和读取入口，提供配置查询、业务判断与派生数据，不复制 Session/存档进度，不持有 UI 控件。
- 关键公开状态补简短业务注释。可变状态通常使用只读读取与 `internal set`；公开 Action 字段规则不适用于业务状态及 Inspector。
- `ClearData` 恢复明确初始值、使旧版本失效，按所属生命周期清理；不得开始玩法命令、读写存档、导航或场景表现。任务取消和事件退订按实际持有者处理。

## Handler 与 View

- 外部业务修改走 Action；注册、依赖配置、物理采样、释放可保留具体生命周期入口。Handler 可桥接规则和公共服务事件。
- `Reduce` 分支逐行展开；复杂操作进入具名私有方法。不要用转发属性掩盖 `State`，不为一条赋值拆方法。
- 本地完整操作结束后 `ApplyState()`；嵌套命令、规则事件及高频采样保持原发布时序，不重复刷新。
- View 初始化时声明 `BindData<TData>`，由 Core 管理订阅。页面读取 Data、提交 Action，具体 `SetData` 保持在显示前交付。
- 异步完成前核对注册实例、会话版本和生命周期；失败/取消恢复状态，旧结果不得修改新会话。
- 私有复杂分支用 `//` 解释同步结果、嵌套发布、取消和回滚顺序，不堆机械注释。

## 完整本地示例

以下三件套可独立编译；按实际模块替换名称和业务内容，不额外生成网络骨架。

```csharp
using Core.Runtime;

namespace Hotfix
{
    /// 修改示例功能的启用状态。
    public sealed class ExampleSetEnabledAction : IAction
    {
        /// 本次请求的启用状态。
        public bool Enabled;

        /// 是否接受请求；未注册 Data 时为 false。
        public bool Result { get; internal set; }

        /// <summary>
        /// 请求修改启用状态；构造后需通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="enabled">true 启用，false 关闭。</param>
        public ExampleSetEnabledAction(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
```

```csharp
using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix
{
    /// 示例功能的状态与读取入口。
    public sealed class ExampleData : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new ExampleHandler()
        };

        /// 功能是否启用；初始为 false。
        public bool Enabled { get; internal set; }

        /// 恢复初始状态。
        public void ClearData()
        {
            Enabled = false;
        }
    }
}
```

```csharp
using Core.Runtime;

namespace Hotfix
{
    /// 接受启用请求并发布状态。
    public sealed class ExampleHandler : HandlerBase<ExampleSetEnabledAction, ExampleData>
    {
        /// <summary>
        /// 同步处理请求；订阅回调执行前先写回结果。
        /// </summary>
        /// <param name="action">当前启用请求。</param>
        protected override void Reduce(ExampleSetEnabledAction action)
        {
            State.Enabled = action.Enabled;
            action.Result = true;
            ApplyState();
        }
    }
}
```

## 网络业务：仅在存在协议时使用

- 先确认真实 command、Request/Response 及 `INetworkService` 注册入口；本地模块不创建 `GetInfo`、`InfoRes` 或假请求类。
- 命令式请求用 `SendMsg(command, request)`，真实回包用 `[MessageHandler(command, MessageHandler.State.Success/Error)]`。
- 回包方法执行后框架自动 `ApplyState()`，方法内不重复调用。泛型异步重载遵循当前 `HandlerBase` 的返回和发布契约。
- 注释写实际协议语义，不导入钓鱼专属网络、日志、资源或任务框架。

## 完成自检

1. 字段命名、构造参数、同步结果、Handler 路由及全部调用方一致。
2. 动作、关键状态、带参公开入口有真实业务说明；构造函数、方法和分支没有多语句挤一行。
3. 注册列表稳定，Data 清理边界及场景退出顺序明确。
4. View 读取 Data、派发 Action，规则、物理、输入和表现保持实际职责。
5. 用 Unity 编译和相关既有 Test Runner 用例验证；不新建测试程序集、不写源码字符串快照锁定写法。
6. 同步维护文档和两侧同名技能，执行 `git diff --check`。
