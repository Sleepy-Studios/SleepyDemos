# 无人机飞行仿真实施计划

> 本计划用于执行 [`无人机飞行仿真 Codex Goal`](../specs/2026-08-13-drone-flight-simulation-goal.md)。复选框是进度真源；未取得对应验证证据时不得勾选阶段门禁。

**目标：** 在 SleepyDemos 中建立具有四旋翼独立推力、级联 PID、大疆式位置保持、沉浸式遥控器接管、多视角和物理抓钩的 `drone_flight` Demo，并为无人机钓鱼保留可扩展挂载边界。

**首个里程碑：** 使用基础几何体完成可测量、可调试、可稳定悬停并能抵抗外力和中心载荷变化的裸机 PID MVP。

**技术栈：** Unity 6000.3.15f1、URP 17.5、Unity Physics/Rigidbody、Input System 1.19、uGUI/TMP、UniTask、YooAsset、NUnit、Unity Test Runner、Hotfix。

**版本控制约束：** 本计划不授权 commit、push、stage、改写历史或创建 PR。工作树中与无人机无关的改动必须保留并排除在任务范围外。

---

## 一、已确认的产品与技术决策

### 1. 产品定位

- 默认体验是大疆式消费级航拍无人机，不是穿越机。
- 飞行必须由四个实际旋翼位置的力和反扭矩驱动。
- Position 模式是玩家默认模式；Rate/Attitude 用于调试和高级控制。
- 抓钩和载荷是首个完整玩法闭环；无人机钓鱼在挂载系统稳定后扩展。
- 遥控器 RT 转场和多视角属于正式体验，但不得阻塞裸机物理验证。

### 2. MVP 美术策略

- 前三阶段只使用基础几何体。
- 正式模型不是物理依赖，施力点、重心和相机使用独立 Transform。
- 一体网格可作为固定机身；活动部件后补独立网格。
- 正式 Blender 建模必须晚于裸机 PID 门禁，并先取得用户视觉批准。

### 3. 建议坐标约定

最终以代码审计和测试为准，若无冲突采用：

- Unity 世界：`+Y` 向上。
- 机体局部：`+Z` 向前、`+X` 向右、`+Y` 向上。
- X 型电机位置：FrontLeft `(-X,+Z)`、FrontRight `(+X,+Z)`、RearLeft `(-X,-Z)`、RearRight `(+X,-Z)`。
- FrontLeft/RearRight 为 CCW，FrontRight/RearLeft 为 CW；正 Yaw 提升 CCW 组。该约定已由混控测试锁定。
- 玩家水平输入按当前机头 Yaw 转换到世界水平面，云台方向不改变飞行控制参考系。

不得在多个文件分别硬编码上述约定。应建立一个电机描述结构和统一配置资产。

## 二、目标目录与职责

具体文件名允许在审计后小幅调整，但职责和边界不得改变。

```text
Assets/Scripts/Hotfix/Module/DroneFlight/
├── Control/          纯数学 PID、姿态误差、级联控制与模式参数
├── Physics/          电机、混控、旋翼施力、机体状态与载荷反馈
├── Input/            统一输入帧、键鼠、手柄和未来移动输入源
├── Camera/           云台、FPV、第三人称、机身固定机位
├── Payload/          挂载点、抓钩、Joint、释放与载荷状态
├── Telemetry/        采样、调试快照、曲线和可视化
├── Runtime/          Demo 生命周期、状态机和场景装配
└── UI/               HUD、遥控器接管和调参界面业务

Assets/LoadResources/Demos/drone_flight/
├── Scenes/
├── Prefabs/
├── Data/
├── Art/
└── VFX/

Assets/Scripts/Tests/Hotfix/
├── EditMode/         纯数学、混控、电机和配置测试
└── PlayMode/         Rigidbody 闭环、载荷和相机生命周期测试（需要时创建）
```

不得在 `Core.Runtime` 中新增无人机业务类型。不得直接修改生成的 MvcBind Component 文件来加入业务逻辑。

## 三、统一验证协议

### Unity 实例

每次首次调用 UnitySkills 前：

- [ ] 读取 `%USERPROFILE%/.unity_skills/registry.json`。
- [ ] 用绝对项目路径 `D:/Unity/Unity_Project/SleepyDemos` 匹配实例。
- [ ] registry 缺失或不可信时扫描 `http://localhost:8090-8100/health`。
- [ ] 核对 `projectName=SleepyDemos`、`unityVersion=6000.3.15f1`、`instanceId`。
- [ ] Unity 已打开时不启动 BatchMode Unity。

### 测试范围

- 纯算法优先精确 EditMode 测试方法或类。
- Rigidbody 闭环只运行 DroneFlight 直接相关的 PlayMode 类。
- 不自动运行全部 Hotfix.Tests、Core.Tests、全部 EditMode/PlayMode 或第三方包测试。
- 不使用 `dotnet build`、`msbuild` 或 Unity 自动生成解决方案作为编译证据。
- 每个阶段记录精确测试名、通过/失败/跳过数量和是否执行全量测试。

### Hot Reload

Play Mode 修改 C# 后：

- [ ] 按 `.codex/skills/hotreload-log/SKILL.md` 检查 `patches.json` 的 `modifiedMethods`、`failures`、`newFields`、`deletedFields`。
- [ ] 关键方法已应用时直接继续验证。
- [ ] 新字段、类型、序列化布局或 unsupported change 未应用时，退出 Play Mode 让 Unity 正式编译。
- [ ] 不以“Console 没报错”代替 Hot Reload 应用证据。

### 每阶段通用收口

- [ ] Unity 编译错误为 0。
- [ ] Console 新增 Error/Exception 为 0。
- [ ] 规定的精确测试全部通过。
- [ ] 完成该阶段列出的 Game View 手动验证。
- [ ] 对本任务文件运行 `git diff --check`。
- [ ] `git status --short` 中无关用户改动保持原样。
- [ ] 更新本计划复选框和模块文档中的当前能力/限制。

## 四、阶段 0：基线审计与设计冻结

### 任务 0.1：审计真实入口和依赖

- [x] 检查 `MainMenuView` 当前可扩展按钮和导航 API，不假设按钮或事件名称。
- [x] 检查 Hotfix.asmdef 对 Input System、uGUI、TMP、Unity Physics 的实际引用；只补真实需要的引用。
- [x] 检查 `Assets/LoadResources` 地址、Label 和场景加载现有模式。
- [x] 检查 Hotfix.Tests 当前 EditMode asmdef；如需要 PlayMode，只按项目测试规则新增一个 `Hotfix.Tests.PlayMode`。
- [x] 记录当前 ProjectSettings 中 fixed timestep、solver iteration、重力和 Physics 设置，但不在未测量前全局修改。
- [x] 检查工作树并列出需保护的无关文件。

审计结论记录在 `docs/modules/drone-flight.md`。其中两个实施修正为：Demo 资源目录不创建未登记的 `UI/`，私有 UI Prefab 放 `Prefabs/UI/`；当前仓库没有正式 Demo 场景加载入口，裸机阶段先通过 Editor 直接打开场景验证，Hub 接入时再查询并使用 YooAsset 的真实 Scene API。

### 任务 0.2：建立长期文档

- [x] 创建 `docs/modules/drone-flight.md`，记录模块职责、控制链路、坐标约定、配置、生命周期、挂载边界和验证重点。
- [x] 创建 `docs/runbooks/tune-drone-flight.md`，首版先写调参顺序和遥测字段，后续阶段持续补充。
- [x] 更新 `docs/README.md` 导航。
- [x] 明确本 Demo 不改变 Core/Hotfix 边界和既有新增 Demo 流程，因此默认不改 `AGENTS.md`/`CLAUDE.md`。

### 阶段 0 门禁

- [x] 所有目标路径来自实际仓库审计。
- [x] 坐标、电机编号、旋向和单位在模块文档中只有一个真源。
- [x] 没有修改无关资源、启动链路或全局 Physics 设置。

## 五、阶段 1：模块骨架、基础几何体与纯数学核心

### 任务 1.1：建立 Demo 骨架

- [x] 创建 `drone_flight` 规范资源目录和必要 `.meta`。
- [x] 创建最小场景：平坦地面、刻度参考、出生点、简单障碍和基础光照。
- [x] 用基本几何体创建无人机 Prefab，清楚标出机体、四电机、前向、旋向、重心、相机和挂载点。
- [x] 使用组合 Primitive Collider，不用渲染网格 MeshCollider 充当动态机体碰撞。
- [x] 创建可由 Editor 直接打开的 DroneFlight 场景入口，未强行接主菜单正式按钮。

### 任务 1.2：输入和状态数据契约

建议创建并测试：

- `DroneControlInput`：Throttle/Lift、Yaw、Pitch/Forward、Roll/Right 的标准化帧。
- `DroneFlightState`：位置、速度、姿态、机体角速度、高度、是否接地。
- `DroneControlSetpoint`：目标位置、速度、姿态、角速度和模式。
- `DroneMotorOutput`：四电机归一化输出、推力和饱和状态。
- `DroneFlightMode`：Rate、Attitude、Position。
- `DroneResponseProfile`：Cine、Normal、Sport。

- [x] 输入值统一限幅到文档规定范围。
- [x] 输入契约不依赖具体 Keyboard/Gamepad/Touch 类型。
- [x] 非法输入由数据边界收口并记录状态，不让 NaN 进入物理层；场景配置中文诊断将在物理桥装配时补齐。

### 任务 1.3：PID 控制器

纯 C# 控制器至少支持：

- P/I/D 增益。
- 输出上下限。
- 积分上下限和抗饱和策略。
- D 项低通或等价的噪声抑制。
- 显式 `Reset()`。
- 非法 `deltaTime`、NaN/Infinity 输入保护和诊断。
- 遥测快照：Error、P、I、D、RawOutput、ClampedOutput、IsSaturated。

测试至少覆盖：

- [x] 仅 P 项符号和比例正确。
- [x] 积分累积和积分限幅正确。
- [x] 输出饱和时积分不会持续 wind-up。
- [x] Reset 清空内部历史。
- [x] D 项面对阶跃和稳定输入行为可解释。
- [x] 非法输入不会把 NaN/Infinity 扩散到后续帧。

### 任务 1.4：电机模型与四旋翼混控器

电机模型：

- [x] 一阶响应，时间常数可配置。
- [x] 输出、转速、推力和反扭矩均有限。
- [x] 推力采用 `T = k * rpm²`，反扭矩采用 `Q = T * reactionTorqueCoefficient`，单位已记录在模块文档。

混控器：

- [x] 输入 Collective/Roll/Pitch/Yaw，输出四电机命令。
- [x] 用测试锁定每个控制轴对四电机增减的符号。
- [x] 先平移 Collective，姿态范围超过 1 时再等比缩放，不使用逐电机静默 Clamp 破坏比例。
- [x] 输出饱和状态进入结果数据。

### 阶段 1 门禁

- [x] 所有当前阶段纯算法 EditMode 测试通过：4 类、17/17；未运行全量测试。
- [x] 基础无人机 Prefab 的四个 Rotor Transform 与配置一一对应，并由 4 个 Prefab 契约测试锁定。
- [x] Scene/Prefab 已由 Unity 正常保存、重新打开并复查层级。
- [x] 阶段 1 报告未提前声称无人机已经“飞稳”。

## 六、阶段 2：独立旋翼物理与 Rate/Attitude 级联 PID

### 任务 2.1：Rigidbody 机体与旋翼施力

- [x] 明确 Rigidbody mass、centerOfMass、inertiaTensor 或自动惯性策略；当前采用配置质量与组合 Collider 自动重心/惯性。
- [x] 四个旋翼分别在 Rotor Transform 使用 `AddForceAtPosition`。
- [x] 每个旋翼按 CW/CCW 施加反扭矩。
- [x] 视觉桨转速只读取电机状态，不反向驱动物理。
- [x] 所有执行使用 fixedDeltaTime；Update 只采集输入。
- [x] 增加 Armed/Disarmed 状态，定向 PlayMode 测试确认未解锁时不产生旋翼推力。

### 任务 2.2：角速度环

- [x] 从 Rigidbody 世界角速度转换到机体局部角速度。
- [x] Pitch/Roll/Yaw Rate 各自使用可配置 PID。
- [x] Rate PID 输出交给混控器，不直接旋转 Rigidbody。
- [x] 达到输出限制时把饱和反馈给积分抗饱和逻辑；当前策略为撤销本次四个 Rate/Vertical PID 的积分推进。
- [x] 提供 Rate setpoint、actual、error 和逐轴 P/I/D 输出遥测。

### 任务 2.3：姿态环

- [x] 用 Quaternion/最短旋转误差生成目标角速度，并有跨 ±180° 测试。
- [x] Attitude 控制松杆回到水平，并允许 Yaw 目标累积。
- [x] 限制最大倾角、目标角速度和水平目标加速度。
- [x] 持续危险倾角超过 0.5 s 时进入 Fault、停桨并复位 PID；不在碰撞时偷偷瞬移。

### 任务 2.4：调试与可视化

- [ ] Scene 视图当前显示四个推力向量；合力、目标姿态、重心和速度仍待补齐。
- [x] 运行时独立调试面板显示四电机、姿态缩放、总推力、fixedDeltaTime，以及逐轴 Rate 目标/实际/误差和 P/I/D。
- [ ] 支持显式恢复默认参数；运行时拖动参数不自动污染配置资产。
- [x] `DroneTelemetryRecorder` 记录最近 500 个 FixedUpdate 样本，按 F4 复制定长摘要，便于比较调参前后。

### 阶段 2 门禁

- [ ] 解锁后能通过 Collective 起飞，飞行完全来自旋翼施力。
- [ ] Attitude 模式松杆能回正；偏航、俯仰、横滚方向与输入一致。
- [ ] 施加小型角度扰动后无持续发散或高频震荡。
- [ ] 定向 PlayMode 测试确认 Rigidbody、Motor、Mixer 无 NaN/Infinity。
- [ ] 完成一次实际 Game View 手飞，记录尚未实现的高度/位置保持限制。

## 七、阶段 3：裸机 PID MVP 与量化验收

### 任务 3.1：高度控制

- [x] 垂直速度环控制总推力，高度环生成目标垂直速度。
- [x] 依据当前质量和重力计算悬停前馈，不把某个模型质量写死在 PID 中，并按推力平方模型补偿倾角。
- [ ] 地面待机/起飞时重置或冻结不合适的积分项。
- [ ] 推力重量比不足时显示超载/推力不足诊断。

### 任务 3.2：自动测试工况

建立只服务测试的确定性工况驱动，不在生产场景放隐藏作弊逻辑：

- [x] LevelHover 稳定悬停工况已程序化建立；自动起飞状态机留在阶段 4。
- [x] LateralImpulse：稳定后施加 `+X 2 N·s` 水平冲量。
- [x] CenterPayloadAttach：增加相当于机体质量 20% 的中心载荷。
- [x] CenterPayloadRelease：稳定后恢复原质量。
- [x] 工况检查高度、姿态、速度、电机输出范围和有限数值。

### 任务 3.3：量化阈值

- [x] 水平静止自动起飞到 1.5 m，稳定段连续 10 s 不触地、不翻转、无 NaN/Infinity。
- [x] 稳定段 500 个样本中至少 95% 同时满足高度误差绝对值 ≤ 0.20 m、倾角 ≤ 3°。
- [x] 水平冲量后 6 s 内恢复到目标高度 ±0.25 m、倾角 ±5°，并回到冲量点 0.75 m 内。
- [x] 中心载荷增加后允许短暂下沉，8 s 内恢复目标高度 ±0.30 m。
- [x] 释放载荷后允许短暂上窜但能恢复，控制量保持有限。

“通常”必须在测试实现中落为明确统计口径，例如稳定段 95% 样本或 RMS；选定后写进模块文档并保持一致。阈值如需修改，必须保留实测曲线和修改理由。

### 裸机 MVP 门禁

- [x] 阶段 1 至 3 的精确 EditMode/PlayMode 测试全部通过。
- [x] Unity 编译 0 错误，Console 0 新增 Error/Exception。
- [ ] 实际 Game View 中完成起飞、悬停、四轴操控、扰动恢复和载荷测试。
- [ ] 输出一份前后参数、测量结果和已知限制摘要。
- [ ] 在此门禁通过前没有开始正式无人机或遥控器建模。

## 八、阶段 4：大疆式位置保持与飞行模式

### 任务 4.1：水平速度和位置级联

- [x] Position 模式由位置误差生成目标水平速度。
- [x] 速度误差生成目标水平加速度/倾角。
- [x] 目标倾角进入既有 Attitude → Rate → Mixer 链路。
- [x] 输入松开时锁定当前位置并平滑制动，不瞬间清零 Rigidbody 速度。
- [x] 目标速度按当前机头 Yaw 转换，忽略机体 Pitch/Roll 对玩家平面方向的污染。
- [ ] 限制最大速度、加速度、倾角和 jerk/输入变化率。

### 任务 4.2：Cine、Normal、Sport

- [x] 三种 Profile 共享一套控制器，只改变速度、加速度、倾角、垂直/偏航速度和输入响应；云台响应档位仍待接入。
- [x] Cine：低速、低倾角、柔和制动，适合拍摄和投饵。
- [x] Normal：默认平衡参数。
- [x] Sport：更高速度和倾角，仍保留 Position 模式基本稳定能力。
- [x] Profile 切换保留位置、高度和 Yaw setpoint，不产生目标突跳。

### 任务 4.3：自动起飞、降落与复位

- [x] 自动起飞使用状态机逐步解锁、升高并进入悬停。
- [x] 自动降落限制下降速度，可靠判断接地后停桨。
- [x] 翻覆/坠毁进入 Fault 并停桨，不在碰撞时偷偷瞬移；显式复位入口仍待玩家体验细化。
- [ ] 暂不实现完整 GPS 返航；可保留 HomePoint 数据与接口，但不展示不可用按钮。

### 阶段 4 门禁

- [ ] Position 模式松杆后可在无风环境稳定刹停和悬停。
- [ ] 三种 Profile 行为差异可观察且切换无突跳。
- [x] 自动起降确定性 PlayMode 工况连续执行两次，不残留 PID 积分或错误目标。
- [ ] 手柄实际操控至少验证一次，键盘数字输入经过平滑处理。

## 九、阶段 5：相机、云台、遥控器接管和 HUD

### 任务 5.1：统一相机架构

- [x] 云台 Yaw/Pitch 使用独立 Transform，姿态稳定不修改机体物理。
- [x] 提供云台主相机、第三人称追尾、自由环绕、机身固定前视和机腹视角。
- [x] 视角切换复用唯一无人机 Camera，不创建多个同时渲染的昂贵 Camera。
- [x] 主云台镜头支持平滑 Pitch、FOV/变焦和限位。

### 任务 5.2：遥控器 RT 到全屏转场

- [x] 状态机包含地面待机、拿起遥控器、开机、连接、RT 预览、推进屏幕、全屏接管、退出。
- [x] RT 阶段由云台相机渲染到遥控器屏幕。
- [x] 屏幕铺满时保留同一 Camera Rig 的姿态/FOV并切换为主输出。
- [x] 全屏接管后解除 targetTexture 并释放不必要的 RT 更新。
- [x] 退出时恢复玩家相机、输入上下文和遥控器状态。
- [x] 第一版使用代理遥控器几何体和简单动画，正式美术延后。

### 任务 5.3：输入源

- [x] Keyboard、Gamepad 均输出统一 `DroneControlInput`，Touch 保留输入适配边界。
- [x] Gamepad 默认采用 Mode 2 语义。
- [x] 键盘离散值经过上升/下降率平滑，不直接产生满量程阶跃。
- [ ] 移动双摇杆只建立输入适配边界；除非已有目标移动 UI 规范，否则不提前做最终皮肤。
- [x] 接管状态控制无人机输入组件启停，退出时恢复玩家 Camera 并释放 RT；当前适配器无全局事件订阅。

### 任务 5.4：HUD

- [x] 玩家 HUD 显示 Position/Profile、解锁状态、升降输入、高度、水平/垂直速度、距离和电机饱和。
- [x] HUD 显示云台角度、FOV、抓钩/载荷状态和告警区域。
- [x] 调试面板已与玩家 HUD 分离并可用 F3 关闭，逐轴 Rate PID P/I/D 明细已补齐。

### 阶段 5 门禁

- [x] Unity 6 Game View 已捕获 RT Preview 与 Fullscreen；全屏修复了遥控器代理入镜，未见双重渲染，输入组件只在 Fullscreen 启用。
- [x] PlayMode 测试确认各视角切换不改变无人机 Rigidbody 状态。
- [x] PlayMode 测试确认退出后玩家 Camera/输入恢复且 RenderTexture 释放。
- [ ] 至少验证 16:9、超宽和窄屏布局。

## 十、阶段 6：物理抓钩与载荷玩法

### 任务 6.1：统一挂载接口

- [x] `PayloadMount` 负责挂载状态和质量信息，不负责具体抓钩动画。
- [ ] 载荷类型、质量、连接点、承载上限和释放原因可查询；允许类型过滤规则仍待具体钓鱼载荷出现后建立。
- [x] 飞控从 Rigidbody 的真实反馈工作；不为抓钩写专用姿态作弊。
- [x] 挂载状态进入玩家 HUD；定长遥测摘要可由 F4 复制。

### 任务 6.2：机械抓钩

- [x] 抓钩底座和活动爪具有独立 Transform/Collider。
- [x] 使用检测区域筛选最近候选，闭合动作与真正建立 Joint 分离。
- [x] 目标保持独立 Rigidbody，通过 ConfigurableJoint 连接。
- [x] 超载拒绝、JointBreak、替换、OwnerDisabled 和显式释放均有原因。
- [x] 未修改全局 Physics 参数，Joint 使用局部 breakForce/breakTorque。

### 任务 6.3：玩法闭环

- [x] 场景包含 0.15/0.35/0.90 kg 三种质量可抓物和明确投放区。
- [ ] 玩家可以起飞、对准、抓取、运输、释放、返航/降落。
- [ ] 中心载荷、偏心载荷和摆动载荷均可观察。
- [x] 0.90 kg 目标超过 0.60 kg 上限时拒绝挂载，HUD 显示超载提示。

### 阶段 6 门禁

- [x] PlayMode 测试确认抓取后目标保持独立、非 Kinematic Rigidbody 与碰撞。
- [x] 生产代码不存在载荷专用锁姿态，载荷由同一飞控反馈恢复。
- [x] 自动载荷工况确认释放后恢复且无 NaN；真实机械爪释放瞬态仍需 Game View 手动观察。
- [x] PlayMode 测试连续 3 次抓取/释放不遗留 Joint 或引用。

## 十一、阶段 7：正式模型审计与美术替换

### 任务 7.1：用户模型适配审计

用户若提供模型，记录：

- [ ] 许可证和可修改/可分发范围。
- [ ] 单网格、子网格、材质槽、骨骼和动画层级。
- [ ] 四螺旋桨、云台、镜头、挂载点是否能按连通区域或材质分离。
- [ ] 比例、前向、轴心、单位、法线、UV、材质和 LOD。
- [ ] 与物理代理的对齐成本。

结论只能是“直接适配、补独立活动部件、Blender 分离、重新制作”之一，并附依据；不能仅凭一体网格判定不能使用。

### 任务 7.2：Blender 建模门禁

- [ ] 物理与抓钩门禁已经通过。
- [ ] 形成外观参考、尺寸、风格和拆分清单。
- [ ] 用户明确批准视觉方向。
- [ ] 再开始正式 Blender 建模、UV、材质、LOD 和导出。

### 任务 7.3：替换而不改物理

- [ ] 正式模型作为 Visual Root 对齐既有物理代理。
- [ ] 不因美术尺寸偷偷改变 Rotor 物理位置；若确实改变，重新执行全部物理门禁。
- [ ] 螺旋桨、云台和抓钩轴心正确。
- [ ] 动画、材质、LOD、阴影和碰撞体性能可接受。

## 十二、阶段 8：无人机钓鱼扩展

此阶段在抓钩玩法完成后独立启动，不属于首轮 MVP。

### 任务 8.1：钓鱼释放器

- [ ] 作为新的 Payload 实现接入现有挂载接口，不修改无人机控制核心。
- [ ] 支持鱼线/铅坠/鱼饵连接、载荷计算、投放和快速释放。
- [ ] 使用机腹视角和距离/高度信息辅助对准钓点。

### 任务 8.2：鱼线技术验证

- [ ] 先比较分段 Joint、Verlet/Rope 模型和视觉假线三种方案。
- [ ] 用独立性能样例验证稳定性、碰撞需求和移动端预算。
- [ ] 未验证前不承诺完整缠绕、柔体和水下碰撞。

### 任务 8.3：最小钓鱼闭环

- [ ] 岸边装载鱼饵。
- [ ] 无人机携带载荷起飞并飞到目标水域。
- [ ] 投放/释放鱼线。
- [ ] 无人机返航，玩家恢复鱼竿玩法。
- [ ] 无人机系统只负责运送和释放，不吞并既有钓鱼结算职责。

## 十三、阶段报告模板

每完成一个阶段，追加或输出：

```text
阶段：
完成能力：
真实修改路径：
关键参数/公式：
自动化测试（精确类或方法）：
Unity 编译：
Console：
Game View 手动验证：
量化测量：
未执行的全量测试：
已知限制：
文档同步：
下一阶段门禁：
```

## 十四、明确禁止的捷径

- 直接设置 Transform 或每帧覆盖 Rigidbody rotation 来伪造主要飞行。
- 用单个中心 AddForce 冒充四旋翼独立推力。
- 抓取后把目标设为无质量子物体。
- 用超大 drag/angularDrag 掩盖控制器不稳定。
- 在 PID 之外偷偷写额外“回正力”却不进入遥测和文档。
- 用 Euler 角直接相减处理姿态跨界。
- 在裸机门禁前制作正式模型或复杂角色动画。
- 未核对许可证就复制 GitHub/Bilibili 教程代码或资产。
- 自动扩大为 Core 公共系统、PX4/ROS、联机或全量传感器仿真。
- 修改用户无关文件、自动 commit/push，或把静态扫描声称为 Unity 运行验证。
