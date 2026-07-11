# Core.Editor 模块说明

## 负责什么

`Core.Editor` 负责 Unity 编辑器内的工程化工具，目标是减少手工重复操作并提升协作稳定性。

## 工具入口总览

所有自研菜单、插件入口、UPM 工具包与 Agent Skill 的索引见 [项目工具与 Agent 技能总览](../runbooks/project-tools.md)。

## 当前可见子域

- `HotUpdate/`
  - 热更构建窗口
  - 本地 Bundle HTTP 服务
- `MvcBind/`
  - 绑定数据、窗口、树视图、层级面板辅助
  - View 配置显式选择 `ViewMode` 和 `IUITransition` 实现，并按需填写 `World Transition Key`
  - 生成的 partial View 通过 `CreateUITransition()` 工厂创建 UI Transition；不再生成每次读取都会创建实例的动画表达式属性，也不直接实例化世界过渡
  - UI Transition 类型必须是 Player 可用的顶级 `public` 非泛型 class，并提供 `public` 无参构造；Editor/Test-only 或不可直接构造的类型不会出现在列表中，手工写入非法类型时生成器会直接报错
- `AssetNaming/`
  - LoadResources 命名校验与 YooAsset Collector 同步
- `HotReload/`
  - Hot Reload 相关编辑器支持

## 适合放什么

- 构建菜单和构建窗口
- 资源同步工具
- 校验工具
- 导入导出工具
- 自动化辅助流程

## 改这里时注意什么

- 编辑器工具应尽量减少对业务层的硬编码依赖
- 新工具如果会改变团队操作流程，要同步补 `runbooks/`
- 如果工具只是临时一次性使用，不要直接落进正式模块

## 常见任务

- 增加 Demo 创建向导
- 增加 Catalog 导入器
- 增加 Build Settings 同步工具
- 调整热更打包与本地调试流程
