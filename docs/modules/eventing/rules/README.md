# 事件系统规则

## 总原则

- 事件系统只负责同步、轻量、无返回值的一次性通知。
- 事件系统不保存业务状态；需要状态快照时使用 `GlobalData`。
- 事件系统不管理 UI 栈；UI 打开关闭仍由 `UIManager` 负责。
- 同一个事件名只能对应一种参数签名。

## 事件命名

Hotfix 业务事件名优先写入：

```text
Assets/Scripts/Hotfix/Eventing/EventConst.cs
```

不要在业务代码里散落裸字符串。

Core 只有确实需要公共事件名时，再新增：

```text
Assets/Scripts/Core/Runtime/Eventing/CoreEventConst.cs
```

事件名使用模块前缀，分隔符使用 `_`，不要使用 `.`。

```csharp
public const string MainMenuRefresh = "MainMenu_Refresh";
public const string DemoEnterRequested = "Demo_EnterRequested";
```

同一个事件名只能对应一种参数签名。

正确：

```csharp
EventDispatcher.AddEventListener<string>(EventConst.DemoEnterRequested, OnDemoEnterRequested);
EventDispatcher.TriggerEvent(EventConst.DemoEnterRequested, "fishing");
```

错误：

```csharp
EventDispatcher.AddEventListener(EventConst.DemoEnterRequested, OnDemoEnterRequested);
EventDispatcher.TriggerEvent(EventConst.DemoEnterRequested, "fishing");
```

## 监听生命周期

UI 监听优先在 `OnShow` 注册，在 `OnHide` 移除。

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

如果监听只跟对象生命周期绑定，也可以在初始化时注册、销毁时移除。

`permanent = true` 只允许用于启动后长期存在的全局监听。普通 UI、Demo、玩法模块不要使用。

```csharp
EventDispatcher.AddEventListener(EventConst.AppResume, OnAppResume, true);
```

## 移除规则

- `RemoveEvent(eventName)`：移除指定事件名下的所有监听。
- `RemoveAll(false)`：移除所有非 permanent 监听，保留 permanent 监听。
- `RemoveAll()`：移除所有监听，包括 permanent 监听。

切换 Demo、重置热更业务或回到主入口时，如果需要统一清理普通业务监听，优先使用 `RemoveAll(false)`。

## 文档示例

文档示例中的事件名必须能在示例 `EventConst` 中找到。

不要用同一个事件名同时演示无参数和带参数。

正确：

```csharp
EventDispatcher.TriggerEvent(EventConst.MainMenuRefresh);
EventDispatcher.TriggerEvent(EventConst.DemoEnterRequested, "fishing");
```

错误：

```csharp
EventDispatcher.TriggerEvent(EventConst.MainMenuRefresh);
EventDispatcher.TriggerEvent(EventConst.MainMenuRefresh, "fishing");
```

展示监听时，优先同时展示注册和移除。
