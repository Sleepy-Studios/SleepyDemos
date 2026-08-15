# DroneFlight 正式模型契约

## 目标

正式模型可以持续换皮和扩展，但不能破坏飞控、起落架、云台和装备挂载使用的结构语义。机器真源为 `DroneFlightModelContract`；`DroneFlightMechanismBuilder` 与契约测试共同读取它，本文只解释维护意图。

## 坐标和根节点

- Blender 使用米制；当前源文件前向按 Blender `-Y`，FBX 导出为 `-Z Forward / Y Up`。
- Unity 机体坐标固定为 `+X` 右、`+Y` 上、`+Z` 前，正式 Mesh 必须应用 Scale 且无负缩放。
- FBX 标准轴转换产生的节点旋转只负责视觉坐标承载，不能直接作为物理推力方向。
- `DronePrototype` 根的局部 `+Y` 是四个 Rotor 的唯一物理推力轴。

## 14 个正式导出对象

- 一个机体 `Airframe`。
- 四个轮毂：`RotorHub_FL`、`RotorHub_FR`、`RotorHub_RL`、`RotorHub_RR`。
- 两个共享桨叶源：`RotorBlade_CCW`、`RotorBlade_CW`。FL/RR 使用 CCW，FR/RL 使用 CW。
- 四个起落架：`LandingGear_FL`、`LandingGear_FR`、`LandingGear_RL`、`LandingGear_RR`。
- 三级云台：`GimbalYaw/GimbalPitch/CameraBody`，Pitch 必须是 Yaw 子节点，CameraBody 必须是 Pitch 子节点。
- `CameraBody` 的可见镜面朝本地 `-Z`，该方向是 Gimbal 画面的唯一光轴；`GimbalPitch +Z` 只是层级坐标轴，不能直接作为输出 Camera 的观察方向。

不要导出预览相机、灯光、地面、标注、辅助 Empty 或第二套 Rotor/起落架/云台包装层。隐藏的两套桨叶源仍必须进入 FBX。

## 材质槽

正式 FBX 只使用以下槽名，由 Builder 确定性映射到 Demo 私有 URP Lit 材质：

| FBX 槽 | Unity 材质 |
|---|---|
| `MAT_Graphite` | `DroneGraphite` |
| `MAT_ShellTop` | `DroneShellTop` |
| `MAT_MechanicalBlack` | `DroneMechanicalBlack` |
| `MAT_SafetyOrange` | `DroneSafetyOrange` |
| `MAT_FrontLED` | `DroneFrontLED` |
| `MAT_CameraLens` | `DroneCameraLens` |

未知槽视为错误，不自动创建近似名称。灯带使用 Emission；当前契约不要求自动生成 BaseColor、Normal 或 ORM 位图。

## 物理语义

- 旋翼坐标、起落架铰链/脚底坐标和容差由机器契约提供，换皮不得修改。
- `BellyEquipmentMount` 位于机体局部 `(0, -0.12, 0)`，装备 Variant 通过该挂点绑定。
- 起落架状态保持 `0=放下、1=收起`；Builder 使用契约角 `-67°` 向机臂折叠，四个 Foot 收起后的机体局部高度必须同时升高。
- 抓斗使用默认 `0.08 m` 的单根刚性 `GrappleArm`；吊臂与底座属于同一复合刚体，不得增加独立 Rigidbody。
- 抓斗底座必须通过唯一 ConfigurableJoint 连接主刚体：三个线性自由度和吊臂轴向扭转锁定，前后、左右双轴摆动 Limited。机体侧 `ConnectedAnchor` 永久与 `BellyEquipmentMount` 重合；J/K 只改变抓斗侧 `anchor.y = ArmLength + Travel`，不得移动机体侧受力点。`LiftSleeveVisual` 必须是 `GrappleBase` 子节点并沿其局部 `+Y` 从固定吊臂顶端延伸到 Joint Anchor，不得带 Rigidbody 或 Collider。
- 抓斗张开态有效口径不小于 `0.38 m`，四爪可见结构和非 Trigger Collider 不得低于展开起落架脚底；闭合后允许向下包围载荷。
- 抓斗捕获辅助环与高度标签是纯显示节点，不得带 Rigidbody 或 Collider。
- 停靠渔叉朝机体局部 `-Y`，其可见结构和启用 Collider 必须高于展开起落架脚底；停靠 Rigidbody 禁用插值并在渲染帧贴合 Muzzle，发射后恢复插值。尺寸与同步方式变化不得改写发射冲量或飞控反馈。
- 渔叉发射器并入机体复合 Collider，不得有独立 Rigidbody；弹体仅在离开发射器后成为动态刚体。
- 机体和机臂使用 `CollisionProxies` 下的 BoxCollider；脚部代理跟随真实起落架节点。
- 不给动态机体添加 MeshCollider，不让视觉 Mesh 承担抓取或地面接触代理职责。
- 新增可动节点时先补机器契约、Builder 和测试，再替换 FBX；禁止只在文档中约定。

## 修改流程

1. 使用项目技能 `unity-demo-model-pipeline` 从玩法和物理需求形成建模需求并确认。
2. 修改 Blender 源文件并按固定参数导出 FBX。
3. 核对源侧与项目内 FBX 的 SHA-256。
4. 执行唯一 Builder 入口重建基础机体、装备和 Variant。
5. 运行 `DronePrototypeContractTests` 和直接受影响的 PlayMode 物理测试。
6. 在 Scene/Game 视图检查旋翼、起落架、云台、材质、碰撞和出生净空。

调试步骤见[调试和整定 DroneFlight](../runbooks/tune-drone-flight.md)。
