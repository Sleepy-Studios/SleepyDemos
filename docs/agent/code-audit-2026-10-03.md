# 全项目代码精简与三端操作审计

审计范围为本项目自有 Core.Runtime、Core.Editor、Hotfix、Demo、测试及保存资源接线。初始清点 445 个 C# 文件；全域扫描类型引用、反射、直接设备读取、一次性工具及异步/生命周期入口，再沿实际调用和保存资源确认处理项。第三方插件、导入包源码与独立 SleepyLoopScroll 仓库未重构。扫描覆盖不等于所有玩法已做实机或性能验收。

## 删除、合并与保留

| 子系统 | 结论与依据 |
|---|---|
| UI 类型发现/构造 | 修正此前无性能证据的手写清单方案：取消登记表，恢复限定程序集的一次性 View 发现与首次 Activator 构造。类名/Mvc 别名查缓存，泛型入口独立于名称索引；不扫描整个 AppDomain，不引入生成流程。 |
| Flux | 按用户最新要求保留现有 Handler、MessageHandler 反射消息路由及网络接口；Flux 源码、模块文档和两份 gen-module 模板均恢复原状。 |
| TMP 自适应 | 删除私有字段读取，明确维护设计字号。当前保存场景/Prefab 没有挂载该组件，无字段资产迁移。 |
| 启动/HybridCLR | 保留 Assembly.Load 和必要入口查找；Core 不反向引用 Hotfix，不另建启动框架。 |
| 编辑器类型分析 | MvcBind、FontEngine 仍需检查类型/编辑器 API；保留。 |
| 公共输入/UI | 复用 Input System/EventSystem；设备活动与提示设备分开，作用域顶层独占导航，业务 Selected 与交互表现分开。保存配置 99 个现有 Selectable，额外 Demo 控件直接保存在各自资源。 |
| 手柄图标 | 55 张实际使用的 Xelu CC0 图标与一个目录资产，含未知手柄的四个面键位置提示；没有引入 Resources 扫描或第三方提示框架。Switch 用布局的 Submit/Cancel 用途同步真实 A/B 绑定。 |
| Demo 设备分支 | 无人机和 DLSS 使用 InputActionSession，镜头/装备读当前帧；赌场提示使用实际动作。Hotfix 生产代码没有直接读 Keyboard.current/Gamepad.current/Mouse.current 的调用。 |
| 一次性编辑器装配 | 删除 LoopScrollExampleBuilder、GraphicsSettingsBuilder、DlssDemoBuilder、DroneFlightMechanismBuilder、DroneFishingMvpSceneBuilder 和专属 DroneFlightBuilderAssetUtility；保留已经验证的资产和实际配置 Inspector/关卡工作台。 |
| 初始化/转发 | UITab 同步/异步初始化合为一条可取消流程；删除仅包装 CancellationToken 的类和仅设置 renderer.enabled 的扩展类。 |
| 无调用原型 | 删除无入口的三数据 View 泛型；旧 F2/F3 二布尔转发结构由真实动作取代，独占的同构断言移除，真实绑定测试补齐。 |
| 指针生命周期 | PressButton 使用 int.MinValue 表示无指针，避免与合法鼠标 -1 冲突；重复进入、禁用释放覆盖永久测试，私有字段按当前规范命名。 |
| 菜单与触控生命周期 | 动态启用/禁用刷新导航，父作用域排除子面板；取消指针保持不吞后续 Submit；界面初始 Point/Enter 不伪造鼠标活动；无人机面板切换与 HUD 隐藏取消双指/长按跟踪。 |
| UIManager/View | 保留 FIFO、加载去重、取消、故障清理和事务回滚。大文件承载已有真实契约，不按行数拆分；相关行为回归保留。 |
| 资源、场景、网络 | 保留 Loader/服务边界、场景相机与声音恢复、网络会话接口；单实现不意味着可以去掉边界或测试替身。 |
| 存档/构建 | 保留版本拒绝、损坏恢复、原子保存、构建配置恢复事务；没有新增旧原型双轨兼容。 |
| DroneFlight 飞控/装备 | 保留 Rotor、PID、分配器、质量同步与两种装备的真实差异；输入收口不改变物理模型，也不为不同装备建立共同大基类。 |
| BlockPorters 规则 | 保留纯调度、解算器与集中演出；不为列数或当前布局建立新架构。旧 Hub 按钮名测试改按卡片稳定 Key 查入口，玩法断言保留。 |
| Streamline | 保留原生插件/RHI/渲染历史和平台门闩；新增触控观察不宣称移动端支持 DLSS。 |
| Luban、事件、生成代码 | 保留现有生成与事件边界，没有改业务表、协议或生成的 ViewComponent。 |
| 测试 | 删除只服务被删除装配器的生成数量/恢复断言；保留资源、输入、生命周期和物理契约。按相关类/方法串行运行，不运行全项目/第三方全量。 |

## 当前使用方式

- 页面自动发现见 [UI 运行时](../modules/ui-runtime.md) 与 [接入 View](../runbooks/create-ui-view.md)。
- 三端设备、状态与提示见 [公共输入](../modules/gameplay-input.md) 与 [接入输入](../runbooks/use-gameplay-input.md)。
- Demo 长期入口仍在 [Demo 文档目录](../demos/README.md)。日常编辑保存资产，不重跑历史装配工具。

## 验证边界

三端重构阶段通过 139 项不同测试；其中 2 项手写注册专用断言已撤销，下面保留 137 项既有行为回归记录。页面发现修正阶段的编译与相关测试结果另列，重复运行不重复计数。

| 范围 | 通过数 | 实际覆盖 |
|---|---:|---|
| UI 导航与生命周期（此前阶段） | 89 | UIManagerNavigation 51、UIViewLifecycle 20、UIWorldTransition 13、UIComponentLifecycle 4、UIViewPrefabConvention 1。原手写注册专用的 2 项断言已由下面的自动发现测试替代。 |
| 公共输入 | 20 | UnifiedInput 6、GameplayInputRouter 11、TouchInputPad 3。Switch A/B 真实用途绑定、多手柄优先与漂移、嵌套/动态菜单、触控视觉、DLSS 动作门闩及鼠标绝对位置。 |
| Demo 及保存资源 | 28 | Hub 导航 3、DroneUnifiedInput 4、DroneHudFormatter 6、DroneHudBinding 1、DroneUiContract 4、BlockPorters 设置/暂停/十二色流程 1、LoopScrollShowcase 7、DLSS 正式进入/模式/返回 1、赌场真实输入进入/暂停/返回 1。 |

### 页面发现方案修正

- 取消手写页面清单，恢复 UITypeReflection.Init/Scan/Get 和 UICache 首次 Activator 构造。每轮指定程序集只枚举一次，空索引和未知名称不再触发 AppDomain 扫描；泛型创建无需登记。
- Unity Editor 编译 0 错误（2026-10-03 12:11）；UITypeReflectionTests 4、UIManagerNavigationPlayModeTests 51、UIViewLifecyclePlayModeTests 20、UIWorldTransitionPlayModeTests 13，共 88 项通过。
- 正式启动及 Editor 直启待补验证：Test Runner 恢复阶段引用了已不存在的 InitTestScene 临时场景，产生 Invalid SceneManagerSetup；后续正式启动请求未正常执行，Editor 主线程持续未处理 REST 请求。原生 TestResults.xml 确认最后完成的是世界过渡 13/13，不能将后续未执行请求记为通过。
- 本次仅修改 Core.Runtime 页面发现、Hotfix 启动入口、相关测试及文档；Flux、三端输入、Demo 保存资源和独立列表包未改动。未执行全项目测试或 Player/IL2CPP 构建。

已查看 Editor 截图，修正 DLSS 关闭按钮与提示重叠、无人机触控按钮文字溢出和静止鼠标使摇杆隐藏的问题。无人机双摇杆截图在 `ValidationArtifacts~/refactor/DroneTouch.png`；DLSS 正式流程截图在 `Library/Streamline/evidence/demo/Direct3D12/`。场景装配临时脚本及 .meta 已全部清理，55 张图标和接线保存在正式资源。

编译和 Test Runner 验证说明 Editor 加载代码的结果。三类真实手柄、Android 触控、Player/IL2CPP 与完整项目回归未执行；模拟输入和 GPU 截图不替代实机验收。临时报告/截图放在忽略的 ValidationArtifacts~/，不提交自动生成的验证目录。

剩余审计/验收：实际设备与不同分辨率的布局、IL2CPP/热更 Player、运行时 Profiler 数据，以及本轮未深入逐文件阅读的其他实现。没有性能证据时不把大型控制器机械拆分，也不因单实现删除资源/网络/构建事务边界。独立列表包继续按自身仓库维护。
