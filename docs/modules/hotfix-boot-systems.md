# Hotfix 启动系统

## 负责什么

Hotfix 启动系统用于承接 Core 完成热更装配后的业务初始化。它参考钓鱼项目的 System 化初始化方式，但当前保持轻量顺序执行，不引入完整 ScheduleState。

主链路：

1. `HotfixEntry.Awake`
2. `UITypeReflection.Scan`
3. `HotfixBootService.RunBootSystems`
4. `LubanConfigSystem`
5. `GlobalDataSystem`
6. `FluxService.InitializeGlobalData`
7. `MainMenuView`

## 代码位置

- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/IHotfixBootSystem.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/HotfixBootService.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/Systems/LubanConfigSystem.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/Systems/GlobalDataSystem.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Services/FluxService.cs`

## 系统约定

每个 Hotfix 启动系统实现：

```csharp
public interface IHotfixBootSystem
{
    string Name { get; }
    string Description { get; }
    UniTask RunAsync(HotfixStartupContext context);
}
```

新增系统统一加入 `HotfixBootService` 的 `systems` 列表。`Name` 用于日志和排查，`Description` 用于加载界面的用户可读进度文案。不要在 `HotfixEntry.Awake` 里散写具体模块初始化逻辑。

## LubanConfigSystem

`LubanConfigSystem` 是当前第一个启动系统。它要求 Core 的 `ResourceServices.Default` 已经初始化，通过 `LubanConfigService` 预加载并解析全部客户端表；只有全部成功后才允许后续全局数据和 UI 访问 `Cfg.Tables`。

详细生命周期、失败语义与扩展边界见 [Luban 配置模块](./luban-config.md)。

## GlobalDataSystem

`GlobalDataSystem` 在配置加载完成后运行，负责通过 `FluxService` 注册全局 Flux Data，并在启动日志中标记全局数据初始化完成。

当前注册：

```csharp
GlobalData.Add<UserData>().InitData();
```

后续全局常驻 Data 也放在 `FluxService.InitializeGlobalData()` 中统一注册，例如玩家资料、设置、账号状态等。当前 `UserData` 放在 `Assets/Scripts/Hotfix/Module/User/`，采用 Action / Data / Handler 三件套；由于当前只记录本机硬件配置，规模还小，暂不单独建立模块文档。

## 重新登录清理

重新登录、切号、退出登录后进入登录页时，统一调用：

```csharp
FluxService.ClearForRelogin();
```

这会调用 `GlobalData.ClearData()`，保留已注册 Data 和 Handler，只清理每个 Data 的内部状态并通知订阅者；同时重置 `FluxService` 初始化标记，下一次进入启动初始化时可以重新刷新全局 Data。

如果某个临时活动模块需要彻底移除状态和 Handler，再单独使用：

```csharp
GlobalData.Remove<ActivityData>();
```

## 修改这里时注意什么

- 启动系统只负责启动期业务初始化，不负责具体 UI 展示细节。
- Core.Runtime 不反向依赖 Hotfix；Hotfix 启动系统只能放在 Hotfix 层。
- 新增全局常驻 Data 时，同步更新本文；如果该 Data 背后形成独立入口、复杂规则或多人高频改动边界，再新增对应模块文档。
- 新增系统时说明执行顺序、前置依赖和验证方式。

## 验证重点

- `HotfixEntry` 能完成 View 扫描后运行启动系统。
- `GlobalDataSystem` 只初始化一次。
- `LubanConfigSystem` 在 `GlobalDataSystem` 前完成，重复初始化不重复加载资源。
- `UserData` 在 `MainMenuView` 显示前已经注册。
- 重新登录清理时调用 `FluxService.ClearForRelogin()` 后，订阅者能收到清空后的 Data。
