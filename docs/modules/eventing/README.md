# Core 事件系统

## 设计目标

事件系统用于处理同步、轻量、无返回值的一次性通知，避免 UI、Demo、流程控制之间为了“通知一声”而直接互相引用。

适合的场景：
- 主菜单请求进入某个 Demo
- Demo 退出后通知 Hub 刷新入口状态
- 某个流程结束后通知 UI 播放一次提示
- Core 运行时向 Hotfix 广播轻量状态变化

第一版沿用参考项目常见的 `AddEventListener / RemoveEventListener / TriggerEvent` 写法，事件名使用 `string` 常量管理。相比参考项目，SleepyDemos 额外补上参数签名校验、重复注册保护、异常隔离和移除接口。

设计约束：
- 事件分发器在 Core，业务事件名在 Hotfix
- 第一版只做同步事件，不做异步、队列、优先级、事件消费
- 同名事件参数签名不一致时立即报错
- UI 生命周期内必须成对注册和移除
- 事件系统只传递一次性通知，不保存业务状态

核心类命名为 `EventDispatcher`，避免和 Unity 的 `UnityEngine.EventSystems.EventSystem` 混名。

## 代码位置

- `Assets/Scripts/Core/Runtime/Eventing/EventDispatcher.cs`：对外静态接口
- `Assets/Scripts/Core/Runtime/Eventing/EventSignal.cs`：内部监听列表和参数签名实现
- `Assets/Scripts/Hotfix/Eventing/EventConst.cs`：热更业务事件名集合

`Core.Runtime` 暂不预置 `CoreEventConst`。只有 Core 内确实需要公共事件名时，再新增 `CoreEventConst`。

## 架构设计

`EventDispatcher` 内部按事件名维护一个 `EventSignalBase`：

```csharp
Dictionary<string, EventSignalBase>
```

不同参数数量使用不同的 Signal 类型：

```csharp
EventSignal
EventSignal<T>
EventSignal<T, U>
EventSignal<T, U, V>
EventSignal<T, U, V, W>
```

这样做的原因是：
- 0 到 4 个参数覆盖当前 UI 和玩法通知常见需求
- 同一个事件名只能绑定到一种参数签名，避免同名事件被不同模块用成不同含义
- 内部不需要把所有参数装箱成 `object[]`

每个监听项记录：

```csharp
handler
permanent
```

`permanent` 用于区分长期全局监听和普通临时监听。`RemoveAll(false)` 会清掉非永久监听，保留永久监听。

## API 范围

当前支持：
- `AddEventListener`：0 到 4 个参数
- `RemoveEventListener`：0 到 4 个参数
- `TriggerEvent`：0 到 4 个参数
- `RemoveEvent`
- `RemoveAll`
- `HasEvent`
- `GetListenerCount`

第一版明确不支持：
- 异步事件
- 事件队列
- 监听优先级
- 阻断传播
- 事件返回值
- ScriptableObject 事件通道

这些能力等真实需求出现后再单独设计，避免事件系统过早变成“大而全”的流程框架。

## 使用方式

事件名统一放在 Hotfix 的 `EventConst` 中：

```csharp
namespace Hotfix
{
    public static class EventConst
    {
        public const string AppResume = "App_Resume";
        public const string MainMenuRefresh = "MainMenu_Refresh";
        public const string DemoEnterRequested = "Demo_EnterRequested";
    }
}
```

无参数事件：

```csharp
EventDispatcher.AddEventListener(EventConst.MainMenuRefresh, Refresh);
EventDispatcher.TriggerEvent(EventConst.MainMenuRefresh);
EventDispatcher.RemoveEventListener(EventConst.MainMenuRefresh, Refresh);
```

带参数事件：

```csharp
EventDispatcher.AddEventListener<string>(EventConst.DemoEnterRequested, OnDemoEnterRequested);
EventDispatcher.TriggerEvent(EventConst.DemoEnterRequested, "fishing");
EventDispatcher.RemoveEventListener<string>(EventConst.DemoEnterRequested, OnDemoEnterRequested);
```

UI 中建议在显示时注册、隐藏或销毁时移除，避免隐藏界面继续响应事件。

```csharp
protected override void OnShow()
{
    EventDispatcher.AddEventListener<string>(EventConst.DemoEnterRequested, OnDemoEnterRequested);
}

protected override void OnHide()
{
    EventDispatcher.RemoveEventListener<string>(EventConst.DemoEnterRequested, OnDemoEnterRequested);
}
```

`permanent = true` 只给启动后长期存在的全局监听使用：

```csharp
EventDispatcher.AddEventListener(EventConst.AppResume, OnAppResume, true);
```

普通 UI、Demo、玩法监听不要设为 permanent。

## 模块规则

事件系统的编码约束独立放在 [rules/](./rules/README.md) 下。接入事件、命名事件、编写示例或调整生命周期时，先看规则文件。

## 与其他系统的边界

- `EventDispatcher`：一次性通知，不保存状态，不要求返回值
- `GlobalData`：保存状态，订阅状态快照，适合玩家数据、设置状态、Demo 当前状态
- `UIManager`：管理 UI 打开、关闭、栈和层级
- `UnityEngine.EventSystems.EventSystem`：处理 Unity UI 输入事件

判断口诀：
- 只是通知一声：用 `EventDispatcher`
- 需要保存并反复读取：用 `GlobalData`
- 是 UI 栈行为：用 `UIManager`
- 是按钮、点击、拖拽输入：用 Unity UI 事件系统

## 约束

- 同一个事件名只能使用同一种参数签名。
- 不要使用裸字符串散落在业务代码里，优先写入 `EventConst`。
- 事件名建议带模块前缀，例如 `MainMenu_Refresh`。
- 触发事件时不依赖监听顺序。
- 回调异常会被记录，但不会中断后续监听。
- 触发时允许监听者移除自己。
- 事件系统只负责通知，不负责业务状态保存。

## 验证重点

修改事件系统时至少验证：
- 无参数和 1 到 4 参数事件可以注册、触发、移除
- 重复注册同一个回调不会重复触发
- 同名事件混用不同参数签名会报错
- 回调中移除监听不会导致遍历异常
- `RemoveAll(false)` 会保留 permanent 监听
- `RemoveAll()` 会清掉全部监听
