# SleepyDemos Agent Guide

请始终使用简体中文回复。

## 目标
- 这是一个多人协作的 Unity 练习项目，采用 Hub + Demo 岛的组织方式持续扩展玩法。
- 代码层以 `Core.Runtime`、`Core.Editor`、`Hotfix` 为主干。
- 资源层以 `Assets/LoadResources` 为主干，启动场景位于 `Assets/Scenes`。

## 开始任务前先看
1. `README.md`
2. `docs/README.md`
3. `docs/architecture/overview.md`
4. `docs/architecture/code-layout.md`
5. 与当前任务直接相关的模块文档或 runbook

## 分层规则
- `Assets/Scripts/Core/Runtime`：通用运行时基础设施。包括启动流程、资源管理、UI 框架、服务注册、通用组件。
- `Assets/Scripts/Core/Editor`：编辑器工具。包括热更构建、MvcBind 工具、导入和检查类工具。
- `Assets/Scripts/Hotfix`：业务层与界面层。包括主菜单、玩法入口、各业务模块逻辑。
- `Assets/LoadResources`：可加载资源主干；`Assets/Scenes`：启动入口场景与相关对象。
- 不允许 `Core.Runtime` 反向依赖 `Hotfix`。
- 先判断需求属于框架能力、编辑器能力还是业务玩法，再决定落点。

## 修改原则
- 优先复用现有模式，不随意引入并行的新框架或新目录体系。
- 优先做最小闭环改动，不顺手扩大为无关重构。
- 涉及启动链路、热更装配、资源加载、公共 UI 时，要明确说明影响面。
- 不修改与任务无关的资源、配置和生成物。

## 文档维护是必做项
- 只要改动影响以下任一内容，必须在同一任务中同步更新文档，不需要用户额外提醒：
  - 目录职责或模块边界
  - 启动流程、热更流程、资源加载流程
  - 新增、删除、合并关键模块
  - 新增或变更 Demo 接入流程
  - 编辑器工具入口、构建流程、排障步骤
- 如果现有文档已失效，必须顺手修正，不要把“文档过期”留给下一位开发者。
- 如果当前改动不足以影响架构或流程，可不改文档，但需要显式判断“本次无需同步文档”。
- 如果规则变化影响入口判断或协作约定，要同时更新 `CLAUDE.md`，保持两份入口文件同步。

## 文档地图
- `AGENTS.md`：Codex / 通用 agent 入口导航
- `CLAUDE.md`：Claude Code 入口导航
- `docs/README.md`：文档总导航
- `docs/architecture/`：全局架构与规则
- `docs/modules/`：关键模块说明
- `docs/runbooks/`：操作流程与排障指引

## 完成任务时至少说明
- 改动位于 `Core.Runtime`、`Core.Editor`、`Hotfix` 还是某个 Demo 资源目录
- 是否同步更新了文档；如果没有，为什么不需要
- 需要在 Unity 中手动验证什么
