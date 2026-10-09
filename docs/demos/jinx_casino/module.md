# 模块职责与生命周期

## 职责和边界

`Hotfix.JinxCasino` 负责赌场规则、筹码、机台、道具、事件、阶段目标、结局和宿主适配。Core提供通用启动、资源、UI、玩法输入和历史会话边界，不引用赌场业务类型。公共输入职责见[玩法输入模块](../../modules/gameplay-input.md)。当前目标为单机沉浸重做，联网方案待定。

旧Fusion/AOT桥接设想保留于历史需求，不作为当前实现承诺或单机依赖。

## 当前样板主链路（S1进行中）

正式导航的JinxCasino地址使用`Scenes/Immersion.unity`。`JinxCasinoController`只接通保存的独立InputActionAsset与`JinxCasinoImmersionHudView`；缺少主相机、输入资产或本地CharacterController时明确报错。菜单使用Core已有Canvas、EventSystem和View生命周期；不新建启动框架。

`JinxCasinoGame`在Controller组件构造时建立，构造不读档；它唯一持有冒险聚合、缓存状态、规则命令、计时、三槽和成长。`Interaction/JinxCasinoPlayerInteraction`连接探索/机台/菜单输入、具体聚焦与物理射线；`Interaction/JinxCasinoTableSession`直接持有Game与真实Station，不再使用TableOperations接口或包装。离桌只清草稿，已投入局及随机状态保留；新局、读档和区域传送前恢复借用相机，退出释放菜单输入作用域、Game订阅和光标状态。

Game的State getter返回既有缓存，不重新Capture；只有规则变更或教学观察刷新。经济命令接收实际输入帧和稳定请求编号，同帧新操作拒绝，已记录请求仍由领域核对指纹。Changed仅发布公开场景效果，Controller据此同步区域/角色/演出；场景模型读取 Game 的公开投影，HUD 从 Data 获取同一 Game 的状态。

读档先从Store取得候选并触发ValidatingRestore，再由BeforeRunReplacement清聚焦并安装；IsRestoring覆盖通知，使音效/模型建立恢复基线。BeforeSave刷新真实教学观察，notify=false只更新缓存，避免保存递归触发场景；Game负责原子保存、阶段自动检查点和成长去重。Controller在Awake绑定Player、Settings和Game订阅，导航稳定后激活输入；单一-500 Update由Player推进Clock/领域/聚焦，销毁时先同步教学位移并保存，再Dispose输入和退订，避免先清观察缓存再存档。较早OnEnable的表现协调器直接使用已有Game，不依赖Controller.Start完成导航。

`JinxCasinoS1PresentationCoordinator`把本台公开状态发送给水果机、二十一点、协作拉杆专属组件，离桌后仍保留演出。组件只读公开牌面和结算序号，不直接支付或开奖；显式Restore静态还原，不重播旧奖励。暗牌收到公开结果后才翻面，暂停保持中间姿态，新局清除演出队列。

`JinxCasinoPresentationClock`是本Demo的暂停时钟，不修改全局timeScale。场景效果、移动桌、按钮按压读取同一时钟；恢复首帧不补算后台时间。专属机台表现收到同一暂停状态并丢弃恢复首帧的墙钟增量。运行UI和实际设备仍需单独验收。

桌面允许专门设计的世界空间UI和铭牌，不能把旧通用操作弹窗贴到机台上。保存样板当前包含三台、互动教学、实体购物、三槽存档与单区标准离场闭环；输入/声音设置已接入，画质设置、音画打磨和双端实玩仍待完成，当前不是S1最终交付。

## 单机规则、资源和存储

旧通用面板、P0钱包/会话、Main场景与P0/P4构建入口已删除；恢复历史原型应使用对应Git基线与历史包，当前源码只运行新单机流程。小游戏`CasinoMiniGameRound`只运行规则与整数时钟；冒险统一提交钱包、库存、事件、任务和阶段。UI不得直接改资金或重新开奖，关闭表现保留已提交局，恢复后继续合法动作。

`JinxCasinoGameSettings`保存每局配置，创建局时复制。当前Immersion配置限制三款样板与三件商品、暂时关闭事件，完整领域内容仍保留等待实体重构。`JinxCasinoWorldArea`只控制已保存的内容与锁门。旧任务Director/Target和目标Prefab已删，后续任务实体按新场景接入；领域唯一发奖规则保留。

UI根继续使用Core Canvas及View生命周期，所有面板、按钮、文字、列表模板保存于HUD Prefab，运行时仅实例化模板，不新建控件或Canvas。父RectTransform适配保持等比，存档摘要只在打开槽面板及槽操作后读取，不在每次HUD刷新访问磁盘。

具体机台通过真实目标提交设备无关命令，没有通用下拉框/数字输入玩法入口。已删旧档案/表情/快捷交流页面，相关成长规则继续保留；完整可见成长入口待后续单机流程接入。

`CasinoMiniGameRound.GetPresentation()`只返回公开牌、骰、线索与位置的数组副本，不含牌堆、随机状态或未授权密码。盲拍动作下限和公开分数只根据本人最高报价；未结束不泄露对手出价，结束仅公开奖值。冒险缓存最近完成的规则局，持久化`LastRoundJson`及`SettledRoundSequence`，表现读取实际经过事件修正的成本/返还。`IsObjectiveSuccess`独立于收益，用于金库门和额外合作资格，止损/彩金不能把失败演成开锁成功。

`JinxCasinoSceneEffects`只呈现已经提交的效果。角色视觉使用独立保存挂点，恢复精确原位置、姿态及材质；同目标保护先校验，失败不消耗道具。传送检查安全点和角色占位，移动赌桌使用完整包围盒及CharacterController skinWidth保留间距。当前Immersion未装配这些环境设施，保留的物理效果组件及资源候选不代表事件体验已完成。

当前存档通过公共 LocalDataManager 原子写入，不创建或读取备份。合法候选先完成领域校验，IO 失败保留有效主档；读取缺失或损坏内容按空槽或新用户处理。

永久成长由`CasinoProfile`统计已提交的正式局流水，按RunId去重；练习不给永久战绩。`Persistence/CasinoProfileStore`在JinxCasino/Profile目录保存领域快照，底层调用公共原子读写。Game先保存候选再提交档案缓存，失败不吞掉待登记RunId，5秒后重试；已删除旧ProfileHost partial。配色、帽子和表情必须已经解锁，损坏档案按新用户读取，读取不会自动覆盖文件；明确保存合法候选时才写入。

本机输入/音量偏好由 JinxCasinoLocalSettings 持有独立副本，使用 LocalDataKeys.CasinoPreferences 与 LocalDataManager 加载、明确保存。预览不写盘，保存失败不提交候选；损坏回默认并提示，不读取旧键或备份。

双人扳手通过`TryApplyCooperationHelp()`落实为每局一次真实帮助：拉杆窗口前后各扩大100毫秒，金库揭示首个尚未查看的线索。帮助标志进入单局快照，读档不重复放宽；已用或全部已知时拒绝并保留库存。操作者仍须拉杆/输入正确密码，不能靠帮助伪造接管资格。

样板机台使用三款S1专属表现；旧Avatar/Station通用表现已删。`JinxCasinoAudioDirector`按流水/结算序号去重，恢复建立基线不重播旧奖励；音量仅作用于本场景音源，不更改全局AudioListener。

正式入口为 AppEntrance → Hub → JinxCasino；场景与资源走既有 `GameSceneNavigator` 和 `ResourceServices`。Demo 不进入 Build Settings，不自行重载 AppEntrance。

场景只提供一个本地主Camera/AudioListener，实体桌面借用该相机。当前装配一区，四区仍是后续目标。

## 退出与恢复

离开Demo前停止输入、保存已选槽、取消本场景工作、按具体实例关闭所属UI并释放输入/Game订阅，再由既有导航卸载场景及Loader。上一局的延迟任务不得修改下一局状态。

单机经济统一由Game提交领域规则，重复请求核对原指纹并返回同一结果。开奖、随机序列与已处理请求进入当前版本快照，离桌和恢复不能重抽或重复支付。

## 验证

规则、随机序列、幂等和快照采用 EditMode；输入、UI、相机及场景清理采用 PlayMode。所有测试进入现有两套测试程序集。Windows/Android Player、Xbox实物和手机触控单独验收；联网不在本轮范围。

触控移动与视角以屏幕高度换算为720p参考像素，使相同屏幕比例的滑动在不同分辨率下产生相同输入。移动、视角各自持有独立指针，额外手指不能抢占，释放、隐藏、失焦或暂停时清空待处理输入。鼠标仍使用原始像素增量；真机手感、屏幕比例及灵敏度设置需要后续单独验收。

## 沉浸交互基础（S1进行中）

根目录 Controller 负责场景初始化与导航/区域同步，Game负责冒险；Interaction放具体玩家、教学、离场及机台/柜台/区域组件，Presentation放专属表现、环境和时钟，UI放菜单/HUD与本机设置，Persistence放存储。Controller不补旧属性/方法转发，UI直接消费Player/Tutorial/Exit/Settings。JinxCasinoStation保存StationId/FocusPose/FocusFieldOfView/Targets；ConfigureTable拒绝外部目标或重复目标ID。JinxCasinoTableFocus借用相机，在0.35秒过渡后开放输入，退出/失效/Dispose恢复原姿态和FOV；不改变角色、钱包或时钟。JinxCasinoTableSelection用实际Collider射线和稳定NavigationOrder选择同一组目标，遮挡不穿透、跳过禁用目标。JinxCasinoTableTarget只发设备无关命令，反馈使用属性块、不实例化材质。

这些组件已接入Immersion场景的Controller、三款实体机台与柜台；旧Main和通用面板已删除，历史恢复使用对应Git基线。入口类使用实际指针与保存物件验证，但样板整体仍待正式体验验收。

## 三设备输入基础（已接入宿主，硬件待验）

通用输入位于`Core.Runtime.Inputs`，由`GameplayInputRouter`及Contracts、`MenuInputScope`、`LocalPauseState`、`TouchInputPad`承担，生命周期和设备语义见[玩法输入模块](../../modules/gameplay-input.md)。Demo继续保存独立`JinxCasinoImmersion.inputactions`及玩法命令映射，将Exploration/Table/Menu分别映射到公共Gameplay/Interaction/Menu上下文。Router克隆资产，切换清边沿，ReadFrame必须每帧调用以释放长按门闩，再ConsumeActions一次。鼠标/触屏是增量，手柄是角速度，LookDegrees已完成换算。菜单借用Core EventSystem，等Submit/Cancel/导航释放后开放导航；桌面独立焦点，不双提交。

LocalPauseState记录后台/失焦/手柄断连并要求显式继续；它只提供状态，宿主仍须冻结领域、移动和各演出时钟。不能仅挡移动或disable SceneEffects后宣称暂停完成。Dispose先MenuScope后Router，恢复公共导航并停止本Demo震动。

输入配置维护保存资产`Assets/LoadResources/Demos/jinx_casino/Data/JinxCasinoImmersion.inputactions`，保留已有Map、动作ID及人工键位，不重写全局InputSystem_Actions。场景、HUD、交互挂点和材质维护各自保存资源；临时生成/装配Builder不作为维护入口，具体路径见[运行与维护](runbooks/run.md)。

## 手柄本机偏好

CasinoLocalPreferences当前记录版本2包含手柄死区、最大半径、视角速度/倍率、反转Y、震动开关/强度。PlayerPrefs使用JinxCasino.LocalPreferences，只读完整当前记录，不查询旧键或迁移旧参数；坏记录只读回退，取消预览不写盘。ToInputSettings输出独立公共输入参数，不能在每帧构建；宿主在加载/预览/取消时ApplySettings。偏好版本独立于冒险版本4和公共输入DTO版本1。

数据存储、三页设置控件与公共输入宿主已接入；Editor真实控件操作仍不代表Xbox实物或Android设置体验已验收。

桌面指针统一由GameplayInputRouter.ReadFrame返回PointerPosition/PointerPressed，后者单次消费；鼠标/触屏共用Pointer绑定，菜单不将其二次转成Confirm。上下文切换、暂停清空待处理点击，已有按住输入须释放。实体宿主使用本地相机ScreenPointToRay交给TableSelection；输入基础本身不直接修改筹码。

离桌先结束具体会话，再平滑归还相机；HasFocus在归位结束前保持true。HUD据HasFocus保留桌面状态，返回中的离桌按钮禁用并显示过渡提示。探索摇杆/滑动/交互触区还须等待IsExplorationInputReady：相机已归位、Gameplay上下文已恢复且已读取、无菜单/暂停。切上下文会清除读取标记。Player保留每帧先切上下文再读取输入的顺序，HUD随后开放触区，避免中途清掉玩家刚按住的手指，也避免新按键被首次中立读取前的门闩屏蔽。会话草稿清理与已投入局恢复仍按原规则处理。

机台/柜台FocusFieldOfView表示16:9参考构图的垂直FOV。TableFocus在更窄的视口按比例扩展垂直视野，保持原水平可见范围；16:9及更宽视口沿用原值。活动聚焦每帧根据当前camera.aspect重新计算，窗口变化无需重进机台；不修改相机比例。退出仍恢复进入前的姿态和FOV。该适配只保证参考构图不被窄视口裁掉，实际文字、按钮大小和触区仍须按确认后的共享机台设计与设备验收打磨。

调整保存的.inputactions时保留Point/Click等宿主所需动作及已有ID。PlayMode路由测试读取实际保存资产；合成设备使用临时InputSettings让输入送入Game View，结束恢复原设置，避免测试操作者的Editor焦点影响事件路由。正式后台/断连暂停规则不改变，仍待宿主与真机验收。

## 三款桌面规则适配

Interaction/JinxCasinoTableSession管理具体机台的本地筹码草稿，直接调用具体Game。Station提供稳定ID、真实启用状态、玩法及操作点；构造固定原身份，轮换或关闭后须重新进入。Slots确认草稿后由实体拉柄执行投入；Blackjack确认后直接发牌，Primary/Secondary分别要牌/停牌；Levers确认后由玩家0号杆提交时机动作。筹码面额10/50/100，清空和离桌只清未投入草稿。

GetView按ActiveStationId/LastStationId过滤公开投影，同一视图给各物件查询可用性和原因。金额、阶段、事件修正变化使已准备的水果机草稿失效，须重新确认。相同帧/相同动作不重复提交，领域重试保留requestId，较早存档不能重放已成功的旧操作。

此层已使用真实领域规则验证；相机、InputRouter、场景实体及UI接入需另做PlayMode和实玩验证，不能用纯适配测试代替。
公共画面设置是按需打开的普通 Pop；赌场入场与返回 Hub 均不自动加载该页面，不创建全局悬浮入口。HUD 无需再持有画质入口抑制作用域。

## S1实体补给柜台

JinxCasinoShopCounter保存s1.supply、三件商品、六个物理目标与报价/回执铭牌；PlayerInteraction把选择、购买、库存使用直接接入Game命令并核对真实场景保护。选中商品不扣款，支付仍由领域请求处理。柜台与机台共用Player输入上下文、TableSelection和TableFocus，后者支持有自身聚焦挂点的Behaviour；关闭、失效及回Hub恢复借用相机并清理目标订阅。

柜台与机台按玩家到交互锚点距离决定接近入口；探索移动不因柜台另建逻辑。LB/RB切换商品目标，确认提交当前目标，次要动作使用选中商品库存。商品报价读取本局配置、余额与库存；当前旅程配置未开放商品时显示未开放。此链仍需Android/Xbox真机体验验收。


## S1互动教学宿主与界面

JinxCasinoTutorialGuide在新的Practice中同步启动教学，并把实际应用的视角、CharacterController位移、成功物件操作、柜台回执与已展示结果报告给原冒险聚合。视角/位移累计每0.15秒提交，保存前补齐累计但不递归刷新View。教学观察不占用经济操作同帧互斥。结果必须由具体机台PresentedSettlementSequence确认已完成演出；Restore的静态结果也可观察，无需再次付款。

教学条保存在 JinxCasinoImmersionHudView；教学确认和完成选择由 JinxCasinoTutorialView 维护。教学条不接管桌面焦点；Ready/完成选择等待离桌过渡结束，或由玩家暂停时打开。暂停菜单可再次打开此前暂缓的教学结果，避免“稍后”后无入口。重玩/新标准冒险共用明确确认卡，取消不替换聚合；跳过保留已投入局。

Player.SetMenuState可选取消回调仅在菜单打开时保存。Core Cancel及Demo手柄Menu由同一菜单返回函数处理，同帧至多退一层，确认窗口取消后保留原暂停。当前保存HUD包含教学入口；View退订和Controller销毁都清理回调。教学仅限制单次投入10，不保证获胜、不补发筹码，不把练习资金带入新正式局。


## 沉浸HUD的三槽存档

JinxCasinoImmersionHudSave复用GetSaveSlotInfo、SaveAdventure和LoadAdventure；不另建文件格式或存储目录。主菜单读取、暂停保存/读取共用三槽列表；空槽禁用读取，覆盖已有槽及替换当前局先进入确认卡。取消只退当前层级并保留暂停，读取成功后显式继续，后台/手柄断连仍由LocalPauseState阻止恢复。

槽摘要仅在打开和操作后重读，包含模式、区域、筹码和时间。确认前重新读取可用性，并核对打开列表时的RunId，避免界面旧选择写入另一个新局。保存后沿用选定槽的自动保存契约。Core Cancel和手柄Menu统一经过CancelSaveWindow，未打开存档时交回教学或暂停逻辑；同帧去重，页面销毁清理监听。底层读写和损坏处理见[公共本地存储](../../modules/local-data.md)。


## S1现场验票、撤离与结果卡

JinxCasinoExitInteraction绑定s1.verify和s1.leave两件保存物件。交互同时检查接近距离、相机视口和无遮挡射线，背对出口不显示可交互提示。标准局达标后在验票口调用原CompleteStage，筹码仅作为达标条件，不扣除额度；Finale在离场口调用原LeaveWithDignity。提前撤离须在同一物件五秒内再次交互，暂停冻结意图时钟，离开、读档或换局清除意图。

活动牌局与Closing保留原机台操作，出口拒绝结束未结算局。结局卡仅在真正Ended且无活动局、相机聚焦已退出时出现，显示原快照和成长写入结果；不自行记战绩或再次选择结局。三槽界面优先于结局卡；取消保存回到结果，只有明确返回按钮请求Hub导航。该出口暂服务StageCount为1的S1标准样板，多区推进随S3设施一起扩展。

## 存档的场景兼容性

LoadAdventure先完成原Store的格式/领域恢复，再由沉浸宿主核对场景能力，全部通过后才清焦点、替换当前局和所选槽。单人样板拒绝多人、尚未开放的无尽/后续区域，以及找不到具体实例的已投入机台；当前区域与机台从宿主保存引用查询，Store只读取当前版本，宿主不向其它已加载场景借机台。

拒绝时文件、当前Run、随机、余额、所选槽与相机均保持。活动局必须匹配当前区域已装配的具体机台ID；缺ID同样拒绝，不再提供显式认领或按游戏种类改绑。已结算结果不要求原桌仍存在，但不会显示在另一张桌。此检查不缩减最终四区/无尽目标，随实际区域与模式入口落地更新能力范围。

## 沉浸设置接入

公共输入设备区分实际操作与提示设备：连接手柄优先显示其实际动作绑定，键鼠/触控操作仍可用。Switch 确认/返回通过 Submit/Cancel 用途解析为 A/B；HUD 绑定文案按设备与绑定变化失效，不每帧解析按键。触控仅保留业务选中视觉，菜单仍由 Core EventSystem 提交和返回。

设置页由JinxCasinoSettingsView显示，Handler处理设置草稿。主菜单与暂停均有入口，关闭后回到原菜单，不隐式恢复暂停。预览立即应用到公共输入与场景音源，明确保存才写盘；取消、关闭或离场同步撤销未保存预览。

保存Prefab将键鼠/触控、手柄、声音分为三页。手柄页暴露倍率、角速度、死区、最大半径、反转Y、震动开关与强度；设置页面通过保存的控件预览本机偏好。菜单焦点复用Core EventSystem，每页使用保存控件，不创建运行时UI。场景音乐与SFX接入既有AudioDirector，不新增AudioListener。此处不代表画质/分辨率与真机体验已经完成。


## 独立 UI 页面（2026-10-05）

`JinxCasinoImmersionHudView` 只保留 HUD 和触控区。主菜单、暂停、设置、三槽存档、教学窗口、结局分别由 `JinxCasinoMainMenuView`、`JinxCasinoPauseView`、`JinxCasinoSettingsView`、`JinxCasinoSaveView`、`JinxCasinoTutorialView`、`JinxCasinoEndingView` 及同名 Prefab 维护。

`JinxCasinoData` 提供现有 Game、Player、Settings 的读取入口，并持有存档/教学确认和设置草稿。`JinxCasinoPage` 明确标识主菜单、暂停、探索、教学、存档、结局与设置状态。按钮和交互命令经具体 Action 进入 Handler，同步结果字段保留交易/投入回执的原时序；请求匹配当前 Game 或场景实例。

Handler 在业务页面变化后直接调用 UIManager，设置和存档弹窗隐藏并保留底页。View 通过 BindData 管理显示订阅，负责首选控件与焦点恢复；玩家继续使用公共 MenuInputScope。Controller 管理初始 HUD 和离场关闭，不再创建独立页面控制器。

场景时钟、连续输入、射线与机台演出保持原链路。Player 的机台请求通过 Handler 调用原 TableSession，经济和随机仍由同一 Game / AdventureSession 修改；规则对象与存档 DTO 未复制。场景初始化注册 Data，退出先关闭页面、取消任务和保存，再移除注册实例。

设置页通过 UITab 管理三页，普通 Slider 使用白 Sprite 和 Image.color。触控左右手布局属于 HUD 的长期订阅，关闭设置页后仍保持保存的布局；OnHide 释放并恢复原布局。

本轮 Flux 重构定向回归通过：资源绑定与规则、教学、存档、偏好共 73 项 EditMode 检查；正式进入、真实输入、存读档、设置预览/撤销、教学、结局及独立包重载等 11 项交互回归，另有机台交互 7 项与暂停演出 2 项。没有运行全项目测试；实体设备和 Player 冷启动仍待验收。

## Flux 代码整理

Action 参数使用 PascalCase 字段与显式构造函数，规则结果由 Handler 同步回填。存档、教学窗口对象只保存快照、确认及查询；命令和 IO 在 Handler，嵌套规则事件延后到完整操作结束后发布。Data 清理通过纯规则重置，不触发场景替换或中间发布；公开 ClearAdventure 仍保留原场景复位契约。页面从 Data 读取规则、交互和窗口状态；Handler 处理必要导航，View 保存自身焦点。

## 当前本机存储与清理

公共接口见[本地存储](../../modules/local-data.md)。本场 Data 随场景注册、同步清空并移除；UI、输入和规则事件由实际持有者释放。只有成功保存才提交永久状态；不维护旧记录迁移或备份恢复。两个存档 Demo 的损坏槽按空槽、损坏永久档案按新用户处理。

## 本轮定向验证

2026-10-09 UI 与存储整理：偏好 12/12、三槽存档 4/4、永久档案 6/6、教学存档 1/1；设置、独立窗口、三槽支付去重、入口焦点/相机、完整教学、独立场景退出及不支持内容拒绝七个定向 PlayMode 用例通过。未执行全量测试或 Player 构建。

独立场景退出用例首次在Hub进入阶段超时，未到退出流程；同一代码单独重跑通过。此结果与其他定向通过分别记录，不视为全量或真机验收。
