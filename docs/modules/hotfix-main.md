# Hotfix 主入口模块说明

## 负责什么

这一块负责业务接管后的首屏体验，目前以主菜单模块为中心，承担 Hotfix 入口、View 注册和首页展示。

## 关键入口

- `Assets/Scripts/Hotfix/AppDelegate/HotfixEntry.cs`
- `Assets/Scripts/Hotfix/AppDelegate/Boot/HotfixBootService.cs`
- `Assets/Scripts/Hotfix/Module/Main/`

## 当前行为

- 扫描 Hotfix 程序集中的 View 类型
- 运行 Hotfix 启动系统，当前会通过 `FluxService` 注册 `UserData`
- 打开 `MainMenuView`
- `MainMenuView` 订阅 `UserData`，打印启动时记录的本机硬件配置
- 在启动完成后销毁加载界面

## 改这里时注意什么

- 改主菜单时，不要把通用 UI 能力塞回业务模块
- 如果新增 Demo 入口，优先从主菜单模块接入，不要绕过入口链路
- 如果新增启动期业务初始化，优先加入 `HotfixBootService`，不要散写在 `HotfixEntry`
- 如果主界面打开失败，先检查 MvcBind 生成、预制体地址和类型扫描

## 常见任务

- 增加新的 Demo 入口按钮
- 调整主菜单展示逻辑
- 接入新的首页模块
- 修复首屏 View 注册或加载异常
