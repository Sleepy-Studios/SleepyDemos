# Unity 自动化测试架构

## 目标

项目使用 Unity Test Runner 作为唯一自动化验证入口。测试代码集中在 `Assets/Scripts/Tests`，不在 `Core`、`Hotfix` 或 Demo 生产目录旁建立零散测试程序集。

## 程序集边界

- 当前只有 `Core.Tests` 逻辑测试域，按 Unity 执行模式分为 `Core.Tests.EditMode` 与 `Core.Tests.PlayMode` 两个物理程序集。
- 只有出现真实 Hotfix 业务测试时才创建 `Hotfix.Tests.EditMode` 或 `Hotfix.Tests.PlayMode`。
- 项目最多维护 Core 与 Hotfix 两个逻辑测试域；物理 asmdef 只允许按 EditMode/PlayMode 拆分，禁止按功能继续拆分。
- 测试程序集只能单向引用生产程序集；生产程序集不得引用测试程序集。
- Test Assembly 必须设置 `autoReferenced=false`。EditMode 限制为 Editor；PlayMode 使用标准 PlayMode Test Assembly 配置，由 Test Assembly 标记保证不进入普通 Player。

测试不能放在 `Assets/Scripts/Hotfix` 下。热更构建会扫描 Hotfix asmdef，把测试放进去会带来测试 DLL 被误收集、NUnit/TestRunner 依赖进入热更包等风险。

## 测试类型选择

- 纯规则、资源约定、编辑器工具和程序集边界使用 EditMode 测试。
- 依赖实际帧循环、Camera、Canvas 或运行时生命周期的行为需要进入 Play Mode。
- 视觉观感、不同屏幕比例、真实交互手感继续保留手动验证。

`UIRootManagerPlayModeTests` 位于 `Core.Tests.PlayMode`，由 PlayMode Test Runner 管理运行状态。测试本身不得调用 `EnterPlayMode` / `ExitPlayMode`，也不得修改 EditorSettings 或吞掉日志来模拟 PlayMode。

## 测试范围选择

自动化验证默认采用能够覆盖当前改动的最小测试集，按以下顺序选择：

1. 能精确覆盖改动时，运行单个测试方法。
2. 同一功能存在多条相关用例时，运行对应测试类。
3. 任务跨越多个模块时，运行所有直接受影响的测试类。

跨模块不等于全量回归。除非用户明确要求“全量测试”或“完整回归”，否则不得默认运行整个 `Core.Tests`、全部 EditMode、全部 PlayMode 或第三方包测试。

项目全量测试默认只包括项目自有的 `Core.Tests` 与未来的 `Hotfix.Tests`。Package、插件或 Unity 自带测试属于第三方测试，只有用户明确指定时才纳入。验收报告需要列出实际运行的方法或测试类，并明确说明是否执行过全量测试。

## 不进包保证

`TestAssemblyBoundaryTests` 自动检查：

- Player 程序集查询不包含 EditMode、NUnit 或 Editor TestRunner；PlayMode Test Assembly 可出现在查询中，但普通构建默认排除带 TestAssemblies 标记的程序集。
- `HotfixConfig` 和热更代码目录不包含 `.Tests.dll`。
- Test Assembly 只位于 `Assets/Scripts/Tests`，EditMode/PlayMode 平台配置正确且 `autoReferenced=false`。

Unity 6.3 的 Player 程序集查询可能因为 Input System 或 Performance Testing 包包含 `UnityEngine.TestRunner`。这不是项目测试程序集泄漏；验收重点是项目测试程序集、NUnit 和 Editor TestRunner 不进入交付边界。

## 修改原则

- 新增生产行为时优先先写失败测试，再实现最小代码。
- 不通过删除断言、忽略业务错误或改变测试分类来规避失败。
- 不再维护与 Unity Test Runner 并行的自定义基础设施校验菜单或 BatchMode 包装器。
- Unity Editor 已打开项目时，不另起 BatchMode 实例，也不使用 `dotnet build` 或 `msbuild` 验证 Unity 工程。
