# 迁移 DroneFlight 到正式项目

## 迁移结论

DroneFlight 采用源码级可迁移边界。运行时代码仍属于 `Hotfix.dll`，迁移不要求复制 SleepyDemos 的程序集、HybridCLR 配置、DLL 加载顺序或构建脚本。

## 复制清单

必须复制：

- `Assets/Scripts/Hotfix/Demos/DroneFlight` 中除 `Adapters` 外的核心目录。
- `Assets/LoadResources/Demos/drone_flight` 中的 `Art`、`Data` 与 `Prefabs`；按正式项目需要选择场景。
- 所有脚本与资源对应的 `.meta`，以保留 GUID 和 Prefab 引用。

建议复制：

- `Assets/Scripts/Hotfix/Editor/DroneFlight` 中仍需使用的配置 Inspector 和资源 Builder。
- DroneFlight 专项测试；合入目标项目已有 Test Runner 程序集，不复制或新增测试 asmdef。
- [架构设计](../architecture/drone-flight-design.md)、[模块维护说明](../modules/drone-flight.md)和本 runbook。

## 只重写宿主适配

`Adapters/{Scene,UI,Fishing,Experience}` 是预期按目标宿主选择、替换或删除的区域：

| 当前能力 | 正式项目处理 |
|---|---|
| `UIManager` 与三个 View | 改接正式 UI；复用 Telemetry 数据模型 |
| `ResourceServices` | 改接正式资源系统，再把实例交给 `DroneFlightVehicleAssembler` |
| `GameSceneNavigator` | 替换进入、重载和返回事务 |
| Hub 生命周期、Editor 直启 | 接入正式启动流程或删除 |
| 捕鱼 QTE/UI | 接入正式玩法编排；不放回核心目录 |

核心目录禁止引用 `Core.Runtime`、`UIManager`、`ResourceServices`、`GameSceneNavigator` 和适配层具体类型。迁移前先运行 `DroneFlightPortabilityBoundaryTests`。

## 最小接入方式

### 新场景只要手动飞行

1. 引用 `DronePrototype` 或装备 Variant 成品 Prefab。
2. 场景新建对象并添加 `DroneFlightStandaloneBootstrap`。
3. 指定无人机 Prefab、出生点和场景 Camera，模式选择“手动飞行”。
4. Play 后由 Bootstrap 完成安全出生、引用装配和相机/输入切换。

不要仅拖 FBX 模型，也不要手工逐个绑定运行时引用。

### 新场景沿几个点自动飞行

1. 在场景创建 `DroneCruiseRoute`，添加至少两个航点。
2. 选择单次、循环或往返模式，并按需配置等待、速度覆盖和朝向策略。
3. `DroneFlightStandaloneBootstrap` 模式选择“自动巡航”，引用该路线和 `DroneAutopilotConfig`。
4. 选择是否自动起飞，以及完成后悬停或自动降落。

## 包与渲染依赖

- Unity Input System：手动输入和快捷键。
- URP Lit：当前正式机体材质；换渲染管线时需替换材质。
- TMP/UGUI、UniTask：仅 SleepyDemos UI/生命周期适配层需要，纯核心与 Standalone 不应依赖。

## 迁移顺序

1. 核对 Unity 版本、Input System、渲染管线和目标程序集归属。
2. 连同 `.meta` 复制核心、配置和资源，等待 Unity 导入。
3. 用 Standalone 手动场景验证纯核心闭环。
4. 用两点航线验证自动起飞、航点抵达和结束策略。
5. 在目标项目建立自己的 Adapter，接资源、UI、场景和生命周期。
6. 合入专项测试，先跑边界/配置/路线 EditMode，再跑真实 Prefab PlayMode。
7. 最后接抓斗、渔叉和正式业务演出。

## 常见错误

- Missing Script：漏复制 `.meta` 或脚本目录不完整。
- 找不到 SleepyDemos 类型：核心中出现宿主依赖；将引用移到目标 Adapter，不要把 Core 整体搬入正式项目。
- 无人机不响应：检查是否使用 StandaloneBootstrap/正式场景装配器，而非只实例化 FBX。
- 自动路线直接瞬移：错误绕过了 Autopilot/FlightController；路线只能提交控制目标。
- Prefab 载荷异常：组合 Variant 或装备配置未完整复制。

## 验收

- 编译无错误，边界测试通过。
- Standalone 手动飞行可解锁、起飞、降落和切换镜头。
- 两点单次、循环和往返路线推进正确，自动飞行不写 Transform 或 Rigidbody 速度。
- 三种成品 Prefab 各只生成一个实例；装备生命周期与载荷反馈正常。
- 正式宿主的 UI、资源加载、重载和返回由目标 Adapter 接管。
- 迁移报告列出复制目录、重写 Adapter、包差异及尚未执行的人工验证。

