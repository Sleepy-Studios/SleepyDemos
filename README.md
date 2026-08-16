# SleepyDemos

多人协作的 Unity 练习项目。

当前目标是用一个主入口 Hub 承载多个相对独立的 Demo，小步迭代玩法，同时持续打磨启动、热更、资源管理、UI 框架和协作流程。

## 快速开始

- Unity 版本：见 [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)
- 推荐使用 `git clone --recurse-submodules https://github.com/Sleepy-Studios/SleepyDemos.git` 克隆；已有仓库执行 `git submodule update --init --recursive`
- 子模块完成后，用 Unity Hub 或 Unity Editor 打开仓库根目录
- 首次进入项目前，建议先读：
  - [docs/README.md](docs/README.md)
  - [docs/architecture/overview.md](docs/architecture/overview.md)
  - [docs/architecture/code-layout.md](docs/architecture/code-layout.md)
  - [docs/architecture/startup-flow.md](docs/architecture/startup-flow.md)

## 项目现状

项目目前不是“单场景小实验”结构，而是已经明确分成两条主轴：

- 代码主轴：`Core.Runtime`、`Core.Editor`、`Hotfix`
- 资源主轴：`Assets/LoadResources` 下的可加载资源，加上 `Assets/Scenes` 下的启动入口场景

启动主链路当前是：

1. `CoreEntrance`
2. `StartupPipeline`
3. `PrepareStartupState`
4. `ResourceStartupState`
5. `BeforeHotfixStartupState`
6. `HotfixEntry`
7. `HotfixBootService`
8. `LubanConfigSystem`
9. `GlobalDataSystem`
10. `MainMenuView`

也就是说，Core 先完成准备、资源和热更装配，Hotfix 再接管业务入口和主界面。

## 目录怎么理解

### 代码目录

- `Assets/Scripts/Core/Runtime`
  - 运行时底座
  - 放启动流程、资源管理、公共 UI、服务注册、通用组件

- `Assets/Scripts/Core/Editor`
  - 编辑器工具
  - 放热更构建、MvcBind 工具、检查与辅助脚本

- `Assets/Scripts/Hotfix`
  - 业务层与界面层
  - `Module/` 放主菜单、公共业务模块、View 和交互逻辑
  - `Demos/<DemoName>/` 放独立 Demo 玩法；Demo 专属适配层也收在这里

- `Assets/Scripts/Hotfix/Editor`
  - 只服务 Hotfix 业务或某个 Demo 的 Builder、Inspector 和装配工具
  - 继续归属独立的 `Hotfix.Editor`，不迫使 `Core.Editor` 反向依赖业务

### 资源目录

- `Assets/LoadResources/Demos/<DemoId>/`
  - 单个 Demo 的可加载资源
  - 当前 `drone_flight` 的场景、三机型、装备、配置和美术都收口在这里

- `Assets/LoadResources/UI` / `Art` / `Audio` / `VFX`
  - 多个 Demo 稳定复用的公共资源

- `Assets/LoadResources/Scenes`
  - 当前主要用于可加载场景模板

- `Assets/Scenes`
  - 当前启动入口场景与启动加载对象

一句话记忆：

- 代码归属看 `Assets/Scripts`
- 可加载资源归属看 `Assets/LoadResources`
- 启动入口场景看 `Assets/Scenes`
- 先判断职责，再决定落点

## 开发规则

- Core 放底座，不放具体玩法业务
- Hotfix 放业务和页面，不反向污染 Core
- Demo 资源优先自包含，只有稳定复用后才提到公共目录
- 优先复用现有模式，不随意引入新的并行体系
- 涉及启动、热更、资源加载、模块边界的改动，要同步更新文档

更完整的规则请看：
- [AGENTS.md](AGENTS.md)
- [CLAUDE.md](CLAUDE.md)
- [docs/architecture/hotfix-boundary.md](docs/architecture/hotfix-boundary.md)
- [docs/architecture/documentation-rules.md](docs/architecture/documentation-rules.md)

## 新增 Demo 怎么做

推荐流程已经整理在：
- [docs/runbooks/add-demo.md](docs/runbooks/add-demo.md)

最简版是：

1. 确定资源用 `DemoId` 和代码用 `DemoName`
2. 在 `Assets/LoadResources/Demos/<DemoId>/` 下建立资源目录
3. 在 `Assets/Scripts/Hotfix/Demos/<DemoName>/` 下建立玩法代码
4. 创建 YooAsset 可加载场景并登记运行期场景目录
5. 通过 `GameSceneNavigator` 从 Hub 暴露该 Demo，并提供返回 Hub 的入口
6. 把测试放入现有 `Tests.EditMode` / `Tests.PlayMode`，不要新建 Demo 测试程序集
7. 检查是否真的需要把能力上提到 Core

## 文档地图

这份 README 只承担项目入口说明，不再承载全部细节。

- [docs/README.md](docs/README.md)：文档总导航
- [docs/architecture/](docs/architecture/)：架构地图、边界、流程
- [docs/modules/](docs/modules/)：关键模块说明
- [docs/agent/](docs/agent/)：Agent 协作入口与项目级技能说明
- [docs/runbooks/](docs/runbooks/)：操作步骤和排障指南
- [docs/runbooks/use-luban-config.md](docs/runbooks/use-luban-config.md)：Luban 配置生成、读取与排障

如果代码、流程、模块入口变化了，文档需要和代码一起更新，不靠人工额外提醒。
