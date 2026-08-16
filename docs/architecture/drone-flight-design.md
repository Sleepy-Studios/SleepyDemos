# DroneFlight 实现原理与架构设计

## DroneFlight 到底模拟了什么

DroneFlight 不是把模型沿一条曲线移动，也不是给 Rigidbody 直接指定速度。玩家、航点或捕鱼演出只负责提出“想往哪里飞”；飞控根据无人机当前的速度、姿态和载荷，算出四个旋翼各自应该产生多大推力，再交给 Unity Physics 解算运动。

这条链路可以概括为：

```text
手动输入 / 通用航点 / 捕鱼演出
              ↓
      连续的速度、高度和偏航目标
              ↓
位置保持 → 速度 PID → 期望总推力
              ↓
姿态控制 → 角速度 PID → 期望机体力矩
              ↓
基于真实 Rotor 几何的四旋翼控制分配
              ↓
电机一阶响应 → RPM² 推力 → 四点施力与反扭矩
              ↓
Rigidbody / Collider / Joint / 动态载荷
              ↓
相机、HUD、调试显示和遥测读取物理结果
```

因此，抓起载荷、撞到障碍或受到外力后，机体会先按物理规律产生偏移，飞控再尝试把它拉回目标状态。视觉螺旋桨、镜头和 HUD 都只是结果的观察者，不是飞行状态真源。

关键入口：`DroneFlightController`、`DroneRotorActuatorRuntime`、`DroneRotor`。

## 一个物理帧里发生了什么

`DroneFlightController.FixedUpdate()` 是飞行主循环。单个固定物理帧按下面的顺序推进：

1. 同步运行时配置和外部质量，保证热调参数与装备状态在本帧一致。
2. 检查飞控是否解锁；锁定或故障状态不产生旋翼推力。
3. 从 Rigidbody 捕获位置、速度、姿态、局部角速度，并计算滤波后的加速度和角加速度。
4. `DroneTrajectoryGenerator` 把输入整形成连续的速度、加速度、偏航角和偏航角速度目标。
5. 松杆时使用最后位置做水平保持；没有升降输入时使用目标高度做定高。
6. 水平与垂直速度 PID 计算期望加速度。
7. 把期望加速度、重力和当前承载质量换算为期望总力，并限制最大倾角。
8. 根据总力方向和目标偏航生成目标局部角速度。
9. Pitch、Yaw、Roll 三个角速度 PID 计算目标角加速度。
10. 结合 Rigidbody 惯性张量换算为机体力矩。
11. 控制分配器把总推力与三轴力矩分给 FL、FR、RL、RR 四个 Rotor。
12. 将无法实现的输出方向反馈给 PID，避免积分继续把控制器推向饱和。
13. 四个电机经过一阶响应得到实际 RPM、推力和反扭矩。
14. 四个 Rotor 在各自真实位置施力，Rigidbody 完成本物理帧求解。

这条顺序很重要。比如装备质量必须在算总力前同步，控制分配饱和必须在下一帧积分前反馈；交换这些阶段会让调参结果与真实受力脱节。

## 控制来源为什么可以替换

DroneFlight 把“谁在控制”与“无人机怎样产生力”分开了。

- `DronePlayerInput` 读取键鼠或 Input System，产生归一化的前后、左右、升降和偏航输入。
- `DroneCruiseRunner` 推进通用航点状态，处理单次、循环、往返、等待和抵达判定。
- `DroneMissionAutopilot` 把世界坐标目标换算为飞控输入、目标高度和偏航输入。
- `Adapters/Fishing/DroneBezierMissionPath` 与 `DroneFishingMissionCoordinator` 负责捕鱼演出路径和阶段编排。

这些控制来源最终都调用飞控现有输入或高度接口。它们不会写 Transform，也不会直接覆盖 Rigidbody 速度，所以自动巡航仍然受电机响应、最大推力、碰撞和载荷影响。

这也是 AI、网络控制或录制回放的扩展点：新增控制来源时，应产生相同的控制目标，而不是复制一套飞控。

关键入口：`DroneControlInput`、`DronePlayerInput`、`DroneCruiseRunner`、`DroneMissionAutopilot`。

## 输入整形：为什么摇杆不会让速度瞬间跳变

消费级航拍无人机需要平顺，但“平顺”不能靠把 Rigidbody 阻尼调得很大来伪造。`DroneTrajectoryGenerator` 会逐步改变目标速度和加速度，并限制加加速度，也就是加速度变化的快慢。

玩家将摇杆推到底时，目标速度不会在一个物理帧内从零跳到最大值；松开时也会按同一套限制减速。Cine、Normal、Sport 三个档位分别提供不同的：

- 最大水平和垂直速度。
- 最大水平和垂直加速度。
- 最大水平和垂直加加速度。
- 最大倾角。
- 最大偏航速度与偏航加速度。

Cine 更慢、更柔和；Sport 允许更快的速度、倾角和响应。档位只改变目标约束，不替换 PID、控制分配或 Rotor 施力链。

### 松开摇杆后的水平保持

有水平输入或轨迹仍在减速时，飞控跟随整形后的目标速度，并把当前位置记为新的保持点。输入和整形速度都回到零后，飞控使用位置误差生成一个有限的修正速度：

`v = Kₚ(pₜ - p)`

这里的 `pₜ` 是最后保持位置，`p` 是当前位置，`Kₚ` 是水平位置增益，`v` 是修正速度。修正速度仍受当前档位最大速度限制。

玩家看到的效果是：松杆后无人机先制动，再回到并保持最后位置，而不是瞬间冻结在空中。

### 高度保持和自动起降

手动升降时，轨迹生成器提供垂直速度，目标高度跟随当前位置更新；松开升降输入后，飞控重新使用高度误差生成垂直修正速度。

自动起飞只是把目标高度设为“当前位置加配置高度”，自动降落则持续降低目标高度。两者仍走垂直速度 PID 和真实旋翼推力，不存在独立的起降动画。

关键入口：`DroneTrajectoryGenerator`、`DroneFlightController.BuildPositionAwareVelocity()`。

## 速度 PID：把速度误差变成期望加速度

轨迹层只说明“希望飞多快”，速度控制器还要比较实际 Rigidbody 速度。速度误差可以简单写成：

`e = vₜ - v`

`vₜ` 是目标速度，`v` 是当前速度，单位都是米每秒（m/s）；`e` 是两者之差。误差越大，飞控通常需要请求越大的加速度。

当前速度控制使用 PID 和轨迹前馈，简化后可以写成：

`a = Kₚe + Kᵢ∫e dt - K_d(dv/dt) + a_f`

`a` 是期望加速度，单位为米每二次方秒（m/s²）；`Kₚ`、`Kᵢ`、`K_d` 分别是比例、积分和微分系数；`a_f` 是轨迹生成器已经知道的前馈加速度。

几部分各自解决的问题是：

- 比例项根据当前误差立即纠正。
- 积分项补偿长期存在的小偏差，例如持续载荷或轻微模型误差。
- 微分项根据实际速度变化抑制过冲。
- 前馈项让控制器提前跟随已知轨迹变化，不必等误差出现后才反应。

代码使用实际测量值的导数计算微分项，而不是直接对误差求导。这样目标速度突然变化时，不会因为目标阶跃产生一次很大的微分冲击。

### 积分为什么不能无限累积

当电机已经达到能力上限时，继续增加积分不会产生更多推力，只会在能力恢复后造成明显过冲。`DronePidController` 做了三层保护：

- 积分状态有限幅。
- 如果本次误差会把输出继续推向饱和，就拒绝本次积分。
- 控制分配器会把总推力、Pitch、Yaw、Roll 哪个方向无法继续输出反馈回来，PID 撤销对应方向的积分推进。

关键入口：`DronePidController.StepWithMeasurement()`、`DronePidController.ApplyDirectionalSaturation()`。

## 重力补偿、质量和动态载荷

无人机即使悬停不动，也必须持续产生向上的力抵消重力。飞控把期望加速度换算成总力时使用：

`F = m(a - g)`

`F` 是期望总力，单位为牛顿（N）；`m` 是机体和当前受支持载荷的总质量，单位为千克（kg）；`a` 是期望加速度；`g` 是 Unity 的重力加速度向量。

当期望加速度为零时，式子仍会产生与重力方向相反的力，这就是悬停推力。抓起载荷后 `m` 增大，维持相同状态所需的总推力也会增加，所以 HUD 中的悬停油门会上升、动力余量会下降。

装备通过 `IDroneExternalMassProvider` 向飞控报告当前受支持质量。飞控读取质量后参与总力计算；装备本身不能直接修改 PID、Mixer 或四个电机输出。

### 为什么要限制最大倾角

无人机向前加速时，要把总推力向前倾斜。倾角越大，水平分量越大，但用于抵消重力的竖直分量会减少。代码根据竖直力与当前档位最大倾角，限制允许的水平力，避免为了追求速度把机体倾斜到无法维持高度的程度。

如果机体和载荷所需的悬停推力已经接近四个电机总上限，控制器只能报告动力余量不足，不能偷偷增加重力补偿或无视电机上限。

关键入口：`DroneFlightController.CurrentSupportedMassKilograms`、`DronePhysicalControlMath.LimitForceByTilt()`、`DronePayloadTuningCalculator`。

## 姿态环和角速度环

速度 PID 算出的总力不一定竖直向上。飞控先把期望总力方向当作机体应该对齐的 Up 方向，再结合目标偏航决定机头朝向。

当前实现使用约化姿态控制：

1. 优先让机体 Up 轴对准期望推力方向，保证升力和水平加速方向正确。
2. 再按 `YawWeight` 处理偏航误差。
3. 姿态误差先变成目标局部角速度，不直接变成电机值。
4. Pitch、Yaw、Roll 三个角速度 PID 再计算目标角加速度。

这样做的直观意义是：机体倾斜或动力受限时，先保证“不要掉下去、倾斜方向正确”，偏航可以暂时少转一些。

姿态计算使用四元数和向量方向，不用 Euler 角直接相减，因此不会在跨越 180 度时突然得到错误的大角度误差。

关键入口：`DronePhysicalControlMath.CalculateReducedAttitudeRate()`、`DroneAttitudeMath`。

## 惯性张量：同样的转动要求不一定需要同样的力矩

角速度 PID 得到目标角加速度后，还不能直接把这个数当作力矩。重心位置、机臂结构和载荷分布都会影响机体转动难度。

刚体转动力矩可以写成：

`τ = Iα + ω × (Iω)`

`τ` 是所需力矩，单位为牛顿·米（N·m）；`I` 是 Rigidbody 惯性张量；`α` 是目标角加速度；`ω` 是当前角速度。

前半部分表示“让当前质量分布产生目标角加速度需要多少力矩”，后半部分补偿旋转刚体本身的耦合效应。代码会把角速度和角加速度转换到 Rigidbody 的惯性主轴计算，再转回机体局部坐标，并没有假定机体三个方向的惯性相同。

所以换模型、移动 Collider、改变装备质量分布都有可能改变姿态手感。视觉换皮时必须保护物理挂点和质量契约，不能只看模型外观是否正确。

关键入口：`DronePhysicalControlMath.CalculateLocalTorque()`。

## 四旋翼控制分配

飞控现在已经知道“需要多少总推力”和“需要多少 Pitch、Yaw、Roll 力矩”，下一步才是决定四个 Rotor 各出多少力。

四个 Rotor 的推力可以写成一个短式子：

`f = A⁻¹u`

`f` 表示 FL、FR、RL、RR 四个旋翼各自的推力；`u` 表示期望总推力和三个方向的机体力矩；`A` 是控制效率矩阵。

`A` 不是写死的正负号表。`QuadrotorControlAllocator` 会读取每个 Rotor 相对重心的真实位置、推力方向和 CW/CCW 旋向：

- Rotor 位置与推力方向的叉积决定它能产生多少 Pitch 和 Roll 力矩。
- CW/CCW 方向与反扭矩系数决定它对 Yaw 的贡献。
- 四个 Rotor 的推力合计形成总升力。

### 为什么要处理饱和优先级

单个 Rotor 的推力只能在零到最大值之间。当某个电机已满转或降到零时，总推力、Pitch、Roll、Yaw 不一定能同时满足。

当前分配策略是：

1. 先求总推力和 Pitch/Roll。
2. 通过整体平移四路推力，尽量放入可用范围。
3. 仍超限时缩放 Pitch/Roll 请求。
4. 在剩余空间内追加 Yaw，空间不足时优先缩放 Yaw。
5. 计算实际实现的力和力矩，把残差方向反馈给 PID。

玩家会看到：动力接近极限时，无人机优先保住升力与基本姿态，转头速度可能先下降，而不是为了完成偏航导致某一侧失去升力。

关键入口：`QuadrotorControlAllocator`、`DroneAllocationResult`、`DroneControlSaturation`。

## 电机响应和 Rotor 物理

控制分配得到的是本帧希望达到的 Rotor 推力，真实电机不会瞬间从停转跳到满转。电机输出按一阶响应逐步接近目标，可以写成：

`c₂ = c₁ + r(cₜ - c₁)`

`c₁` 是当前归一化输出，`c₂` 是下一物理帧输出，`cₜ` 是目标输出；响应比例 `r` 由固定步长 `Δt` 和电机响应时间 `T` 决定：

`r = 1 - e^(-Δt/T)`

响应时间越大，电机追上命令越慢，机体动作也会更柔和但更迟钝。实际转速再由 `n = c nₘ` 得到，其中 `nₘ` 是最大 RPM。

电机输出换算为 RPM 后，单个旋翼推力按转速平方增长：

`F = k_fn²`

`F` 是单个旋翼推力，`k_f` 是推力系数，`n` 是旋翼转速。转速翻倍时，推力不是翻倍，而是按平方关系增长。

`DroneRotorActuatorRuntime` 对四个 Rotor 分别执行：

- 在 `DroneRotor.ForceTransform` 的真实世界位置调用 `Rigidbody.AddForceAtPosition`。
- 沿显式物理推力轴施加推力；正式机体固定为根节点局部 `+Y`。
- 根据 CW/CCW 方向调用 `Rigidbody.AddTorque` 施加正负相反的反扭矩。
- 把实际 RPM 交给视觉桨叶；视觉旋转不参与物理计算。

四点施力会自然产生 Pitch 和 Roll，反扭矩差会产生 Yaw。位移、转动、碰撞和 Joint 反作用最终都由 Rigidbody 求解。

关键入口：`DroneMotorModel`、`DroneRotorActuatorRuntime.StepAndApply()`、`DroneRotor`。

## 飞控状态和安全保护

`DroneFlightOperationState` 把飞行生命周期分成：

- `Disarmed`：电机锁定，不产生飞行施力。
- `ArmedIdle`：已经解锁，但仍处于地面待机。
- `TakingOff`：自动提高目标高度，达到高度且垂直速度稳定后进入 Flying。
- `Flying`：正常手动或自动飞行。
- `Landing`：按配置速度逐步降低目标高度，落地稳定后自动锁定。
- `Fault`：配置、Rotor 几何、动力或安全状态不允许继续施力。

落地判断来自碰撞接触法线；只有存在明显向上的接触法线才认为机体受到地面支撑。机体 Up 方向与世界 Up 的点积持续低于安全值时，飞控会锁定并进入 Fault，避免翻覆后电机继续全力推地。

锁定、重置或销毁时还会清理 PID 历史、电机状态、目标速度和视觉 Rotor 相位，避免上一次飞行状态泄漏到下一次控制会话。

关键入口：`DroneFlightController.UpdateOperationState()`、`SetArmed()`、`ResetFlightState()`。

## 三机型不是三套飞控

当前三个选项共享同一个基础飞行平台，差别来自保存态装备组合，而不是复制三份 Controller。

| 机型 | 保存资源 | 组成与职责 |
|---|---|---|
| 纯无人机 | `DronePrototype.prefab` | 基础 Rigidbody、四个 Rotor、飞控、输入、镜头、遥测、起落架和装备宿主；同时是其它机型的基础 |
| 抓斗无人机 | `DroneGrappleVariant.prefab` | 基础机体加嵌套的 `DroneGrappleEquipment.prefab`；抓斗通过 Joint、抓取约束和质量反馈参与物理 |
| 渔叉无人机 | `DroneHarpoonVariant.prefab` | 基础机体加嵌套的 `DroneHarpoonEquipment.prefab`；包含真实弹体、碰撞、绳索、张力和载荷反馈 |

`DroneFlightSceneCoordinator` 根据 `DroneVehicleKind` 选择三个成品地址之一。Prefab 实例先在失活状态完成出生位置和运行引用配置，`DroneFlightVehicleAssembler` 注入相机、输入、控制会话、装备宿主和遥测引用，然后才激活机体。

装配器不会在运行时创建抓斗或渔叉结构，也不会把纯无人机临时改造成装备机。装备 Prefab 和组合 Variant 由 Editor Builder 预先建立并保存，便于在 Inspector 中检查，也让资源引用和物理层级能被契约测试锁定。

三机型共同使用：

- `DroneFlightController` 和全部 PID。
- `QuadrotorControlAllocator`。
- `DroneMotorModel` 与四个 `DroneRotor`。
- Cine、Normal、Sport 档位。
- CameraRig、HUD、调试显示和遥测。

装备只能改变实际质量、约束、碰撞和外力，不能修改这条飞控核心。

关键入口：`DroneVehicleKind`、`DroneFlightSceneCoordinator`、`DroneFlightVehicleAssembler`。

## 装备与载荷边界

`DroneEquipmentHost` 是机体与装备之间的统一入口：

- `IDroneEquipmentModule` 提供装备操作、状态快照、运行时配置同步和清理。
- `IDroneExternalMassProvider` 汇总飞控需要读取的承载质量。
- `IDroneAimingEquipment` 只服务需要专用玩家瞄准镜头的装备。
- `IDroneAutomatedAimingEquipment` 允许捕鱼演出提供世界坐标目标，但不改变玩家原有手动入口。

抓斗和渔叉可以：

- 保持独立 Rigidbody。
- 使用 Joint、碰撞和真实约束。
- 向机体施加反作用。
- 把当前受支持载荷反馈给飞控。

它们不能：

- 直接移动机体 Transform。
- 覆盖机体 Rigidbody 速度。
- 修改 PID、控制分配、电机模型或 Rotor 输出。
- 用视觉绳、HUD 数字或动画充当物理状态。

捕鱼演出也是同一边界：贝塞尔路径属于特定演出编排，自动驾驶只产生目标；渔叉命中后，弹体、FixedJoint、绳索张力和载荷质量继续通过真实物理链工作。

## 配置、镜头和遥测

可跨场景复用的调参值进入独立 ScriptableObject，场景结构引用继续保存在 Scene/Prefab，运行时可推导或由装配器注入的引用不重复序列化。

| 配置 | 负责什么 |
|---|---|
| `DroneFlightConfig` | 机体质量、动力、电机、PID、安全限制、响应档位、自动起降和起落架 |
| `DroneCameraConfig` | 第三人称、环绕、云台、避障、平滑和 FOV |
| `DroneInputConfig` | 键盘回退速度、重载长按等输入参数 |
| `DroneAutopilotConfig` | 通用自动驾驶速度、位置增益和抵达容差 |
| `DroneDiagnosticsConfig` | 遥测缓存和界面刷新频率 |
| `DroneGrappleConfig` / `DroneHarpoonConfig` | 两类装备自身的结构与玩法参数 |
| `DroneFishingMissionConfig` | 捕鱼演出的区域、节奏、超时和固定机位参数 |

CameraRig 只修改相机和云台表现，不写机体 Rigidbody。Telemetry、HUD 和 F2/F3 调试显示读取控制目标、实际速度、Rotor 推力、载荷和饱和状态，但不会反向驱动飞控。

## 核心与宿主适配边界

DroneFlight 仍通过 `DroneFlight.asmref` 归属 `Hotfix.dll`，没有新增 `DroneFlight.Runtime` 或 `DroneFlight.Editor` 程序集，也没有改变 HybridCLR 热更新列表和 DLL 装配顺序。

运行时代码位于 `Assets/Scripts/Hotfix/Demos/DroneFlight/`：

- `Control`、`Physics`、`Input`、`Camera`、`Equipment`、`Payload`、`Telemetry`、`Vehicle`、`Cruise`、`Runtime` 是可脱离 SleepyDemos 宿主理解的核心。
- `Adapters/Scene` 负责资源加载、场景导航和 Hub 生命周期。
- `Adapters/UI` 负责正式 UIManager View 和强类型 ViewData。
- `Adapters/Fishing` 负责捕鱼演出。
- `Adapters/Experience` 负责项目内遥控器接管体验。

核心目录不能引用 `Core.Runtime`、`UIManager`、`ResourceServices`、`GameSceneNavigator` 或 Adapters 具体类；该边界由 `DroneFlightPortabilityBoundaryTests` 扫描锁定。

迁移到新宿主时，应保留飞控、装备、配置和成品 Prefab，替换 `Adapters/` 中对应的资源、UI、导航与演出接入。当前边界是“降低宿主耦合”，不是承诺复制两个目录后零修改运行。

## 当前能力与明确边界

当前仓库已经具备：

- 纯无人机、抓斗无人机、渔叉无人机三个成品机型。
- 手动飞行、Cine/Normal/Sport、位置保持和定高。
- 自动起飞、自动降落和通用航点巡航。
- 第三人称、环绕、云台、机腹等镜头能力。
- HUD、F2/F3 调试显示和遥测。
- 四爪抓斗、向下渔叉、真实约束和动态载荷反馈。
- 从 Hub 进入、Editor 直开 Demo 岛和独立场景 Bootstrap。
- 使用真实渔叉和载荷返航的捕鱼演出 MVP。

这些能力不等于已经具备：

- 完整环境感知和避障路径规划。
- 网络同步或多人所有权模型。
- AI 飞行策略或训练接口。
- PX4、ArduPilot、SITL/HITL 等科研或工程飞控仿真。
- 任意项目零修改迁移。
- 任意新装备自动兼容。

新增 AI、网络或回放控制时，从统一控制来源扩展；新增路线事件时扩展 Cruise；新增装备时实现装备与质量接口；接入新项目时替换 Adapters。不要为了新需求绕过现有飞控链建立第二套运动系统。

## 修改原则

- 不用 Transform、直接速度或中心总推力替代真实四旋翼链。
- 不用超大阻尼掩盖控制器或 Joint 不稳定。
- 不因视觉换皮移动 Rotor 施力点、装备挂点、Collider 或质量契约。
- 不在运行时动态重建正式机体和装备组合。
- 不让适配层依赖反向进入可迁移核心。
- 算法原理写在本文；模块入口和生命周期写在模块文档；操作步骤写在 runbook；历史替代原因写在 history。
- 源码、Prefab、Unity 编译和运行验证才是实现证据，文档与静态搜索不能替代它们。
