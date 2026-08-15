# DroneFlight 设计演进与决策记录

## 用途

本文只记录影响长期维护的改版原因、被替代方案和仍有效约束。当前实现以 [DroneFlight 模块说明](./drone-flight.md) 为准，具体操作以相关 runbook 为准。

## 2026-08-13：飞行仿真初版

提交 `5fa86eb` 建立了 DroneFlight Demo 岛、四个 Rotor 独立施力、PID、电机模型、相机、输入、遥测、吊挂原型和第一批自动化测试。

关键决策：

- 使用 Rigidbody 与四个真实施力点，不采用中心总推力动画方案。
- 飞控核心用可独立测试的数学类型承载，MonoBehaviour 负责与 Unity 物理桥接。
- Demo 从一开始同时维护模块文档、调参 runbook、Goal 和实施计划。

后来被替代：

- 早期 `DroneFlightDemoLauncher`、`DroneHudPresenter` 和单一原型抓钩不再是正式入口。
- 初版几何占位机体已被正式 FBX 与确定性 Builder 替代。

## 2026-08-14：试玩、六轴飞控与吊挂载荷重构

提交 `179ea30`、`d0b0fdf` 根据试玩反馈完善飞行体验，并重构物理抓斗、载荷交接、控制分配和自动载重调校。

关键决策：

- 控制链固定为输入整形、位置/速度、推力方向姿态、角速度 PID、物理控制分配、电机、四点施力。
- 额定载重影响动力调校，最大载荷倍率只影响抓斗门禁，两者不与 Cine/Normal/Sport 混用。
- 载荷质量按真实约束竖直拉力平滑进入飞控前馈，不在抓取瞬间直接加入完整质量。
- 释放载荷不清零速度或 RPM，让无人机按真实剩余电机转速短暂上浮并恢复。

后来被替代：

- 旧卷扬和单抓钩结构被短行程四爪抓斗替代。
- 飞控主动防摆不再由装备宿主提供；抓斗的摆动只由装备内部 Joint 限位和被动阻尼处理。

## 2026-08-15：正式机体与模块化装备

提交 `a359fa8` 引入正式 Blender/FBX 机体、六套 URP 材质、三级云台、真实起落架节点、独立抓斗/渔叉 Prefab 和三个成品机型；提交 `467634f` 将测试统一迁到项目标准测试程序集。

关键决策：

- `DronePrototype` 同时是共享飞行基础和纯无人机成品；装备机型是编辑期保存的 Prefab Variant。
- 运行时只选择并实例化成品 Prefab，不调用 Builder，也不动态拼装装备。
- FBX 导入节点的轴转换不能充当物理推力轴；物理推力固定使用机体根局部 `+Y`。
- UI 通过正式 UIManager 和强类型 ViewData 工作，生成的 `*Component.cs` 不手改。
- 所有自动化测试统一位于 `Assets/Scripts/Tests/EditMode|PlayMode/Demo/DroneFlight`。

## 2026-08-15：代码治理与迁移准备

本轮把机体同步装配从 SleepyDemos 场景事务中提取为纯 Unity/DroneFlight 协作者，将旋翼执行细节从飞控编排器中分离，并建立 Builder、测试共用的机器模型契约。

新增长期规则：

- 可迁移核心不能引用 UIManager、ResourceServices 或 GameSceneNavigator；这些依赖只允许出现在明确的宿主适配器中。
- 三套配置 Inspector 必须共用互斥语言状态，并保证所有字段、Tooltip 和错误诊断完整双语。
- 模型层级、轴向、材质槽和物理挂点只在 `DroneFlightModelContract` 定义；文档解释语义，测试执行门禁。
- 当前不承诺只复制两个目录即可零修改运行；迁移时按 [迁移 DroneFlight](../runbooks/migrate-drone-flight.md)替换宿主适配点。

## 2026-08-15：移除原型期兼容层

旧六爪抓钩、卷扬、软抓取、悬挂遥测、主动防摆入口和旧重置协调器已全部退出当前三机型链路，因此不再保留空实现组件或零值接口。腹部装备输入正式统一为 `DroneEquipmentInput`，当前场景、配置和三个成品 Prefab 直接使用新结构。

长期规则：Demo 阶段不维护已废弃序列化字段和类名的向后兼容。删除或重命名字段时同步更新 Builder、资产 YAML 和契约测试；历史原因只记录在本文，不留在运行时代码中。

## 始终有效的禁区

- 不用中心总推力替代四个 Rotor，不用高阻尼掩盖振荡。
- 不因视觉换皮移动施力点、挂点或 Collider 契约。
- 不在运行时动态重建正式机体或装备。
- 不直接修改生成 View Component，不让 Core 反向依赖 Hotfix。
- 不把静态检查当作 Unity 编译、Prefab 引用、物理行为或视觉验收。
