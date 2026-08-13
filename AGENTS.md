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
- `Assets/Scripts/Hotfix/Editor`：只服务 Hotfix 业务模块的编辑器扩展，使用独立 `Hotfix.Editor` 程序集；允许引用 Hotfix，不得迫使 `Core.Editor` 反向依赖业务层。
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
- 启动系统、热更新、Flux、资源运行时、公共 UI 等大型模块必须形成完整模块文档；小型 Data 三件套可先不单独建文档，等形成独立入口或复杂规则后再补。
- 文档按三层落点：`docs/architecture/` 给开发人员看设计思路，`docs/modules/*.md` 给维护模块的人看边界和生命周期，`docs/runbooks/*.md` 给使用者或接入者看具体步骤。
- C# 注释与命名按 `docs/architecture/documentation-rules.md` 执行：只有带参数的公开方法使用完整 XML 注释；无参公开方法、公开字段/属性/事件只写简短 `///`；私有成员用 `//`；命名遵循 PascalCase / camelCase，不使用 `_` 私有字段前缀，避免拼音和无意义缩写。
- 如果规则变化影响入口判断或协作约定，要同时更新 `CLAUDE.md`，保持两份入口文件同步。

## 文档地图
- `AGENTS.md`：Codex / 通用 agent 入口导航
- `CLAUDE.md`：Claude Code 入口导航
- `docs/README.md`：文档总导航
- `docs/architecture/`：全局架构与规则
- `docs/modules/`：关键模块说明
- `docs/agent/`：Agent 协作入口与项目级技能说明
- `docs/runbooks/`：操作流程与排障指引

## 完成任务时至少说明
- 改动位于 `Core.Runtime`、`Core.Editor`、`Hotfix` 还是某个 Demo 资源目录
- 是否同步更新了文档；如果没有，为什么不需要
- 需要在 Unity 中手动验证什么



## 测试

自动化测试统一放在 `Assets/Scripts/Tests`。项目最多维护 Core.Tests 与按需创建的 Hotfix.Tests 两个逻辑测试域；物理 asmdef 只允许按 `.EditMode` / `.PlayMode` 拆分。禁止在 `Assets/Scripts/Core` 或 `Assets/Scripts/Hotfix` 下创建 Test Assembly；测试程序集必须设置 `autoReferenced=false`，EditMode 限制为 Editor，PlayMode 使用标准 PlayMode Test Assembly 配置，生产程序集不得反向引用测试程序集。

Unity Test Runner 是唯一自动化验证入口，不再新增并行的自定义基础设施校验菜单或 BatchMode 包装器。完整步骤见 `docs/runbooks/run-unity-tests.md`。

测试默认只运行当前任务直接相关的最小范围，选择顺序为：精确测试方法、当前功能对应的测试类、当前任务涉及的多个测试类。跨模块改动可以运行所有直接受影响的测试类，但不得自动扩大为整个 `Core.Tests`、全部 EditMode、全部 PlayMode 或第三方包测试。只有用户明确要求“全量测试”或“完整回归”时才运行项目全量测试；这里的全量默认仅包含项目自有的 `Core.Tests` 与未来的 `Hotfix.Tests`，第三方包测试必须另行明确指定。完成报告必须说明实际测试范围以及是否执行过全量测试。


运行 Unity 测试时，优先使用当前会话已安装的 `unity-skills` 技能（常见用户级入口为 `~/.agents/skills/unity-skills/SKILL.md`，以会话技能目录为准，不假设仓库内存在同名技能）。在第一次调用任何 UnitySkills REST 接口前，必须先主动确认当前实例端口：优先从 `~/.unity_skills/registry.json` 按项目路径读取；若 registry 缺失或不可信，再扫描 `http://localhost:8090-8100/health`，以 `/health` 返回的 `projectName`、`unityVersion`、`instanceId` 确认目标实例。禁止想当然写死 `8090` 或 `8091`。确认端口后，再调用 `test_run` / `test_run_by_name` 并用 `test_get_result` 轮询结果；不要在 Unity Editor 已打开同一项目时另起 batchmode Unity 实例。

禁止使用 `dotnet build`、`msbuild` 或类似方式构建 Unity 自动生成的 `.sln` / `.csproj` 解决方案；这些项目文件不能作为本仓库的编译验证入口。需要编译验证时，通过 Unity Editor 编译/控制台或项目既有 Unity 测试入口确认。

### Hot Reload 开发验证

项目已安装 Hot Reload 插件。Unity 处于 Play 模式且 Hot Reload 功能开启时，修改 C# 代码后应先查看 Hot Reload 面板或 Unity Console 的最新热重载日志。

- 如果日志明确显示改动已热重载成功（例如 `Reload finished`、`Changes applied`，或列表中包含本次修改的方法且已应用），直接按最新代码继续验证，不要为了重新编译而停止 Play 模式。
- 如果日志显示热重载失败、改动未应用、存在 unsupported changes，或 `Changes partially applied` 中没有覆盖本次修改的关键方法，才停止 Play 模式并让 Unity 重新编译。
- 不确定日志含义时，先把 Hot Reload 最新日志作为证据说明，不要臆测已经生效。

Hot Reload 面板 Timeline 不一定完整写入 Unity Console。需要让 AI/脚本判断本次改动是否已应用时，使用项目技能 `.codex/skills/hotreload-log/SKILL.md`，重点读取 `Library/com.singularitygroup.hotreload/patches.json` 中的 `modifiedMethods`、`failures`、`newFields`、`deletedFields`。
