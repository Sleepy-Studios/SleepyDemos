# DroneFlight 实现原理与架构设计

## 一句话说明

DroneFlight 不是“直接移动模型”的飞行动画，而是一套由输入或航点生成控制目标、经级联控制器和四旋翼混控计算四个电机推力、最终通过 Rigidbody 真实物理施力飞行的无人机 Demo。

## 分层结构

```text
场景/宿主适配器
  ├─ SleepyDemos UI、资源加载、Hub 生命周期、捕鱼演出
  └─ StandaloneBootstrap（无宿主框架也能启动）
                  ↓
控制来源
  ├─ DronePlayerInput（手动）
  └─ DroneCruiseRunner + DroneMissionAutopilot（自动航点）
                  ↓ DroneControlInput / 高度与偏航目标
DroneFlightController
  输入整形 → 位置/速度外环 → 姿态/角速度内环 → 控制分配
                  ↓ 四路电机指令
电机一阶响应 → 四个 DroneRotor 独立施力与反扭矩
                  ↓
Unity Rigidbody、碰撞、装备约束与载荷反馈
                  ↓
Telemetry / Camera / HUD
```

核心原则是“手动飞行和自动巡航只替换控制目标来源，共用同一套真实飞控与物理链”。自动巡航不会写 `Transform`、不会覆盖刚体速度，因此仍会受到质量、惯性、碰撞、档位和装备载荷的影响。

## 源码边界

所有运行时代码仍由 `DroneFlight.asmref` 归属 `Hotfix.dll`，编辑器代码仍归属 `Hotfix.Editor`。这里采用源码级迁移边界，不新增 `DroneFlight.Runtime` 或 `DroneFlight.Editor` 程序集，也不改变 HybridCLR 热更新列表、DLL 加载顺序和构建基线。

`Assets/Scripts/Hotfix/Demos/DroneFlight` 下分为：

- 核心：`Control`、`Physics`、`Input`、`Camera`、`Equipment`、`Payload`、`Telemetry`、`Vehicle`、`Cruise`、`Runtime`。
- 宿主适配：`Adapters/SleepyDemos`，集中存放 `UIManager`、`ResourceServices`、`GameSceneNavigator`、Hub 生命周期、Editor 直启和捕鱼 UI 等项目专属能力。
- 编辑器：`Assets/Scripts/Hotfix/Editor/DroneFlight`，负责 Inspector 与确定性资源构建。
- 测试：统一位于现有 `Tests.EditMode` / `Tests.PlayMode`，不创建 DroneFlight 专属测试程序集。

核心目录不能引用 `Core.Runtime`、SleepyDemos 资源/UI/导航类型或适配层具体类。该规则由 `DroneFlightPortabilityBoundaryTests` 进行源码扫描锁定。

## 关键运行链路

### 手动飞行

`DroneFlightStandaloneBootstrap` 或 SleepyDemos 场景协调器实例化成品 Prefab，`DroneFlightVehicleAssembler` 在对象激活前注入相机、输入、装备与控制会话引用。进入控制后，`DronePlayerInput` 产生升降、平移、偏航等设备无关输入，飞控在 `FixedUpdate` 中计算四个 Rotor 推力。

在新场景中不能只拖一个原始模型；应引用成品 Prefab，并用 `DroneFlightStandaloneBootstrap` 提供出生点和场景相机。这样无需 SleepyDemos UI、资源系统或 Hub。

### 通用自动巡航

场景放置 `DroneCruiseRoute`，配置至少两个航点、单次/循环/往返、默认速度、等待时间和朝向策略。`DroneCruiseRunner` 推进航点状态，`DroneMissionAutopilot` 把世界坐标目标转换为飞控输入，抵达判定同时考虑水平距离、垂直距离和速度。

捕鱼演出的贝塞尔路径仍属于 `Adapters/SleepyDemos/Fishing`，它是特定演出编排，不等同于通用航点巡航。

### 装备与载荷

`DroneEquipmentHost` 通过公共装备接口转发抓斗/渔叉操作，并把动态装备质量和真实载荷反馈给飞控。装备只能影响物理质量、约束和外力，不能绕过飞控修改 PID、混控或 Rotor 输出。

## 配置策略

可跨场景复用的数值进入独立 ScriptableObject；场景结构引用继续序列化；运行时能够由装配器推导或注入的引用不序列化。

| 配置资产 | 职责 |
|---|---|
| `DroneFlightConfig` | 机体、动力、电机、PID、安全限制、自动起降 |
| `DroneCameraConfig` | 第三人称/环绕镜头、平滑、避障、云台和 FOV |
| `DroneInputConfig` | 键盘回退速度与重载长按时间 |
| `DroneAutopilotConfig` | 自动驾驶速度、位置增益、到达容差 |
| `DroneDiagnosticsConfig` | 遥测缓存和界面刷新频率 |
| `DroneGrappleConfig` / `DroneHarpoonConfig` | 两类装备自身参数 |
| `DroneFishingMissionConfig` | SleepyDemos 捕鱼演出的区域、节奏和固定机位参数 |

Inspector 的策划可调字段使用中文名称和悬浮解释。代码中的英文命名继续遵循 C# 规范，不把显示语言混进 API。

## 可扩展方向

- 新控制来源：AI、网络遥控、录制回放，只需产生统一控制目标。
- 新路线能力：暂停/恢复、动态换点、事件航点、路线资产化、避障规划。
- 新机型：保持基础飞控接口，通过成品 Prefab 和配置差异接入。
- 新装备：实现装备公共接口和质量反馈，不改飞控核心。
- 新宿主：复制核心、配置和资源，重写 `Adapters/SleepyDemos` 对应能力。
- 新遥测出口：文件、训练评分、网络面板均可从 Telemetry 数据源扩展。

## 向项目总监的汇报口径

可以概括为：项目采用“控制来源、飞控算法、物理执行、装备载荷、宿主适配”五层结构。手动输入和自动巡航共用真实四旋翼物理链；配置用 ScriptableObject 集中管理；项目专属 UI、资源与场景导航被隔离到适配层，因此迁移时不需要重写飞控，只需要替换宿主接入。现阶段已支持三机型、手动飞行、自动起降、通用航点巡航、镜头、遥测、抓斗和渔叉，并为 AI、网络控制、新装备与路线事件保留扩展点。

