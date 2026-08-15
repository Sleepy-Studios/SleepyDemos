# 文档导航

这套文档的目标不是把所有目录都配一个说明文件，而是给协作者和 agent 一张可维护的“地图”。

## 入口文件
- `AGENTS.md`：Codex / 通用 agent 的入口导航
- `CLAUDE.md`：Claude Code 的入口导航
- 两者都只保留短导航和硬规则，详细内容统一落在 `docs/` 中

## 先读哪里
1. [架构总览](./architecture/overview.md)
2. [代码与资源布局](./architecture/code-layout.md)
3. [启动与热更流程](./architecture/startup-flow.md)
4. [Core / Hotfix 边界](./architecture/hotfix-boundary.md)
5. [资源命名规范](./architecture/asset-naming.md)
6. 根据任务类型继续查对应模块说明、Agent 技能入口或 runbook

大型 UI 导航或过渡改动需要同时阅读 [Core UI 渲染设计原则](./architecture/ui-rendering.md)、[Core UI 运行时](./modules/ui-runtime.md)、[接入 Core UI View](./runbooks/create-ui-view.md) 和 [运行 Unity 自动化测试](./runbooks/run-unity-tests.md)，不要只修改其中一层。

## 文档结构
- `architecture/`
  - 给开发人员看思路：全局规则、分层边界、启动流程、设计原则、文档维护规则
  - [资源命名规范](./architecture/asset-naming.md)
  - [资源系统设计原则](./architecture/resource-system.md)
  - [配置系统设计](./architecture/config-system.md)
  - [Core UI 渲染设计原则](./architecture/ui-rendering.md)
  - [Unity 自动化测试架构](./architecture/testing.md)
  - [文档维护与 C# 规范](./architecture/documentation-rules.md)
- `modules/`
  - 给维护模块的人看：关键模块的职责、入口、主链路、生命周期、边界和验证重点
  - [Core.Runtime](./modules/core-runtime.md)
  - [热更新模块](./modules/hotfix.md)
  - [Core 资源运行时](./modules/resource-runtime.md)
  - [Core UI 运行时](./modules/ui-runtime.md)
  - [运行期场景导航](./modules/scene-runtime.md)
  - [Core 事件系统](./modules/eventing/README.md)
  - [Core Flux 状态流](./modules/flux.md)
  - [Hotfix 启动系统](./modules/hotfix-boot-systems.md)
  - [Luban 配置模块](./modules/luban-config.md)
  - [Hotfix 主入口](./modules/hotfix-main.md)
  - [DroneFlight 无人机飞行仿真](./modules/drone-flight.md)
  - [DroneFlight 正式模型契约](./modules/drone-flight-model-contract.md)
  - [DroneFlight 设计演进与决策记录](./modules/drone-flight-history.md)
- `agent/`
  - Agent 协作入口与项目级技能说明
  - [项目 Skill 入口](./agent/skills.md)
- `superpowers/`
  - 大型任务的设计规格与可执行实施计划
  - [无人机飞行仿真 Codex Goal](./superpowers/specs/2026-08-13-drone-flight-simulation-goal.md)
  - [无人机飞行仿真实施计划](./superpowers/plans/2026-08-13-drone-flight-simulation.md)
- `runbooks/`
  - 给使用者或接入者看：新增 Demo、构建热更、模块接入、排障等操作步骤
  - [项目工具与 Agent 技能总览](./runbooks/project-tools.md)
  - [接入 Core UI View](./runbooks/create-ui-view.md)
  - [使用 Core 基础 UI 组件](./runbooks/use-core-ui-components.md)
  - [使用资源 Loader](./runbooks/use-resource-loader.md)
  - [接入运行期场景导航](./runbooks/use-scene-navigation.md)
  - [在 Unity Editor 直接运行 Demo 岛](./runbooks/run-demo-island-directly.md)
  - [使用 Luban 配置](./runbooks/use-luban-config.md)
  - [运行 Unity 自动化测试](./runbooks/run-unity-tests.md)
  - [调试和整定 DroneFlight](./runbooks/tune-drone-flight.md)
  - [迁移 DroneFlight 到正式项目](./runbooks/migrate-drone-flight.md)

## 如何判断文档该写到哪里
- 这是全局规则、设计原则或架构边界：写到 `architecture/`
- 这是关键模块维护说明：写到 `modules/`
- 这是 Agent 技能入口、技能发现或技能同步约定：写到 `agent/`
- 这是使用者接入步骤或可执行流程：写到 `runbooks/`
- 只是单个函数实现细节：优先留在代码和注释里，不额外建 md

## 文档维护规则
- 关键入口、边界、流程变化时，必须同步改文档。
- 新增重要模块时，补对应 `modules/*.md`。
- 删除或合并重要模块时，清理对应模块文档和导航链接。
- 如果规则变化影响入口判断或协作方式，要同步更新 `AGENTS.md` 和 `CLAUDE.md`。
- 文档应保持“短、能定位、可执行”，不要写成长篇空话。
