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
6. 根据任务类型继续查对应模块说明或 runbook

## 文档结构
- `architecture/`
  - 全局规则、分层边界、启动流程、文档维护规则
  - [资源命名规范](./architecture/asset-naming.md)
- `modules/`
  - 关键模块的职责、入口、常见改法、注意事项
  - [Core.Runtime](./modules/core-runtime.md)
  - [Core 资源运行时](./modules/resource-runtime.md)
  - [Core UI 运行时](./modules/ui-runtime.md)
  - [Hotfix 主入口](./modules/hotfix-main.md)
- `runbooks/`
  - 新增 Demo、构建热更、排障等操作步骤
  - [项目工具与 Agent 技能总览](./runbooks/project-tools.md)
  - [验证 Core 运行时基础设施](./runbooks/validate-core-runtime-infrastructure.md)

## 如何判断文档该写到哪里
- 这是全局规则或边界：写到 `architecture/`
- 这是关键模块的局部说明：写到 `modules/`
- 这是可执行步骤：写到 `runbooks/`
- 只是单个函数实现细节：优先留在代码和注释里，不额外建 md

## 文档维护规则
- 关键入口、边界、流程变化时，必须同步改文档。
- 新增重要模块时，补对应 `modules/*.md`。
- 删除或合并重要模块时，清理对应模块文档和导航链接。
- 如果规则变化影响入口判断或协作方式，要同步更新 `AGENTS.md` 和 `CLAUDE.md`。
- 文档应保持“短、能定位、可执行”，不要写成长篇空话。
