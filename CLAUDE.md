# SleepyDemos Claude Guide

请始终使用简体中文回复。

## 目标
- 这是一个多人协作的 Unity 练习项目，采用 Hub + Demo 岛的组织方式持续扩展玩法。
- 代码主干分为 `Core.Runtime`、`Core.Editor`、`Hotfix`。
- 资源主干位于 `Assets/LoadResources`，启动场景位于 `Assets/Scenes`。

## 会话开始时先读
1. `README.md`
2. `docs/README.md`
3. `docs/architecture/overview.md`
4. `docs/architecture/code-layout.md`
5. 与当前任务直接相关的模块文档或 runbook

## 工作方式
- 先自己查代码、Prefab、调用链和相关配置，能自己确认的不要问用户。
- 大一点的改动先理解边界和入口，再动手修改。
- 优先复用项目现有模式，不随意引入新的并行体系。
- 优先做最小闭环改动，不顺手扩大为无关重构。

## 分层规则
- `Assets/Scripts/Core/Runtime`：运行时底座。放启动流程、资源管理、公共 UI、服务注册、通用组件。
- `Assets/Scripts/Core/Editor`：编辑器工具。放热更构建、MvcBind 工具、导入与检查类工具。
- `Assets/Scripts/Hotfix`：业务层与界面层。放主菜单、玩法入口、各业务模块逻辑。
- `Assets/LoadResources`：可加载资源主干；`Assets/Scenes`：启动入口场景与相关对象。
- 不允许 `Core.Runtime` 反向依赖 `Hotfix`。

## 文档与规则同步
- `AGENTS.md` 和 `CLAUDE.md` 是两份入口导航，必须保持一致的方向和边界判断。
- 详细规则统一以 `docs/` 为准，避免两份入口文件各写一套细节。
- 只要改动影响目录职责、模块边界、启动流程、热更流程、资源加载流程、Demo 接入流程或编辑器工具入口，必须在同一任务中同步更新文档，不需要用户提醒。
- 启动系统、热更新、Flux、资源运行时、公共 UI 等大型模块必须形成完整模块文档；小型 Data 三件套可先不单独建文档，等形成独立入口或复杂规则后再补。
- 文档按三层落点：`docs/architecture/` 给开发人员看设计思路，`docs/modules/*.md` 给维护模块的人看边界和生命周期，`docs/runbooks/*.md` 给使用者或接入者看具体步骤。
- `Core.Runtime` / `Core.Editor` 中会被业务侧直接调用的底层公共方法，尤其是带 bool、command、Type、回调、异步返回值等参数的方法，必须补完整 C# XML 注释；纯标记接口、内部容器和显而易见属性不机械补注释。
- 如果规则变化影响入口判断，要同时更新 `AGENTS.md` 和 `CLAUDE.md`。

## 文档地图
- `docs/README.md`：文档总导航
- `docs/architecture/`：全局架构与规则
- `docs/modules/`：关键模块说明
- `docs/agent/`：Agent 协作入口与项目级技能说明
- `docs/runbooks/`：操作流程与排障指引

## 完成任务时至少说明
- 改动位于 `Core.Runtime`、`Core.Editor`、`Hotfix` 还是某个 Demo 资源目录
- 是否同步更新了文档；如果没有，为什么不需要
- 需要在 Unity 中手动验证什么
