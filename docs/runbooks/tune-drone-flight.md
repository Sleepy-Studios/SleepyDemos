# 调试和整定 DroneFlight

## 启动方式

- 正式链：运行 `Assets/Scenes/AppEntrance.unity`，从 Hub 进入 DroneFlight。
- Editor 直启：直接打开 `Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity` 后 Play。等待最小宿主初始化完成和机型选择 Pop 出现，不要在初始化前操作。
- Development/Release 不支持 Demo 场景直启，入口仍是 AppEntrance。

## 公共操作

- `F`：Waiting → 第三人称 Active，仍保持锁定。
- `R`：短按解锁/锁定；长按达到配置时间后重载整个 DroneFlight 并返回机型选择。
- `T/G`：自动起飞/降落。
- `WASD`：水平移动；`Q/E`：偏航；`Space/左 Ctrl`：升降。
- `1/2/3`：平稳（Cine）/普通（Normal）/运动（Sport）。
- `C`：切视角；方向键：云台/环绕；`-/=`：FOV。
- `L`：手动起落架。
- `F3`：中文调试面板和 Game View 受力/运动矢量。
- `F4`：复制最近遥测摘要。
- `Backspace`：正式链返回 Hub；Editor 直启时进入 AppEntrance，由正式启动链接管。

纯无人机：没有 H/J/K 装备操作，只保留公共飞行、镜头和起落架控制。

抓斗机：`J` 短行程收纳/放下，`H` 四爪开合。

渔叉机：`H` 发射或解除回收，按住 `J` 缩短目标绳长，按住 `K` 增加目标绳长。

## 配置位置

- 无人机：`Data/DroneFlightConfig.asset`。
- 抓斗：`Data/Equipment/DroneGrappleConfig.asset`。
- 渔叉：`Data/Equipment/DroneHarpoonConfig.asset`。

不要在 DroneFlightConfig 中寻找装备参数。三个 Inspector 顶部均可切换中文/English；装备配置另有普通/高级分页。运行时修改只同步到各自运行时副本。

## 正式模型重建

1. Blender 源文件保存在仓库外 `F:/个人/DroneFlight/DroneFlight.blend`，以米为单位；机头按 Blender `-Y`，导出使用 `-Z Forward / Y Up`。
2. 只导出 14 个正式对象到同级 `DroneFlight.fbx`，确认 `RotorBlade_CCW`、`RotorBlade_CW` 即使在 Blender 预览中隐藏也已纳入导出。不要导出相机、灯光、地面、标注、预览旋翼或辅助 Empty。
3. 将同一 FBX 复制到 `Assets/LoadResources/Demos/drone_flight/Art/Models/DroneFlight.fbx`，确认仓库外与项目内文件 SHA-256 一致。
4. 在 Unity 执行 `Tools > SleepyDemos > DroneFlight > 重建基础、装备与组合机体`。工具会更新六个外部 URP Lit 材质、基础视觉和代理碰撞，并重建抓斗/渔叉装备与两个 Variant；不会修改飞控、Rotor 施力点或运行时公开接口。
5. 检查 `DronePrototype/DroneModel` 中完整嵌套的 FBX；`DroneRotor` 应直接位于四个 `RotorHub_*`，四个 Hub 下各有一个共享 Mesh 的桨叶实例，起落架控制器直接引用 `LandingGear_FL/FR/RL/RR`，CameraRig 直接引用 FBX 内 `GimbalYaw/GimbalPitch`。根节点只额外保留 `CollisionProxies` 和必要运行时挂点，不得再出现第二套 Rotor、LandingGear、Gimbal 包装层。正式 Mesh 节点必须 Scale=1，项目中不得出现动态 MeshCollider 或自动提取位图贴图。四个 Rotor 的物理推力轴必须显式绑定机体根的局部 `+Y`，不得直接使用 FBX 导入节点的轴转换朝向。

## 飞控调参顺序

1. 先确认四个 Rotor 位置、局部 `+Y` 推力方向和 CW/CCW 旋向。
2. Disarmed 时推力为零；仅增加 Collective 时四电机应等量增加。
3. 依次调 Rate P/D/I，再调 Attitude，再调垂直/水平速度和位置外环。
4. 检查电机饱和与积分抗饱和后，再修改 Profile 的速度、加速度、Jerk、倾角和偏航响应。
5. 额定载重决定动力储备；最大载荷倍率只决定抓斗许可；Profile 决定目标速度，三者不可混用。

任一符号错误时修 Rotor 几何或控制分配，不用负 PID 参数“调回来”。装备异常时不要提高机体 drag/angularDrag 或改 PID 掩盖。

## F3 必看项

- 四电机指令、RPM、单 Rotor 升力与总升力。
- 目标/实际速度、目标加速度、目标推力、可实现合力、重力和目标力矩。
- Roll/Pitch/Yaw 误差、P/I/D、输出和饱和。
- 当前机体质量、装备设备质量、真实载荷和飞控受支持载荷。

抓斗额外观察短行程状态、有效爪接触、辅助约束拉力和载荷接入比例。渔叉额外观察云台瞄准有效性、弹体状态、目标绳长、实际距离、张力和命中点。

## 四爪抓斗调试

1. 地面 Waiting 选择抓斗机，确认起落架净空足够，抓斗收纳且爪 Collider 不与地面接触。
2. 按 F、解锁并起飞到稳定悬停，再按 J。抓斗只移动约 `0.18 m`，状态最终变为已放下。
3. 空抓斗放下不应产生质量突变；允许短暂关节扰动，但高度应由既有飞控恢复。
4. 按 H 闭合。只有一对非相邻爪接触同一载荷且目标位于包围区时才建立辅助约束。
5. 载荷仍在地面时，F3 的约束竖直拉力应接近零，飞控受支持载荷不得瞬间跳到完整质量；缓慢抬升时应随真实拉力平滑增加。
6. 张开 H 后载荷自由释放；不能有脚本 AddForce、速度覆写或 RPM 清零。
7. 载荷未释放或爪未张开时按 J，应收到中文拒绝提示且不强制收纳。

常见问题：

- 一放下就爆炸：检查底座/四爪初始 Collider 穿透、共同锚点和内部碰撞忽略；Projection 必须为 None。
- 长期摆动：先调抓斗配置的被动阻尼与最大阻尼扭矩，不改飞控，也不加入主动防摆。
- 抓不到：检查四爪是否完全放下、Hinge 是否闭合、非相邻接触对、包围区和载荷 `DronePayload`/Rigidbody。
- 0.75 kg 被拒绝：检查 DroneFlightConfig 的 `额定载重 × 最大载荷倍率`，抓斗自身 `0.05 kg` 不占该额度。

## 渔叉调试

1. 选择渔叉机后，用屏幕中心准星对准静态墙和动态方块；超出云台限位时准星必须显示不可发射。
2. 按 H 发射，观察弹体沿发射口方向直线离膛。无人机只因等量反向冲量产生真实后坐，不得出现额外向下冲量。
3. 动态目标命中后检查 FixedJoint；静态目标命中后检查世界连接。接触锚点应重合且 Projection 为 None。
4. 按住 J/K，只应改变目标绳长。绳松弛时张力为零，拉紧后双方受到等量反向弹簧阻尼力。
5. 再按 H 解除，确认命中 Joint 销毁并回收同一支渔叉；停靠前不得瞬移弹体。
6. 未命中到达最大绳长时，弹体应悬挂在绳端，可按 H 回收。

常见问题：

- 准星与弹道不一致：检查瞄准 Camera、gimbal 轴、muzzle 前向和限位，不允许弹道拐弯补偿。
- 绳索推开目标：检查只受拉条件；当前距离小于目标绳长时必须零力。
- 回收残留拉力：检查 hit Joint、碰撞忽略、目标引用、ropeTension 和 dock Joint 是否在清理路径统一收口。

## 三机型人工验收

1. 分别从 Editor 直启和 `AppEntrance → Hub` 进入，确认正式 Pop 提供纯无人机、抓斗机和渔叉机三个选项，选择前场景没有活动无人机。
2. 每次选择后只加载一个已保存的成品 Prefab：纯无人机直接使用 `DronePrototype`，另外两种使用各自组合 Variant；运行时不得临时挂载装备。HUD 按键随机型变化，F3 始终保留飞控矢量。
3. 纯无人机确认腹部没有装备模型，HUD 不显示 H/J/K 装备键，空载飞行只使用机体质量。
4. 抓斗连续三次放下、抓取、运输、释放、收纳；渔叉连续三次发射、命中/未命中、放收线、解除回收。
5. 长按 R，确认 Loading 后整个场景重建并回到三机型选择，不复用旧 Joint、载荷或弹体。
6. Backspace：正式链回 Hub；Editor 直启进入 AppEntrance 且不重复注册 Hotfix 系统。
7. 三种机型地面出生后四个脚板最低点应位于地面上方约 `0.01 m`，机体原点距脚底约 `0.236 m`；收纳抓斗和渔叉的可见结构、启用 Collider 不得低于脚底。
8. 分别检查起落架展开/收起不穿过机身、旋翼绕局部 `+Y` 且 CW/CCW 方向正确、云台 Yaw/Pitch 可动、前灯带发光、飞行时代理碰撞无持续抖动。

## 运行自动化测试时怎么看

1. 在 Test Runner 中精确选择 DroneFlight 的测试方法或测试类，不要自动扩大为全部 EditMode、PlayMode 或第三方测试。
2. Console 过滤 `[DroneFlight测试]`。每条用例会先输出测试组目的与完整名称，结束时再输出 NUnit 结果；看到失败后继续查看紧邻的 Assertion 或异常堆栈。
3. 运行 `DroneRotorPhysicsTests` 时打开 Scene 视图并关闭 Gizmos 过滤。合成夹具名为 `DronePhysicsFixture`，包含灰色机身 Cube、两条黑色 X 形机臂，以及橙色 CCW、蓝色 CW 旋翼标记；这些对象没有启用 Collider，只用于观察姿态和轨迹，不参与物理结果。
4. `FormalDronePrototype_AutomaticTakeoffProducesRealLift` 使用真实 `DronePrototype` 模型。观察它从测试出生高度自动上升，测试同时校验真实总推力、高度增量、姿态和有限速度。
5. 如果运动过快看不清，可在测试运行时暂停 Unity 或使用 Test Runner 的单用例执行；不要为了方便观察修改 `Time.timeScale`、飞控配置或断言阈值。
6. 新增 DroneFlight 测试文件时，放入对应 TestMode 的 `Demo/DroneFlight` 目录并使用 `Tests.Demo` 命名空间；先在文件顶部补中文测试说明，再在 `Tests/EditMode/Demo/DroneFlight` 中唯一的 `DroneFlightTestDiagnostics` 描述表登记测试类。该入口同时覆盖 EditMode 与 PlayMode，保证 Console 日志可读。

重建设备资源执行：`Tools > SleepyDemos > DroneFlight > 重建基础、装备与组合机体`。该工具只在编辑期重建 `DronePrototype`、两个独立装备 Prefab 以及两个已保存的组合 Variant，不生成重复的纯无人机 Variant，也不覆盖 UI Prefab 或场景。游戏运行时不会执行该工具或动态拼装装备。执行后通过 Unity Test Runner 运行本任务精确测试，不另起 BatchMode，也不使用 dotnet/msbuild。
