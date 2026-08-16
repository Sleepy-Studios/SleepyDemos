# Core.Editor 模块说明

## 负责什么

`Core.Editor` 负责 Unity 编辑器内的工程化工具，目标是减少手工重复操作并提升协作稳定性。

## 工具入口总览

所有自研菜单、插件入口、UPM 工具包与 Agent Skill 的索引见 [项目工具与 Agent 技能总览](../runbooks/project-tools.md)。

## 当前可见子域

- `Hotfix/`
  - 热更构建窗口
  - 本地 Bundle HTTP 服务
- `MvcBind/`
  - 绑定数据、窗口、树视图、层级面板辅助
  - View 配置显式选择 `ViewMode` 和 `IUITransition` 实现，并按需填写 `World Transition Key`
  - 生成的 partial View 通过 `CreateUITransition()` 工厂创建 UI Transition；不再生成每次读取都会创建实例的动画表达式属性，也不直接实例化世界过渡
  - UI Transition 类型必须来自 Unity Player 脚本程序集，是顶级 `public` 非泛型 class，并提供 `public` 无参构造；Editor/Test-only 或不可直接构造的类型不会出现在列表中，手工写入非法类型时生成器会直接报错
  - 默认生成目录为 `Assets/Scripts/Hotfix/Module/{Module}/{Prefab名}/View`；勾选“自定义 Module 输出目录”后，通过文件夹选择器指定当前 Module 的目录，生成器只追加 `{Prefab名}/View`，不会重复追加 Module
  - 自定义目录只接受当前项目 `Assets` 内目录，路径只读且不持久化到 EditorPrefs；未勾选时保持原有目录规则
  - 窗口下方的绑定索引以 `Assets/LoadResources` 中根节点带 `ComponentItemIndex` 的 Prefab 为数据源，并在 `Assets/Scripts/Hotfix` 全域匹配手写 View 与生成 Component，因此支持 Demo 自定义源码目录
  - 索引在窗口打开、生成成功后自动刷新，也可点击“刷新绑定索引”；不会监听全局 `projectChanged`，避免无关资源导入反复扫描
  - 缺少脚本、`Source` 地址不一致或绑定数组损坏的 Prefab 统一进入 `[异常绑定]`，不会与正常 Module 混排
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
- MvcBind 索引必须保持 Prefab-first：不要重新引入只有脚本、没有绑定 Prefab 的索引项，也不要在每个 Prefab 匹配时重复扫描脚本目录
- 新工具如果会改变团队操作流程，要同步补 `runbooks/`
- 如果工具只是临时一次性使用，不要直接落进正式模块

## 常见任务

- 增加 Demo 创建向导
- 增加 Catalog 导入器
- 增加 Build Settings 同步工具
- 调整热更打包与本地调试流程
