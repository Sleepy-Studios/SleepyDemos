# 运行 Unity 自动化测试

## 适用场景

修改 Core.Runtime、Core.Editor、公共 UI、资源服务或热更收集规则后，使用本流程运行当前任务直接相关的最小测试集。不要把“改了 Core”自动等同于运行整个 `Core.Tests`。

只有用户明确要求“全量测试”或“完整回归”时，才运行全部项目测试。全量默认仅包含项目自有的 `Core.Tests` 与未来的 `Hotfix.Tests`；Package、插件或 Unity 自带测试需要用户另外明确指定。

## 确认 Unity 实例

自动化优先使用当前会话已安装的 `unity-skills` 技能。技能位置由会话技能目录解析；常见用户级入口为 `~/.agents/skills/unity-skills/SKILL.md`，不要假设仓库内存在同名项目技能，也不要硬编码某台机器的用户绝对路径。

1. 从 `~/.unity_skills/registry.json` 按项目绝对路径读取端口。
2. registry 缺失或不可信时，扫描 `http://localhost:8090-8100/health`。
3. 核对 `/health` 的 `projectName=SleepyDemos`、Unity 版本和 `instanceId`。
4. Domain Reload 后端口可能变化，连接失败时必须重新读取 registry，不要复用旧端口。

不要在 Unity 已打开当前项目时另起 BatchMode Unity，也不要运行 `dotnet build` 或 `msbuild`。

## 运行测试

先按以下顺序确定范围：

1. 优先运行能精确覆盖改动的测试方法。
2. 同一功能涉及多条用例时，运行对应测试类。
3. 跨模块任务运行所有直接受影响的测试类，但不自动扩大到整个程序集或全部 TestMode。

可在 `Window > General > Test Runner` 中选择具体方法或测试类。只有用户明确要求全量测试时，才分别运行 `Core.Tests.EditMode` 和 `Core.Tests.PlayMode` 的全部项目测试。

通过 UnitySkills 自动运行时：

1. 已知完整方法名或类名时，直接调用 `test_run_by_name`，不要先扫描无关 TestMode。
2. 不知道准确名称时，只对当前需要的 TestMode 调用 `test_list`；首次返回 discovery job 时轮询 `test_discover_get_result`。
3. EditMode 方法或类传 `testMode="EditMode"`，UI Root 等 PlayMode 测试传 `testMode="PlayMode"`。
4. 一次只启动一个目标测试任务，使用返回的 job ID 轮询 `test_get_result`，完成后才能启动下一个目标，禁止并行运行 TestRunner。
5. 如果任务长时间没有进度，停止无边界轮询，检查 job 状态、Console 和 Test Runner；不得通过重复启动同一测试来掩盖卡死。
6. 汇总实际运行目标的 total、passed、failed，并查询 Console，确认没有新增 Error 或 Exception。

`UIRootManagerPlayModeTests` 是标准 PlayMode 测试，不在测试代码中手工切换 Editor 状态。

## 范围示例

- 只修改资源地址规范：运行 `Core.Tests.Resource.ResourceServiceTests`。
- 只修改 UI Root 创建或层级 Canvas：运行 `Core.Tests.UI.UIRootManagerPlayModeTests`。
- 修改 UI Prefab 根节点约定：运行 `Core.Tests.UI.UIViewPrefabConventionTests`；若同时修改 UI Root，再追加对应 PlayMode 测试类。
- 修改热更 asmdef 过滤与交付边界：运行 `Core.Tests.HotUpdate.HotUpdateAssemblyDefinitionFilterTests` 和 `Core.Tests.Assemblies.TestAssemblyBoundaryTests`。
- 用户明确要求全量测试：运行全部项目自有 EditMode 与 PlayMode 测试，并在报告中标记为全量；不要自动包含第三方包测试。

大型 UI 导航与过渡改动按以下顺序串行运行直接受影响类，不并行启动 Test Runner job：

1. `Core.Tests.UI.UINavigationContractsTests`（EditMode）
2. `Core.Tests.UI.UIStackTests`（EditMode）
3. `Core.Tests.UI.MvcBindTransitionGenerationTests`（EditMode）
4. `Core.Tests.UI.UIViewLifecyclePlayModeTests`（PlayMode）
5. `Core.Tests.UI.UIManagerNavigationPlayModeTests`（PlayMode）
6. `Core.Tests.UI.UITransitionPlayModeTests`（PlayMode）
7. `Core.Tests.UI.UIWorldTransitionPlayModeTests`（PlayMode）
8. `Core.Tests.UI.UIRootManagerPlayModeTests`（PlayMode）
9. `Core.Tests.UI.UIViewPrefabConventionTests`（EditMode）

这 9 类属于 UI 模块受影响回归，不代表项目全量测试。

## 结果与排障

- 原生结果默认写入 `%USERPROFILE%/AppData/LocalLow/SleepyStudio/SleepyDemos/TestResults.xml`。
- Domain Reload 后 UnitySkills job 丢失时，以原生 XML、Console 和 Test Runner 窗口为证据，并重新读取 registry 确认端口。
- 编译失败时先查看完整 `CSxxxx`、文件和行号；修复后通过 Unity AssetDatabase Refresh 正式编译。
- PlayMode 测试若因第三方 Editor 扩展报错，先关闭对应扩展窗口或排查扩展，不得在测试中全局忽略错误日志。

## 验收重点

- `HotUpdateAssemblyDefinitionFilterTests`：热更 asmdef 过滤规则。
- `UIViewPrefabConventionTests`：View Prefab 根节点 Canvas 三件套规则。
- `ResourceServiceTests`：默认资源服务、Loader 和地址规范化。
- `TestAssemblyBoundaryTests`：Player、热更配置、热更目录和 asmdef 边界。
- `UIRootManagerPlayModeTests`：UI Root、固定层 Canvas、Mask 和重复初始化。

完成报告必须列出本次实际运行的方法或测试类，并明确写出“已执行全量测试”或“未执行全量测试”。
