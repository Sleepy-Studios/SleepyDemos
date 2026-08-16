# 文档维护规则

## 目标

文档不是装饰品，而是这个项目给协作者和 agent 的导航系统。
因此文档更新不是“有空再补”，而是功能改动的一部分。

## 什么时候必须更新文档

满足任意一条，就必须更新：

- 新增、删除、合并关键模块
- 目录职责发生变化
- 启动流程、热更流程、资源加载流程变化
- 新增或修改 Demo 接入流程
- 新增或修改编辑器工具入口
- 原有文档中的路径、入口、步骤已经不准确

## 三层文档体系

项目文档按读者和用途分三层，不把“设计思路”“模块维护”“接入步骤”混在一篇里。

### `docs/architecture/`

给开发人员看思路。

这一层回答：

- 为什么这样设计
- 分层边界是什么
- 哪些职责不能混在一起
- 改动某个主干能力时要遵守哪些原则
- 这个模块和其它模块如何协作

适合放：全局架构、分层规则、启动流程、资源系统设计原则、文档规范。

不适合放：具体使用步骤、完整 API 手册、一次性任务操作。

### `docs/modules/*.md`

给维护模块的人看。

这一层回答：

- 模块负责什么、不负责什么
- 代码入口在哪里
- 主链路和生命周期是什么
- 修改这里要注意哪些边界
- 应该怎样验证模块仍然可用

模块文档可以有少量代码片段辅助定位，但不要写成使用教程。详细“怎么接入、怎么操作”应放到 `docs/runbooks/`。

### `docs/runbooks/*.md`

给使用者或接入者看。

这一层回答：

- 我要完成某个任务，步骤是什么
- 前置条件是什么
- 使用哪个入口、调用哪个方法
- 常见错误怎么排查
- 完成后怎么验证

适合放：新增 Demo、构建热更、使用资源 loader、接入某个模块能力等具体流程。

### 原始 Goal 归档

较完整的 Demo 初始 Goal 如果需要保留需求来源，放在 `docs/agent/prompts/demos/<demo_id>/`。归档文件必须注明它只用于追溯，不代表当前路径、接口、进度或实现状态，并链接到当前模块文档。

原始 Goal 不属于 architecture / modules / runbooks 第四层，也不能充当实施计划或进度真源。阶段计划、复选框和已被实现替代的规格不长期归档；其中仍有效的约束应改写进正确的三层文档。

## 应该更新哪一类文档

- 全局规则变化：更新 `docs/architecture/`
- 关键模块维护边界变化：更新 `docs/modules/`
- 使用方式、接入步骤、排障步骤变化：更新 `docs/runbooks/`
- 只是局部实现细节变化：通常不需要单独写 md
- 如果规则变化影响 agent 的入口判断或协作约定：同时更新 `AGENTS.md` 和 `CLAUDE.md`
- 需要保存 Demo 初始需求来源：归档到 `docs/agent/prompts/demos/<demo_id>/`，当前事实仍写入三层文档

## 模块文档什么时候要增减

### 需要新增模块文档
- 模块本身是关键入口
- 模块包含多人会反复修改的复杂规则
- 模块边界不直观，协作者容易放错代码
- 模块影响启动、热更新、资源加载、Flux 数据流、公共 UI 等主干流程

### 大型模块文档规范

大型模块必须形成完整模块文档，至少包含：

- 模块职责：负责什么、不负责什么
- 代码位置：运行时、编辑器、配置和资源入口
- 主链路：从入口到结果的关键调用顺序
- 接入方式：新增能力时应该加在哪里
- 清理或生命周期：启动、退出、重新登录、卸载等状态如何处理
- 边界规则：不能依赖什么、不能把什么职责塞进来
- 验证重点：在 Unity 或命令行中至少验证哪些行为

当前按大型模块维护的对象包括：

- 启动系统
- 热更新模块
- Flux 单向数据流
- 资源运行时
- UI 运行时

像 `UserData` 这种当前只承担少量状态记录的小型 Data 三件套，可以先不单独建模块文档；当它发展出独立入口、复杂业务规则或多人高频修改边界时，再补 `docs/modules/*.md`。

### 需要删除或合并模块文档
- 模块已经废弃
- 模块职责被合并到别处
- 文档继续保留只会误导后续开发

## 提交前自检

完成任务前，至少检查：
- 文档中的路径和类名是否仍然存在
- 导航页是否有失效链接
- 本次改动是否触发了文档同步条件
- 如果没有改文档，是否能明确说明“为何不需要”

## C# 注释风格

注释目标是解释调用语义、维护意图和容易误用的地方，不要机械复述代码。代码里优先让命名和结构自解释，注释只补调用者容易误解的部分。

### XML 注释

只有“公开方法且带参数”时使用完整 C# XML 注释：

- `public` / `protected` 方法带普通参数、可选参数、泛型参数、返回值或异步返回值
- 参数包含 bool、string command、Type、回调、索引、路径、异步开关等容易误用的语义
- 框架扩展点，例如 Handler 基类可重写方法、网络请求封装、订阅入口、公共 UI 组件入口

完整 XML 注释必须写真实语义，包括参数默认值、是否触发回调、副作用、生命周期、异步行为等，不允许空壳模板。

```csharp
/// <summary>
/// 设置当前选中项。
/// </summary>
/// <param name="index">目标索引；非法索引会被忽略。</param>
/// <param name="notify">是否触发选中回调。</param>
public void SetIndex(int index, bool notify = true)
```

泛型方法补 `<typeparam>`；返回值语义不直观时补 `<returns>`。

### 简短 `///` 注释

公开方法没有参数时，只写简短 `///` 概括用途，不补完整 `<summary>` 块。

公开字段、公开属性、公开事件也只写简短 `///`，说明它代表什么；不要为了简单属性补完整 XML 模板。

```csharp
/// 当前选中索引；未选中时为 -1。
public int Index => currentIndex;

/// 清空当前选择。
public void ClearSelection()
```

### 私有成员

私有字段和私有方法使用普通 `//` 注释，只在命名无法表达意图、状态机分支复杂或有特殊副作用时补充。

不要给私有字段、私有方法机械补 `///` XML 注释。

### 大段说明

较长的维护说明可以按场景使用：

- `#region`：同一文件内存在多组明显职责分区，且分区能提高阅读效率时使用。
- `/* ... */`：需要保留一段多行背景说明、协议约定或算法解释时使用。

不要用大段注释替代拆分方法、提取类型或改进命名。

## C# 命名规范

### 总原则

命名优先表达业务语义和生命周期含义，避免缩写、拼音、无意义前后缀。项目不使用下划线 `_` 作为私有字段前缀。

### 类型与成员

- 类型、方法、属性、事件：PascalCase，例如 `PlayerController`、`LoadAssetAsync`、`CurrentIndex`、`OnValueChanged`。
- 接口：`I` + PascalCase，例如 `IResourceLoader`、`IUITransition`。
- 局部变量、参数、私有字段、序列化私有字段：camelCase，例如 `currentIndex`、`targetImage`、`initIndex`、`isAsync`。
- 常量：PascalCase，例如 `DefaultTimeout`、`SelectedStateId`。
- 静态只读字段：PascalCase 或按既有文件风格；不要使用 `_` 前缀。
- 枚举类型和枚举值：PascalCase，例如 `ViewState`、`FirstInit`。

### Unity 字段

Unity Inspector 暴露字段优先使用序列化私有字段：

```csharp
[SerializeField] private Image targetImage;
[SerializeField] private bool initializeOnAwake;
```

避免为了 Inspector 直接使用 public 字段。需要外部读取时提供只读属性：

```csharp
[SerializeField] private int currentIndex;

/// 当前选中索引；未选中时为 -1。
public int Index => currentIndex;
```

### Bool 命名

bool 命名要表达判断语义：

- `isAsync`
- `isExpanded`
- `hasChildren`
- `canCollapseFirstLevel`
- `shouldNotify`
- `enableAnimation`

避免含糊的 `flag`、`state`、`check`。

### 异步命名

返回 `Task` / `UniTask` / 异步语义的方法使用 `Async` 后缀：

- `LoadAssetAsync`
- `InitAsync`
- `DownloadPackageAsync`

同步方法不加 `Sync` 后缀，除非同一类型中必须同时暴露同步和异步同名能力且会产生歧义。

### 回调与事件命名

- 注册方法：`Register`
- 取消注册：`Unregister`
- 覆盖回调：`SetAction`
- 事件或回调字段按语义命名，例如 `onSelected`、`onValueChanged`、`ShowStateChanged`

`Register` 必须表示追加，`Unregister` 必须表示移除，`SetAction` 必须表示覆盖。

### Unity 生命周期方法

Unity 生命周期方法保持官方名称，不额外包装命名：

- `Awake`
- `Start`
- `OnEnable`
- `OnDisable`
- `OnDestroy`

生命周期方法一般不写 XML 注释；逻辑复杂时用 `//` 写关键维护意图。

### 禁止项

- 尽量不使用 `_camelCase` 私有字段前缀。
- 不使用拼音命名。
- 不使用无意义缩写，如 `mgr`、`btns`、`cfg`，除非是项目内已稳定约定。
- 不用注释解释糟糕命名；优先改名。
- 不给显而易见成员堆模板注释。
