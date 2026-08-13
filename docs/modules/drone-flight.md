# DroneFlight 无人机飞行仿真

## 负责什么

DroneFlight 是 Hotfix 业务层的独立 Demo，负责：

- 四旋翼独立推力、电机响应、反扭矩与 X 型混控。
- Rate、Attitude、高度、速度和位置级联控制。
- 键鼠、手柄和未来移动双摇杆的统一输入语义。
- 云台、无人机视角、遥控器接管和飞行 HUD。
- 机腹挂载、物理抓钩、载荷反馈及后续钓鱼释放器扩展。
- PID、电机、姿态、载荷和饱和状态的运行时遥测。

## 不负责什么

- 不实现 PX4、ArduPilot、ROS2、MAVLink 或工程级传感器融合。
- 不把无人机专属算法提前提升到 `Core.Runtime`。
- 不修改 Core 启动链路；最终从 Hotfix 主菜单进入。
- 不负责鱼咬钩、鱼线结算等既有钓鱼业务，只负责运送和释放载荷。
- 裸机 PID 门禁通过前不负责正式模型和角色遥控器成品美术。

## 代码与资源位置

计划中的生产代码根目录：

```text
Assets/Scripts/Hotfix/Module/DroneFlight/
├── Control/
├── Physics/
├── Input/
├── Camera/
├── Payload/
├── Telemetry/
├── Runtime/
└── UI/
```

Demo 资源必须遵守 `LoadResourcesNamingSpec` 的现有目录矩阵：

```text
Assets/LoadResources/Demos/drone_flight/
├── Scenes/       可加载场景
├── Prefabs/      无人机、测试物、HUD 和遥控器玩法 Prefab
├── Data/         ScriptableObject/JSON 配置
├── Art/          模型、贴图、材质和动画
└── VFX/          Demo 私有特效
```

不存在 `Demos/drone_flight/UI/` 资源目录。Demo 私有 UI Prefab 放在 `Prefabs/UI/`，其贴图和材质放在 `Art/UI/`；规则允许在已登记目录下继续细分。

自动化测试放在：

- `Assets/Scripts/Tests/Hotfix/EditMode/`：PID、混控、电机和配置等纯算法。
- `Assets/Scripts/Tests/Hotfix/PlayMode/`：Rigidbody 闭环、载荷和相机生命周期；需要时按测试架构新增 `Hotfix.Tests.PlayMode`。

## 当前基线

2026-08-13 审计结果：

- Unity：`6000.3.15f1`。
- Input System：`1.19.0`；`Hotfix.asmdef` 已按实际键鼠/手柄适配需求引用 `Unity.InputSystem`。
- 固定物理步长：`0.02 s`（50 Hz）。
- 重力：`(0, -9.81, 0) m/s²`。
- 默认 3D solver iterations：位置 6、速度 1；首版不修改全局值。
- 默认最大角速度：`50 rad/s`。
- Build Settings 只包含 `Assets/Scenes/AppEntrance.unity`；DroneFlight `Scenes/Main.unity` 由 YooAsset `Demos` Collector 收集。
- `Assets/LoadResources/Demos` 已存在并由 YooAsset `Demos` Collector 递归收集，地址使用 LoadResources 全路径。
- DroneFlight 通过全局 `GameSceneNavigator` 进入和返回，使用运行期 CommonLoading、Additive 场景句柄和统一相机切换，不直接重载 Hub。

## 坐标、电机与单位约定

此处是 DroneFlight 的单一语义来源；代码用统一描述结构表达，不在多个控制器重复硬编码。

- 世界坐标：Unity 左手坐标，`+Y` 向上。
- 机体局部：`+Z` 向前、`+X` 向右、`+Y` 向上。
- 布局：X 型四旋翼。
- FrontLeft：`(-X, +Z)`。
- FrontRight：`(+X, +Z)`。
- RearLeft：`(-X, -Z)`。
- RearRight：`(+X, -Z)`。
- FrontLeft、RearRight 为逆时针（CCW）组，FrontRight、RearLeft 为顺时针（CW）组（均按机体上方俯视旋翼定义）。
- 正 Roll 提升左侧电机、正 Pitch 提升后侧电机、正 Yaw 提升 CCW 组；这些符号由 `QuadrotorMixerTests` 固定。
- 长度使用米、质量使用千克、力使用牛顿、力矩使用牛顿米、角速度内部使用弧度每秒。

## 主控制链路

```text
输入源
-> DroneControlInput
-> 飞行模式/Profile
-> 位置环
-> 水平/垂直速度环
-> 目标姿态
-> 姿态环
-> 目标机体角速度
-> Rate PID
-> 四旋翼混控器
-> 电机响应模型
-> 四个 Rotor AddForceAtPosition + 反扭矩
-> Rigidbody 状态反馈
```

前期按门禁逐层启用：Rate → Attitude → 垂直速度/高度 → 水平速度/位置。未启用的外环不得偷偷写额外回正力。

## 生命周期

1. 场景装配并解析配置、Rigidbody、四个 Rotor 和传感 Transform。
2. Disarmed 状态检查配置，不产生起飞推力。
3. Armed 后每个固定步长读取输入/Setpoint，执行控制链并施力。
4. 遥测只读取控制状态，不反向修改物理。
5. 接地停桨、故障或显式复位时重置所有 PID 历史和 Setpoint。
6. 离开 Demo 时释放输入、RenderTexture、调试订阅和临时 Joint。

遥控器接管状态依次为 GroundIdle → PickingUp → PoweringOn → Connecting → Preview → Expanding → Fullscreen。Preview 时玩家 Camera 继续输出且无人机 Camera 只更新 RT；Fullscreen 时 RT 停止更新、玩家 Camera 关闭、无人机 Camera 接管主输出，遥控器代理退出世界渲染。

## 配置与运行时调参

- 配置资产放在 `Demos/drone_flight/Data/`。
- 配置至少分为 Airframe、Motor/Mixer、Rate/Attitude、Position/Profile、Camera 和 Payload 职责，最终是否拆为多个资产由首轮实现复杂度决定。
- 运行时调参使用实例副本；不得自动写回 ScriptableObject。
- 保存或恢复默认值必须是显式操作。
- 无效质量、Rotor 缺失、错误限幅、非正 fixedDeltaTime 或 NaN 输入必须给出中文诊断并停止施力。

## 挂载边界

- `PayloadMount` 只管理连接、质量、承载上限和释放原因。
- 抓取物始终保留独立 Rigidbody，通过 Joint/约束连接。
- 飞控从真实 Rigidbody 反馈补偿载荷，不加入抓钩专用锁姿态作弊。
- 钓鱼释放器作为新的 Payload 实现接入，不修改 PID、Mixer 或 Motor 核心。

## 边界规则

- 主要飞行禁止直接设置 Transform 或覆盖 Rigidbody rotation。
- 禁止用单一中心总推力替代四个 Rotor 施力。
- 禁止使用异常大的 drag/angularDrag 掩盖控制器振荡。
- 视觉桨叶、云台、RT 和 HUD 不是物理真源。
- 全局 Time/Physics 设置只有在局部参数无法满足且有测量证据时才允许调整。
- 正式模型只替换 Visual Root；物理 Rotor、重心和 Collider 改变后必须重跑裸机门禁。

## 验证重点

- EditMode：PID、抗饱和、混控符号、电机响应、输入限幅和非法数值保护。
- PlayMode：水平悬停、冲量恢复、中心载荷挂载/释放、无 NaN/Infinity。
- Game View：四轴方向、松杆回正、起飞/降落、多视角、RT 接管和抓钩闭环。
- 每次只运行当前阶段相关的精确测试类，不自动执行全量测试。

当前量化门禁与阶段进度见 [`实施计划`](../superpowers/plans/2026-08-13-drone-flight-simulation.md)。

## 当前实现进度

阶段 1 的纯数学核心已建立：

- `DroneControlInput`：统一归一化输入、限幅和非法数值收口。
- `DronePidController`：P/I/D、积分限幅、条件抗饱和、混控输出饱和回滚、D 项低通、Reset 和遥测。
- `DroneMotorModel`：一阶电机响应，采用 `T = k * rpm²`，推力单位为 N；反扭矩采用 `Q = T * reactionTorqueCoefficient`，单位为 N·m。
- `QuadrotorMixer`：X 架四轴符号、总推力平移和姿态等比反饱和。

基础几何体样机已建立在 `Demos/drone_flight/Prefabs/DronePrototype.prefab`，场景为 `Scenes/Main.unity`，配置为 `Data/DroneFlightConfig.asset`。动态机体使用组合 BoxCollider，四个 Rotor 通过 `DroneRotor` 显式绑定位置、旋向和视觉桨叶。

当前控制能力包括：

- Armed/Disarmed；锁定后立即清空电机和 PID 历史。
- Quaternion 最短姿态误差、Attitude → Rate PID → Mixer → Motor → 四点施力。
- Mixer 任一电机达到输出限制时，把饱和状态反馈给四个 Rate/Vertical PID，撤销本次积分推进，避免执行器受限时继续 wind-up。
- 基于质量与重力计算悬停前馈，高度 → 垂直速度级联控制。
- 推力平方模型下的倾角补偿：电机基准命令乘 `1 / sqrt(cos(tilt))`。
- 水平位置 → 速度 → 加速度 → 目标姿态控制；有输入时按机头 Yaw 解释速度命令，松杆后锁定当前位置。
- 键盘 `R` 解锁、`WASD` 水平、`Q/E` 偏航、`Space/左 Ctrl` 升降；手柄使用 Mode 2 摇杆语义。

2026-08-13 当前验证：

- EditMode：10 个 DroneFlight 相关测试类共 32/32 通过，覆盖输入、PID、电机、混控、姿态、Prefab、Profile、遥控器状态机、HUD 和 Hub 导航。
- PlayMode：3 个 DroneFlight 相关测试类共 11/11 通过，覆盖四旋翼闭环、自动起降/故障、相机/RT 生命周期和抓钩/载荷循环。
- 本次饱和反馈与遥测优化另做精确回归：`DronePidControllerTests` 7/7、`DroneTelemetryBufferTests` 1/1、`DroneRotorPhysicsTests` 6/6；未执行项目全量测试。
- 稳定窗口径固定为 500 个 50 Hz 样本中至少 95% 同时满足高度误差 ≤ 0.20 m、倾角 ≤ 3°。
- 冲量后 6 秒检查高度、姿态和水平位置恢复；载荷增加/释放各使用 8 秒恢复窗。
- Unity 正式编译 0 error、0 warning；Unity 6 实际 Game View 运行时 Console 为 0 Error、0 Warning。
- Game View 已观察到自动接管、RT Preview、Fullscreen、第三人称视角、HUD，以及无人机在 1.500 m 高度的稳定悬停；运行时读取的水平/垂直速度接近 0。
- Stage 4 已实现 Cine/Normal/Sport 共享控制器参数、Position 保持、自动起降和持续危险倾角故障停桨。
- Stage 5 已实现单一 Drone Camera 的云台/第三人称/环绕/固定前视/机腹视角、遥控器代理动画、RT 转场和玩家 HUD；F3 调试面板与玩家 HUD 分离，并显示逐轴 Rate PID 的目标、实际、误差与 P/I/D。
- Stage 6 已实现独立 Rigidbody 载荷、ConfigurableJoint 挂载、质量上限、断裂/释放原因、机械爪检测，以及 0.15/0.35/0.90 kg 三档场景载荷和投放区。
- `DroneTelemetryRecorder` 保留最近 500 个 FixedUpdate 样本（默认约 10 秒），按 `F4` 可复制高度误差、最大倾角、最大水平速度、饱和次数、非法值次数和末次角速度摘要，便于试飞反馈与调参对比。

尚未完成的门禁：没有对 Unity 6 窗口进行真人键盘手飞或实体手柄验证；截图接口固定输出 1920×1080，不能作为超宽/窄屏视觉验收；真实玩家抓取、运输和释放闭环仍需 Game View 手动体验。当前恢复点固定为“等待用户试飞反馈”，在收到反馈前暂停 Stage 7 正式模型和 Stage 8 无人机钓鱼工作。正式模型仍受视觉审批门禁约束，在用户批准外观方案前不开始 Blender 成品建模。

## Stage 7 外观提案（待用户批准）

- 定位：无商标的消费级航拍无人机，克制、现代、适合写实偏游戏化场景，不复制 DJI 具体产品外壳。
- 建议尺寸：轴距约 `0.42 m`，机身约 `0.28 × 0.18 × 0.10 m`；保留当前 Rotor 物理点为约束，不因外观偷偷改变力臂。
- 配色：暖灰白上壳、深石墨机臂/底壳、少量安全橙状态灯；抓钩保持工业深灰。
- 必拆活动节点：四个 Propeller、`GimbalYaw`、`GimbalPitch`、CameraLens、HookBase、LeftClaw、RightClaw。
- 可保持一体的固定节点：主壳、固定机臂、起落支撑；正式网格挂在 Visual Root，不替换 Rigidbody/Collider/施力点。
- 交付档位：先中模与简单 PBR 材质验证轮廓和轴心，再做 UV、细节法线、LOD0/LOD1；角色手和成品遥控器外观另行审批。

## 相关文档

- [无人机飞行仿真 Codex Goal](../superpowers/specs/2026-08-13-drone-flight-simulation-goal.md)
- [无人机飞行仿真实施计划](../superpowers/plans/2026-08-13-drone-flight-simulation.md)
- [调试和整定 DroneFlight](../runbooks/tune-drone-flight.md)
- [新增 Demo](../runbooks/add-demo.md)
- [运行 Unity 自动化测试](../runbooks/run-unity-tests.md)
