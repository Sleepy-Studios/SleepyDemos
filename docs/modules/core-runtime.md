# Core.Runtime 模块说明

## 负责什么

`Core.Runtime` 是运行时底座，负责让项目在业务接管前稳定启动，并为 Hotfix 提供通用能力。

## 重点目录

- `Common/`：通用基础能力
- `Components/`：可复用基础 UI 组件和通用组件
- `Eventing/`：全局同步事件分发，用于临时事件通知
- `Flux/`：轻量单向数据流，负责 Action 派发、Data 状态、Handler 处理和订阅通知
- `Hotfix/`：热更相关运行时能力
- `Networking/`：通用会话、可靠命令、共享快照与 AOT 网络 SDK 适配边界
- `Resource/`：资源相关能力
- `Startup/`：启动状态机与系统
- `UI/`：UI 框架、管理器、反射注册等

## 入口与主链路

关键入口：
- `CoreEntrance`
- `StartupPipeline`

关键职责：
- 准备运行时环境
- 初始化资源系统
- 通过 `ResourceServices` 注册和访问当前资源实现
- 装配热更程序集
- 初始化 UI 框架
- 将控制权移交给 Hotfix

## 改这里时注意什么

- 新能力只有在多个业务都能复用时才沉淀进来
- 不要直接写入具体页面或具体玩法规则
- 改启动顺序时，必须联动检查 `docs/architecture/startup-flow.md`

## 公共时间与颜色

`Common/TimeUtil` 统一 UTC 墙钟、秒/毫秒时间戳、日期显示、ISO 8601 存档时间、时长与每日刷新计算。默认使用系统时间，`ITimeSource` 可由服务器接入或测试替换；测试结束调用 `SetTimeSource(null)` 恢复。日期默认使用设备时区，刷新小时由调用方显式指定。玩法步进、物理与暂停时钟仍由各自宿主管理。

`Common/ColorUtil` 提供十六进制转换、透明度替换与 TMP 富文本标签。公共语义色保存在 `Colors`，Demo 专属色板继续归 Demo，计算颜色不经过十六进制量化。`Components/UICountdown` 绑定 TMP 文本，按绝对结束时间刷新，禁用或销毁取消，完成事件允许重入启动下一轮。

规则回归由 `Tests.Module.CommonUtilityTests` 和 `Tests.Module.CommonTipsPlayModeTests` 覆盖。接入示例见[使用 Core 基础 UI 组件](../runbooks/use-core-ui-components.md)。

## 常见任务

- 补一个新的启动系统
- 调整资源初始化顺序
- 扩展公共 UI 基类或管理器
- 增加通用运行时服务
- 补充或接入 Flux 状态流
- 通过 `Tests.Module` 下的相关测试验证资源抽象、公共 UI、热更程序集和测试程序集边界

## 相关模块文档

- [Core 资源运行时](./resource-runtime.md)
- [Core UI 运行时](./ui-runtime.md)
- [Core 事件系统](./eventing/README.md)
- [Core Flux 状态流](./flux.md)
- [热更新模块](./hotfix.md)
- [通用网络会话边界](./network-session.md)
- [Unity 自动化测试架构](../architecture/testing.md)
- [运行 Unity 自动化测试](../runbooks/run-unity-tests.md)
