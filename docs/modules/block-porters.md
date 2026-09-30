# 小人搬砖 Demo

## 职责与边界

`BlockPorters` 是竖屏、斜俯视的颜色搬运解压 Demo。玩家点击四列队伍的列头，小人沿空格道路搬走同色方块，举砖跳进深坑。初始五个任务位，堵满且无工作可推进时失败；每关可通过本地模拟奖励增加两个任务位一次。

本模块包含五关、搬运演出、进度、暂停、音效、重开、下一关和返回 Hub。不包含真实广告、支付、联网、排行榜、持久进度或小游戏平台 SDK；不宣称已完整复刻分享链接中的原游戏。

## 代码与资源入口

- 规则与角色引用：`Assets/Scripts/Hotfix/Demos/BlockPorters/`，归属 `Hotfix`。
- 宿主与 UI：该目录的 `Adapters/`；`BlockPortersController` 管理场景会话，`BlockPortersHudView` 进入 Core 的 Decorate/Widget 层，固定节点由 MvcBind 与 `ComponentItemIndex` 保存。
- 资源：`Assets/LoadResources/Demos/block_porters/`；`Scenes/Main.unity`、`Prefabs/Porter.prefab`、`Prefabs/Brick.prefab`、`Prefabs/UI/BlockPortersHudView.prefab`、`Data/Level1.asset` 至 `Level5.asset`。程序化深坑网格保存为 `Data/PitRing.asset`，自制音效位于 `Audio/SFX/`。

## 主链路


`AppEntrance → MainMenuView.BlockPortersButton → GameSceneNavigator.BlockPorters → BlockPortersController → BlockPortersSession → BlockPortersHudView`。

规则会话复制关卡网格和队列。派队消耗一个任务位，每队 1–8 人；每人预约一个方块，预约仍视为障碍。四方向洪泛只从棋盘外侧进入，封闭空洞和对角缝不能作为入口。小人抬起时打开格子，入坑时才计入交付；全队交付后释放任务位，所有方块交付后通关。

场景控制器统一更新去程、抬砖、返程和跳坑，按可达预约惰性创建角色并复用对象池，逻辑等待不显示角色。规则层不依赖 MonoBehaviour、Core UI 或资源服务，不使用逐方块物理或逐角色 NavMeshAgent。

## 关卡与奖励扩展

`BlockPortersLevel` 保存网格、颜色表、四列队伍、初始容量和列号组成的参考解。加载时验证尺寸上限 32×32、颜色范围、人数上限，以及每种颜色人数与方块数量一致。修改关卡后必须回放参考解测试，不能只检查数量。

奖励适配器 `IBlockPortersReward` 返回 `Completed`、`Canceled` 或 `Unavailable`。只有完成结果可复活；请求期间防止重复请求，会话版本防止旧结果影响重开后的新关卡，规则本身保证复活至多一次。当前实现仅本地模拟，按钮明确标注模拟奖励。

## 生命周期

控制器等待全局场景导航稳定后显示带强类型会话数据的 HUD。暂停只停止本 Demo 的调度与动画，不修改全局 `Time.timeScale`。重开回收全部角色与方块、清空特效、取消旧奖励结果的生效资格；不卸载重建启动壳。

返回 Hub 前禁用输入并按具体 HUD 实例关闭，再执行导航卸载；失败时恢复当前 HUD。销毁时取消异步等待和奖励请求，释放订阅。UI 的 Hide/Destroy 均解绑场景引用。音效资源为场景引用，由资源场景句柄管理。

## 验证

- `Tests.Demo.BlockPortersSessionTests`：可达性、预约、部分等待、释放任务位、交付、失败、一次复活、队列和五关参考解。
- `Tests.Demo.BlockPortersAssetTests`：资源命名与标签、MVC 固定引用、UI 根约定、小人挂点与补字。
- `Tests.Demo.BlockPortersFlowTests`：正式 Hub 入口、搬运、暂停、重开、模拟奖励分支、旧结果隔离、最大棋盘/逻辑等待、竖屏截图与返回清理。
- 运行证据保存至 `Library/BlockPorters/Evidence/`，不进入可加载资源或发布包。

Editor 的性能采样包含测试与编辑器开销，不能替代抖音/微信真机验收。双端发布前需要独立验证 Unity 版本、WebGL 转换、YooAsset 与 HybridCLR 启动兼容性。

## 相关文档

- [装配与运行](../runbooks/run-block-porters.md)
- [场景导航](scene-runtime.md)
- [新增 Demo](../runbooks/add-demo.md)

UI 与场景直接维护已保存资产，绑定更新使用公共 MvcBind。启动不预建小人，每列显示列头和一队预览。
