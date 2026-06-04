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
- MvcBind 相关编辑器窗口与生成辅助
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
- 与具体业务流绑定的 View、Presenter、Module

当前可见模块包括：
- `AppDelegate`
- `Eventing`
- `Module/Main`
- `Module/Test`

不要放：
- 通用资源加载框架
- 底层服务容器
- 所有业务都共用的基础设施

## 资源布局

可加载资源的**目录矩阵、文件名与地址规则**见 [资源命名规范](./asset-naming.md)：类型由目录与扩展名决定，文件名为纯语义 PascalCase。`DemoId` 目录名使用小写 + 下划线（如 `gravity_well`）。

### `Assets/LoadResources/Demos/<DemoId>/`

适合放：
- 该 Demo 专属预制体、材质、音效、脚本引用资产
- 该 Demo 的可加载资源
- 当前目录还比较空，后续新增 Demo 建议优先收口到这里

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
- 这是“编辑器辅助”吗：去 `Core.Editor`
- 这是“业务或玩法”吗：去 `Hotfix`
- 这是“某个 Demo 独有资源”吗：去 `Assets/LoadResources/Demos/<DemoId>/`
- 这是“多个 Demo 共享资源”吗：去公共资源目录
