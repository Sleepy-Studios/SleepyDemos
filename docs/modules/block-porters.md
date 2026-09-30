# 小人搬砖 Demo

## 职责与边界

`BlockPorters` 是竖屏、斜俯视的颜色搬运解压 Demo。玩家点击四列队伍的列头，小人沿空格道路搬走同色方块，举砖跳进深坑。初始五个任务位，堵满且无工作可推进时失败；每关可通过本地模拟奖励增加两个任务位一次。

本模块包含五个教学关卡、同图三档策略关卡、图片关卡工作台、搬运演出、进度、暂停、音效、重开、下一关和返回 Hub。不包含真实广告、支付、联网、排行榜、持久进度或小游戏平台 SDK；不宣称已完整复刻分享链接中的原游戏。

## 代码与资源入口

- 规则与角色引用：`Assets/Scripts/Hotfix/Demos/BlockPorters/`，归属 `Hotfix`。
- 图片工作台：`Assets/Scripts/Hotfix/Editor/BlockPorters/`，归属 `Hotfix.Editor`，仅处理关卡内容。
- 原图与编辑配方：`Assets/Settings/BlockPorters/Sources/`、`Recipes/`，不进入 YooAsset Collector。
- 宿主与 UI：该目录的 `Adapters/`；`BlockPortersController` 管理场景会话，`BlockPortersHudView` 进入 Core 的 Decorate/Widget 层，固定节点由 MvcBind 与 `ComponentItemIndex` 保存。
- 资源：`Assets/LoadResources/Demos/block_porters/`；`Scenes/Main.unity`、`Prefabs/Porter.prefab`、`Prefabs/Brick.prefab`、`Prefabs/UI/BlockPortersHudView.prefab`、`Data/Level1.asset` 至 `Level5.asset`。程序化深坑网格保存为 `Data/PitRing.asset`，自制音效位于 `Audio/SFX/`。

## 主链路


`AppEntrance → MainMenuView.BlockPortersButton → GameSceneNavigator.BlockPorters → BlockPortersController → BlockPortersLevelCatalog → BlockPortersScheduler / BlockPortersSession → BlockPortersHudView`。

规则会话复制关卡网格和队列。派队消耗一个任务位，每队 1–8 人；每人预约一个方块，预约仍视为障碍。四方向洪泛只从棋盘外侧进入，封闭空洞和对角缝不能作为入口。小人抬起时打开格子，入坑时才计入交付；全队交付后释放任务位，所有方块交付后通关。

场景控制器统一更新去程、抬砖、返程和跳坑，按可达预约惰性创建角色并复用对象池，逻辑等待不显示角色。规则层不依赖 MonoBehaviour、Core UI 或资源服务，不使用逐方块物理或逐角色 NavMeshAgent。

`BlockPortersScheduler` 负责分配及抬起、交付事件，按计划时刻和任务 ID 稳定排序，控制器只消费事件并按时间插值姿态。编辑器用同一调度器的 `Settle()` 跳到稳定点，解算器仅在稳定点选择列头。暂停不推进虚拟钟；帧率不会改变已有任务的事件顺序。参考解按“派一队、所有可推进工作到稳定点、再派队”回放。实时玩家仍可在运输中派队。

## 关卡与奖励扩展

`BlockPortersLevel` 保存网格、颜色表、四列队伍、初始容量和列号组成的参考解。加载时验证尺寸上限 32×32、颜色范围、人数上限，以及每种颜色人数与方块数量一致。修改关卡后必须回放参考解测试，不能只检查数量。

色号范围为 0–11，精确按 ID 匹配。可选 `colorLabels` 缺项时使用编号，旧五关无需改写网格与队伍。控制器读取 `Data/LevelCatalog.asset` 的顺序，不再写死关卡数组；关卡材质由共享模板和当前色表创建，切关/退出销毁。工作台导出更新原对象，保留 GUID，并追加至关卡集。

### 工作台与数据约定

`BlockPortersRecipe` 保存原图、归一化裁剪、网格尺寸、Lab 色数/合并参数、近色组、额外改色比例、种子、锁定格、手工色表/标签和四列队伍。像素化快照供并排预览，转换尺寸单独保存，改尺寸后必须重新转换。网格左下开始逐行，`-1` 为空，四方向连通；透明度低于阈值为空，白色等不透明背景默认保留。

Lab 聚类与近色合并在独立像素副本上计算，近色辅助默认最小 ΔE76 为 10。自动拆色保持占用轮廓、默认额外改色不超过 10%；手工画笔可更改轮廓，锁定格拒绝涂色/填充/擦除与受影响合并。“仅安排队伍”不修改图案或锁定区。手工队伍需满足每色人数守恒，不能用图案颜色近似代替 ID。

生成器从可剥离轨迹构造队伍，再扰动列归属、同色队伍之间的人数配额及隐藏顺序；人数转移始终保持 1–8 人与总量守恒。困难初始候选按色号组织列头，让未来颜色占位形成风险；扰动候选逐个重新搜索。最多 24 个候选，默认每个搜索 20,000 状态或 5 秒；预算耗尽是 `Unknown`，穷尽稳定点空间才是 `Unsolvable`。每个可解候选最多 100 次随机和 100 次贪心回放，取固定种子。候选耗时因棋盘与队列而异，后台任务可取消。

等待峰值覆盖参考解、备选派队及模拟策略；关键选择统计存在占位分支的多选稳定点，堵塞次数只计立即失败分支。这些是规则和模拟指标，不等于玩家胜率。初版普通门槛为有等待压力且随机通过率低于 98%；困难为等待峰值至少 2 且随机通过率低于 80%。不达标保留图案和诊断，不能按目标标签导出。

后台只处理快照；主线程版本、窗口编辑代次和取消状态共同阻止过期结果写回。分析不持久化为可信标签，再次打开配方必须分析；Undo/Redo、图案、队列及参数改动使报告失效。导出重放当前参考解，且校验当前版本与难度门槛；原图/配方不作为运行数据读取。

奖励适配器 `IBlockPortersReward` 返回 `Completed`、`Canceled` 或 `Unavailable`。只有完成结果可复活；请求期间防止重复请求，会话版本防止旧结果影响重开后的新关卡，规则本身保证复活至多一次。当前实现仅本地模拟，按钮明确标注模拟奖励。

## 生命周期

控制器等待全局场景导航稳定后显示带强类型会话数据的 HUD。暂停只停止本 Demo 的调度与动画，不修改全局 `Time.timeScale`。重开回收全部角色与方块、清空特效、取消旧奖励结果的生效资格；不卸载重建启动壳。

返回 Hub 前禁用输入并按具体 HUD 实例关闭，再执行导航卸载；失败时恢复当前 HUD。销毁时取消异步等待和奖励请求，释放订阅。UI 的 Hide/Destroy 均解绑场景引用。音效资源为场景引用，由资源场景句柄管理。

## 验证

- `Tests.Demo.BlockPortersSessionTests`：可达性、预约、部分等待、释放任务位、交付、失败、一次复活、队列和五关参考解。
- `Tests.Demo.BlockPortersAssetTests`：资源命名与标签、MVC 固定引用、UI 根约定、小人挂点与补字。
- `Tests.Demo.BlockPortersFlowTests`：正式 Hub 入口、搬运、暂停、重开、模拟奖励分支、旧结果隔离、最大棋盘/逻辑等待、竖屏截图与返回清理。
- `Tests.Demo.BlockPortersWorkbenchTests`：Lab、轮廓与改色预算、锁定/Undo、种子、取消、搜索边界、难度门槛、参考解与 GUID。
- `Tests.Demo.BlockPortersBatchTests`：批量单图失败继续、取消后不导出与工作台窗口生命周期。
- 运行证据保存至 `Library/BlockPorters/Evidence/`，不进入可加载资源或发布包。

Editor 的性能采样包含测试与编辑器开销，不能替代抖音/微信真机验收。双端发布前需要独立验证 Unity 版本、WebGL 转换、YooAsset 与 HybridCLR 启动兼容性。

## 相关文档

- [装配与运行](../runbooks/run-block-porters.md)
- [图片关卡工作台](../runbooks/block-porters-workbench.md)
- [场景导航](scene-runtime.md)
- [新增 Demo](../runbooks/add-demo.md)

UI 与场景直接维护已保存资产，绑定更新使用公共 MvcBind。启动不预建小人，每列显示列头和一队预览。
