# 无人机飞行仿真 Codex Goal

## 使用方式

将下方“Goal 提示词”完整复制到新的 Codex Goal。执行时以本文件和
[`实施计划`](../plans/2026-08-13-drone-flight-simulation.md) 为共同约束：本文件定义最终目标、边界与验收，实施计划定义阶段顺序和每个门禁的证据。

本 Goal 是持续任务，不授权一次性跳过物理验证直奔美术效果，也不授权自动提交或推送 Git。

## Goal 提示词

```text
在 D:\Unity\Unity_Project\SleepyDemos 中设计并实现一个可从 Hub 进入的 `drone_flight` 无人机 Demo。全程自主推进，持续到计划规定的阶段目标真正完成；遇到一般实现选择时先检查现有代码、Prefab、场景、包版本和项目规范并作出可逆的合理决定，不要把普通技术选择反问给用户。只有缺少会显著改变产品方向的素材授权、视觉方案或外部权限时才请求用户确认。

开始前必须完整阅读：

1. `AGENTS.md`
2. `README.md`
3. `docs/README.md`
4. `docs/architecture/overview.md`
5. `docs/architecture/code-layout.md`
6. `docs/architecture/hotfix-boundary.md`
7. `docs/architecture/testing.md`
8. `docs/architecture/documentation-rules.md`
9. `docs/runbooks/add-demo.md`
10. `docs/runbooks/run-unity-tests.md`
11. `docs/superpowers/specs/2026-08-13-drone-flight-simulation-goal.md`
12. `docs/superpowers/plans/2026-08-13-drone-flight-simulation.md`

在执行任何阶段前，先检查工作树并保护用户已有改动。当前任务不得修改无关字体材质、URP 项目设置、用户测试场景或其它已有脏文件。除非用户另行明确授权，不得 commit、push、改写历史、创建 PR 或 stage 无关文件。

### 产品目标

实现的不是 FPV 穿越机，也不是简单的“飞行摄像头”，而是一台适合游戏玩法、具有真实四旋翼受力和级联 PID 飞控的大疆式消费级航拍无人机：

- 无人机由四个独立旋翼在各自位置产生推力和反扭矩，禁止直接设置 Transform 位置/旋转伪造主要飞行。
- 电机具有转速上限、响应延迟和可观察输出。
- 使用可测试的四旋翼混控器和级联控制器，实现角速度、姿态、高度、水平速度和位置保持。
- 默认 Position 模式应具备松杆回正、悬停、定高和水平制动；另提供 Cine、Normal、Sport 参数档位，并为 Attitude/Rate 调试模式保留清晰边界。
- 载荷、碰撞、外力和重心变化必须通过 Rigidbody/Joint 真实影响机体，飞控再对扰动进行补偿。
- 最终支持人物拿起带屏遥控器、屏幕开机显示无人机 RenderTexture、画面无缝放大接管到无人机主摄像机、飞行 HUD、PC/手柄/移动端输入、多机位切换。
- 通过统一机腹挂载接口加入机械抓钩，目标继续保留独立 Rigidbody；为后续无人机钓鱼的鱼线释放器预留扩展边界。

参考体验是用户提供的 Bilibili 视频 `BV1FWHczGEnE` 中展示的“纯物理四轴无人机 + 勾爪 + 载荷扰动”效果。只把视频作为行为参考，不臆测或复制作者未公开的内部实现。所有外部源码和资产在使用前必须核对许可证并记录来源；许可证不明确时不得导入。

### 实施策略

严格执行 `docs/superpowers/plans/2026-08-13-drone-flight-simulation.md` 的阶段门禁：

1. 基线审计与模块骨架。
2. 基础几何体四旋翼、纯数学控制核心和调试遥测。
3. 独立旋翼物理、Rate/Attitude 级联 PID 和稳定裸机飞行。
4. 高度、速度、位置保持及 Cine/Normal/Sport 模式。
5. 输入、自动起降、相机、云台、RT 转场与 HUD。
6. 物理抓钩、载荷扰动和抓取运输玩法。
7. 正式模型评估或 Blender 制作。
8. 无人机钓鱼扩展。

每个阶段只有在对应自动化测试、Unity 编译、Console 检查和规定的 Play Mode 手动验证都有证据后才能进入下一阶段。若阶段门禁失败，继续诊断和修复当前阶段，不要用后续表现层掩盖问题。

第一轮执行的强制里程碑是“基础几何体裸机 PID MVP”：完成阶段 1 至阶段 3并验证稳定飞行。到达该里程碑后输出测量数据、测试结果、仍存在的物理限制和下一阶段建议，再继续阶段 4。不要在裸机飞稳前投入正式无人机建模、复杂遥控器动画、鱼线或钓鱼业务。

### 架构边界

- 无人机是具体 Demo 玩法，主要代码放在 `Assets/Scripts/Hotfix/Demos/DroneFlight/`。
- Demo 专属场景、Prefab、材质、配置和临时基础几何体放在 `Assets/LoadResources/Demos/drone_flight/` 的规范子目录。
- 从现有 `MainMenuView`/Hub 接入，不修改 Core 启动主链路。
- 不允许 `Core.Runtime` 反向依赖 Hotfix。只有在至少两个真实 Demo 已稳定复用且经过边界评审后，才允许把能力提取到 Core；本 Goal 默认不做该上提。
- 纯数学 PID、混控、电机模型即使是可复用算法，首版仍归属 DroneFlight 业务模块，避免过早污染 Core。
- 不新建第三套生产程序集或测试体系。自动化测试只使用项目现有 Core.Tests / Hotfix.Tests 逻辑域，并按 EditMode/PlayMode 物理拆分；若确实需要 `Hotfix.Tests.PlayMode`，必须符合现有测试架构且 `autoReferenced=false`。
- 场景和 Prefab 应通过 Unity Editor/项目现有工具创建与保存，不手工拼写 Unity YAML，不编辑自动生成的 `.csproj`/`.sln`。

### 物理与控制硬约束

- Unity 世界坐标采用 Y 向上；在模块文档中固定机体轴、电机编号、X 型布局和 CW/CCW 旋转方向，并用测试锁定符号约定。
- 每个旋翼在实际 Rotor Transform 位置使用 `Rigidbody.AddForceAtPosition` 施加机体上方向推力；偏航使用成对反向的旋翼反扭矩。
- 电机响应使用确定的离散模型，输入输出限幅；总推力不足时必须可诊断，不得偷偷提高 Rigidbody 重力补偿。
- PID 必须具备输出限幅、积分限幅/抗饱和、积分复位和可配置 D 项低通；不得直接用 Euler 角相减处理跨 180 度姿态误差。
- 使用 Rigidbody 的速度、角速度和姿态作为反馈；所有物理执行在 FixedUpdate 或明确的固定步进中，控制器使用实际 fixedDeltaTime。
- 混控器必须处理饱和/反饱和，优先保留稳定所需的姿态控制余量，并通过单元测试验证四电机对 Throttle/Roll/Pitch/Yaw 的符号响应。
- 控制参数使用 DroneFlight 专属可序列化配置资产；运行时调参不得偷偷写回资产，保存必须由显式操作完成。
- 视觉螺旋桨转动、云台动画、RT 和 HUD 不得成为物理状态真源。
- 抓取物保持独立 Rigidbody，通过 Joint/约束连接；禁止抓取后直接设为无质量子节点。载荷释放后应自然表现短暂上窜并由高度控制器恢复。

### 美术边界

- MVP 只使用基础几何体，清楚标识机身、四个电机、推力方向、重心和挂载点。
- 一体化无人机模型不是阻塞项：固定机身可保持单网格，四个螺旋桨、云台 Pitch/Yaw、摄像机和抓钩活动爪应使用独立 Transform；必要时可用附加代理网格补足。
- 在物理门禁通过前不得制作正式 Blender 模型。
- 开始 Blender 正式建模前必须先形成外观方案、比例图和部件拆分清单，并取得用户明确视觉批准。
- 用户提供模型后先审计许可证、层级、子网格、骨骼、动画、轴心、比例、材质和可拆分性，再决定适配、补件或重做，不能因“网格一体”直接判废。

### 测试与验证

- 先写纯算法 EditMode 测试，再写最小实现；物理闭环使用少量确定性 PlayMode 测试和实际 Game View 验证共同确认。
- 首次调用 UnitySkills REST 前必须从 `%USERPROFILE%/.unity_skills/registry.json` 按绝对项目路径确认实例端口；registry 不可信时扫描 8090-8100 `/health`，核对 projectName、unityVersion、instanceId。禁止写死端口。
- Unity Editor 已打开时禁止启动第二个 BatchMode Unity；禁止用 dotnet build、msbuild 或生成解决方案作为编译证据。
- Play 模式改 C# 后先按 `.codex/skills/hotreload-log/SKILL.md` 检查 Hot Reload Timeline/patches.json；只有关键修改未应用或出现 unsupported changes 时才退出 Play Mode 重新编译。
- 默认只运行当前任务直接相关的精确方法或测试类，不自动运行全量 Core.Tests、全量 EditMode/PlayMode 或第三方测试。
- 每个阶段报告实际测试范围、是否运行全量测试、Unity 编译状态、Console Error/Exception 数量和未覆盖的手动行为。
- 执行 target-scoped `git diff --check`；不要把用户已有脏文件混入验证或报告为本任务改动。

裸机 PID MVP 的初始验收目标如下。若 Unity/PhysX 实测证明阈值不合理，只能基于曲线和测量证据在计划文档中显式修订，不能静默降低：

- 水平静止起飞到 1.5 m，进入稳定段后连续悬停 10 s，无 NaN/Infinity，无触地或翻转。
- 稳定段高度误差绝对值通常不超过 0.20 m，Roll/Pitch 误差通常不超过 3 度。
- 施加一次计划规定的水平冲量后，6 s 内恢复到目标高度 ±0.25 m、Roll/Pitch ±5 度，并保持有限电机输出。
- 增加机体质量 20% 的中心载荷后允许短暂下沉，但 8 s 内恢复到目标高度 ±0.30 m；若推力重量比不足，应明确报告超载而不是数值发散。
- 释放中心载荷后允许短暂上窜，随后恢复；整个过程 Rigidbody、PID 和电机输出无 NaN/Infinity。

### 文档与完成定义

实现过程中同步维护：

- `docs/modules/drone-flight.md`：职责、代码位置、控制链路、生命周期、配置、挂载边界和验证重点。
- `docs/runbooks/tune-drone-flight.md`：调参顺序、遥测读取、振荡/漂移/饱和/Joint 抖动排障。
- `docs/README.md`：导航。
- 若 Demo 接入方式本身发生变化，更新 `docs/runbooks/add-demo.md`；否则不要无关改写。

完成每个阶段时至少说明：

- 改动位于 Hotfix、Demo 资源目录、测试目录还是文档目录。
- 创建/修改了哪些真实入口，哪些内容明确延后。
- 文档是否同步以及原因。
- 自动化测试的精确范围和结果；是否执行过全量测试。
- Unity 中完成了哪些手动验证、还需要用户观察什么。

最终完成定义：用户可以从 Hub 进入 DroneFlight Demo，经历无人机地面待机和遥控器接管流程，使用键鼠或手柄稳定操控具有真实四旋翼推力与级联 PID 的无人机，切换云台/第三人称/固定部位视角，用物理抓钩抓取、运输和释放不同重量物体；系统具有可观察遥测、可维护配置、测试和文档，并为移动双摇杆和无人机钓鱼提供明确扩展点。任何未实际验证的效果必须明确标注，不得把静态检查当成运行验证。
```

## 当前明确延后

以下事项不属于裸机 MVP，也不能在前三阶段顺手实现：

- 正式 Blender 无人机、角色手部和遥控器成品美术。
- 完整鱼线柔体、水面浮力、鱼咬钩和钓鱼结算。
- GPS、IMU、气压计的工程级噪声和传感器融合。
- PX4、ArduPilot、ROS2、MAVLink、SITL/HITL。
- 联机同步、移动端性能专项优化和商业化资产导入。
- 单桨失效、桨洗、地面效应、电池电压下降等增强物理；先作为后续候选，不进入首轮门禁。
