# 新增 Demo 操作流程

## 适用场景

用于把一个相对独立的玩法接入 SleepyDemos 的 Hub、YooAsset 场景导航和统一测试体系。目标是让 Demo 资源自包含、运行入口明确，并避免把具体玩法塞进 Core。

## 前置条件

- 已阅读[代码与资源布局](../architecture/code-layout.md)、[资源命名规范](../architecture/asset-naming.md)和[运行期场景导航](../modules/scene-runtime.md)。
- 已确认玩法确实是独立 Demo，而不是现有业务模块的一部分。
- 开始前检查工作树，保护已有代码、资源和 `.meta`。

## 1. 确定名称

同时确定两个名称：

- `DemoId`：资源目录和地址使用英文小写加下划线，例如 `gravity_well`。
- `DemoName`：C# 目录和类型前缀使用 PascalCase，例如 `GravityWell`。

不要在文件名中使用短横线、空格或特殊字符。

## 2. 建立资源目录

在 `Assets/LoadResources/Demos/<DemoId>/` 建立 Demo 根目录。根据实际需要使用现有目录矩阵，例如：

```text
Assets/LoadResources/Demos/<DemoId>/
├── Scenes/
├── Prefabs/
├── Art/
├── Audio/
├── Data/
└── VFX/
```

资产必须进入功能子目录，不要直接堆在 Demo 根目录。只有两个以上 Demo 已经稳定复用的资源，才考虑移动到公共 `UI`、`Art`、`Audio` 或 `VFX`。

## 3. 建立玩法代码

独立 Demo 的运行时代码放在：

```text
Assets/Scripts/Hotfix/Demos/<DemoName>/
```

普通 Hotfix 业务模块仍使用 `Assets/Scripts/Hotfix/Module/`，不要把二者全局混为一谈。

复杂 Demo 建议明确区分：

- 核心玩法：只依赖 Unity 和本 Demo 类型，放控制、物理、状态、装备等逻辑。
- 宿主适配：放项目专属 UI、资源加载、Hub 导航和演出编排，收口到 `Adapters/`。

核心代码不能反向引用适配器。Demo 专属能力也不能因为调用方便塞进 `Core.Runtime`。

只服务该 Demo 的 Builder、Inspector 和资源装配工具放在：

```text
Assets/Scripts/Hotfix/Editor/<DemoName>/
```

它们继续归属 `Hotfix.Editor`。不要让 `Core.Editor` 为业务工具反向依赖 Hotfix，也不要为单个 Demo 新建生产程序集。

## 4. 创建场景和资源

- 可加载场景使用纯语义名，例如 `Scenes/Main.unity`。
- 启动入口 `AppEntrance.unity` 继续留在 `Assets/Scenes`，不要把 Demo 场景混入启动目录。
- Prefab、材质、贴图、配置和音效按[资源命名规范](../architecture/asset-naming.md)放入对应子目录。
- MvcBind 生成的 `*Component.cs` 不可手改；需要自定义输出时使用现有 MvcBind 自定义 Module 目录能力。

## 5. 接入 YooAsset 与场景导航

1. 确认 Demo 资源位于 YooAsset `Demos` Collector 覆盖范围。
2. 在 `GameSceneId` 增加业务枚举值。
3. 在 `GameSceneCatalog` 登记场景地址。
4. 从 Hub 入口调用 `GameSceneNavigator.Instance.SwitchAsync(...)`。
5. Demo 内提供切换到 `GameSceneId.Hub` 的明确返回入口。

Demo 场景不加入 Build Settings。当前 Build Settings 只保留 `Assets/Scenes/AppEntrance.unity`；不要通过加入场景绕过 Collector、地址或加载问题。业务组件也不要直接调用 `SceneManager.LoadSceneAsync` 绕开导航事务。

返回 Hub 时至少处理：

- 停止或释放输入会话。
- 关闭 Demo 临时 UI。
- 释放场景持有的资源 Loader 和异步任务。
- 清理静态订阅、事件和临时控制状态。
- 不重载或重复创建 `AppEntrance`。

## 6. 支持 Editor 直启（可选）

如果开发时需要直接打开 Demo 场景后 Play，按[在 Unity Editor 直接运行 Demo 岛](./run-demo-island-directly.md)接入现有 `DemoIslandEditorBootstrap`。

这条旁路只在 Editor 补齐最小运行时，不执行完整 HybridCLR 装配，也不得扩展到 Development 或 Release。正式运行仍从 `AppEntrance → Hub` 进入。

## 7. 添加测试

自动化测试只进入现有两个物理程序集：

```text
Assets/Scripts/Tests/EditMode/Demo/<DemoName>/
Assets/Scripts/Tests/PlayMode/Demo/<DemoName>/
```

- 命名空间使用 `Tests.Demo`。
- 不在 Hotfix 或 Core 下创建测试程序集。
- 不为单个 Demo 新建 asmdef。
- 生产程序集不得引用测试程序集。
- 优先写纯逻辑 EditMode 测试；必须依赖 Rigidbody、Scene 或生命周期时再使用 PlayMode。

验证时按“精确方法 → 当前测试类 → 本任务直接影响的多个测试类”选择最小范围。只有用户明确要求全量回归时，才运行整个 `Tests.EditMode` 和 `Tests.PlayMode`。

## 8. 判断文档落点

- 设计原则、跨层边界或为什么这样拆：更新 `docs/architecture/`。
- 关键 Demo 的职责、入口、生命周期和维护边界：更新 `docs/modules/`。
- 接入、构建、调试和排障步骤：更新 `docs/runbooks/`。

如果 Demo 来源于一份较完整的原始 Goal，且确实需要保留需求来源，可归档到：

```text
docs/agent/prompts/demos/<demo_id>/original-goal.md
```

归档文件必须说明它不是当前实现或进度真源，并链接到当前模块文档。阶段计划和复选框不长期归档。

## 9. 验证

至少完成：

1. 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名`，确认无 Error。
2. 外部复制或改名资源后，运行 `Tools/SleepyDemos/同步 LoadResources 资产 Label`，确认 Project 搜索 `l:demo` 可找到资源。
3. 从 `AppEntrance` 进入 Hub，再进入 Demo。
4. 从 Demo 返回 Hub，确认输入、UI 和临时资源已清理。
5. 如支持 Editor 直启，直接打开 Demo 场景 Play，并确认正式入口没有重复初始化。
6. 运行当前 Demo 直接相关的精确测试或测试类。
7. 检查 Console 无本次改动引入的 Error/Exception。

## 常见错误

- 把独立 Demo 代码继续放进 `Hotfix/Module`，导致资源和代码归属不一致。
- 把 Demo 专属 UI、导航或玩法塞进 Core。
- 在运行时临时拼装本应保存为 Prefab 的结构。
- 把 Demo Scene 加进 Build Settings 绕过 YooAsset。
- 直接加载场景，绕过 `GameSceneNavigator`。
- 手改 MvcBind 生成文件。
- 为单个 Demo 新建测试 asmdef。
- 只验证进入，不验证返回 Hub 后的生命周期清理。
- 接入方式变化后没有同步长期文档。
