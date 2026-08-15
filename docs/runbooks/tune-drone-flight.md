# 调试和整定 DroneFlight

## 启动方式

- 正式链：运行 `Assets/Scenes/AppEntrance.unity`，从 Hub 进入 DroneFlight。
- Editor 直启：直接打开 `Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity` 后 Play。等待最小宿主初始化完成和机型选择 Pop 出现，不要在初始化前操作。
- Development/Release 不支持 Demo 场景直启，入口仍是 AppEntrance。

## 公共操作

- 选择机型后会在首个物理步直接进入第三人称 Active，但电机仍锁定；`F` 只兼容旧 Waiting 会话，不是正常启动步骤。
- `R`：短按解锁/锁定；长按达到配置时间后重载整个 DroneFlight 并返回机型选择。
- `T/G`：自动起飞/降落。
- `WASD`：水平移动；`Q/E`：偏航；`Space/左 Ctrl`：升降。
- `1/2/3`：平稳（Cine）/普通（Normal）/运动（Sport）。
- `C`：切视角；方向键：云台/环绕；`-/=`：FOV。
- `F1`：收起/展开分类操作面板；默认展开。
- `L`：手动起落架。
- `F2`：Game 与 Scene 视图共享的世界空间受力/运动矢量；显示层会平滑 FixedUpdate 的离散数据，不改变真实物理。
- `F3`：中文原始遥测调试面板。
- `F4`：复制最近遥测摘要。
- `Backspace`：正式链返回 Hub；Editor 直启时进入 AppEntrance，由正式启动链接管。

纯无人机：没有 H/J/K 装备操作，只保留公共飞行、镜头和起落架控制。

抓斗机：`H` 四爪开合；`J` 上收、`K` 下放伸缩吊臂。

渔叉机：`V` 进入/退出机腹向下瞄准，瞄准有效时 `H` 发射；再次 `H` 解除回收，按住 `J` 缩短目标绳长，按住 `K` 增加目标绳长。

## 配置位置

- 无人机：`Data/DroneFlightConfig.asset`。
- 抓斗：`Data/Equipment/DroneGrappleConfig.asset`。
- 渔叉：`Data/Equipment/DroneHarpoonConfig.asset`。

不要在 DroneFlightConfig 中寻找装备参数。三个 Inspector 顶部均可切换中文/English；装备配置另有普通/高级分页。运行时修改只同步到各自运行时副本。

## 工业训练场路线

场地是中心位于原点的 `100 × 100 m` 地坪，四面围墙高 `10 m`。从中心起飞区朝 `+Z` 飞行，依次通过北侧起始门、三柱蛇形区、东北错位墙、东侧低空横梁、东南抬升窗口和南侧连续框架，最后沿西侧地面箭头返回中心。

- 起飞、抓取和投放调试优先留在黄色 8 米安全圈内，避免障碍碰撞干扰载荷调校。
- 低空横梁用于验证精细高度控制；抬升窗口用于验证爬升和下降；连续框架用于验证直线姿态与偏航修正。
- 围墙、门框、立柱和面板使用 BoxCollider；地面标线与安全带无碰撞。发现 MeshCollider 或 ProBuilder 组件时视为烘焙失败。
- ProBuilder 只用于编辑期灰盒制作。交付资源是 `Art/Generated/Arena` 下的 Mesh Asset 和 `Prefabs/Environment/DroneFlightArena.prefab`，运行时不依赖插件，也不需要 FBX Exporter。

## 正式模型重建

开始前先读[DroneFlight 正式模型契约](../modules/drone-flight-model-contract.md)；新建、换皮或扩展模型时使用项目技能 `unity-demo-model-pipeline` 先形成并确认建模需求。

1. Blender 源文件保存在仓库外 `F:/个人/DroneFlight/DroneFlight.blend`，以米为单位；机头按 Blender `-Y`，导出使用 `-Z Forward / Y Up`。
2. 只导出 14 个正式对象到同级 `DroneFlight.fbx`，确认 `RotorBlade_CCW`、`RotorBlade_CW` 即使在 Blender 预览中隐藏也已纳入导出。不要导出相机、灯光、地面、标注、预览旋翼或辅助 Empty。
3. 将同一 FBX 复制到 `Assets/LoadResources/Demos/drone_flight/Art/Models/DroneFlight.fbx`，确认仓库外与项目内文件 SHA-256 一致。
4. 在 Unity 执行 `Tools > SleepyDemos > DroneFlight > 重建基础、装备与组合机体`。工具会更新六个外部 URP Lit 材质、基础视觉和代理碰撞，并重建抓斗/渔叉装备与两个 Variant；不会修改飞控、Rotor 施力点或运行时公开接口。
5. 检查 `DronePrototype/DroneModel` 中完整嵌套的 FBX；ModelImporter 必须启用 `Bake Axis Conversion`，`DroneModel`、`Airframe` 与正式节点不得存在额外 90° 包装旋转或负缩放。`DroneRotor` 应直接位于四个 `RotorHub_*`，四个 Hub 下各有一个共享 Mesh 的桨叶实例，起落架控制器直接引用 `LandingGear_FL/FR/RL/RR`，CameraRig 直接引用 FBX 内 `GimbalYaw/GimbalPitch/CameraBody`。根节点只额外保留 `CollisionProxies` 和必要运行时挂点，不得再出现第二套 Rotor、LandingGear、Gimbal 包装层。四个 Rotor 的物理推力轴继续显式绑定机体根局部 `+Y`；在 Scene 的 Local 模式确认 Yaw `+Y`、Pitch `+X`、CameraBody `+Z`，再把 Yaw 转到左右侧检查模型镜面和 Game 画面同向。

## 飞控调参顺序

1. 先确认四个 Rotor 位置、局部 `+Y` 推力方向和 CW/CCW 旋向。
2. Disarmed 时推力为零；仅增加 Collective 时四电机应等量增加。
3. 依次调 Rate P/D/I，再调 Attitude，再调垂直/水平速度和位置外环。
4. 检查电机饱和与积分抗饱和后，再修改 Profile 的速度、加速度、Jerk、倾角和偏航响应。
5. 额定载重决定动力储备；最大载荷倍率只决定抓斗许可；Profile 决定目标速度，三者不可混用。

任一符号错误时修 Rotor 几何或控制分配，不用负 PID 参数“调回来”。装备异常时不要提高机体 drag/angularDrag 或改 PID 掩盖。

## F2/F3 必看项

F2 用于观察矢量方向和趋势，F3 用于读取未经显示平滑的精确数值。F2 长度经过饱和并限制为屏幕短边的 `22%`，标签会限制在安全区，因此即使极端力值也不应跑出 Game 画面。标签的 TMP Auto Size 必须关闭，基础字号为 `36`，实际显示高度限制为 `18–26 px`（1080p 约 `22 px`）；布局会优先保留总量/加速度，再放置旋翼标签，并避开机体投影和其他标签。不要用视觉长度反推单帧 PID 或电机输出。

- 四电机指令、RPM、单 Rotor 升力与总升力。
- 目标/实际速度、目标加速度、目标推力、可实现合力、重力和目标力矩。
- Roll/Pitch/Yaw 误差、P/I/D、输出和饱和。
- 当前主刚体质量、附加设备质量（固定 `0 kg`）、关节求解质量、真实载荷和飞控受支持载荷。主刚体与空载动态装备质量之和应等于 `BodyMassKilograms`。

抓斗额外观察升降行程、机腹固定锚点、双轴摆动、捕获候选数、FixedJoint 拉力和载荷接入比例。渔叉额外观察机腹准星有效性、弹体状态、目标绳长、实际距离、张力和命中点。

## 四爪抓斗调试

1. 选择抓斗机后确认已经进入 Active、仍为 `DISARMED`，抓斗处于 `Ready`；结构应为机腹固定上座、单根刚性吊臂、底座和四爪，不得残留 `GrappleCableVisual` 或防扭吊缆组件。张开态口径不小于 `0.38 m` 且非 Trigger 爪 Collider 不与地面接触。
2. 按 R 解锁并起飞到稳定悬停，观察抓斗在前后、左右方向自然摆动；底座上应只有一个连接主刚体的 ConfigurableJoint，三个线性自由度和轴向扭转锁定、双轴摆动 Limited。
3. 空抓斗仍应有真实摆动与机体反作用，并由被动阻尼自然衰减；主刚体加底座/四爪动态质量之和必须等于 `BodyMassKilograms`，不能产生额外重量。
4. 按住 J/K，确认额外行程只在默认 `0–0.35 m` 内变化，速度不超过 `0.18 m/s`、约束长度加速度不超过 `0.45 m/s²`；抓斗侧 `anchor.y` 随行程增加，但机体侧 `connectedAnchor` 始终与 `BellyEquipmentMount` 重合，松键保持当前长度。
5. 从侧面观察伸缩套筒：它必须沿抓斗底座局部 `+Y` 与固定吊臂共线，底端接固定吊臂顶端、上端接机腹万向节；抓斗摆动时不得继续保持世界竖直。
6. 观察辅助环：橙色为无候选、绿色为捕获体积中存在 `DronePayload`、红色为下方无有效地面；标签应显示离投射面高度，Carrying 后隐藏。
7. 将底座中心对准 `DronePayload` 后按 H 闭合。目标质心进入底座下方捕获体积时建立临时 FixedJoint；不要求四个爪面同时接触。
8. 载荷仍在地面时，F3 的 FixedJoint 竖直拉力应接近零，飞控受支持载荷不得瞬间跳到完整质量；缓慢抬升时应随真实拉力平滑增加。F2 只用于确认力方向和变化趋势。
9. 张开 H 后载荷自由释放；不能有脚本 AddForce、速度覆写或 RPM 清零。

常见问题：

- 一运行就掉落或爆炸：检查底座/四爪是否先以 Kinematic 完成定位和四个 HingeJoint、一个 ConfigurableJoint 的连接，再统一开放重力；确认资源中没有上一试验版本的 `DroneGrappleCableVisual`，且关节初始两端锚点重合。
- 长期横向摆动：这是机腹万向节的真实自由度；先降低操作突变或缩短行程，不改飞控，也不加入主动防摆。若出现高速发散，检查是否误加了显式弹簧吊缆、防扭矩或 Projection 强拉。
- 抓不到：检查 Hinge 是否闭合、载荷质心是否进入 `GrappleCaptureVolume`、载荷是否同时具备 `DronePayload` 和 Rigidbody；爪面接触不是抓取门禁。
- 升降改变摆动：J/K 会产生符合真实物理的小幅扰动；若机体容易被掀翻，检查是否错误移动了 `connectedAnchor`。当前实现只允许改变抓斗侧 `anchor.y`，机体侧受力点必须固定在机腹。
- 伸缩套筒垂直悬空：检查 `LiftSleeveVisual` 是否错误留在装备根节点。它必须位于 `GrappleBase` 下，并用局部 `+Y` 表示当前额外行程。
- 0.75 kg 被拒绝：检查 DroneFlightConfig 的 `额定载重 × 最大载荷倍率`，抓斗 `0.05 kg` 只是已包含在整机空载质量内的关节求解质量，不占该额度。

## 渔叉调试

1. 选择渔叉机后先等待至少 `2 s`，确认弹体保持 Kinematic 停靠在 Muzzle、无重力、Collider 关闭且绳索隐藏；按 V 后原视角被保存并切到 `BellyCameraMount` 机腹向下视角。
2. 移动鼠标，世界准星只能在 `3 m` 水平半径和局部向下 `25°` 圆锥内变为有效。确认短小锋利弹体整体高于起落架脚底，所有 MeshRenderer/LineRenderer 材质无紫红丢失。
3. 瞄准有效后按 H，观察弹体沿机体局部 `-Y` 离膛。`DroneHarpoonConfig` 的“弹体发射冲量”默认 `0.12 N·s`；无人机只承受同值反向冲量，不得出现额外脚本冲量。
4. 动态目标命中后检查 FixedJoint；静态目标命中后检查世界连接。接触锚点应重合且 Projection 为 None。
5. 按住 J/K，只应改变目标绳长。绳松弛时张力为零，拉紧后双方受到等量反向弹簧阻尼力。
6. 再按 H 解除，确认命中 Joint 销毁并回收同一支渔叉；默认 PD 回收速度 `2 m/s`、响应时间 `0.18 s`、最大加速度 `15 m/s²`，停靠前不得瞬移弹体或绕机甩锤。
7. 未命中到达最大绳长时，弹体应悬挂在绳端，可按 H 回收；按 V 退出后恢复进入瞄准前的视角且仍只有一个 AudioListener。

常见问题：

- 准星与弹道不一致：检查瞄准 Camera、gimbal 轴、muzzle 前向和限位，不允许弹道拐弯补偿。
- 一进入玩法弹体就落地：同时检查 Prefab 保存态和 `Stowed` 运行态；两者都必须是 Kinematic、无重力、Collider 关闭，不能只依赖 Host Awake 补救。
- 未发射弹体相对枪口闪动：确认 Stowed 弹体的 Rigidbody 插值为 None，并由 `DroneHarpoonModule.LateUpdate` 贴合 Muzzle；不要把已经稳定的绳索 LateUpdate 当成弹体抖动修复点。
- 后坐过大：直接降低 `DroneHarpoonConfig/弹体发射冲量 (N·s)`；不要修改 Sport 速度、机体质量、PID 或额外乘后坐缩放。
- 绳索紫红或过粗：检查 LineRenderer 是否引用 `DroneMechanicalBlack`，宽度应为 `0.003 m`；停靠状态必须禁用。
- 绳索推开目标：检查只受拉条件；当前距离小于目标绳长时必须零力。
- 绳索隔帧闪烁或端点晃动：确认 `DroneHarpoonModule.FixedUpdate` 只更新物理和目标绳长，LineRenderer 顶点只由 `DroneHarpoonRopeVisual.LateUpdate` 写入；首尾点必须直接使用当前 Muzzle/弹体 Transform，不能平滑端点。停靠态下一渲染帧必须重新禁用 Renderer 并把 `positionCount` 清为 `0`，不能只依赖一次 `SetVisible(false)`。
- 绷紧附近下垂反复跳变：检查绷紧阈值 `5 mm`、释放阈值 `8 mm` 和默认 `0.08 s` 视觉下垂响应；发射、解除、回收和停靠切换都要重置视觉缓存。
- 回收仍绕机甩锤：检查 PD 是否同时使用弹体与 Muzzle 的相对速度并保留最大加速度限制；视觉绳必须每帧仅由两端点和目标绳长重建，不能累积上一帧节点速度。
- 回收残留拉力：检查 hit Joint、碰撞忽略、目标引用和 ropeTension 是否在清理路径统一收口；进入停靠范围后应先关闭弹体碰撞，再等位置/速度双阈值锁回 Muzzle。

## 三机型人工验收

1. 分别从 Editor 直启和 `AppEntrance → Hub` 进入，确认正式 Pop 提供纯无人机、抓斗机和渔叉机三个选项，选择前场景没有活动无人机。
2. 每次选择后只加载一个已保存的成品 Prefab：纯无人机直接使用 `DronePrototype`，另外两种使用各自组合 Variant；运行时不得临时挂载装备。HUD 按键随机型变化，F2 始终可独立显示飞控矢量，F3 始终可独立显示原始遥测面板。
3. 纯无人机确认腹部没有装备模型，HUD 不显示 H/J/K 装备键，空载飞行只使用机体质量。
4. 抓斗连续三次抓取、运输、释放；渔叉连续三次机腹瞄准、向下发射、命中/未命中、放收线、解除回收。
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

重建设备资源执行：`Tools > SleepyDemos > DroneFlight > 重建基础、装备与组合机体`。该工具只在编辑期重建 `DronePrototype`、两个独立装备 Prefab、两个已保存的组合 Variant，并同步 DroneFlight HUD 的保存态布局；不生成重复的纯无人机 Variant，也不覆盖场景。游戏运行时不会执行该工具或动态拼装装备。执行后通过 Unity Test Runner 运行本任务精确测试，不另起 BatchMode，也不使用 dotnet/msbuild。
