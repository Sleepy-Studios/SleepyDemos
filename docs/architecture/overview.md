# 架构总览

## 项目定位

SleepyDemos 是一个多人协作的 Unity 练习项目。整体形态是：
- 一个主入口 Hub
- 多个相对独立的 Demo 岛
- 一套共享的启动、资源、UI、热更基础设施

项目当前使用：
- Unity `6000.3.15f1`
- HybridCLR
- YooAsset
- UniTask
- asmdef 分层

## 两套组织轴线

这个项目需要同时理解两套组织方式：

### 代码轴线
- `Core.Runtime`：运行时底座
- `Core.Editor`：编辑器工具
- `Hotfix`：业务层与界面层

### 资源轴线
- `Assets/LoadResources/Demos/<DemoId>/`：单个 Demo 专属可加载资源
- `Assets/LoadResources/UI`、`Art`、`Audio`、`VFX`：共用可加载资源
- `Assets/LoadResources/Scenes`：可加载场景模板
- `Assets/Scenes`：当前启动入口场景与启动相关对象

不要把这两套轴线混成一套目录规则：
- `Assets/LoadResources` 负责可加载资源归属
- `Assets/Scenes` 负责启动入口场景归属
- `Assets/Scripts` 负责代码职责归属

## 当前主干调用关系

启动主链路大致为：

1. `CoreEntrance`
2. `StartupPipeline`
3. `PrepareStartupState`
4. `ResourceStartupState`
5. `BeforeHotfixStartupState`
6. `HotfixEntry`
7. `HotfixBootService`
8. `MainMenuView`

也就是说：
- Core 先完成准备、资源初始化、元数据和程序集装配
- Hotfix 再运行启动系统，注册全局 Flux Data，接管业务入口和界面显示

## 设计原则

- 底座能力沉淀在 Core，不把业务玩法塞进 Core
- Demo 资源尽量岛状隔离，降低多人协作冲突
- 只有稳定复用的能力才上提为公共模块
- 文档以导航和边界为主，不追求“每个目录一个 md”
