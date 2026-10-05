# 代码与资源布局

## 一句话原则

- 代码看 `Assets/Scripts`
- 可加载资源看 `Assets/LoadResources`
- 启动入口场景看 `Assets/Scenes`
- 判断落点时，先看职责，再看复用范围

## 代码布局

### `Assets/Scripts/Core/Runtime`

放这些内容：
- 启动流程和状态机
- 资源初始化与下载
- 热更程序集装配前的基础设施
- 通用事件分发等跨 Demo 的运行时通信能力
- 通用 UI 框架、服务注册、组件基类
- 所有 Demo 都可能依赖的公共运行时能力

不要放：
- 某个具体 Demo 的玩法规则
- 某个界面的专属业务判断
- 临时测试逻辑

### `Assets/Scripts/Core/Editor`

放这些内容：
- 热更构建工具
- UIBind 相关编辑器窗口与生成辅助
- 本地资源服务器、导入器、检查器
- 将来所有“提升协作效率”的 Unity 编辑器扩展

不要放：
- 运行时逻辑
- 只为一次性临时操作存在的脚本

### `Assets/Scripts/Hotfix`

放这些内容：
- 业务入口
- 主菜单和界面逻辑
- 玩法模块
- 业务 Action / Data / Handler、View 和独立界面子组件

当前可见模块包括：
- `AppDelegate`
- `Eventing`
- `Module/Main`
- `Module/Common`
- `Module/User`、`Module/GraphicsSettings`
- `Demos/`：搬豆工、无人机、DLSS、赌场、渔力全开与 LoopScroll 示例

不要放：
- 通用资源加载框架
- 底层服务容器
- 所有业务都共用的基础设施

### `Assets/Scripts/Hotfix/Editor`

放只服务某个 Hotfix 业务模块的 Editor 扩展，例如业务配置自定义 Inspector、需要持续维护的 Demo 私有 Inspector 或导入工具。该目录使用独立 `Hotfix.Editor` 编辑器程序集，可以引用 `Hotfix`、`Core.Editor` 和 `Core.Runtime`；生产 `Hotfix` 不引用它。

通用 UIBind、构建、导入和跨业务检查工具仍放 `Core.Editor`。禁止为了实现业务 Inspector 让 `Core.Editor` 反向引用 `Hotfix`。

### `Assets/Scripts/Hotfix/Demos/<DemoName>`

放独立 Demo 的运行时代码。Demo 内可以继续按职责拆分控制、物理、输入、装备和宿主适配，但仍归属 `Hotfix.dll`；不要为了单个 Demo 新建生产程序集。

Demo 内按玩法规则、场景表现和界面职责组织代码。外部服务接入负责接口转换；规则通过业务数据流驱动场景和页面，公共能力复用 Core。

## 资源布局

可加载资源的**目录矩阵、文件名与地址规则**见 [资源命名规范](./asset-naming.md)：类型由目录与扩展名决定，文件名为纯语义 PascalCase。`DemoId` 目录名使用小写 + 下划线（如 `gravity_well`）。

### `Assets/LoadResources/Demos/<DemoId>/`

适合放：
- 该 Demo 专属预制体、材质、音效、脚本引用资产
- 该 Demo 的可加载资源
- 当前 `drone_flight` 已按此规则收口场景、三机型、装备、配置和美术资源

### `Assets/LoadResources/UI` / `Art` / `Audio` / `VFX`

适合放：
- 两个及以上 Demo 会稳定复用的资源
- Hub 和 Demo 共用的公共资源

### `Assets/LoadResources/Scenes`

适合放：
- 模板场景
- 未来需要走可加载路径的场景资源

### `Assets/Scenes`

适合放：
- 当前启动入口场景
- 启动加载对象
- 不走 `LoadResources` 组织的基础场景对象

## 决策口诀

- 这是“底座能力”吗：去 `Core`
- 这是“跨业务编辑器底座”吗：去 `Core.Editor`
- 这是“只服务某个 Hotfix 模块的 Editor 扩展”吗：去 `Hotfix/Editor`
- 这是“业务或玩法”吗：去 `Hotfix`
- 这是“独立 Demo 玩法”吗：去 `Hotfix/Demos/<DemoName>`
- 这是“某个 Demo 独有资源”吗：去 `Assets/LoadResources/Demos/<DemoId>/`
- 这是“多个 Demo 共享资源”吗：去公共资源目录

## 一次性工具的生命周期

场景、UI、Prefab 批量生成和一次性迁移可在开发期间临时执行；保存生成资产并验证后删除脚本、菜单和对应 meta，不将临时 Builder 固化为维护入口。人工打磨后的资产是当前真源，不能靠重跑旧生成器覆盖。正式平台构建、资源导入、业务 Inspector 等有持续调用方的工具继续保留；删除临时工具时同时清除其独占 helper、无效测试和当前文档入口，历史 Git 记录不必重写。
