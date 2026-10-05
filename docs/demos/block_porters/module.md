# 小小搬豆工 Demo

## 职责与边界

`BlockPorters` 是竖屏、斜俯视的颜色搬运解压 Demo。玩家点击五列队伍的列头，小人沿空格道路搬走同色方块，举砖跳进小坑。初始五个免费任务位，左右两个固定广告槽可在本关逐个模拟解锁；堵满且无工作可推进时失败。

本模块包含五个教学关卡、同图三档策略关卡、图片关卡工作台、搬运演出、进度、暂停、音效、重开、下一关和返回 Hub。不包含真实广告、支付、联网、排行榜、持久进度或小游戏平台 SDK；不宣称已完整复刻分享链接中的原游戏。

## 代码与资源入口

- 规则与角色引用：`Assets/Scripts/Hotfix/Demos/BlockPorters/`，归属 `Hotfix`。
- 图片工作台：`Assets/Scripts/Hotfix/Editor/BlockPorters/`，归属 `Hotfix.Editor`，仅处理关卡内容。
- 原图与编辑配方：`Assets/Settings/BlockPorters/Sources/`、`Recipes/`，不进入 YooAsset Collector。
- Action、Data、Handler、奖励服务与主题加载位于 Demo 根目录；`BlockPortersHandler` 修改规则状态，`BlockPortersController` 管理对象池、镜头和搬运演出。界面、屏幕布局和 UI 规格放在 `UI/`，`BlockPortersHudView` 进入 Core 的 Decorate/Widget 层，固定节点由 UIBind 与 `ComponentItemIndex` 保存。业务类型使用 `Hotfix.BlockPorters` 命名空间，三个 View 保持 `Hotfix`。
- 资源：`Assets/LoadResources/Demos/block_porters/`；`Scenes/Main.unity`、`Prefabs/Porter.prefab`、`Prefabs/Brick.prefab`、`Prefabs/UI/BlockPortersHudView.prefab`、`Data/Level1.asset` 至 `Level5.asset`。程序化深坑网格保存为 `Data/PitRing.asset`，自制音效位于 `Audio/SFX/`。

## 主链路


`AppEntrance → MainMenuView` 选择 `block_porters` 卡片 → 开始按钮或键盘/手柄确认 → `MainMenuEnterAction → MainMenuHandler → GameSceneNavigator → BlockPortersController → BlockPortersLoadLevelAction → BlockPortersHandler / BlockPortersData → BlockPortersHudView`。

规则会话复制关卡网格和队列。派队消耗一个任务位，每队 1–8 人；每人预约一个方块，预约仍视为障碍。四方向洪泛只从棋盘外侧进入，封闭空洞和对角缝不能作为入口。小人抬起时打开格子，入坑时才计入交付；全队交付后释放任务位，所有方块交付后通关。

场景控制器统一更新去程、抬砖、返程和跳坑，按可达预约惰性创建角色并复用对象池，逻辑等待不显示角色。规则层不依赖 MonoBehaviour、Core UI 或资源服务，不使用逐方块物理或逐角色 NavMeshAgent。

`BlockPortersScheduler` 负责分配及抬起、交付事件，按计划时刻和任务 ID 稳定排序，控制器只消费事件并按时间插值姿态。编辑器用同一调度器的 `Settle()` 跳到稳定点，解算器仅在稳定点选择列头。暂停不推进虚拟钟；帧率不会改变已有任务的事件顺序。参考解按“派一队、所有可推进工作到稳定点、再派队”回放。实时玩家仍可在运输中派队。

## 关卡与奖励扩展

`BlockPortersLevel` 保存网格、颜色表、五列队伍（兼容旧四列）、初始容量和列号组成的参考解。加载时验证尺寸上限 32×32、颜色范围、人数上限，以及每种颜色人数与方块数量一致。修改关卡后必须回放参考解测试，不能只检查数量。

色号范围为 0–11，精确按 ID 匹配。可选 `colorLabels` 缺项时使用编号，旧五关无需改写网格与队伍。控制器读取 `Data/LevelCatalog.asset` 的顺序，不再写死关卡数组；关卡材质由共享模板和当前色表创建，切关/退出销毁。工作台导出更新原对象，保留 GUID，并追加至关卡集。

### 工作台与数据约定

`BlockPortersRecipe` 保存原图、归一化裁剪、网格尺寸、Lab 色数/合并参数、近色组、额外改色比例、种子、锁定格、手工色表/标签和五列队伍（兼容旧四列）。像素化快照供并排预览，转换尺寸单独保存，改尺寸后必须重新转换。网格左下开始逐行，`-1` 为空，四方向连通；透明度低于阈值为空，白色等不透明背景默认保留。

Lab 聚类与近色合并在独立像素副本上计算，近色辅助默认最小 ΔE76 为 10。自动拆色保持占用轮廓、默认额外改色不超过 10%；手工画笔可更改轮廓，锁定格拒绝涂色/填充/擦除与受影响合并。“仅安排队伍”不修改图案或锁定区。手工队伍需满足每色人数守恒，不能用图案颜色近似代替 ID。

生成器从可剥离轨迹构造队伍，再扰动列归属、同色队伍之间的人数配额及隐藏顺序；人数转移始终保持 1–8 人与总量守恒。困难初始候选按色号组织列头，让未来颜色占位形成风险；扰动候选逐个重新搜索。最多 24 个候选，默认每个搜索 20,000 状态或 5 秒；预算耗尽是 `Unknown`，穷尽稳定点空间才是 `Unsolvable`。每个可解候选最多 100 次随机和 100 次贪心回放，取固定种子。候选耗时因棋盘与队列而异，后台任务可取消。

等待峰值覆盖参考解、备选派队及模拟策略；关键选择统计存在占位分支的多选稳定点，堵塞次数只计立即失败分支。这些是规则和模拟指标，不等于玩家胜率。初版普通门槛为有等待压力且随机通过率低于 98%；困难为等待峰值至少 2 且随机通过率低于 80%。不达标保留图案和诊断，不能按目标标签导出。

后台只处理快照；主线程版本、窗口编辑代次和取消状态共同阻止过期结果写回。分析不持久化为可信标签，再次打开配方必须分析；Undo/Redo、图案、队列及参数改动使报告失效。导出重放当前参考解，且校验当前版本与难度门槛；原图/配方不作为运行数据读取。

奖励服务 `IBlockPortersReward.RequestExtraSlotAsync(side, token)` 返回 `Completed`、`Canceled` 或 `Unavailable`。Controller 的 `RequestUnlockSlot` 只在完成结果上调用规则 `TryUnlockExtraSlot`；每侧每关一次，重开/切关重新锁定。请求期间停止调度并防止重复请求，会话版本隔离旧结果。规则用 `UnlockedExtraSlots` 位掩码记录左右状态；免费槽编号 0–4、左槽 5、右槽 6，开放数量不等同于物理编号。右侧先解锁也只开放槽 6。失败可通过剩余锁恢复继续，两侧都已开放时不再追加容量。当前仅模拟广告，未接平台 SDK。

## 生命周期

### 随机场景主题

`Data/ThemeCatalog.asset` 登记玩具桌、花园、海边、星空的 ID、名字和背景地址。默认图为场景回退，其余按需加载，不保存四张图的直接引用。首次进入、新关及再次进入随机排除上次成功应用的主题，重开保持；独立随机源不影响关卡 seed。

主题加载器 `BlockPortersThemeLoader` 为每次请求持有独立的 IResourceLoader，成功后才释放旧加载器，异常／空资源保留可用背景。请求版本、退出标记隔离旧结果；在途操作完成后释放，已应用资源在销毁时释放。Controller 共用背景 MPB 更新 `_BaseMap` 与安全区 UV，不修改共享材质。全主题共用同一棋盘框模板，装饰不影响色表或灯光。

### 软胶玩具视觉

HUD 沿用 Core Decorate/Widget 的宿主，不新增 Canvas。五列四排卡片显示大人数和小色号，只有首排可点击；三排预览独立递补，队列不足时隐藏不存在的预览。首排边框高亮，预览保留色表辨识。五个免费任务槽固定一排，左右广告槽位于坑口两侧；开放后原地显示任务徽块和运输状态。

`BlockPortersScreenLayout` 是本 Demo 的 600×1080 安全区坐标来源。HUD、正交相机和背景平面共同等比适配；棋盘中心设计点为 (300,304)，最大图案宽度 416，背景框为 (24,104,552,400)。`Data/UiStyle.asset` 同时约束 HUD 的 80×80 方格、64×64 内容面、字体、间距及世界投影。所有类型的格子嵌套 `TileBase.prefab`，牌面原色与深／浅数字由 HUD View 显示。背景使用世界相机后景的无光照贴图平面，UV 超出内容区时通过私有背景 Shader 平滑延展边缘底色，不以 Decorate 图片覆盖玩法。旧三维托盘和桌面 Renderer 已移出场景，小人接触投影继续保留。坑口整体缩小至旧尺寸约 45%，交付坐标与调度时刻不变。

坑内使用私有开口裁剪 Shader，按世界相机正交视线投回坑沿平面，防止深处坑底在后景图片上露出孔外轮廓；不依赖被移除的三维地面遮挡，不引入全屏后处理。参数由 Controller 随布局适配更新，孔心使用实际 PitRim 高度，半径向内留半设计像素余量，不参与搬运规则或事件时刻。

设置入口保留 `Pause` 节点名，打开时记录原暂停状态，关闭只撤销面板产生的暂停；声音、重开、返回位于设置中。设置与结果分别保存为 `BlockPortersSettingsView`、`BlockPortersResultView` 的独立 Prefab 和 UIBind View，位于 Tip/Modal；各自全屏背景负责拦截，公共 Modal Mask 不改。重开后的刷新关闭设置、恢复队伍和进度。

安全区变化时内容整体适配，遮罩反向补偿内容的缩放和偏移，覆盖整个宿主；弹窗卡片仍居中于安全区。长竖屏顶部留白不能穿透到公共画质入口。齿轮位置避开公共画质按钮，两者继续独立维护。

`BlockPortersHudView` 合并同一帧的规则通知，集中更新文字、进度插值、队伍递补、弹窗和 `BlockPortersButtonFeedback`。后者只记录指针和回弹状态，不维护独立 Update。派队后递补 0.2 秒期间锁住同列，暂停、切换会话和解绑清理布局与输入锁。进度填充位于凹槽的圆角遮罩内部，按已交付数量平滑变化，Won 立即满格，切换会话立即归零。界面动画用未缩放时间，搬运仍由既有调度时钟控制。

方块保持根 Renderer 与挂点，使用共享倒角网格；小人不换模型，新增共享接触投影，起跳时隐藏。坑口有柔和暖灰棕渐变内壁与底面，交付波纹由控制器统一推进，用 MaterialPropertyBlock 更新透明度，不逐次创建材质。重开清空波纹和粒子。原坐标、路径、抬起与交付时刻保持原样。

Hub 的相机关闭后，其灯光仍会影响 Additive 内容场景。Controller 在导航稳定后记录并临时停用其他场景当前启用的灯光，Disable/Destroy 时恢复；只记录启用项，原来关闭的灯光不变。此隔离由本 Demo 的场景控制器负责，不修改公共场景导航或项目渲染管线。

生成原图与提示词位于独立美术目录的 `Assets/Settings/BlockPorters/ArtSource/`，运行 Sprite 位于 Demo 的 `Art/UI/`。只保留已生成资产，不保留本次一次性装配脚本。直接修改 Prefab 或 Sprite Editor 元数据的步骤见 [视觉维护](runbooks/visuals.md)。

场景入口等待导航稳定后注册 BlockPortersData，并打开 HUD。页面通过 BindData 读取同一规则状态，按钮提交 Action；Handler 管理暂停、重开和异步奖励，退出时关闭页面并取消任务后移除 Data。暂停只停止本 Demo 的调度与动画，不修改全局 `Time.timeScale`。重开回收全部角色与方块、清空特效、取消旧奖励结果的生效资格；不卸载重建启动壳。

返回 Hub 前禁用输入并按具体 HUD 实例关闭，再执行导航卸载；失败时恢复当前 HUD。销毁时取消异步等待和奖励请求，释放订阅。UI 的 Hide/Destroy 均解绑场景引用。音效资源为场景引用，由资源场景句柄管理。

## 验证

- `Tests.Demo.BlockPortersSessionTests`：可达性、预约、部分等待、释放任务位、交付、失败、一次复活、队列和五关参考解。
- `Tests.Demo.BlockPortersThemeTests`：随机排除上次、独立种子、空目录、加载失败、过期／退出隔离和资源释放。
- `Tests.Demo.BlockPortersAssetTests`：资源命名与标签、MVC 固定引用、UI 根约定、小人挂点与补字。
- `Tests.Demo.BlockPortersFlowTests`：正式 Hub 入口、八关参考解的实际角色回放、四套主题加载与重开保持、搬运、暂停、设置遮罩/暂停恢复、十二色显示、重开、模拟奖励分支、旧结果隔离、最大棋盘/逻辑等待、竖屏截图与返回清理。
- `Tests.Demo.BlockPortersWorkbenchTests`：Lab、轮廓与改色预算、锁定/Undo、种子、取消、搜索边界、难度门槛、参考解与 GUID。
- `Tests.Demo.BlockPortersBatchTests`：批量单图失败继续、取消后不导出与工作台窗口生命周期。
- 运行证据保存至 `Library/BlockPorters/Evidence/`，不进入可加载资源或发布包。

Editor 的性能采样包含测试与编辑器开销，不能替代抖音/微信真机验收。双端发布前需要独立验证 Unity 版本、WebGL 转换、YooAsset 与 HybridCLR 启动兼容性。

## 相关文档

- [装配与运行](runbooks/run.md)
- [图片关卡工作台](runbooks/workbench.md)
- [场景导航](../../modules/scene-runtime.md)
- [新增 Demo](../../runbooks/add-demo.md)

UI 与场景直接维护已保存资产，绑定更新使用公共 UIBind。启动不预建小人，每列显示列头和三队预览。

## 菜单输入

HUD、设置和结果资源保存 UIMenuScope，方向选择/确认只派发一次原按钮行为；设置 Cancel 恢复原暂停状态。HUD 入口已按 Hub 循环卡片稳定 Key 定位。手机只显示普通/业务状态，派队递补 Pulse 是玩法演出，不当作按下反馈。

## 独立页面维护

`BlockPortersHudView` 只负责队列、任务位与进度。设置页持有声音开关、继续、重开与返回；结算页持有胜负文案、奖励、重开、下一关与返回。`BlockPortersUIController` 随 Controller 会话创建，串行关闭旧弹窗与打开目标弹窗；返回 Hub 先关闭弹窗，再关闭 HUD。导航失败恢复当前会话，避免下一次场景误关旧 View。

设置页使用公共 UIBtnSwitch，按钮使用 UIState/UIStateInteraction 和独立取消作用域。队列的业务色与计数色仍由 HUD 管理，公共五态只持有强调边与内部卡面缩放；BlockPortersButtonFeedback 只保留队列递补回弹。弹窗布局共用本 Demo 的安全区布局组件，不创建额外 Canvas 或 EventSystem。

隐藏和销毁释放 Changed/Cancel 回调。Changed 的当轮委托快照可能仍调用刚隐藏的页面，因此刷新先检查绑定；不能只依赖退订避免同步关闭后的调用。

本轮定向验证：设置/暂停/十二色351c54c5、Hub派队/重开/奖励/返回3d259e35、进度/递补/暂停/重开1cf24f6d，各1/1通过；设置正常与长屏截图已检查。未执行全量测试。
