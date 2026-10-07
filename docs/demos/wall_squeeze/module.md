# 夹爆它模块

`Hotfix.WallSqueeze` 负责本地六关滑墙原型。公共 Core 输入、资源、UI 和场景导航保持原接口，本 Demo 不增加生产程序集。

## 真源与职责

- `WallSqueezeSettings` / `WallSqueezeLevel`：Inspector 保存的参数、种子与初始布局；规则实例只复制状态，不修改资产。
- `WallSqueezeSimulation`：轴对齐矩形接触约束、推链压缩、安全恢复、死亡批次及结果优先级；不依赖相机、物理刚体或 UI。
- `WallSqueezeInput`：消费 `GameplayInputRouter` 的 Interaction 导航、选墙与 Pause；指针命中属于世界交互，按下先排除 UI，单指持有直到释放或取消。
- `WallSqueezePresentation`：实例化保存的纯色形状模板，同步规则尺寸，播放短音效和轴向碎片。中央箭头保存为 Sprite，固定黑色把手保证未选中时仍可读。
- `WallSqueezeWorld`：关卡与规则时钟、输入暂停门闩、世界表现及具体 View 生命周期。
- `WallSqueezeAction` / `Data` / `Handler`：场景来源核对、流程命令和状态发布。HUD / Modal 读取同一规则对象，不复制计数。
- `WallSqueezeHudView` / `WallSqueezeMenuView`：UIManager 管理的独立 Widget / Modal，UIBind 实际生成 Component 绑定；保存的 Panel 组件持有控件引用。

## 主链路与生命周期

Hub 卡片通过 `MainMenuData` 登记，`GameSceneCatalog` 将 `GameSceneId.WallSqueeze` 映射到 `Scenes/Main.unity`。World 等待导航稳定后建立输入会话、注册 Demo Data、建立关卡并显示 HUD。Editor 直启使用保存场景里的既有 `DemoIslandEditorBootstrap`。

本次为 Editor 直启补齐公共 Hotfix 的路径识别：Bootstrap 按当前 Scene 路径解析已登记 Demo ID 后初始化导航；正式 Hub 默认入口不变。World 在共享 UIRoot 初始化完成后恢复自己正交相机的纯色背景，只调整本场景相机。

每帧先消费公共输入，再按 PauseState 冻结规则和演出；输入只决定选墙与轴上目标。规则以不超过 1/120 秒的内部步推进，连续位移按墙和方块接触约束限制，按互不连通的推链分别求统一压缩。最小尺寸后须沿接触链追到两侧真实墙或边界支撑；撤墙会清除该轴压力来源，恢复填空不会重新制造夹持。整个规则步先收集死亡，再统一移除碰撞与判断住户优先的失败结果。

暂停、失焦、后台、手柄断连使用 Core 门闩；恢复必须明确继续。设备切换、重试和退出取消拖墙。Modal 使用串行状态重比较，快速暂停/继续不会遗留菜单；菜单焦点由公共 MenuInputScope 限制到当前 Modal。返回 Hub 前按具体 View 实例关闭 UI，再走 GameSceneNavigator；导航失败重新恢复当前会话页面与输入。销毁只释放输入、作用域和 Data，不从 OnDestroy 发起按类型关闭新页面。

## 维护边界

规则使用小规模一维约束图和矩形扫描，当前对象数量很少，不引入通用物理引擎或空间索引。怪物只轴向游走、遇阻转向；住户不自主移动。布局改变后必须运行受影响保存关卡的真实种子参考路径，不能通过隐藏游走限制保证胜利。

`SpecialMonsters` 保存类型与位置，普通怪仍存于 `Monsters`。每个规则对象保存 `RestSize` / `MinimumSize`：顶墙怪 X 轴不能压缩、Y 轴可夹死；钻缝怪原始尺寸 0.8 × 0.4、默认速度倍率 1.8，通缝由真实矩形碰撞决定。表现层对应保存的特殊模板，受压时特性标记仍存在。

`FixedWalls` 追加到规则墙数组，参与同一推动、夹持、恢复与墙互阻计算；输入切墙和点击跳过 `IsFixed`，表现层隐藏固定墙把手与箭头。`TimeLimit=0` 不限时；有时限时使用未暂停的规则时间。判定优先级为住户死亡、怪物全灭、超时；`Reason` 区分误伤与超时，重试清空时钟与结果。

HUD 保存黑框头尾分格：标题、剩余、保护、关卡与限时；底部怪物轮廓/夹法图例、当前设备提示、重来。规则倒计时按整秒更新 Data，群夹数量短时回显，不增加计分与成长系统。相机留出头尾 HUD 空间；所有图形节点在公共 UI Camera 可见层。

世界 Prefab、Scene、HUD / Modal Prefab、Data/PlayerInput.asset 和关卡资产是正式真源；一次性装配工具在完成保存与验证后删除。Demo 使用独立静态 TMP 字体 `Data/InterfaceFont.asset`，不向用户公共动态字体写入新字形。

## 验证入口

EditMode：`Tests.Demo.WallSqueezeSimulationTests`；PlayMode：`Tests.Demo.WallSqueezeRuntimeTests`。仅运行本次直接相关类，不默认全量。实际结果、截图与未完成的体验验收见 [验证记录](runbooks/validation.md)。
