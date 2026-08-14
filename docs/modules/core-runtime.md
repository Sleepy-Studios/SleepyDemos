# Core.Runtime 模块说明

## 负责什么

`Core.Runtime` 是运行时底座，负责让项目在业务接管前稳定启动，并为 Hotfix 提供通用能力。

## 重点目录

- `Common/`：通用基础能力
- `Components/`：可复用基础 UI 组件和通用组件
- `Eventing/`：全局同步事件分发，用于临时事件通知
- `Flux/`：轻量单向数据流，负责 Action 派发、Data 状态、Handler 处理和订阅通知
- `Hotfix/`：热更相关运行时能力
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
- [Unity 自动化测试架构](../architecture/testing.md)
- [运行 Unity 自动化测试](../runbooks/run-unity-tests.md)
