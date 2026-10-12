# 运行 Unity 自动化测试

## 适用场景

需要运行已选择的目标测试时使用本流程；永久保留标准、程序集边界与最小范围选择统一见 [测试架构](../architecture/testing.md)。纯文档改动无需 Unity 验证。

## 确认 Unity 实例

自动化优先使用当前会话已安装的 `unity-skills` 技能。技能位置由会话技能目录解析；常见用户级入口为 `~/.agents/skills/unity-skills/SKILL.md`，不要假设仓库内存在同名项目技能，也不要硬编码某台机器的用户绝对路径。

1. 从 `~/.unity_skills/registry.json` 按项目绝对路径读取端口。
2. registry 缺失或不可信时，扫描 `http://localhost:8090-8100/health`。
3. 核对 `/health` 的 `projectName=SleepyDemos`、Unity 版本和 `instanceId`。
4. Domain Reload 后端口可能变化，连接失败时必须重新读取 registry，不要复用旧端口。

不要在 Unity 已打开当前项目时另起 BatchMode Unity，也不要运行 `dotnet build` 或 `msbuild`。

## 运行测试

按测试架构选好范围后，可在 `Window > General > Test Runner` 选择方法或类，也可通过 UnitySkills 执行。

通过 UnitySkills 自动运行时：

1. 已知完整方法名或类名时，直接调用 `test_run_by_name`，不要先扫描无关 TestMode。
2. 不知道准确名称时，只对当前需要的 TestMode 调用 `test_list`；首次返回 discovery job 时轮询 `test_discover_get_result`。
3. EditMode 方法或类传 `testMode="EditMode"`，UI Root 等 PlayMode 测试传 `testMode="PlayMode"`。
4. 一次只启动一个目标测试任务，使用返回的 job ID 轮询 `test_get_result`，完成后才能启动下一个目标，禁止并行运行 TestRunner。
5. 如果任务长时间没有进度，停止无边界轮询，检查 job 状态、Console 和 Test Runner；不得通过重复启动同一测试来掩盖卡死。
6. 汇总实际运行目标的 total、passed、failed，并查询 Console，确认没有新增 Error 或 Exception。

误启动超出任务范围的测试时，立即调用 `test_cancel`，并向用户说明实际启动范围和取消结果，不继续等待整套测试完成。

`UIRootManagerPlayModeTests` 是标准 PlayMode 测试，不在测试代码中手工切换 Editor 状态。

## 范围示例

- 只修改资源地址规范：运行 `Tests.Module.ResourceServiceTests`。
- 只修改 UI Root 创建或层级 Canvas：运行 `Tests.Module.UIRootManagerPlayModeTests`。
- 修改 UI Prefab 根节点约定：运行 `Tests.Module.UIViewPrefabConventionTests`；若同时修改 UI Root，再追加对应 PlayMode 测试类。
- 修改热更 asmdef 过滤与交付边界：运行 `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests`。
- 用户明确要求全量测试：运行全部项目自有 EditMode 与 PlayMode 测试，并在报告中标记为全量；不要自动包含第三方包测试。

大型 UI 导航与过渡改动按以下顺序串行运行直接受影响类，不并行启动 Test Runner job：

1. `Tests.Module.UINavigationContractsTests`（EditMode）
2. `Tests.Module.UIStackTests`（EditMode）
3. `Tests.Module.UIBindTransitionGenerationTests`（EditMode）
4. `Tests.Module.UIViewLifecyclePlayModeTests`（PlayMode）
5. `Tests.Module.UIManagerNavigationPlayModeTests`（PlayMode）
6. `Tests.Module.UITransitionPlayModeTests`（PlayMode）
7. `Tests.Module.UIWorldTransitionPlayModeTests`（PlayMode）
8. `Tests.Module.UIRootManagerPlayModeTests`（PlayMode）
9. `Tests.Module.UIViewPrefabConventionTests`（EditMode）

这 9 类属于 UI 模块受影响回归，不代表项目全量测试。

## Hot Reload 与测试证据

遵守 [根 Hot Reload 规则](../../AGENTS.md#hot-reload)。测试只证明当次加载代码的行为；没有最新修改已应用或正式编译证据时，报告这一限制，不据测试通过推断代码已生效。

## 结果与排障

- 原生结果默认写入 `%USERPROFILE%/AppData/LocalLow/SleepyStudio/SleepyDemos/TestResults.xml`。
- Domain Reload 后 UnitySkills job 丢失时，以原生 XML、Console 和 Test Runner 窗口为证据，并重新读取 registry 确认端口。
- 编译失败时先查看完整 `CSxxxx`、文件和行号；修复后通过 Unity AssetDatabase Refresh 正式编译。
- PlayMode 测试若因第三方 Editor 扩展报错，先关闭对应扩展窗口或排查扩展，不得在测试中全局忽略错误日志。

## 验收重点

- `HotfixAssemblyDefinitionFilterTests`：热更 asmdef 过滤规则。
- `UIViewPrefabConventionTests`：View Prefab 根节点 Canvas 三件套规则。
- `ResourceServiceTests`：默认资源服务、Loader 和地址规范化。
- `TestAssemblyBoundaryTests`：Player、热更配置、热更目录和 asmdef 边界。
- `UIRootManagerPlayModeTests`：UI Root、固定层 Canvas、Mask 和重复初始化。

结果报告要求见 [测试架构](../architecture/testing.md#测试范围选择)。
