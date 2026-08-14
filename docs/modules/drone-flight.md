# DroneFlight 无人机飞行仿真

## 职责与边界

DroneFlight 是 `Hotfix` 业务 Demo，提供真实四旋翼飞控、三机型选择、正式 UI 和遥测。飞控继续使用四个 Rotor 独立施力、级联 PID、物理控制分配与一阶电机模型；装备不得修改 PID、Mixer、电机模型、Rotor 施力或 Cine/Normal/Sport 控制规律。

业务代码位于 `Assets/Scripts/Hotfix/Module/DroneFlight/`，业务 Inspector 位于独立 `Hotfix.Editor`。资源位于 `Assets/LoadResources/Demos/drone_flight/`，自动化测试按模式位于 `Assets/Scripts/Tests/EditMode|PlayMode/Demo/DroneFlight`，统一使用 `Tests.Demo` 命名空间。

## 资源结构

```text
Assets/LoadResources/Demos/drone_flight/
├── Scenes/Main.unity
├── Art/
│   ├── Models/DroneFlight.fbx
│   └── Materials/
│       ├── DroneGraphite.mat
│       ├── DroneShellTop.mat
│       ├── DroneMechanicalBlack.mat
│       ├── DroneSafetyOrange.mat
│       ├── DroneFrontLED.mat
│       └── DroneCameraLens.mat
├── Data/DroneFlightConfig.asset
├── Data/Equipment/
│   ├── DroneGrappleConfig.asset
│   └── DroneHarpoonConfig.asset
└── Prefabs/
    ├── DronePrototype.prefab
    ├── DroneGrappleVariant.prefab
    ├── DroneHarpoonVariant.prefab
    ├── Equipment/
    │   ├── DroneGrappleEquipment.prefab
    │   └── DroneHarpoonEquipment.prefab
    └── UI/DroneFlightVehicleSelectView.prefab
```

`DronePrototype` 既是共享飞行基础，也是“纯无人机”选项直接加载的成品 Prefab。抓斗和渔叉分别保存为独立装备 Prefab；`DroneGrappleVariant`、`DroneHarpoonVariant` 是编辑期已组合并保存的机体，内部以嵌套 Prefab 引用对应装备。运行时不会调用 Builder、不会临时创建装备结构，也不会把模块动态挂到基础无人机上。场景不预放活动无人机；选择机型后只实例化一个对应成品 Prefab。

正式视觉模型由仓库外 `F:/个人/DroneFlight/DroneFlight.blend` 维护，FBX 副本进入 Demo 私有 `Art/Models`。模型使用米制、Unity `+X` 右 / `+Y` 上 / `+Z` 前；导出层只有 Airframe、四个 RotorHub、两套 CW/CCW 共享桨叶、四个 LandingGear 和三级云台共 14 个正式对象。Prefab 中 FL/RR 共用 CCW Mesh，FR/RL 共用 CW Mesh，不复制四份桨叶资源。

`DroneFlightMechanismBuilder` 是视觉装配的唯一编辑期入口：它关闭 FBX 动画、灯光、相机和 BlendShape 导入，按 `MAT_*` 槽确定性映射到六个外部 URP Lit 材质，并把完整 FBX 嵌套为 `DronePrototype/DroneModel`。`DroneRotor` 直接挂到四个 `RotorHub_*`，起落架控制器直接引用四个 `LandingGear_*`，CameraRig 直接引用 FBX 内 `GimbalYaw/GimbalPitch`；只在各轮毂下实例化必要的共享 CW/CCW 桨叶，不再维护第二套 Rotor、LandingGear 或 Gimbal 包装层。机体与机臂 BoxCollider 统一收在 `CollisionProxies`，脚底代理跟随真实起落架节点，不使用动态 MeshCollider。灯带材质启用 Emission，本阶段不生成 BaseColor、Normal 或 ORM 位图。

FBX 标准轴转换会在导入层保留坐标承载旋转，因此 RotorHub 的 Transform 朝向不能直接当作物理推力方向。`DroneRotor` 仍直接挂在 RotorHub 上，但由 Prefab 根节点显式提供机体局部 `+Y` 作为物理推力轴；桨叶视觉也绕同一机体轴旋转。Builder 和契约测试同时校验四个施力坐标及推力轴，避免模型能显示但控制分配矩阵失效。

## 进入和 UI 生命周期

1. 正式入口为 `AppEntrance → Hub → DroneFlight`；Editor 也可直接打开 `Main.unity` 后 Play。
2. 场景协调器等待运行时与导航器稳定，通过 `UIManager.ShowAsync<DroneFlightVehicleSelectView, DroneFlightVehicleSelectionData>()` 打开 `Pop/Modal` 机型选择，提供纯无人机、四爪抓斗无人机和渔叉无人机三个选项。
3. 选择后关闭 Pop，通过资源 Loader 直接实例化 `DronePrototype`、`DroneGrappleVariant` 或 `DroneHarpoonVariant`。实例先进入失活的临时父节点以完成安全出生定位和运行时引用配置，但不会在此阶段拼装装备。`SpawnPoint` 只提供地面 XZ 与朝向，根节点高度由四个起落架 `Foot` Collider 的最低点计算并保留 `0.01 m` 净空。
4. 在失活状态完成 Context、装备、Camera 和输入装配，关闭收纳爪/停靠弹体碰撞；激活后经过首个物理步再次强制 `Waiting`、锁定和零初速度，最后才开放 F 输入并显示 HUD。
5. HUD 以 `Decorate/Widget` 打开；F3 调试 View 以 `Tip/Widget` 打开。两者使用强类型 `DroneFlightViewData`，不读取静态 Context，也没有 `BindContext()`。
6. 无人机处于 `Waiting`；按 F 进入第三人称 `Active`，但保持锁定。
7. 长按 R 只发送一次 `ReloadRequested`。场景协调器先按具体实例关闭本会话选择/HUD/F3，再调用 `GameSceneNavigator.ReloadCurrentAsync()`；新场景稳定后重新打开选择，正常 `Canceled` 不记录 Error。

## 配置边界

- `DroneFlightConfig` 只保存无人机本体：动力调校、机体、Rigidbody、电机、PID、Profile、自动起降、起落架、公共镜头/输入/重载参数。
- `DroneGrappleConfig` 只由抓斗 Prefab 引用，保存设备质量、短行程、关节限位与阻尼、四爪驱动、接触门禁、辅助约束、断裂和载荷平滑参数。
- `DroneHarpoonConfig` 只由渔叉 Prefab 引用，保存设备/弹体质量、出膛速度、云台限位、命中规则、绳长、卷线、弹簧阻尼、张力和回收参数。
- 三个配置各自创建运行时副本；Play Mode 修改源资产后只在安全物理步同步，不回写资产、不清零速度或飞控状态。
- 三个自定义 Inspector 均支持中文/English；装备 Inspector 提供普通/高级页，偏好只写本机 `EditorPrefs`。

## 装备公共接口

`DroneEquipmentHost` 通过 `IDroneEquipmentModule` 统一转发装备类型、状态、主操作、收放线、HUD/F3 快照和清理。Host 同时适配既有 `IDroneExternalMassProvider`：

- 纯无人机保留零质量 Host 作为飞控接口适配层，但不包含 `IDroneEquipmentModule`、装备刚体或装备 Collider，HUD 不显示 H/J/K 装备操作。
- 设备质量和当前被装备实际承载的载荷质量进入悬停前馈。
- 抓斗设备质量不占额定载荷或最大载荷门禁；抓取门禁只比较目标 `Rigidbody.mass`。
- 渔叉不因目标过重拒绝命中，过重目标通过真实绳索张力影响无人机。
- 装备不向飞控写目标姿态，也不实现主动防摆。

## 四爪抓斗

抓斗由一个紧凑底座 Rigidbody、四个按 90° 分布的爪 Rigidbody、四个 HingeJoint 和一个连接机腹的 ConfigurableJoint 组成。四爪总质量与底座质量之和始终等于配置设备总质量，默认 `0.05 kg`；物理根统一 `scale=1`。

- J 在约 `0.08 m` 收纳与 `0.26 m` 放下之间切换，物理锚点只在 FixedUpdate 推进。
- 新机体四个脚板最低点约为局部 `-0.236 m`，出生定位额外保留 `0.01 m` 净空。抓斗收纳包围判定使用约 `0.12 m` 半径和 `0.12 m` 半高；设备质量仍为 `0.05 kg`，不进入额定载荷门禁。
- 收纳时锁定角度并关闭爪外部碰撞；完全放下后开放默认 `35°` 摆角与 `±25°` 扭转，并由被动 Drive 衰减摆动。
- 不使用 Transform 跟随、速度清零或 Joint Projection。带载或四爪未张开时拒绝收纳。
- H 只在完全放下后驱动四个 HingeJoint 的限位弹簧开合。
- 同一载荷需被一对非相邻爪稳定接触并位于包围区，才建立辅助 ConfigurableJoint。
- 辅助约束两端使用同一个世界接触质心，角度自由、Projection 关闭；真实爪碰撞负责主要支撑。
- 飞控承载质量由辅助约束的实际竖直拉力换算并平滑，载荷仍受地面支撑且拉力接近零时不会提前加入完整负荷。
- 张开爪只解除约束，不施加冲量、不改速度、不清空 RPM。

## 渔叉

渔叉机使用屏幕中心射线瞄准；发射器云台在配置偏航/俯仰限位内对准目标。超限时准星不可发射。

- H 发射唯一可回收弹体；飞行、命中或悬挂状态再次按 H 会解除并自动回收。
- 弹体为独立 Rigidbody，使用真实重力、Collider 与连续碰撞检测。
- 发射时按 `弹体质量 × 出膛速度` 给弹体冲量，并在同一物理步从发射口向无人机施加等量反向冲量；不增加人为向下力。
- 动态目标用 FixedJoint 连接目标 Rigidbody；静态目标连接世界。锚点在接触点重合，Projection 关闭。
- 绳索为无质量、只受拉的弹簧阻尼约束：松弛不施力，超出目标绳长才向两端施加等量反向力。
- J 缩短、K 增加目标绳长，不瞬移弹体或目标。视觉绳使用无碰撞 Verlet/PBD 下垂表现。
- 未命中达到最大绳长后悬挂；解除后自动卷线，满足停靠位置和相对速度阈值后重新锁回发射器。

## 飞控与遥测

主控制链保持：输入整形 → 位置/速度控制 → 推力方向姿态控制 → 三轴角速度 PID → 物理控制分配 → RPM → 四点施力与反扭矩。装备只通过外部质量接口提供受支持质量。

F3 保留四旋翼独立升力、总升力、重力、目标/实际速度、目标加速度、目标推力、可实现合力和目标力矩绘制，并按机型追加：

- 纯无人机：明确显示无附加模块，装备质量和载荷均为零。
- 抓斗：短行程、四爪接触、辅助约束拉力、真实/受支持载荷。
- 渔叉：瞄准有效性、弹体状态、目标绳长、张力与命中点。

## 边界规则

- 不在玩法或输入代码中直接调用 `SceneManager`；切换和重载统一走导航器。
- 装备模块必须独立保存为 Prefab；装备机体必须保存为基础无人机加嵌套装备的成品 Prefab。运行时禁止动态拼装或调用编辑器 Builder。
- 不修改生成的 `*Component.cs`；View 数据由正式 UIManager 导航事务传入。
- 不使用中心总推力替代四个 Rotor，不用高 Rigidbody 阻尼掩盖振荡。
- 装备内部碰撞必须忽略，装备与载荷/场景碰撞保留。
- 场景卸载、断绳、释放与回收必须清理临时 Joint、碰撞忽略和弹体引用。
- 正式模型节点必须保持旋转已应用、Scale `(1,1,1)` 且无负缩放；四个 Rotor 施力坐标和 `BellyEquipmentMount (0,-0.12,0)` 不随视觉迭代改变。

## 验证

自动化只运行本任务精确类：配置/Prefab 契约、正式 `DronePrototype` 自动起飞、载荷调校、UI 数据时序、场景导航及装备物理。Game View 仍需人工确认四爪抓取手感、摆动衰减、渔叉准星/弹道一致、真实后坐、动态/静态命中和连续回收。

DroneFlight 测试文件顶部必须用中文说明该测试组负责验证什么。`Tests/EditMode/Demo/DroneFlight` 中的单一 `DroneFlightTestDiagnostics` 编辑器回调统一覆盖 EditMode 与 PlayMode，输出 `[DroneFlight测试][开始/结束]` 日志；日志包含测试组目的、完整用例名、中文结果和耗时，不在每个测试方法中重复粘贴 `Debug.Log`。`DroneRotorPhysicsTests` 的合成夹具必须保留可见机身、X 形机臂和四个 CW/CCW 彩色旋翼标记；正式 Prefab 起飞用例直接显示 `DronePrototype`，使维护者在 Scene 视图中能够分辨正在测试的对象和运动结果。可视化节点只属于测试程序集，不得进入生产 Prefab 或运行时代码。

操作和调参见[调试和整定 DroneFlight](../runbooks/tune-drone-flight.md)，Editor 直启见[直接运行 Demo 岛](../runbooks/run-demo-island-directly.md)，进度见[实施计划](../superpowers/plans/2026-08-13-drone-flight-simulation.md)。
