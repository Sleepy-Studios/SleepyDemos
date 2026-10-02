# 模块职责与生命周期

## 职责和边界

`Hotfix.JinxCasino` 负责赌场规则、筹码、机台、道具、事件、阶段目标、结局和宿主适配。Core提供通用启动、资源、UI和历史会话边界，不引用赌场业务类型。当前目标为单机沉浸重做，联网方案待定。

旧Fusion/AOT桥接设想保留于历史需求，不作为当前实现承诺或单机依赖。

## 当前样板主链路（S1进行中）

正式导航的JinxCasino地址在当前工作分支使用`Scenes/Immersion.unity`。`JinxCasinoController`根据保存的独立InputActionAsset选择`JinxCasinoImmersionHudView`，旧Main无该引用时继续使用原型HUD。菜单使用Core已有Canvas、EventSystem和View生命周期；不新建启动框架。

`JinxCasinoImmersionHost`连接探索/机台/菜单输入上下文、具体机台聚焦与物理射线选择。`JinxCasinoTableSession`通过`JinxCasinoControllerTableOperations`向原领域提交命令。离桌只清草稿，已投入局及随机状态保留；新局、读档和区域传送前恢复借用相机，退出释放菜单输入作用域、事件订阅和光标状态。

`JinxCasinoS1PresentationCoordinator`把本台公开状态发送给水果机、二十一点、协作拉杆专属组件，离桌后仍保留演出。组件只读公开牌面和结算序号，不直接支付或开奖；显式Restore静态还原，不重播旧奖励。暗牌收到公开结果后才翻面，暂停保持中间姿态，新局清除演出队列。

`JinxCasinoPresentationClock`是本Demo的暂停时钟，不修改全局timeScale。场景效果、移动桌、按钮按压读取同一时钟；恢复首帧不补算后台时间。专属机台表现收到同一暂停状态并丢弃恢复首帧的墙钟增量。运行UI和实际设备仍需单独验收。

桌面允许专门设计的世界空间UI和铭牌，不能把旧通用操作弹窗贴到机台上。保存样板当前包含三台、互动教学、实体购物、三槽存档与单区标准离场闭环；完整设置、音画打磨和双端实玩仍待完成，当前不是S1最终交付。

## 保留的原型主链路

内容优先的离线入口由`JinxCasinoAdventurePresenter`绑定当前`JinxCasinoController`，后者持有`CasinoAdventureSession`。小游戏`CasinoMiniGameRound`只运行规则与整数时钟；冒险统一提交钱包、库存、事件、任务和阶段。UI不得直接改资金或重新开奖，关面板保留已提交局，恢复后继续合法动作。旧P0`CasinoNetworkCoordinator`入口独立保留用于已有联网边界回归，不能与内容局共用钱包。

`JinxCasinoGameSettings`保存每局配置，创建局时复制。P1限制三机台/三商品/三事件，奖励池也限制为可用内容；四区版本解除这些限制。`JinxCasinoWorldArea`只控制已保存的内容与锁门；任务Director实例化保存的触发目标，验证本地身份与接近距离，再由领域唯一发奖。

UI根继续使用Core Canvas及View生命周期，所有面板、按钮、文字、列表模板保存于HUD Prefab，运行时仅实例化模板，不新建控件或Canvas。父RectTransform适配保持等比，存档摘要只在打开槽面板及槽操作后读取，不在每次HUD刷新访问磁盘。

机台选择使用`CasinoMachineUiOptions`把可读选项映射到冻结的整数协议，`TMP_Dropdown`模板保存于Prefab。轮盘区域、骰宝下注、签筒风险、落点、杠杆、跑者与电梯都显示语义；密码/出价用数字输入。查看金库线索的选择器与密码输入各自保存状态。旧隐藏wire只保留现有测试与调用兼容，不能作为玩家主操作界面。

`CasinoMiniGameRound.GetPresentation()`只返回公开牌、骰、线索与位置的数组副本，不含牌堆、随机状态或未授权密码。盲拍动作下限和公开分数只根据本人最高报价；未结束不泄露对手出价，结束仅公开奖值。冒险缓存最近完成的规则局，持久化`LastRoundJson`及`SettledRoundSequence`，表现读取实际经过事件修正的成本/返还。`IsObjectiveSuccess`独立于收益，用于金库门和额外合作资格，止损/彩金不能把失败演成开锁成功。

`JinxCasinoSceneEffects`只呈现已经提交的效果。角色视觉使用独立保存挂点，恢复精确原位置、姿态及材质；同目标保护先校验，失败不消耗道具。传送检查安全点和角色占位，移动赌桌使用完整包围盒及CharacterController skinWidth保留间距；任务Director发奖依赖真实唯一目标，不依赖视觉动画。

`CasinoLocalSaveStore`默认使用`Application.persistentDataPath/JinxCasino`的三个独立槽。完整规则快照先校验再原子替换，保留上一不同快照；损坏主文件不能滚入有效备份，相同快照不滚动备份。主动选槽后阶段边界和退出保存，未选槽不会隐式覆盖用户其他旅程。

永久成长由`CasinoProfile`统计已提交的正式局流水，按RunId去重；练习不给永久战绩。`CasinoProfileStore`使用独立Profile目录的原子JSON/校验和备份。`JinxCasinoProfileHost`先保存候选再提交缓存，失败不吞掉待登记RunId，有限频率重试；每帧不访问磁盘。配色、帽子和表情必须已经解锁，修改损坏档案不能被静默重置覆盖。

本机偏好由`CasinoLocalPreferencesStore`使用独立PlayerPrefs键保存，不进入旅程/网络状态。设置预览只影响本机场景，关闭、事件打断或View释放都会撤销未确认修改；明确保存后先写盘，再提交偏好。PC和触控增量先分别乘各自倍率，再沿用原基础灵敏度。左右手仅镜像保存的触控区域并清理持有指针，不修改Core安全区。

双人扳手通过`TryApplyCooperationHelp()`落实为每局一次真实帮助：拉杆窗口前后各扩大100毫秒，金库揭示首个尚未查看的线索。帮助标志进入单局快照，读档不重复放宽；已用或全部已知时拒绝并保留库存。操作者仍须拉杆/输入正确密码，不能靠帮助伪造接管资格。

正式表现由`JinxCasinoAvatarPresentation`、`JinxCasinoStationPresentation`、`JinxCasinoAudioDirector`承担：角色只调整明确衣服槽，临时墨迹/换装结束后再应用永久配色；机台只读公开结果驱动已有语义Pivot；音频按流水/结算序号去重，恢复建立基线不重播旧奖励。音量仅作用于本场景音源，整蛊实例播放前登记，不更改全局AudioListener。

正式入口为 AppEntrance → Hub → JinxCasino；场景与资源走既有 `GameSceneNavigator` 和 `ResourceServices`。Demo 不进入 Build Settings，不自行重载 AppEntrance。

场景只提供一个本地主 Camera / AudioListener。Avatar Prefab 不带相机，本地相机跟随自己的视角锚点；四个区域作为同一场景中的区域装配。

## 退出与恢复

离开 Demo 前停止输入、取消会话工作、按具体实例关闭所属 UI、断开网络并移除回调，再卸载场景及 Loader。上一局的延迟任务不得修改下一局状态。

共享经济由协调者处理，重复请求只得到同一结果。开奖状态、随机序列与已处理请求纳入快照，权威切换不得重新开奖或重复支付。

## 验证

规则、随机序列、幂等和快照采用 EditMode；输入、UI、相机及场景清理采用 PlayMode。所有测试进入现有两套测试程序集。真实互联网和双端 Player 单独验收，离线测试不代表 Photon 联机成功。

触控移动与视角以屏幕高度换算为720p参考像素，使相同屏幕比例的滑动在不同分辨率下产生相同输入。移动、视角各自持有独立指针，额外手指不能抢占，释放、隐藏、失焦或暂停时清空待处理输入。鼠标仍使用原始像素增量；真机手感、屏幕比例及灵敏度设置需要后续单独验收。

## 沉浸交互基础（S1进行中）

机台仍位于Adapters/World，命名空间保持Hotfix.JinxCasino.Adapters。JinxCasinoStation新增保存的StationId/FocusPose/FocusFieldOfView/Targets；ConfigureTable拒绝外部目标或重复目标ID。JinxCasinoTableFocus借用相机，在0.35秒过渡后开放输入，退出/失效/Dispose恢复原姿态和FOV；不改变角色、钱包或时钟。JinxCasinoTableSelection用实际Collider射线和稳定NavigationOrder选择同一组目标，遮挡不穿透、跳过禁用目标。JinxCasinoTableTarget只发设备无关命令，反馈使用属性块、不实例化材质。

这些组件已接入Immersion场景的Controller、三款实体机台与柜台；旧Main保留原型面板用于历史恢复。入口类使用实际指针与保存物件验证，但样板整体仍待正式体验验收。

## 三设备输入基础（已接入宿主，硬件待验）

Adapters/Input使用独立JinxCasinoImmersion.inputactions；InputRouter克隆资产，Exploration/Table/Menu切换清边沿，ReadFrame必须每帧调用以释放长按门闩，再ConsumeActions一次。鼠标/触屏是增量，手柄是角速度，LookDegrees已完成换算。MenuInputScope借用Core EventSystem，等Submit/Cancel/导航释放后开放菜单导航；桌面独立焦点，不双提交。

PauseState记录后台/失焦/手柄断连并要求显式继续；它只提供状态，宿主仍须冻结领域、移动和各演出时钟。不能仅挡移动或disable SceneEffects后宣称暂停完成。Dispose先MenuScope后Router，恢复公共导航并停止本Demo震动。

Hotfix.Editor新增菜单“Tools/SleepyDemos/整蛊赌场/沉浸样板/创建或检查输入配置”，仅首次创建，已存在时校验而不覆盖自定义键位。输入配置和Editor所需Unity.InputSystem引用已保存；未改全局InputSystem_Actions。

## 手柄本机偏好

CasinoLocalPreferences版本2保留全部旧字段，增加手柄死区、最大半径、视角速度/倍率、反转Y、震动开关/强度。PlayerPrefs地址仍为JinxCasino.LocalPreferences.v1，合法旧记录只在内存补默认值，明确保存才写新版；坏记录只读回退，取消预览不写盘。ToInputSettings输出独立输入参数，不能在每帧构建；后续宿主在加载/预览/取消时ApplySettings。

数据存储已接入，手柄设置控件与实际输入宿主仍在S1整合中，不能据此声明硬件设置体验完成。

桌面指针统一由InputRouter.ReadFrame返回PointerPosition/PointerPressed，后者单次消费；鼠标/触屏共用Pointer绑定，菜单不将其二次转成Confirm。上下文切换、暂停清空待处理点击，已有按住输入须释放。实体宿主使用本地相机ScreenPointToRay交给TableSelection；输入基础本身不直接修改筹码。

输入装配菜单仅补缺失的Point/Click，保留已有动作ID与人工键位。PlayMode路由测试读取实际保存的.inputactions；合成设备使用临时InputSettings让输入送入Game View，结束恢复原设置，避免测试操作者的Editor焦点影响事件路由。正式后台/断连暂停规则不改变，仍待宿主与真机验收。

## 三款桌面规则适配

JinxCasinoTableSession管理具体机台的本地筹码草稿，通过IJinxCasinoTableOperations薄边界调用原冒险规则。Slots确认草稿后由实体拉柄执行投入；Blackjack确认后直接发牌，Primary/Secondary分别要牌/停牌；Levers确认后由玩家0号杆提交时机动作。筹码面额10/50/100，清空和离桌只清未投入草稿。

GetView按ActiveStationId/LastStationId过滤公开投影，同一视图给各物件查询可用性和原因。金额、阶段、事件修正变化使已准备的水果机草稿失效，须重新确认。相同帧/相同动作不重复提交，领域重试保留requestId，较早存档不能重放已成功的旧操作。

此层已使用真实领域规则验证；相机、InputRouter、场景实体及UI接入需另做PlayMode和实玩验证，不能用纯适配测试代替。
沉浸HUD在OnShow借用GraphicsSettingsUI的控件显隐作用域，在OnHide/OnDestroy释放，避免公共画质入口遮挡暂停；具体恢复与多使用者语义见公共Streamline模块文档。新入口回归使用真实InputSystem鼠标操作桌面暂停按钮，并检查回Hub后的公共入口恢复。

## S1实体补给柜台

JinxCasinoShopCounter保存s1.supply、三件商品、六个物理目标与报价/回执铭牌；JinxCasinoShopHost把选择、购买、库存使用接入已有PurchaseItem、UseAdventureItem及CancelPreparedItem。选中商品不扣款，支付仍由领域请求处理。ShopHost与机台共用输入上下文、TableSelection和TableFocus，后者支持有自身聚焦挂点的Behaviour；关闭、失效及回Hub恢复借用相机并清理目标订阅。

柜台与机台按玩家到交互锚点距离决定接近入口；探索移动不因柜台另建逻辑。LB/RB切换商品目标，确认提交当前目标，次要动作使用选中商品库存。商品报价读取本局配置、余额与库存；旧存档没有新商品时显示未开放。此链仍需Android/Xbox真机体验验收。


## S1互动教学宿主与界面

JinxCasinoTutorialHost在新的Practice中同步启动教学，并把实际应用的视角、CharacterController位移、成功物件操作、柜台回执与已展示结果报告给原冒险聚合。视角/位移累计每0.15秒提交，保存前补齐累计但不递归刷新View。教学观察不占用经济操作同帧互斥。结果必须由具体机台PresentedSettlementSequence确认已完成演出；Restore的静态结果也可观察，无需再次付款。

教学入口与选择控件保存在原JinxCasinoImmersionHudView中，JinxCasinoImmersionHudTutorial作为同一Presenter的partial维护绑定和退订。教学条不接管桌面焦点；Ready/完成选择等待离桌过渡结束，或由玩家暂停时打开。暂停菜单可再次打开此前暂缓的教学结果，避免“稍后”后无入口。重玩/新标准冒险共用明确确认卡，取消不替换聚合；跳过保留已投入局。

SetImmersionMenuState可选取消回调仅在菜单打开时保存。Core Cancel及Demo手柄Menu由同一菜单返回函数处理，同帧至多退一层，确认窗口取消后保留原暂停。旧未配置教学HUD保持原入口；View退订和Controller销毁都清理回调。教学仅限制单次投入10，不保证获胜、不补发筹码，不把练习资金带入新正式局。


## 沉浸HUD的三槽存档

JinxCasinoImmersionHudSave复用GetSaveSlotInfo、SaveAdventure和LoadAdventure；不另建文件格式或存储目录。主菜单读取、暂停保存/读取共用三槽列表；空槽禁用读取，覆盖已有槽及替换当前局先进入确认卡。取消只退当前层级并保留暂停，读取成功后显式继续，后台/手柄断连仍由PauseState阻止恢复。

槽摘要仅在打开和操作后重读，包含模式、区域、筹码、时间及备份恢复提示。确认前重新读取可用性，并核对打开列表时的RunId，避免界面旧选择写入另一个新局。保存后继续使用选定槽的原自动保存契约。Core Cancel和手柄Menu统一经过CancelSaveWindow，未打开存档时交回教学/暂停逻辑；自身按帧去重，菜单销毁清理监听。


## S1现场验票、撤离与结果卡

JinxCasinoExitHost绑定s1.verify和s1.leave两件保存物件。交互同时检查接近距离、相机视口和无遮挡射线，背对出口不显示可交互提示。标准局达标后在验票口调用原CompleteStage，筹码仅作为达标条件，不扣除额度；Finale在离场口调用原LeaveWithDignity。提前撤离须在同一物件五秒内再次交互，暂停冻结意图时钟，离开、读档或换局清除意图。

活动牌局与Closing保留原机台操作，出口拒绝结束未结算局。结局卡仅在真正Ended且无活动局、相机聚焦已退出时出现，显示原快照和成长写入结果；不自行记战绩或再次选择结局。三槽界面优先于结局卡；取消保存回到结果，只有明确返回按钮请求Hub导航。该出口暂服务StageCount为1的S1标准样板，多区推进随S3设施一起扩展。
