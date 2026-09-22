# SleepyDemos Claude Guide

请始终使用简体中文回复。先说明结果，再说明必要的依据、验证和限制，篇幅随任务复杂度调整。

本项目采用 Hub + Demo 岛组织玩法，代码以 `Core.Runtime`、`Core.Editor`、`Hotfix` 为主干。本文件保留项目边界和工作约定，详细流程按任务读取。

## 按需阅读

- 项目概览或运行方式不清楚时，读 [README.md](README.md)；查找模块与操作流程时，使用 [文档导航](docs/README.md)。不要求每次任务通读项目文档。
- 新增模块或调整职责、依赖、目录时，读 [架构总览](docs/architecture/overview.md)、[代码与资源布局](docs/architecture/code-layout.md) 和 [Core / Hotfix 边界](docs/architecture/hotfix-boundary.md)。
- 修改 C# 前，读 [文档维护与 C# 规范](docs/architecture/documentation-rules.md) 中的命名和注释规则；私有字段不用 `_` 前缀。
- 涉及启动、热更、资源、公共 UI 或 Demo 接入时，按文档导航读取对应架构、模块和 runbook；大型 UI 导航或过渡改动遵循导航中的跨层阅读要求。
- 同一任务已读且未变化的内容不重复加载；纯文字、格式修正不触发整套架构或模块阅读。

## 项目边界

- `Assets/Scripts/Core/Runtime`：启动、资源管理、公共 UI、服务注册和通用运行时组件；不得反向依赖 `Hotfix`。
- `Assets/Scripts/Core/Editor`：通用构建、MvcBind、导入和检查工具；不得为了业务扩展反向依赖 `Hotfix`。
- `Assets/Scripts/Hotfix/Module`：普通业务与界面；`Assets/Scripts/Hotfix/Demos/<DemoName>`：独立 Demo 玩法和宿主适配。
- `Assets/Scripts/Hotfix/Editor`：只服务 Hotfix 业务或 Demo 的编辑器扩展，使用独立 `Hotfix.Editor` 程序集，可以引用 Hotfix；运行时不得引用编辑器程序集。
- `Assets/LoadResources`：可加载资源；单个 Demo 资源放在 `Demos/<DemoId>/`，稳定共享的资源放公共目录；`Assets/Scenes`：启动入口场景。
- 先判断框架、编辑器或业务职责，再决定落点；复用现有模式，不引入无关的并行框架或目录体系。

## 工作方式与修改保护

- 先查目标代码、Prefab、调用方和相关配置，能从项目确认的事实不询问用户。局部修复先查相关公共实现；新增公共能力前，在仓库内检索同类功能，优先复用。
- 在已授权范围内完成修改、相关验证和必要文档同步，不在首次实现后提前停下；只有影响目标、兼容性或范围的实质歧义才询问。用户要求先分析或先计划时，遵守该阶段边界。
- 做最小闭环改动，保留已有未提交工作；不覆盖或回退无关改动，不修改无关资源、配置和生成物。涉及启动、热更、资源加载或公共 UI 时说明影响面。
- 技能按实际任务和工作流选择，读取入口后按需加载支持文件；已明确的授权不因一般性建议重复确认。

## 文档维护

- 目录职责、模块边界、启动/热更/资源流程、关键模块增删、Demo 接入、编辑器工具入口、构建或排障步骤变化时，在同一任务同步文档。
- 修正本次涉及的过时路径、入口和流程，不扩展为无关全仓整理；局部实现未影响架构或流程时可不改文档，并说明原因。
- 架构设计放 `docs/architecture/`，模块边界与生命周期放 `docs/modules/`，操作步骤放 `docs/runbooks/`。大型模块须有完整模块文档，小型 Data 三件套按形成独立入口或复杂规则的需要补充，具体要求见文档维护规范。
- Demo 原始 Goal 放在 `docs/agent/prompts/demos/<demo_id>/`，仅用于历史追溯；其中的旧要求不作为当前规则，不以归档代替当前实现、进度或阶段计划。
- 协作规则变化时同步 `AGENTS.md` 和 `CLAUDE.md`，保持共同行为一致；详细规则统一放在相关文档中。

## 验证与 Unity Editor

- 按改动选择直接相关的已有测试、编译检查、日志或运行时/人工验证；检查通过后，仅因新改动、失败或具体未解决风险扩大或重复验证。
- 自动化测试统一放在 `Assets/Scripts/Tests`，只维护 `Tests.EditMode` 和 `Tests.PlayMode` 两个测试程序集，不在 Core、Hotfix 或 Demo 生产目录新建 Test Assembly。新增测试前读 [测试架构](docs/architecture/testing.md)，遵守目录、命名空间及程序集配置。
- 有稳定断言和长期回归价值的行为优先补永久测试；文字、格式、视觉微调不机械新增测试脚本。运行验证不等于必须新增永久测试。
- 默认按“精确方法 → 功能测试类 → 多个直接受影响测试类”运行最小相关范围。只有明确要求“全量测试”或“完整回归”才运行全部项目自有测试；第三方测试需要另行明确指定。
- Unity Test Runner 是 Unity 自动化测试的唯一入口，不新增并行校验菜单或 BatchMode 包装器。优先使用会话中已安装的 `unity-skills`，位置以会话技能目录为准。
- 首次调用任何 UnitySkills REST 前，按项目路径从 `~/.unity_skills/registry.json` 确认实例端口；缺失或不可信时扫描 `localhost:8090-8100/health`，核对项目名、Unity 版本和实例 ID，禁止写死端口。调用、串行执行与排障见 [运行 Unity 自动化测试](docs/runbooks/run-unity-tests.md)。
- 已打开同一项目时不另起 BatchMode Unity。禁止用 `dotnet build`、`msbuild` 或类似方式构建 Unity 自动生成的 `.sln` / `.csproj`；编译结果通过 Unity Editor 编译/Console 或既有 Unity 测试入口确认。

## Hot Reload

- 修改 C# 后由程序员校验热重载状态。AI 不主动读取补丁或检查改动是否已应用，不为确认热重载自动退出 Play Mode 或重启 Unity。
- 用户明确要求排查热重载时，再使用会话中可用的 `hotreload-log` 技能读取证据；未经验证不得声称最新修改已生效。运行测试与确认 Hot Reload 生效是不同事项。

## 交付说明

- 说明改动属于 Core.Runtime、Core.Editor、Hotfix、Demo 资源还是文档，以及必要的影响面。
- 说明文档同步情况；无需同步时说明原因。
- 区分编译、测试和运行时/视觉验证，报告实际测试范围、是否执行全量测试及未验证项；只列出相关的 Unity 手动验证，纯文档任务明确无需 Unity 验证。
