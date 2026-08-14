# 新增 Demo 操作流程

## 目标

新增一个 Demo 时，尽量做到：
- 资源目录清晰
- 业务入口明确
- 不污染公共底座
- 以后容易并行协作

## 推荐步骤

1. 确定 `DemoId`
   - 使用英文小写 + **下划线**（禁止短横线）
   - 例如 `gravity_well`

2. 创建资源目录
   - 在 `Assets/LoadResources/Demos/<DemoId>/` 下建立该 Demo 的根目录
   - 按目录矩阵拆出 `Scenes/`、`Prefabs/`、`Art/`、`Data/`、`VFX/` 等子目录
   - 资产必须落在功能子目录里，不要直接堆在 Demo 根目录

3. 创建或复制场景
   - 从模板场景或现有 Demo 复制起步
   - 可加载场景：纯语义名如 `Main.unity`，放在 `Demos/<DemoId>/Scenes/`
   - 启动入口层场景仍放在 `Assets/Scenes`，不要与 Demo 可加载场景混淆

4. 按命名规范添加资源（纯语义文件名，类型靠目录决定）
   - 预制体：`Prefabs/Crate_01.prefab`；特效：`VFX/HitSparks_01.prefab`
   - 美术：`Art/Textures/Rock_01_BaseColor.png`、`Art/Materials/Rock_01.mat`
   - 数据：`Data/Levels.json`、`Data/ItemTable.bytes`、`Data/GameConfig.asset`
   - 文件名遵循 PascalCase 语义规范，无 `-`/空格/特殊字符；完整目录矩阵见 [资源命名规范](../architecture/asset-naming.md)

5. 接入业务代码
   - 若是具体玩法或页面逻辑，优先放在 `Assets/Scripts/Hotfix/Module/`
   - 不要因为方便把业务逻辑塞进 `Core.Runtime`

6. 接入主入口
   - 根据项目当前首页方案，将 Demo 暴露到主菜单或 Catalog
   - 可加载 Demo 场景不加入 Build Settings；确认它位于 YooAsset Collector 覆盖的目录，并在 `GameSceneId` / `GameSceneCatalog` 登记
   - 入口统一调用 `GameSceneNavigator.Instance.SwitchAsync(...)`，不要在业务组件直接调用 `SceneManager.LoadSceneAsync`
   - 主菜单由 MvcBind 生成的 `*Component.cs` 不可手改；新增按钮可挂独立 Hotfix `MonoBehaviour`，并以 Prefab 契约测试确认组件真实落盘
   - Demo 必须提供切换到 `GameSceneId.Hub` 的明确入口，并验证往返后输入和临时资源释放；不要重载 `AppEntrance`
   - 如需 Editor 直接打开 Demo 场景 Play，按[直接运行 Demo 岛](./run-demo-island-directly.md)挂独立 `DemoIslandEditorBootstrap`；该能力不得扩展到 Development/Release 直启
   - 如果接入方式变化，要同步更新相关架构文档和 runbook

7. 检查公共沉淀点
   - 只有确认多个 Demo 稳定复用的能力，才上提到 `Core`

8. 手动验证
   - 运行 `Tools/SleepyDemos/校验 LoadResources 资源命名`，LoadResources 下无 Error
   - 若资源来自 git 拉取、批量复制或外部改名，运行 `Tools/SleepyDemos/同步 LoadResources 资产 Label`，确认 Project 搜索 `l:demo` 能筛出 Demo 资源
   - 启动是否能进入主菜单
   - Demo 是否能从主入口进入
   - 场景和资源引用是否正常
   - 是否影响热更与资源加载链路

## 新增 Demo 时常见错误

- 资源放进公共目录，导致归属不清
- 文件名含短横线 `-`、空格或其它非法字符，或资产没放进登记的功能子目录
- 玩法逻辑误塞进 `Core.Runtime`
- 直接改启动链路接玩法，绕过主入口
- 接入步骤变了却没更新文档

## 文档同步要求

满足任意一条时，新增 Demo 的同时要改文档：
- 新 Demo 的接入流程和现有做法不同
- 新增了新的模块入口或中间层
- 把某类共用能力从 Demo 中上提到了 Core
