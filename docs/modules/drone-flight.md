# DroneFlight 无人机飞行仿真

## 负责什么

DroneFlight 是 Hotfix 业务层的独立 Demo，负责：

- 四旋翼独立推力、电机响应、反扭矩与 X 型混控。
- Rate、Attitude、高度、速度和位置级联控制。
- 键鼠、手柄和未来移动双摇杆的统一输入语义。
- 云台、无人机多视角、直接控制会话和正式 UIManager HUD。
- 手动起落架、可停靠卷扬吊链、工业六爪物理抓斗、载荷反馈及后续钓鱼释放器扩展。
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

控制会话只保留 `Waiting / Active`。进入场景时为 Waiting；按 `F` 立即进入 Active、切到第三人称并启用飞行与机构输入，但保持锁定。按 `Escape` 返回 Waiting，同时锁定电机并清空输入。该流程不再创建遥控器代理、拿起动画或 RenderTexture。

正式试玩入口是 `AppEntrance → Hub → DroneFlight`。场景中的 `DroneFlightSceneContext` 持有飞控、相机、输入、起落架、卷扬和抓斗引用；`DroneFlightUIController` 通过 `UIManager.ShowAsync` 打开 `Decorate/Widget` HUD，并按 F3 切换 `Tip/Widget` 调试 View。场景卸载时两者一并关闭。DroneFlight 场景不再包含独立 Canvas，也不保证脱离完整启动链时显示正式 UI。

## 配置与运行时调参

- 单一配置资产位于 `Demos/drone_flight/Data/DroneFlightConfig.asset`，不建立普通/高级两套并行资产。`Hotfix.Editor` 的自定义 Inspector 只是该资产的两种视图，语言和分页选择只写本机 `EditorPrefs`。
- 普通设置面向策划：额定载重、最大载荷倍率、满载动力占用、电机响应速度、三个 Profile、自动起降、起落架、卷扬（包含收纳长度）和抓取牢固度。只读区域显示自动机体质量、部署吊挂设备质量、推力系数、动力余量和最大载荷理论悬停能力。
- 高级设置保留所有底层序列化字段。自动载重调校下，机体质量、响应时间、推力系数和弱约束断裂值是只读派生值；手动物理模式才使用这些真实字段，并显示悬停和 PID 饱和风险。
- `DronePayloadTuningCalculator` 是纯计算边界。额定载重决定自动机体质量和动力，最大载荷倍率只决定抓取门禁；抓取许可只比较被抓物体自身的 `Rigidbody.mass`，默认 `0.05 kg` 吊挂设备不占用有效载荷额度。
- Cine/Normal/Sport 只决定目标速度、加速度、倾角、升降、偏航和输入响应；额定载重只决定动力储备，载重变化不得改写 Profile。
- 飞控在 Awake 创建运行时配置副本。Play Mode 修改源资产后，FixedUpdate 将变化同步到副本并安全更新 Rigidbody 和电机参数；保留当前 RPM、PID、位姿与速度，不回写源资产。
- 无效质量、非法倍率、满载占用越界、推力不足、Rotor 缺失、错误限幅、非正 fixedDeltaTime 或 NaN 输入必须给出中文诊断并拒绝污染物理状态。

## 旋翼、起落架与卷扬抓斗

- 四个 Rotor 的推力和反扭矩仍只由 FixedUpdate 的真实电机模型产生；`DroneRotorVisual` 只读取 RPM，并在渲染帧累计局部 Y 轴相位。轮毂和细长桨叶无 Collider、无质量。
- 起落架只响应 `L` 的显式切换，飞行高度、锁定、起飞和降落均不改变目标；降落时仍收起只由 HUD 中文警告。重新加载场景后由新实例恢复默认放下状态。
- 抓斗收纳时所有吊挂 Rigidbody 变为 Kinematic、关闭重力和碰撞，并临时断开内部 Joint 的 `connectedBody` 后停靠机腹；此时 Joint 不参与无人机主刚体求解，向飞控报告的外部质量为零。放出完成后先恢复一节连接杆和抓斗的部署姿态，再恢复 Joint、重力和碰撞；设备总质量默认 `0.05 kg`，可在普通设置修改。
- 一节连接杆、抓斗基座和六个爪体始终保留各自的真实 Rigidbody 质量，总和严格匹配“吊挂设备总质量”。收纳时只是停止模拟并向悬停前馈贡献 `0 kg`；放出后配置质量才加入“当前受支持总质量”。它不并入裸机自重，被抓物体也始终按自己的 `Rigidbody.mass` 单独计算。
- 部署动画最后 `18%` 行程会把设备质量从 `0` 平滑预告到配置值，模拟连接逐渐拉紧，让一阶电机模型提前建立转速；物理启用后仍只使用真实设备质量。
- `J` 的放出/空载收回阶段会在停靠姿态和完整工作姿态之间逐帧插值，完成后状态确定地进入 `Deployed / Stowed`；只有 `Deployed` 才接受 `H`。基础 Joint 锚点独立序列化，Prefab 重建不会累加长度偏移；启用物理前整套机构按当前卷扬长度对齐顶端锚点，禁止让 Joint 投影代替部署定位。
- 六个 `scale=1` 物理爪各有双段弯折 Mesh、复合 Collider、独立 HingeJoint 和接触传感器。HingeJoint 使用限位弹簧驱动且不允许运行时断裂；内部碰撞被忽略。`H` 仅在抓斗放出后开合，同一合法载荷必须进入包围区并被至少三个不同爪接触，才建立可断裂弱约束。
- 飞控通过只读外部质量提供者区分“已抓住的真实载荷”和“弱约束张力当前实际承担的载荷”。物体仍由地面支撑时，HUD 可以显示已抓质量，但悬停前馈不会瞬间计入全重；离地并拉紧后才按 PhysX 约束力平滑计入。摆动惯性仍由 PhysX 产生，不修改裸机 Rigidbody 阻尼、PID 或档位参数。
- 张开爪时弱约束和受力载荷立即清空，但不清零电机 RPM、不写速度也不施加额外冲量；残余转速自然产生短暂上窜，高度 PID 随后恢复原目标。
- 吊链、抓斗、六爪和载荷 Rigidbody 使用插值显示；内部 Joint 使用配对质量缩放并关闭预处理，载荷弱约束关闭连接双方自碰撞。爪与载荷的真实碰撞仍保留，不能用冻结 Transform 或增加无人机阻尼掩盖抖动。
- `R` 松开前未达到配置时长按短按处理；达到时长后只发一次场景重载请求。`GameSceneNavigator.ReloadCurrentAsync()` 会卸载并重新加载整个 DroneFlight 场景，玩法对象、载荷、约束、HUD 和调试 View 全部走正常销毁和创建生命周期，不再逐项恢复状态。

编辑器中打开 F3 调试 View 时，`DroneDebugPresenter` 会直接在 Game 视图绘制四旋翼独立升力、总升力、重力、实际速度、目标速度和目标加速度。这些矢量只读取最近物理步，不参与控制或施力。偏航目标相对真实机头的领先量受姿态环能力限制，避免持续 Q/E 积累不可及时消化的航向误差；水平移动输入始终按当前真实机头朝向转换到世界速度。姿态外环分别计算真实机头坐标系下的 Pitch/Roll 目标角速度和独立 Yaw 目标角速度，禁止把未追上的目标航向与倾角合成一个大误差 Quaternion，避免高速转向后左右轴耦合摇摆。

## 挂载边界

- `PayloadMount` 只管理连接、真实质量、约束受力质量、承载上限和释放原因。
- 抓取物始终保留独立 Rigidbody，通过 Joint/约束连接。
- 飞控从真实 Rigidbody 反馈补偿载荷，不加入抓钩专用锁姿态作弊。
- 钓鱼释放器作为新的 Payload 实现接入，不修改 PID、Mixer 或 Motor 核心。

## 边界规则

- 主要飞行禁止直接设置 Transform 或覆盖 Rigidbody rotation。
- 禁止用单一中心总推力替代四个 Rotor 施力。
- 禁止使用异常大的 drag/angularDrag 掩盖控制器振荡。
- 视觉桨叶、云台和 HUD 不是物理真源。
- 全局 Time/Physics 设置只有在局部参数无法满足且有测量证据时才允许调整。
- 正式模型只替换 Visual Root；物理 Rotor、重心和 Collider 改变后必须重跑裸机门禁。

## 验证重点

- EditMode：PID、抗饱和、混控符号、电机响应、输入限幅和非法数值保护。
- PlayMode：水平悬停、冲量恢复、中心载荷挂载/释放、无 NaN/Infinity。
- Game View：四轴方向、松杆回正、起飞/降落、多视角、直接控制会话和抓钩闭环。
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

2026-08-14 试玩反馈修正后验证：

- 本轮精确 EditMode：6 个直接相关测试类共 16/16 通过，覆盖旋翼视觉积分、5 秒复位状态机、起落架状态、Prefab 结构、HUD 文本和 UIManager View 契约。
- 本轮精确 PlayMode：3 个直接相关测试类共 12/12 通过，覆盖四旋翼闭环、相机/RT 默认视角和三爪弱约束/连续释放。
- 第二轮精确 EditMode：6 个直接相关测试类共 17/17 通过，覆盖两态控制会话、手动起落架、中文 F3/HUD、工业抓斗 Prefab 契约、UI View 和 5 秒复位状态机。
- 第二轮精确 PlayMode：3 个直接相关测试类共 14/14 通过，覆盖无 RT 的第三人称接管、三爪门禁、多 Collider 接触、载荷快照复位和既有四旋翼闭环。
- 收纳关节侧翻修正精确回归：`DronePrototypeContractTests` 7/7、`DroneWinchControllerTests` 2/2；真实 Prefab 在四脚架水平着地及抓斗收纳状态下模拟 3 秒未侧翻，实际 DroneFlight 场景运行时姿态保持水平。
- 本次饱和反馈与遥测优化另做精确回归：`DronePidControllerTests` 7/7、`DroneTelemetryBufferTests` 1/1、`DroneRotorPhysicsTests` 6/6；未执行项目全量测试。
- 本次载荷受力与短卷扬锚点修正精确回归：`DroneWinchControllerTests` 5/5、`DroneHudFormatterTests` 4/4、`DronePrototypeContractTests` 7/7、`DronePayloadMountTests` 8/8、`DroneRotorPhysicsTests` 12/12；Unity Console 0 error，未执行项目全量测试。
- 稳定窗口径固定为 500 个 50 Hz 样本中至少 95% 同时满足高度误差 ≤ 0.20 m、倾角 ≤ 3°。
- 冲量后 6 秒检查高度、姿态和水平位置恢复；载荷增加/释放各使用 8 秒恢复窗。
- Unity 正式编译 0 error、0 warning；Unity 6 实际 Game View 运行时 Console 为 0 Error、0 Warning。
- 第一轮曾验证 RT 接管与稳定悬停；第二轮已按试玩反馈移除 RT 流程，保留第三人称直接控制和正式 HUD。
- Stage 4 已实现 Cine/Normal/Sport 共享控制器参数、Position 保持、自动起降和持续危险倾角故障停桨。
- Stage 5 使用单一 Drone Camera 的云台/第三人称/环绕/固定前视/机腹视角；`F` 直接进入第三人称。正式 HUD 为 `Decorate/Widget`，F3 调试面板为右下角中文 `Tip/Widget`。
- Stage 6 已升级为四支腿手动起落架、可停靠一节物理连接杆、卷扬和 `0.7` 尺寸工业双段六爪；内部 Joint 不可断裂并启用投影纠偏，载荷保留独立 Rigidbody，通过三爪接触门禁后的可断裂弱约束抓取。
- 四个旋翼已有轮毂和细长临时桨叶，视觉组件按真实 RPM 在渲染帧积分，不反向影响 Rigidbody/PID。
- 配置资产已有 `Hotfix.Editor` 双语 Inspector；中文/English 偏好不写入资产。
- 配置 Inspector 已增加普通/高级分页与自动载重调校/手动物理互斥模式；额定 `1 kg` 默认派生 `1.2 kg` 机体、`1.25 kg` 抓取上限和 `90%` 额定满载悬停指令。
- F3 增加动力模式、额定/真实/受力/最大载荷、机体/设备/受支持总质量、理论与实际电机指令、动力余量和载重区域。
- `DroneTelemetryRecorder` 保留最近 500 个 FixedUpdate 样本（默认约 10 秒），按 `F4` 可复制高度误差、最大倾角、最大水平速度、饱和次数、非法值次数和末次角速度摘要，便于试飞反馈与调参对比。

尚未完成的门禁：仍需用户在 Game View 对比额定 `1 kg / 10 kg` 下的空载 Normal 手感和同一 `0.95 kg` 载荷动力余量，完成真实三爪抓取/运输/高空释放，并确认 Play Mode 普通设置即时生效。下一恢复点固定为“等待策划友好型载重配置试飞反馈”；在反馈前暂停正式模型和无人机钓鱼工作。

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
