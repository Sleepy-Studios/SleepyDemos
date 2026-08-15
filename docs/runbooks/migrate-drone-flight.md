# 迁移 DroneFlight 到正式项目

## 适用场景

将当前无人机玩法接入具有相近 UI、资源和场景框架的正式 Unity 项目。本流程是迁移准备和适配清单；当前任务不执行真实跨项目复制。

## 主要迁移内容

必须复制：

- `Assets/LoadResources/Demos/drone_flight`
- `Assets/Scripts/Hotfix/Module/DroneFlight`

建议同时带走用于继续维护的内容：

- `Assets/Scripts/Hotfix/Editor/DroneFlight`：双语配置 Inspector 和正式模型 Builder。
- `Assets/Scripts/Tests/EditMode|PlayMode/Demo/DroneFlight`：契约、数学和物理回归测试；在目标项目中合入既有测试程序集，不复制 asmdef 建立第三套测试体系。
- DroneFlight 模块、模型契约、历史与本 runbook。

## 包和渲染依赖

- Unity Input System：玩家、相机、装备和退出输入。
- UniTask：场景会话、UI 和遥控体验异步事务。
- TextMeshPro/UGUI：HUD、F3 和机型选择。
- URP Lit：正式机体六套材质。

先核对目标项目版本和 asmdef 名称，不把 SleepyDemos 的 `.csproj` 当作迁移依据。

## 宿主适配点

| 依赖 | 当前入口 | 目标项目处理 |
|---|---|---|
| 资源实例化 | `IResourceLoader` / `ResourceServices` | 替换场景协调器的加载、释放事务；把实例交给 `DroneFlightVehicleAssembler`。 |
| UI | `UIManager`、View、ViewData | 保留飞控遥测数据，按目标 UI 框架重写三个 View 适配器。 |
| 场景 | `GameSceneNavigator`、`GameSceneId` | 替换进入、重载、退出；玩法和输入代码不得直接调用 SceneManager。 |
| Editor 直启 | `DemoIslandEditorBootstrap` | 正式项目可删除或适配自己的 Editor 启动宿主。 |
| 绑定 | `ComponentItemIndex` | 目标项目有同类绑定组件时重新生成/核对索引，不手改生成 Component。 |
| 字体 | `HarmonyOS_CN` Font Asset/Material | 复制字体依赖或在三个 UI Prefab 中替换为目标项目字体。 |

`Control`、`Physics`、`Equipment`、`Payload`、`Input`、`Camera`、`Telemetry` 以及同步机体装配器属于可迁移核心，不得新增 UIManager、ResourceServices 或 GameSceneNavigator 引用。

## 接入顺序

1. 安装并核对包依赖、Render Pipeline 和 Hotfix/Core asmdef 边界。
2. 复制两个主要目录，等待 Unity 导入，先处理纯类型/包缺失错误。
3. 接入目标项目资源 Loader，将加载后的成品 Prefab 交给 `DroneFlightVehicleAssembler`。
4. 替换场景协调器的导航和生命周期事务。
5. 重新生成或适配三个 UI View，替换字体引用。
6. 合入 DroneFlight 测试到目标项目已有 Test Runner 程序集。
7. 运行配置、模型、数学、场景适配和 PlayMode 物理测试。

## 编译错误分类

- 找不到 InputSystem/UniTask/TMP/URP：包依赖未齐，不改玩法代码规避。
- 找不到 Core UI、资源或场景类型：只修改宿主适配器，不把框架类型引入飞控核心。
- Prefab Missing Script：确认 `.meta` 与脚本一起迁移，禁止重建 GUID 后逐个手绑。
- UI 字体或绑定丢失：替换外部字体并按目标绑定工具重新生成。
- 模型节点或材质契约失败：按模型契约修 Blender/FBX，不在 Builder 中写临时别名蒙混通过。

## 验收

- Unity 编译、Console 和 DroneFlight 专项测试通过。
- Editor/正式入口各进入一次，三机型各只生成一个成品 Prefab。
- 起飞、降落、档位、镜头、重载和退出工作。
- 抓斗连续完成放下、抓取、运输、释放、收纳；渔叉完成发射、命中/未命中、收放线和回收。
- 正式模型旋翼方向、推力轴、起落架、云台、材质、碰撞代理和脚底净空符合契约。
- 迁移报告明确列出改过的宿主适配器和未执行的人工验收，不宣称“双目录原样零修改运行”。
