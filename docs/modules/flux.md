# Core Flux 状态流

## 负责什么

`Core.Runtime/Flux` 提供轻量单向数据流：

1. 业务创建 `IAction`
2. 调用 `GlobalData.Dispatch(action)`
3. 对应 `HandlerBase<TAction, TState>` 处理 Action
4. Handler 修改 `State`
5. 应用状态：纯本地修改手动 `ApplyState()`；网络回包由 `[MessageHandler]` 方法执行完后自动 `ApplyState()`
6. `GlobalData` 通知 `Subscribe<TState>` 的订阅者刷新

它适合保存并反复读取的业务状态，例如玩家数据、Demo 当前状态、设置状态。一次性通知仍优先使用 `EventDispatcher`。

如果 Action 走网络请求，推荐使用钓鱼项目同款写法：`Execute` 中调用 `SendMsg(command, request)`，回包方法使用 `[MessageHandler(command, MessageHandler.State.Success)]` 或 Error 标注。网络成功 / 错误处理方法执行完成后，框架会自动 `ApplyState()`。只有纯本地修改才需要手动调用 `ApplyState()`。

## 代码位置

- `Assets/Scripts/Core/Runtime/Flux/FluxInterfaces.cs`：`IAction`、`IData`、`IHandler`、`INetworkService`
- `Assets/Scripts/Core/Runtime/Flux/HandlerBase.cs`：Handler 基类、`MessageHandler` Attribute、命令式网络回包路由
- `Assets/Scripts/Core/Runtime/Flux/GlobalData.cs`：状态注册、派发、订阅、清理
- `Assets/Scripts/Core/Runtime/Flux/ActionConvert.cs`：订阅回调容器
- `Assets/Scripts/Hotfix/AppDelegate/Services/FluxService.cs`：Hotfix 全局 Data 注册和重新登录清理入口
- `Assets/Scripts/Hotfix/Module/User/`：当前第一个实际 Data 三件套，启动时记录本机硬件配置

## 核心 API

### 注册状态

```csharp
GlobalData.Add<MyData>();
```

或传入已有实例：

```csharp
GlobalData.Add(new MyData());
```

同一个 `IData` 类型只注册一次。重复注册会返回已有实例，不会把 Handler 绑定到未保存的新实例。

### 派发 Action

```csharp
GlobalData.Dispatch(new MyAction());
```

`GlobalData` 会按 Action 类型寻找 Handler，也支持派发子类 Action 时命中父类 Action 对应的 Handler。

### 订阅状态

```csharp
GlobalData.Subscribe<MyData>(OnMyDataChanged, true);
GlobalData.UnSubscribe<MyData>(OnMyDataChanged);
```

第二个参数 `triggerOnSub` 控制第一次订阅时是否立即通知当前状态：

```csharp
GlobalData.Subscribe<MyData>(OnMyDataChanged, true);  // 订阅后立即回调当前状态
GlobalData.Subscribe<MyData>(OnMyDataChanged, false); // 只接收后续状态变化
```

默认值是 `true`，与钓鱼项目一致。回调列表使用快照遍历，回调中取消订阅不会破坏本轮通知。

### 清理状态

```csharp
GlobalData.ClearData();
GlobalData.Remove<MyData>();
```

- `ClearData()` 会调用每个状态的 `ClearData()`，并通知订阅者。
- `Remove<T>()` 会移除状态、订阅者、Processing 标记，以及该状态注册过的 Handler，避免后续 Dispatch 命中残留 Handler。

## FluxService

Hotfix 不直接在 `HotfixEntry` 中散写 `GlobalData.Add<T>()`，而是通过 `FluxService` 统一管理全局 Data：

```csharp
FluxService.InitializeGlobalData();
```

当前注册：

```csharp
GlobalData.Add<UserData>().InitData();
```

重新登录、切号或退出登录时统一调用：

```csharp
FluxService.ClearForRelogin();
```

这会调用 `GlobalData.ClearData()`，保留 Data 与 Handler 注册，只清空状态并通知订阅者；同时重置 `FluxService` 的初始化标记，下一次进入启动初始化时可以重新刷新全局 Data。

### 网络服务

```csharp
GlobalData.SetNetworkService(networkService);
```

如果 Handler 继承 `HandlerBase`，会收到当前 `INetworkService`。命令式网络请求写法：

```csharp
SendMsg(DemoGetInfo, request);
```

回包方法：

```csharp
[MessageHandler(DemoGetInfo, MessageHandler.State.Success)]
void OnDemoGetInfo(DemoResponse response)
{
    State.Info = response.Info;
}
```

`SendMsg(command, request)` 会根据 `MessageHandler` 标注找到 Success 回包参数类型，并通过 `INetworkService.SendAsync(command, request, responseType)` 发起请求。Success / Error handler 执行完成后会自动 `ApplyState()`。

如果需要自定义回调，也保留泛型重载：

```csharp
await SendMsg<MyRequest, MyResponse>(request, response =>
{
    State.Info = response.Info;
});
```

## Data 模板

```csharp
using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix
{
    public class DemoData : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new DemoHandler()
        };

        public bool Enabled { get; set; }

        public void ClearData()
        {
            Enabled = false;
        }
    }
}
```

## Handler 模板

```csharp
using Core.Runtime;

namespace Hotfix
{
    public class DemoHandler : HandlerBase<DemoAction, DemoData>
    {
        public const string DemoGetInfo = "demo/getInfo";

        protected override void Reduce(DemoAction action)
        {
            switch (action)
            {
                case DemoSetEnabledAction setEnabled:
                    State.Enabled = setEnabled.Enabled;
                    ApplyState();
                    break;

                case DemoRefreshAction refresh:
                    SendMsg(DemoGetInfo, refresh.Request);
                    break;
            }
        }

        [MessageHandler(DemoGetInfo, MessageHandler.State.Success)]
        void OnDemoGetInfo(DemoResponse response)
        {
            State.Enabled = response.Enabled;
        }

        [MessageHandler(DemoGetInfo, MessageHandler.State.Error)]
        void OnDemoGetInfoError(string errorMessage)
        {
            State.Enabled = false;
        }
    }
}
```

## 注意事项

- 纯本地 Handler 修改 `State` 后必须调用 `ApplyState()`。
- 网络回包 `[MessageHandler]` 方法里修改 `State` 后不要手动调用 `ApplyState()`，框架会自动处理。
- 命令式请求必须存在对应 Success handler；Error handler 可选，缺失时会触发 `ChannelErrorCode` 事件。
- `IData.ClearData()` 只重置数据，不直接操作 UI。
- UI 生命周期内订阅状态时，要在隐藏或销毁时取消订阅。
- 不要在 Core Flux 中写具体玩法规则；玩法 Action / Data / Handler 放在 Hotfix。
- 网络请求前必须确认命令、Request / Response 类型和 `INetworkService` 注册方式，不要臆造协议接口。
