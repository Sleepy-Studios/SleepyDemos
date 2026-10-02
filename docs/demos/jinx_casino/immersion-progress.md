# 单机沉浸进度

更新：2026-10-02。Goal active，无Token预算。当前 S0/S1准备中；S1体验尚未实现或验收，S2禁止提前扩展。

## 状态

|项|当前事实|缺口|
|---|---|---|
|原型归档|按依赖分五笔提交并推送；旧P4包保留|文档修正及S0设计仍整合|
|视觉与桌面|已确定三样板及实体契约；场景/输入候选在Library隔离|尚未装配运行|
|手柄|现有原型无Gamepad完整路径|路由候选、菜单、桌面、断连及真机待验|
|教学|新目标明确动作驱动|实现和新手实玩待验|
|样板交付|尚无新包和录像|三机台、区域、三输入、双端证据|
|用户门槛|尚未提交S1试玩验收|确认之前不扩S2|

## 已推送基线

- 9b9b415：公共离线会话协议、测试、边界文档。未来网络方案未决定，不继续扩建。
- e2d3149：赌场规则、存档、成长和直接相关测试。
- 13f26a9：原型美术、音效与可复现DCC源；排除自动备份。
- c0e4fb0：宿主、旧界面、场景、资源装配、Hub与字体及对应测试。
- 1cc64e7：双平台离线构建与配置恢复。

基线拆分未改变生产逻辑，采用原有Unity Test Runner和构建记录；本轮另查Console为0错误。git diff检查源码/文档通过；Unity生成meta空键尾空格原样保留。未执行全量测试。以上不证明新沉浸体验完成。

## 保留与阻塞

保留无关UnitySkills配置、vTabs与Mobile_RPAsset工作区状态。旧Blender自动备份未纳入源码。未获得Android真机运行及Xbox实物验证证据；可先实现和模拟输入回归，但不能宣称实物验收。S1用户验收是明确门槛，不能用自动化代替。

## S1实体交互基础验证

2026-10-02：JinxCasinoTableInteractionTests，PlayMode作业e0348f14，5/5通过；真实Collider射线遮挡/跨台拒绝、目标禁用/导航、嵌套聚焦拒绝、移动挂点跟随、退出与失效精确恢复相机。正式Editor编译完成、Console0错误。未执行全量测试；尚未接入Controller和机台资源，不代表视觉或三输入实玩。

计划切换文档已推送399e676。S0可恢复原型与规范落盘，S1基础代码开始实现。

## S1三设备输入基础验证

实体基础已推送8d9edee。独立输入资产通过Editor菜单保存并校验；Editor正式编译、Console0错误。JinxCasinoInputStateTests：dd1d77e2，EditMode 5/5。输入PlayMode初测2565032b为6/7；失败因测试释放帧未执行宿主要求的ReadFrame，补齐采样且保留断言。审查修复手柄Disabled后未发送震动归零，增加实际InputSystem.DisableDevice用例；重测825483dc为8/8。未执行全量测试。

以上为InputAction/模拟设备与Core同类UI模块的回归，不等于Xbox硬件、Android或完整宿主输入验收。输入设置持久化迁移、Controller/菜单/桌面接入、场景演出暂停仍待完成。新场地及三机台源制作中，尚无样板包。

## 焦点生命周期修复

三设备输入基础已推送ac22eca。审查发现同目标重新可用时高亮不恢复、目标销毁后导航访问失效对象；已做窄范围修复。JinxCasinoTableInteractionTests扩展后94ac799d，PlayMode7/7通过。当前直接相关范围为桌面7、输入状态5、输入路由8，共20项通过，未执行全量测试。S1尚缺Controller/机台规则接入、真实模型、教学、菜单/HUD、双端包与录像；用户样板验收未触发，Goal仍active。

下一闭环：整合独立S1模型与蓝图、稳定机台实例存档、宿主输入和桌面命令、暂停演出、精简HUD与教学。仅完成基础设施不计S1体验完成。

## S1机台定位与存档迁移

规则增加活动机台/最近结算机台定位；错误机台操作拒绝且不改变筹码、活动局或随机状态。版本1只读迁移，旧局显式同玩法认领，首次写新版保留不滚动的原件备份。新桌面宿主尚待接入这些接口。

Unity Test Runner：CasinoStationIdentityTests 5eaebcd1，4/4；CasinoAdventureTests 78b0253b，24/24；CasinoLocalSaveStoreTests 8334babd，4/4。未执行全量测试。三机台和大厅首轮DCC预览已实际查看，要求继续改善大厅空间/材质及庄家牌位；还未进入Unity模型门禁和视觉验收。

审查追加：Restore拒绝ActiveGame与活动局实际玩法不一致的快照；Save同时保护主档和普通.bak中的合法v1原件。扩展后CasinoStationIdentityTests作业37a3c3f4，5/5通过。与本轮冒险24项、三槽4项合计33项直接相关断言用例通过；没有执行全量测试，也不作为新桌面Player验收。

## 手柄偏好迁移

机台定位已推送25636e3。本机偏好v1→v2增加手柄参数并保留旧设置、原存储键和明确保存语义。CasinoLocalPreferencesMigrationTests作业11b0eca1，5/5；CasinoLocalPreferencesTests作业d245acad，8/8。正式Editor编译通过，未执行全量测试。控件及Host实际应用待接入，未冒充Xbox硬件验收。

## 桌面鼠标与触屏指针

手柄偏好迁移已推送ebe03f6。Table新增Point/Click动作，统一返回屏幕坐标和单次按下，供实体射线选择；Editor更新实际资产并保留原动作ID。工厂配置测试27910950为10/10；改读保存资产后d163e366为4/10（键鼠/触屏未收到输入），精确诊断25e2d760为1/1并记录设备启用、应用聚焦、原始坐标及绑定正常。为排除Editor焦点分流对合成设备的影响，测试用临时InputSettings且结束恢复；实际资产整组重测1637ab29，10/10。一次性诊断日志已移除，生产后台策略未改。正式编译通过，无全量测试；未将合成事件当作手机/Xbox真机验收。

## 三款桌面玩法适配

JinxCasinoTableSessionTests作业ab91c93f，EditMode7/7通过：Slots明确拉柄/幂等、筹码面额与同帧去重、准备规则变化失效、Blackjack要停牌与离返、Levers真实窗口/NPC边界、旧局认领及公开投影隔离、较早存档拒绝重试旧成功请求。源使用真实CasinoAdventureSession，没有第二套开奖。宿主输入/聚焦连接已写入工作区，正在独立审查和装配，尚未正式实玩验收。

## S1独立模型导入

三款桌面规则适配已推送c75190e。新S1Slots/S1Blackjack/S1Levers/S1Hall进入Art/Immersion独立目录，Source与FBX的SHA256一致，独立12共享URP材质未改旧原型palette。JinxCasinoImmersionModelTests作业b2ddc5da，EditMode5/5：四模型无轴向/缩放补偿、无导入相机/Collider、材质引用、单模型面数预算，以及22个操作物件真实Unity坐标。总源面数101871含全牌库，非单帧绘制量。尚未以Unity场景画面验收，DCC预览不能替代。

## 宿主整合工作区检查点

已推送c75190e（三款桌面规则）和61cbbfc（独立模型/材质/源）。工作区新增ImmersionHost与Controller薄适配，接入输入、相机聚焦、具体目标命令、离桌、偏好应用；新局/读档/跨区传送前清理旧桌面，旧表现组件也按实例ID过滤。PointerMoved避免静止鼠标覆盖方向键，输入回归48238536为10/10。最新代码正式编译无错误，但新场景尚未装配，Host及这些接缝暂不作为实玩通过，也尚未提交。

接下来：合并Library中的暂停演出小patch及三款专属表现候选，复用LayoutBuilder生成独立样板场景/精简HUD，接入明确菜单焦点与互动教学，然后实际指针/手柄焦点、相机恢复和完整一局验证。旧SceneEffects/MovingTable的wall-clock暂停仍待接通，不能仅以领域冻结宣称完整暂停。

## 输入焦点与暂停接缝复核

独立Immersion场景与薄HUD已保存，三款专属表现组件已接入工作区；正式Hub仍指向可恢复旧原型。样板缺少教学、购物、设置/存档界面和实际运行验收，当前不作为完整S1提交成果。

输入修复：静止指针不再覆盖键盘目标导航；重新锁定光标时只丢弃一次视角增量，保留离散交互。InputRouter整组ec37f25f为10/11，新用例漏传真实锁定状态导致失败；补齐pointerLocked后精确重测0c98fc15为1/1，其余10项未变，无全量测试。

暂停整合：PresentationClockTests 651e38e8为3/3；PresentationPauseTests 1b8cd396为2/3。移动桌测试原fixture将根节点悬空，触发防悬空保护；改为地面根+抬高Collider中心后，精确重测a7234176为1/1，未改生产安全判断。以上暂停组件仍随宿主整合留在工作区，不能视作完整场景暂停验收。

## 起始机台范围与旧档兼容

输入焦点修复已推送c731601。领域配置增加InitiallyAvailableGames，让独立样板能在首区开放协作拉杆，同时保留AllowedGames最终限制。旧存档缺少字段时仍按原区域解锁，不扩大旧原型内容。

三个精确EditMode方法通过：db601de4（默认/旧档缺字段）、b43ce73f（三样板实际拉杆投入及恢复幂等）、8ed0bf3c（标准/练习恢复后不能绕过AllowedGames），共3/3。未执行全量测试。保存的样板配置曾仍是默认值，正在用独立分步装配入口修正；生产规则通过不代表资产或实玩通过。

## 独立场景保存验证与首张Unity画面

起始范围规则已推送f4fa4d1。用SerializedObject分步更新独立ImmersionSettings，实际资产已核查为1区、1200目标、Slots/BJ/Levers、事件暂关闭。新增JinxCasinoImmersionSceneTests作业c47edf16，EditMode3/3：保存配置实际解锁、唯一相机/Listener、三台稳定ID与22目标、专属表现及HUD引用。Editor编译Console0错误；未执行全量测试。

实际Unity Camera截图已查看，证据Library/JinxCasino/ImmersionStaging/UnityHall.png。三机台轮廓可辨，但顶棚偏灰、地面过亮、铭牌小字需聚焦近景检查，视觉未验收。截图后恢复干净AppEntrance场景；此截图不含运行HUD，也不是Player实玩。宿主、场景、表现及装配仍为工作区整合，需进一步运行验证后提交。

二十一点翻牌候选已在Library/JinxCasino/ImmersionStaging/BlackjackFlip冻结，尚未合并；下一步合并并验证暗牌翻面/暂停/恢复，再补正式样板入口、教学、菜单与完整游玩。完整S1包、录像、Android/Xbox实物和用户体验验收均待完成。

## S1真实入口与首笔实体投入

工作分支Hub的JinxCasino地址已切到Immersion；旧Main场景保留。Controller在保存输入配置存在时选择沉浸HUD，Core启动/资源/View生命周期未另建入口。窗口焦点、物理射线与相机过渡通过正式AppEntrance→Hub链验证。

二十一点：合并实体暗牌翻转；偶发长帧时单帧演出最多推进80ms，避免整段翻面跳过，规则时钟不受影响。夹具先修Unity6 SceneHandle类型及延迟sceneLoaded隔离；7b02e2c2因工具回调文件共享冲突失败、无测试结果。f411551c加载夹具失败，b44a30d0未捕捉中间姿态，诊断精确2c3d48e2为1/1；加演出步长上限后e80f42f4整类2/2通过，覆盖背面/中间姿态/公开牌面、暂停、Restore不重播及新Run清队列。

实体入口：2747b5c1失败于拉杆射线，07251915确认命中LocalPlayer。保存本地胶囊改Ignore Raycast层，仍保留物理碰撞；动态铭牌原.040米行高不足，按真实字库指标提高至.046米。保存资源6e413965为4/4，包含实际TMP可见字符断言。a9926bbe完成首笔投入后在暂停等待超时；夹具原FOV容差提前判定过渡结束，改等待光标重新锁定、探索输入就绪后再发下一次Esc。

最终JinxCasinoImmersionEntryTests精确作业c2b2d2ec为1/1：实际InputSystem鼠标点击保存菜单、W/A移动、E聚焦、射线点筹码/确认/拉杆、一次正确结算、离桌精确恢复相机、Esc暂停/真实继续按钮、真实返回按钮回Hub及唯一Listener/UI清理。使用独立GUID存档，未触碰用户存档；没有直接调用按钮监听器或机台业务命令替代输入。未执行全量测试。

运行截图在Library/JinxCasino/Verification/S1MainMenu-*、S1SlotsFocus-*、S1Paused-*；已查看焦点前后图，金额铭牌已可见。当前仍存在全局画质入口与暂停区域重叠、地面过亮、赔率未在桌面完整展示等体验缺口。新教学、完整设置/存档/购物、Xbox/触屏完整实玩、S1包/录像及用户验收仍待完成。旧原型UI测试以Main/旧HUD为对象，不将它们当作新场景验证。
宿主、专属演出与暂停代码已按依赖先行推送de4e139；下一笔保存场景、HUD与正式入口及实际回归。

## 桌面完整规则牌

入口场景与真实操作已推送c8b86b7。三台各增加保存的实体夹板，显示完整规则、最高投入、当前返还加成与预备道具；表现不再截掉第二行。水果机明确香蕉最高奖，合拍提示改为玩家可观察的绿灯操作。夹板有物理支架与遮挡，不是运行时通用弹窗。

初次入口回归3a934c0a通过但实际截图显示牌板出镜，因此增加聚焦视口检查；2f93632e/50800d0a分别发现水果机横向、牌桌顶端越界。调整并同步两份S1Layout聚焦数据后，4e75a407精确EditMode1/1：三台全部22操作目标与规则牌四角位于16:9聚焦视口内。最终入口f9a6d11f为1/1，实际指针操作仍畅通，规则正文与真实TableView一致且没有TMP截断；已查看新的水果机聚焦截图，完整规则牌可读。倍率/上限/加成随后仅作分行排版改善。未执行全量测试，也不将16:9截图当Android真机验收。

分步菜单“更新桌面规则铭牌”仅更新夹板与蓝图聚焦挂点；机台模型、材质人工调整保留。样板仍缺互动教学、完整设置/存档/购物与双设备实玩。下一闭环处理公共画质入口覆盖暂停按钮；候选已放Library，尚未合并验证。

## 公共画质入口遮挡修复

完整规则牌与三台聚焦视角已推送7feed12。GraphicsSettingsUI新增主线程同步显隐作用域；DlssSettingsView仅隐藏自身OpenButton/SettingsPanel子树，保留View根、缓存和UI栈，最后释放恢复原开闭。赌场HUD显示时借用，隐藏/销毁时幂等释放；公共Initialize原签名与渲染服务不变。

GraphicsSettingsEntryScopeTests首轮2e8de926为0/3，夹具在异步启动完成前访问空Navigator；补齐等待后61506692为3/3，覆盖初始化顺序、嵌套与重复Dispose、面板原状态、旧View退订与替换实例。最终实体入口4ffb6b7a为1/1：新增桌面暂停按钮真实鼠标点击、继续、退出后公共入口恢复断言；原首笔筹码/拉柄/结算/相机链仍通过。已查看最新聚焦截图，右上仅显示暂停，左侧完整规则与分行加成可读。正式Editor编译无错误，未执行全量测试；不作为Windows Player、Android或Xbox实物验收。

下一闭环优先互动教学与实体柜台购物，继续补S1完整体验，不扩展S2。

## 实体补给柜台闭环

公共画质入口修复已推送1cd1aea。本次在大厅原售票台装配s1.supply：三件实物商品、顶部报价牌、实体购买/库存/说明按钮，复用原Purchase/UseItem与唯一相机。新增通用焦点owner重载及柜台目标绑定，旧机台规则未另建钱包。新局商店只售双人扳手、重抽牌、止损券；旧存档售卖配置保留，已有库存不受售卖列表限制。

实际入口ec02c68e、e35cf39f各1/1通过购买使用与随后水果机结算；截图发现商品过大、拱门遮报价，不能只按测试通过验收。前移报价后f4149eb9失败，真实射线被QuoteFrame遮住；改成拱门顶部短招牌，最终ee816431为1/1，已查看真实截图S1SupplyCounter-20261002090550-*，商品/价格/按钮及完整报价可见，物件射线不穿透招牌。资金1000→900，库存扳手1→0、协作帮助+1，离柜恢复相机后仍完成真实水果机投入/结算/暂停/返回Hub。使用独立GUID测试存档。

泛化聚焦/导航回归51e099e2为7/7。静态复核修复柜台组件单独销毁时的订阅释放，以及装配激活目标场景、缺资源预检和失败仅清理本次新建根节点；不覆盖用户已有柜台。保存场景按序列化对象对照，原466对象无删除，只有Controller绑定及父节点子列表修改，新增115对象；Unity重排文档块造成行级diff较大，不是重建旧机台。

教学规则候选仍在Library，未合并，不能当成已完成教学。当前未执行全量测试，未有新S1 Player包、Android/Xbox真机或用户体验验收；整体美术、互动教学、完整菜单与S1其余闭环继续推进。

旧库存按钮精确EditMode：bb533c31首轮因测试夹具Config未初始化失败；修正并显式重新编译后fe99afc3为1/1，确认不售商品仍可用已有库存、无库存禁用且展示不扣款。Editor Console0错误，未运行全量测试。

## 教学规则与持久检查点

实体柜台已推送364d2d6。教学规则已从审阅候选移植到Hotfix Demo现有Adventure聚合，State.Teaching与SchemaVersion3一同持久化。限定新的单人Practice；观察真实操作/已展示结果，Ready须明确确认，Skip只停提示。旧v1/v2读取只内存迁移，后续保存按原版本和checksum归档原件。未另建钱包、随机、存档文件或生产程序集。

本轮Unity Test Runner：4fac0021教学规则12/12，95b5a046教学三槽/旧档3/3，b37c7e10现有冒险24/24，fdbae6db机台定位5/5。包括真规则完成三局、天然二十一点、NPC独自超时拒绝、已付结果和活动局恢复、重复观察不写回执、旧回执裁剪、fresh入口约束、旧文件字节不变及迁移原件不覆盖。结果只是规则证据，宿主/UI候选仍在准备，尚无新的教学场景实玩证据。

直接受影响补验：e8925ac3原三槽4/4，683d93bb旧档缺起始机台字段精确1/1；共49项直接相关检查通过。更新旧测试模拟版本时取当前SchemaVersion，避免硬编码v2导致实际未模拟旧版。Editor编译Console0错误，未执行全量测试。


教学规则/迁移已推送8f86995。宿主真实事实接线已合入工作区：新Practice同调用启动教学、实际旋转/位移累计、聚焦就绪、成功物件回执、已展示结果序号与离桌过渡，保存前flush不触发View递归。Host编译Console0错误；原实体入口b93d3aa8为1/1，柜台购买/使用、水果机结算、暂停与返回未回归。教学HUD候选和真实教学流程测试仍待合并，宿主暂不单独标记体验完成。

教学表现观察补验：eb160aeb二十一点翻牌2/2，新增断言确保翻牌中PresentedSettlementSequence为0，全部演出结束后才返回结算序号；Restore静态结果可立即观察，活动快照和新局归零。此证据验证宿主结果观察接缝，仍不能替代真实教学流程。


## 保存教学HUD与真实教学路线

接入既有HUD的教学入口、非模态提示、Ready/结束选择、暂停跳过/重来与共用确认卡。新增暂停“完成教学/教学后选择”回开入口，暂缓不会丢失完成路径。手柄Menu在确认卡中统一走取消回调，不解除原暂停；Core Cancel与Menu同帧只退一层。核对保存inputactions后确认Menu/Pause仅手柄Start，早期静态审查所说“Esc双绑定”不成立；修复针对实际Start切暂停路径。

首轮真实教学1cb21af8通过水果机后，夹具在二十一点相机过渡未完成前取屏幕坐标失败；增加实际聚焦位置等待，853c114c完整教学1/1。截图仍有旧原型毫秒/周期/编号输出，改为专属机台结果加简短HUD提示，并缩窄教学条、按有无反馈收起空白。

最终JinxCasinoImmersionEntryTests作业80c75331为2/2：原普通入口+柜台/水果机/暂停/回Hub链，以及真实教学入口→视角/步行→水果机确认/拉柄/演出→离桌→BJ停牌/结算→柜台买用扳手→绿灯真实拉杆+NPC结算→离桌→稍后完成→暂停回开→明确完成→继续原Practice。包括InputSystem模拟手柄Start取消重玩后保持暂停、RunId不变，再用真实按钮继续。各机台操作走保存物件的真实Collider射线，不直接调用监听器或Observe推进教学。独立GUID存档，未接触用户存档。

已查看最新S1TutorialReady-20261002093856-*及水果机截图，底部不再显示内部周期/毫秒，教学条不再横盖大段机台标题。Prefab按对象对比保留原136对象，新增160、修改13、无删除，GUID/MVC入口保留。Editor编译0错误，未执行全量测试。此为Editor实输证据，模拟Gamepad不等同Xbox实物；Windows Player、Android真机、触控/Xbox完整流程和视频仍待完成。合拍台规则牌右缘仍有物理遮挡，正式美术/教学演示节奏、存档设置入口及标准局阶段/结局闭环需继续打磨，S1未验收。


## 合拍台规则与仪表同时可见

教学宿主/HUD已推送9e590c0。本轮只调整合拍台规则牌的场景位置及聚焦距离，同步ArtSource与运行时S1Layout。原先规则牌右侧被机身盖住；前移第一版3a95711a发现左缘越界，第二版020275de发现顶缘贴HUD；a3d090d4虽视口1/1通过，1d9ea09b实输1/1后的截图仍发现牌板盖住玩家仪表，未据此宣布修复。

最终将夹板放在仪表外侧并把聚焦点后移至本地z3.25，保留48度FOV；fcb488a5视口精确1/1、ed5e724a完整真实教学1/1。已查看S1TutorialReady-20261002095329-*，两侧圆盘、指针、拉杆与完整规则同时可见；实际物件射线操作通过。规则装配入口明确激活目标场景再创建缺失物件，finally恢复原场景，避免创建到其它已加载场景。

Editor编译0错误，未执行全量测试。此项修复不代表整体美术或手机可读性已验收；三槽界面、标准局离场与设置仍在接入。


## 三槽存档的真实菜单接入

规则牌视野修复已推送c3db403。三槽Presenter与保存Prefab接入原Store/Host，空槽不可读，覆盖/读取替换均有确认，取消保留文件和暂停；场景返回后继续同一目录。教学装配工具增加已有存档入口识别，避免重复装配把四按钮导航/布局退回三按钮。

7139bdec精确真实菜单回归1/1：三个槽实际保存、取消覆盖保持文件字节、取消读取保持钱包/随机/所选槽、确认回到旧进度，返回Hub并从新实例主菜单读取已结算水果机，不重复扣款或开奖。补充模拟Gamepad南键返回空列表、十字键选第二槽与南键确认后，617f7635整个直接相关入口类3/3（普通、三槽、教学）；所有业务操作通过实际InputSystem/保存按钮/物理射线，独立GUID测试目录不触碰用户存档。已查看S1ThreeSaveSlots运行截图，三个摘要与返回控件完整可读。

最后重复“装配互动教学HUD”前后保存Prefab SHA256完全相同（75F39EE824C15AAA7615DDBCFC66043A726DD889D3B629AA374DFF6756A07988），证明该操作保留新存档布局；Editor编译0错误。没有全量测试，也不将模拟手柄当Xbox实物验收。当前仍缺标准局现场离场/结局、完整设置及更多视觉打磨；现场出口候选仅在Library，尚未合并。


## 标准样板的现场验票与离场

三槽界面已推送5d1f60c。本次沿用原冒险和成长规则，在入口两侧保存验票/离场物件，接近且朝向可见、射线无遮挡才能交互。达标核验只检查额度，不扣除额度；领取离场券才提交体面结局。提前撤离须再次交互，暂停保持短时意图，离开物件清除确认。结果卡可保存再返回Hub，取消键不隐式退场。

2688a8e2初版成功路径1/1，但截图发现铭牌位于面板背后、再次靠近时残留旧未达标反馈；上移操作件并把文字前移到面板正面，离开/阶段改变清反馈。撤离实际输入b6760b94为1/1，覆盖第一次不结束、暂停保持确认、离开取消、重新两次确认、结局保存以及成长只登记一次。已查看S1WithdrawalEnding-20261002102548-*，结局内容和两个控件可读。

b9281ceb恢复Closing二十一点精确1/1：夹具用原领域规则构造已超时活动局并写独立测试槽，实际菜单读取、出口拒绝未结算离场、回原桌停牌、完成唯一结算、再确认撤离；余额/随机和成长断言通过。此项验证恢复边界，不代表实际等待四分钟或Player完整一局。

保存资源按序列化对象对照：Scene保留原581对象，新增64、修改2、无删除；HUD保留原414对象，新增40、修改2、无删除。修改只涉及Hotfix Demo宿主、Hotfix.Editor装配及Demo场景/HUD和直接相关Tests.PlayMode；没有新Core依赖或经济系统。

修正反馈后f60138cc成功路线仍1/1，但截图显示文字XY仍沿用旧值；定位到TextMeshPro的RectTransform保存锚定位置未随普通Transform修改落盘。装配工具改为明确写anchoredPosition3D并标脏，已核查两块文字各自保存为1.68/1.56高度；等待最终真实截图验证，不以文件值单独验收。

最终aae3473d成功路线1/1；已查看S1QuotaVerifier-20261002103320-*，完整金额/动作文字位于实体牌内，底部提示独立可读，无旧错误残留。Editor编译Console0错误，未执行全量测试；Windows Player、Android/Xbox实物和最终体验均待验。本次结果仍是S1小闭环，不是S1整体完成。


## 临时工具清理与输入框架归位

现场离场闭环已推送b39a292。按本次用户修正，删除20份一次性UI/场景/机台/配置装配Builder及meta，保留PlayerBuildPipeline、PlayerBuildWindow、BuildSceneProcessor三个正式构建工具；已保存场景、Prefab、模型、inputactions和GUID不变。运行时未使用的InputAsset生成工厂也清理。历史进度中的生成菜单只是当时证据，当前维护入口以runbooks为准。

删除PrototypeContracts、FormalContent、ModelContracts三类一次性生成/固定布局/初期AxisGate探针测试及其唯一夹具；ImmersionScene去掉3项固定布局快照，保留库存展示不扣款、唯一相机及机台接线、金额实际可见性3项行为契约。规则、存档、生命周期、正式模型契约和真实输入回归未删；不增加新的测试文件来锁定本次重构写法。

设备识别、死区/增量换算、输入上下文、菜单作用域、震动、触控采样和暂停门闩移入Core.Runtime.Inputs，Demo只消费公共类型并保留Exploration/Table/Menu动作资产及玩法命令。通用上下文为Gameplay/Interaction/Menu，Map名构造时指定。TouchInputPad保留原脚本GUID、isLookPad及MovedFrom；本机偏好键/schema未改。原三份输入测试随实现迁至Tests.Module，沿用两个现有测试程序集，不复制第二套。

首次编译发现迁移测试命名空间后GameViewResolution夹具不可见，改为明确复用原共享测试辅助类，无需复制。随后Editor编译0错误；9eb7b7d5输入数学/暂停5/5，a1196997实际InputSystem路由11/11，d36d560e触控归属/分辨率3/3。e6924a01保存场景接缝3/3，b446b88a真实入口1/1（柜台买用、水果机物理操作/结算、暂停及返回Hub）；共23项直接相关回归通过，未运行全量。实际Demo资产引用公共TouchInputPad正常，原inputactions无改动。

AGENTS/CLAUDE同步一次性Builder清理、有效测试保留和输入框架归属三条约定；公共模块及接入手册新增，当前Demo文档取消过时装配菜单。此项调整不改变S1体验门槛，不能代替Windows/Android Player及Xbox硬件验证。

本次只读复核：正式三构建文件未修改，删除类型无源码残余引用，Demo资源无差异，TouchInputPad新旧meta GUID完全相同。代码/文档差异检查通过；Unity新建目录meta的空字段尾随空格按序列化格式保留。未作Player构建，公共输入的AOT迁移在下一次双端构建时继续验证。


## 载入前检查实际场景能力

公共输入/清理已推送7795d6d。本轮修复有效旧档在新样板无法继续的问题：LoadAdventure在Store恢复后、清焦点及替换旅程前检查单人、当前区域、后续标准区域和活动机台。当前单区样板暂拒绝未开放的无尽；非空机台ID必须准确匹配实际实例，无ID旧档仍走既有显式认领。此门槛来自现阶段实际内容，不改变最终四区/无尽目标；原型宿主及领域迁移保持原流程。

Unity编译0错误。97fd18e0真实菜单兼容用例1/1，循环覆盖旧四阶段在Stage0、无尽、多人、缺失玩法活动局、同玩法旧实例ID；逐项断言拒绝后的Run/余额/随机/槽位/相机/暂停与主文件及备份字节不变。沿用现有EntryTests文件，只新增此项数据保护回归，没有生成器或新测试框架。42148004原Closing二十一点实际恢复路线1/1，确认合法S1已投入局仍可载入、停牌结算及离场。未执行全量测试、Player或真机验证。

后续S1重点为完整设置：当前沉浸入口尚未绑定原设置Presenter，因此不会主动LoadLocalPreferences；需一并接入偏好加载、预览/保存/取消与手柄控件，不另建偏好存储。该审查只是下一步依据，不作为设置已完成。


## 输入、声音设置与场景音源

存档保护已推送70ed2de。本轮复用原LocalPreferencesStore和LocalSettingsPresenter，将主菜单/暂停设置入口接入沉浸HUD，state9优先，关闭返回原菜单且保持暂停。入场绑定即读取原偏好；旧数据键/schema不变，三页分别提供键鼠/触屏、七项手柄参数和声音，预览/保存/取消调用Core公共输入及原音频服务。旧HUD缺新增控件时保留原参数，并修复关闭设置后再开旧表情页的根节点显隐。

发现样板场景原先没有音源，因此同时保存原AudioDirector与两个独立Music/Sfx源，绑定已有14段音频；没有新增Listener。场景新增7对象、修改2、删除0；HUD新增387对象、修改10、删除0，原引用和人工机台资产保留。

初次设置真实输入03274a4f为1/1，覆盖独立偏好键、手柄调滑条、预览不写盘、B取消、保存反转/倍率、暂停内静音预览、Menu取消仍暂停、音源mute恢复。截图却发现新Graphic落在默认层而受场景绘制影响，且Slider手柄高度被拉伸叠加；修为项目UI层与正确手柄尺寸。一次性装配脚本已从Assets连同meta删除，仅Library保留诊断材料，不进提交。

修正层与手柄后73984124设置1/1、443c9070原购物/机台/暂停/返回入口1/1；截图仍有半透明背景下的场景干扰，最终把设置卡改为实色。删除第二份临时修正脚本及meta后，6d2da946设置真实流程1/1。已查看S1GamepadSettings-20261002112413-*，各行标签、滑条和按钮清晰、无场景穿透干扰；主菜单读取/设置同行也已查看。最后Editor编译0错误，未执行全量测试。合成Gamepad和AudioSource状态断言不替代实际Xbox、Android或声音质量验收。


## 大厅背景材质与机台分离

设置闭环已推送75196cc。本轮只调整大厅背景：原36块地砖实际全部用PaperLight，与机台牌面共享，直接改原材质会影响操作辨识。新增HallFloor/HallCeiling/HallPlum/HallTeal四份独立材质，在S1Hall.Mesh和Hall.Ceiling.Mesh保存6处槽覆盖；铜金、原CreamYellow及三机台材质不改，FBX/挂点/碰撞未变。场景保存时Unity补入KeyLight的默认URP AdditionalLightData，灯光数量、强度和环境参数未改。

008a9cec原真实入口回归1/1，覆盖主菜单、柜台购买使用、水果机操作结算、暂停与返回。已查看S1MainMenu-20261002113556-*、S1SlotsFocus-20261002113603-*：地板由大片白亮降为暖灰，有可辨阴影；背景梅红/青绿和顶棚压低后，机台及规则牌仍可读。不新增锁定颜色/布局的测试，也未运行全量测试。暂不调整全场光强，避免降低桌面可读性。

临时材质脚本已从Assets与meta清理，Editor编译0错误；只保留四份正式材质、保存场景和视觉维护说明。此为S1视觉迭代，不代表最终美术、Android性能或用户体验已验收；后续继续改善演出、NPC及可玩包。


## S1独立包启动与返回主菜单

大厅材质已推送791391b。本轮Core HotfixConfig新增通用StartupScene字符串，HotfixEntry经场景目录解析并使用原Navigator直达目标，不先显示Hub；空值保持默认Hub，Core不引用赌场枚举。S1构建配置指定JinxCasino，Windows/Android与历史P0/P4分目录保存，版本0.5.0；原AppEntrance和默认配置不改。独立模式暂停/结局返回使用原ReloadCurrentAsync，主菜单有明确退出按钮，Editor仍回Hub。

94c38b55独立返回真实按钮流程1/1，确认重载后没有活动旅程、Hub未显示且只有一个Listener；该夹具在Editor设置导航启动方式，不能证明Player的StartupScene冷启动。已查看S1StandaloneMainMenu-20261002115127-*，新增退出控件可读且未遮挡其它入口。d19ad317默认Hub→购物/机台/暂停/返回流程1/1，原接入未回归。没有全量测试；退出按钮一次性装配脚本已删除。Windows/Android实际构建与启动仍待验证。

S1仍采集DefaultPackage既有范围，包内有历史资源；Android与旧P4同应用ID，安装可能替换旧应用。先完成独立启动验证，不把资源裁剪或完整S1验收当成已完成。

## Windows S1首次独立构建与实机启动

启动闭环已推送581ef2b。Windows IL2CPP构建20261002115613-84b9f866成功，流水线报告已恢复编辑器配置。产物位于Builds/JinxCasino/S1/StandaloneWindows64同名目录；分享ZIP为192817488字节、115项，含独立exe和S1说明。SHA256为2FAE40C7AC33A9E5F53CA42BF680CF973FA7F8A843EA96625CFB9B73B1F74FD6。当前仍为Development Build，包中包含Burst插件PDB及历史资源，尚非最终精简发布包。

实际Windows Player冷启动直接显示赌场主菜单，没有Hub；真实鼠标操作打开设置、切换手柄页、返回并进入自由练习。自动化Escape尚未观察到暂停菜单，锁定鼠标时点击屏幕暂停位置反而改变视角；没有对应Player异常。该问题保留待排查，不能以Editor用例通过替代Player键盘验收，也未将本次启动记为完整游玩。

本次构建后工作区仅保留原有无关改动及设置目录meta，没有残留临时Builder。Android S1构建、Xbox实物、完整标准局、演示录像及S1用户体验验收仍未完成。

## Windows前后台恢复与输入排查

同一Windows Player后续验证：切到编辑器再切回，会显示暂停菜单；真实鼠标点击返回主菜单成功重载赌场入口，未显示Hub；主菜单退出按钮正常结束进程。工具注入的Escape与Down在暂停菜单中也未观察到响应，不能只归因为探索Pause分支。只读复核动作映射、上下文取消回调、暂停状态和HUD接线，未发现足以确证根因的缺陷；保留实体键盘比较验证，不进行猜测性修补。此前Editor用例使用InputSystem虚拟KeyboardState，其证据不覆盖Windows原生键盘交付。

S1协作拉杆另有明确体验缺口：保存场景只有伙伴仪表与自动杆，没有可见助手。现有原创Avatar模型具备左右臂和头部关节，可用于后续保存场景装配；助手应由机台表现独占关节并消费真实NPC拉杆状态。旧AvatarPresentation使用独立非缩放时钟且重置关节，不能直接挂载为本机台动作控制器。目前仅完成审查，尚未装配或验收助手。

## Android S1首次独立构建

基于f95ec8b源码，通过现有Editor事务构建20261002122156-4e63d7ed。APK位于Builds/JinxCasino/S1/Android/20261002122156-4e63d7ed/Player/JinxCasinoS1-Offline.apk，120173881字节，SHA256为175F9855D1970D8602E3EEDFE5369B740323E0C7ED4CA3CBB238A301BABF479B。aapt确认应用com.sleepystudio.jinxcasino、版本0.5.0、minSdk26、targetSdk36、仅arm64-v8a；apksigner验证v2签名通过。包内有72项assets/yoo资源，未包含检查范围内的Streamline/DLSS/UnityPlayer Windows插件。

此项证明本次公共输入迁移及S1入口通过Android IL2CPP与资源构建，不证明实际安装、启动、离线加载或手柄可用。ADB设备列表为空，Android真机30分钟、后台恢复、安全区与触控/Xbox均待验。Windows启动验证所在机器为i7-12700KF、RTX3050、约64GB内存，未采集性能数据，也不替代指定GTX1650级性能目标。

事务最终报告Android S1构建成功且编辑器已恢复；git状态未留下新的平台配置/生成代码变更，Windows ZIP哈希与构建前相同。此次未修改C#、未新增或运行测试；修正运行手册中一处已删除布局测试的旧入口，改为实际机台可读性检查。S1仍待可见助手、完整实玩、录像与用户验收，未进入S2。

## 合拍台可见助手

双端构建记录已推送b1efc6e。本轮在保存场景的s1.sync旁装配现有原创Avatar，保留FBX引用、原材质与关节；没有增加Collider、Camera或Listener。S1LeversPresentation独占右臂与头部，按真实杆角驱动握点及点头，发条袖臂小幅伸缩保持手掌接触；不修改经济、规则或NPC拉杆时间。临时装配脚本及meta已删除，编辑器目录仍仅三个正式构建工具。

复用原StandardWinUsesVisibleVerifierAndDepartureBeforeRecordingOneEnding用例，增加助手真实动作、暂停冻结、Restore握点接触和资金不变断言。68ab8dff首轮在等待实际绿灯时失败，尚未走到新助手断言，原因未确证；随后补充实际投入检查、聚焦截图及新增暂停步骤的虚拟手柄夹具，9deb3a61精确1/1通过。没有放宽绿灯等待时间或新增测试类，没有全量测试。Editor编译及最终Console均无错误。

已查看S1LeverAssistantWaiting-20261002124812与S1LeverAssistantCompleted-20261002124813实际720p截图：助手脸部/伸臂可见，握点随杆拉下；主要操作、双仪表及完整规则牌仍可读。新Run与重复Restore路径经只读审查，未单独实测，不将本次断言扩大为全部恢复验收。此前Windows ZIP/Android APK尚不含本次助手，后续样板交付需重构建；真机、完整Player实玩、录像与S1用户验收仍待完成。

## 输入切换收口与暂停实施

助手已推送1c40120。本轮修正Android平台条件强制显示摇杆：Core Router在移动端初始为Touch，之后HUD只跟随实际设备；手柄收起触控区域，实际触屏可取回，漂移不抢占。新增会话私有touch*/press Action只识别设备、遵守白名单并Dispose释放，不另提交点击。原因来自本地InputSystem对由phase派生TouchPress的通用按钮枚举限制；0a823679、b2c023b3均复现探索态触屏切换失败，诊断未观察到触摸回调或鼠标覆盖日志，临时日志随后清理。

修复后94748e02公共触屏精确1/1，b525cfe5完整入口精确1/1，覆盖真实InputSystem触屏→漂移→手柄→键鼠及原购物/水果机/暂停/返回。截图发现桌面Esc离开与右上Esc暂停冲突，改为桌面点击暂停、探索Esc暂停；768af2b8入口再次1/1，已查看最终S1SlotsFocus-20261002130950和手柄探索截图。仅补充两个已有用例，无新增测试类、无全量测试、无Builder或资源装配；Console无错误。Android实际触屏/Xbox、Windows原生键盘问题仍未验，当前包不含本轮改动。

用户明确要求手头收口后暂停开发，重新研究原版、3A美术及成熟手游交互。当前功能/截图通过不改变其对画面和手机操作的否定反馈；S1质量门槛仍未通过。后续先给出研究、交互方案和美术概念稿供审阅，再决定重做方向，暂停自动扩展与继续装配。

## PrototypeV2 新计划与四稿（2026-10-02）

用户已批准「新原型精简与视觉优化计划」，恢复本轮执行。创建单机长期 Goal，不设 Token 预算；至多两个子 Agent。本轮先完成共享 UI、新原型唯一流程和简化复古方向的合同与审查。a949052 已推送：同步 AGENTS/CLAUDE，更新当前实施计划，归档批准记录，标记复杂方向 A / 手机专属页面 / 旧档兼容已被替代。

两名 Agent 只读审查赌场旧链与 Hub 接线，没有修改文件或运行 Unity。赌场需保留共用 CasinoGameKind、随机、实际使用的模型/音频、当前原子保存；删旧链不能按目录整包删除。移动 Presenter 命名空间时必须同步保存 Prefab 的 MvcBind 类型字符串、bindingKeys 和生成引用。Controller 原脚本 GUID 与场景序列化字段应保留，不使用永久旧类型别名兜底。实际代码仍处于旧结构，SchemaVersion=4 / PrototypeV2 尚未实现。

使用内置 imagegen 完成大厅、共享二十一点、Hub、Loading 四张简化稿。大厅清除机台前凳子；牌桌修正三个明牌的倒置角标，玩家 10+6=16、庄家 7+暗牌一致。Hub 卡片插画不是当前 Demo 实机截图；Loading 的 68% 仅为构图。四个 PNG 和完整提示词已保存到 ArtSource/jinx_casino/concepts/prototype-v2；[视觉审阅文档](architecture/prototype-v2-visual-review.md)记录还原约束与待确认状态。

本次交付仅为文档、设计参考和只读审查；无 C# / Scene / Prefab / 平台配置修改，无临时 Builder、无新增永久测试。检查限定差异与文档链接，逐图检查可读性和模型复杂度；不需要 Unity 编译/测试，未执行全量测试、Player 构建或真机。已有 ZIP / APK 不含新设计，不能作为本轮样板交付。

下一步按本轮明确门槛等待四稿视觉确认，再制作 Hub / 两种 Loading 正式 UI，后续清理赌场与制作三样板。S0 尚未完成，S1 体验未通过，未扩展 S2。参考稿提交记录以 git log 的「asset(jinx): 保存四张简化参考稿与还原约束」为准，不将提交本身作为用户确认。

## PrototypeV2 当前格式存档闭环

四稿已推送d19ee84，视觉确认尚未收到；正式资源不越过该门槛。本轮独立推进已授权的存档清理：CasinoAdventureState改为CurrentSchemaVersion=4，Restore只接受明确当前版本，删除v1→v2→v3转换。缺失版本先置0再Overwrite，避免字段默认值冒充合法版本。删除PreserveLegacySnapshot及旧迁移原件归档；保留checksum、完整规则校验、三槽原子替换、不同检查点备份、损坏主文件不覆盖有效备份。

冒险默认路径改为persistentDataPath/JinxCasino/PrototypeV2，成长使用其Profile子目录；不搜索、不搬移、不删除旧目录。三个存储类及folder meta移至Demo/Persistence，命名空间改为Hotfix.JinxCasino.Persistence，全部调用方同步，四份meta/GUID与提交前一致。这里均为普通C#存储类，没有修改场景/Prefab或MvCBind中的组件类型；其余Adapters、旧房间、旧面板、机台认领、偏好迁移仍待清理，不能声称全部旧链已删除。

删除StationIdentity、TutorialSaveStore中的旧档迁移/原件归档用例及专用夹具；Tutorial原迁移用例改为当前格式的拒绝与状态不变断言，Content改为当前存档还原后的区域解锁，不新增测试文件或程序集。保留教学、定位、余额/随机/回执、三槽和成长真实行为回归。JsonUtility对无任务内联null的归一化仍用于当前数据，不是旧档兼容，不随迁移一起误删。

Unity正式重新编译完成，最终Console 0错误。Unity Test Runner直接范围：391a6e7e教学/版本16/16；6ae6ef29三槽4/4；b3ce1cb8教学存档1/1；f755b71e定位3/3；b5794ff5默认区域解锁精确1/1；92dbc98a成长保存/RunId去重精确1/1。PlayMode实际InputSystem菜单保存/读取1163260b精确1/1，收尾二十一点原桌恢复/结算4d6fbf2c精确1/1；总28/28。进入/退出PlayMode时REST暂时不可用，恢复后查询原job，没有重复启动或重启Editor。未执行全量测试。

同步module、rules-baseline、runbook和实施状态。提交名「refactor(jinx): 切换当前存档并移除旧版本迁移」，SHA以git log为准。本轮不改美术和Hub、不新增Builder，未构建Player、未真机验证；已有包不含版本4。下一步可独立清理本机偏好旧迁移和机台认领，正式UI仍等四稿确认。

## PrototypeV2 本机偏好与测试合并

存档闭环已推送d23eb26。本机设置改用JinxCasino.PrototypeV2.LocalPreferences，当前完整偏好记录保持自身版本2，不读取原键、不迁移v1手柄参数。Load直接Overwrite偏好对象并校验，删除重复PreferencesRecord；缺版本/数值用0/NaN哨兵拒绝，坏记录返回完整默认值但不写盘。保留明确保存、保存失败恢复本键内存内容和独立副本预览。Core输入参数只修正文档注释，说明持久化由使用方负责，未修改设备换算、导航或公共输入行为。

删除CasinoLocalPreferencesMigrationTests及meta，迁移专属用例不再保留；手柄全部参数往返、输入DTO副本独立、合法0死区/0强度、坏参数拒绝和预览取消断言合并到CasinoLocalPreferencesTests已有方法。没有新测试文件、数量/布局/源码字符串快照或新程序集。此项整合后偏好测试为10项，低于合并前两类13项；数量变化仅记录事实，不是验收标准。

正式编译后Console 0错误。76bd9c82 EditMode偏好10/10，09193fb5 PlayMode设置真实InputSystem流程精确1/1，覆盖鼠标选设置/保存、模拟手柄调节/B取消/Menu返回、当前记录重新读取、静音预览取消、暂停时钟和返回Hub。没有调用按钮监听器代替输入，没有全量测试；模拟设备仍不证明Xbox实物/Android触屏及后台验证。总计本轮存档与偏好相关36项EditMode、3项PlayMode通过。

同步module、rules-baseline与runbook，提交名「refactor(jinx): 精简本机偏好并删除迁移测试」，SHA见git log。未改正式UI/机台、美术、平台配置或构建流程；TMP动态补字资产、UnitySkills/vTabs等既有工作区改动继续保留、不纳入提交。下一步整理具体Game/玩家交互和删除旧局认领、旧房间/面板；四稿确认与S1用户体验门槛仍未通过，Goal保持未完成。

## 删除旧局认领与机台身份兜底

偏好清理已推送783e6d0。本轮删除规则BindActiveStation、Controller/转发接口的BindAdventureStation、TableView.NeedsLegacyClaim、Claim命令及确认恢复分支；公开状态只显示准确属于此桌的活动局或最近结算。已投入局没有准确实例ID时不能由同类机台接管。沉浸读档先验证当前区域的实际机台ID；缺失、错误或未装配的ID均在清聚焦/替换Run前拒绝，不重绑、不开奖、不扣款。

原LegacyClaim用例删除认领断言，保留公开投影副本不污染真实局、离桌/当前版本恢复不改随机和资金的回归。UnsupportedSaves原真实UI用例追加当前格式无机台ID的活动局，继续检查主/备文件字节、当前Run、余额、随机、槽和相机不变；不增加测试文件、数量快照或新的测试程序集。

Unity正式编译及最终Console 0错误。d2ec34f1 EditMode桌面7/7；c4ebc8ec PlayMode精确1/1，覆盖未开放区域/模式/人数/玩法、错误机台ID及缺ID拒绝；7dbdd5ae合法Closing二十一点原桌恢复/结算/撤离精确1/1。共9/9，未执行全量测试；测试仍使用Unity Test Runner和实际InputSystem指针，不直接调用按钮监听器。未修改保存场景/Prefab、未构建Player或真机，尚不证明最终画面和手机手感。

原审查Agent本轮只读细化下一步Game迁移，无文件修改或Unity操作。AdventureHost的状态/命令/计时/存档及ProfileHost的成长归具体Game，Controller保留场景同步、读档前校验/聚焦清理和保存前教学观察；TableSession直接持有Game/Station并删除转发接口。S1PresentationCoordinator在-100顺序的OnEnable即创建会话，因此Game须在其绑定前存在，不能等Controller.Start或await导航后才创建；缓存状态getter不得每次Capture。此为下一步实施依据，不表示拆分已完成。

同步rules-baseline和module，提交名「refactor(jinx): 删除旧局认领并要求原机台身份」，SHA见git log。旧P0/P4链和完整Adapters清理仍待完成，正式美术/Hub制作仍等四稿确认；不以本次代码回归越过S1用户验收。

## 具体Game与直接桌面会话

旧局认领清理已推送366d08c。本轮从Controller迁出唯一冒险聚合、缓存状态、计时、命令、三槽、教学规则调用与成长，新增具体JinxCasinoGame，不持有Controller/场景对象，构造不读取数据。State getter返回已捕获副本；经济操作使用实际帧号和稳定请求，回执重试仍由领域核对指纹。保存前教学观察只更新缓存、不递归场景；相位自动存档与候选成长原子提交由Game负责，失败保留可重试RunId。

Controller的Game属性在组件构造时建立，Awake绑定Changed/BeforeRunReplacement/ValidatingRestore/BeforeSave，销毁前保存并退订。场景侧只负责真实距离、区域/角色/镜头清理、设置和演出；读档先验证候选，恢复标志覆盖表现通知。早于Controller.Start启用的S1PresentationCoordinator直接使用现有Game，避免懒工厂或启动后补接口。当前9份场景Controller partial仍待后续PlayerInteraction归位，不声称全部职责拆分已经完成。

TableSession/Contracts移至Interaction，保留原meta；直接持有Game和真实Station，删除IJinxCasinoTableOperations、ControllerTableOperations及meta。状态、动作、公开表现和资金命令不再经Controller转发，Controller/HUD/音效/场景调用方与相关测试同步。删除旧ProfileHost partial及meta，成长逻辑归Game。测试原RuleHost接口夹具改为具体Game及真实Station/Target，无新增测试文件、程序集、固定数量/布局快照或生产Builder。

首轮正式编译0错误，c8df8cef EditMode桌面7/7；52d4eed8当前入口PlayMode类9/9，覆盖真实输入探索/购物/机台/教学、设置、三槽、重返、独立返回、两种结局与Closing牌桌恢复。未执行项目全量测试。这些证据覆盖保存场景中较早启用的表现协调器接入，不覆盖实际Xbox/Android或新参考稿视觉。

补查音效恢复去重时，d24638a6旧PresentationTests在等待旧HUD阶段超时，尚未到音效断言；将该有效用例迁入当前ImmersionEntryTests，不再使用旧HUD夹具，不删断言跳过。旧Avatar用例及其旧夹具仍待随旧表现清理。迁移中修正缺少HUD局部引用的编译接缝；bc7ade68在结局后错误点击暂停菜单返回失败，改为实际结局卡返回。bc0b2e4a精确1/1通过，覆盖重复请求、结算/结局恢复、换装通知不重播声音和注册音源音量/静音。最终17项相关用例通过，保留上述初次失败事实；迁移只重跑该精确方法，生产代码未因此改变。

Controller脚本及两份移动桌面meta逐项与HEAD核对一致；保存Scene/Prefab无改动，MvcBind组件类型与字段本次未迁移，不重生成原人工UI。Game/Interaction新meta由Editor产生；无关字体动态补字、UnitySkills/vTabs/平台设置等保留。同步README、实施状态、module和rules-baseline；代码属于Hotfix Demo，无Core或新程序集变化。正式资源仍等待四稿确认，当前包不包含本次重构。

提交名「refactor(jinx): 拆出具体游戏对象并删除桌面转发层」，SHA见git log。Game、直接桌面及调用方构成同一可编译依赖闭环，未等待整个重做阶段结束；下一闭环移出玩家交互并清理旧P0/P4流程。S1画面、Android触屏/Xbox、Windows原生键盘及新Player构建仍待验证，Goal不标记完成。

## 2026-10-03：删除旧入口、面板及场景资源

上一个Game闭环已推送5292add。本轮先独立提交并推送c9731ee「build(jinx): 收敛双平台样板构建入口」：三份正式Editor构建文件仅保留S1 Windows/Android及重打包，Start(BuildTarget)/RepackageWindowsPlayer(string)不再接受阶段参数。删除P0/P4菜单、旧场景/HUD选择及阶段兜底，StartupScene固定JinxCasino。保留平台切换、HybridCLR/YooAsset、签名检查、原生插件和配置/生成物备份恢复；没有另起构建或产生新Player包。

Controller删除P0网络服务/协调器、创建加入离房/下注API及旧键鼠移动分支，只连接当前Immersion输入和HUD。删除CasinoNetworkCoordinator、CasinoSession、P0请求/回执/快照；保留CasinoGameKind，把统一投入上限移至当前MiniGameRound，冒险配置和小游戏创建/恢复同步使用。移除旧Presenter模态绑定、表情/社交分支和旧Avatar/Station/任务/轮换表现；附近机台查找迁至当前输入宿主，HUD、教学与提交共用同一范围。当前8份Controller partial及完整Adapters归位仍待下一闭环，不把旧入口删除说成全部职责重构完成。

旧通用HUD及其Adventure/Profile/Social/选项/卡片/缩放源码删除；当前设置保留输入/音量预览保存和取消，不保留未装配旧表情字段。一次性Editor工具先验证保存场景/HUD无MissingScript，再重存移除旧settings/stationAnchors和五个空表情字段；删除前检查仓库保留资产依赖闭包。删除旧Main、通用HUD Prefab、SessionSettings/AdventureSettings、社交标记、七个旧任务目标、四区合并网格及旧图标/结局画。当前Scene/HUD各仅删除5行旧字段，meta/GUID没有变化。临时工具及meta随后删除，不提交Builder或菜单。现用助手/商品/效果候选模型、共享材质、14段绑定音频、CarriedGold与物理效果组件保留。

删除8个旧PlayMode入口/界面测试类、旧CasinoSession/NetworkCoordinator测试及旧65件模型数量合同。PresentationPause仅删除旧通用老虎机组件用例，保留实际物理效果/移动桌/按压暂停；音频去重先前已迁当前入口类。本次偏好测试改为公共输入参数/预览与明确保存断言，不保留旧输入数学包装；没有新建测试文件、程序集、布局快照或并行验证入口。

Unity正式编译与最终Console均0错误；直接相关Test Runner结果：43e384a8偏好EditMode10/10，c03212bc保存场景3/3，5261c8f2具体桌面7/7，5cb01e50当前入口PlayMode10/10，91f09f5d物理暂停2/2，共32/32。入口覆盖真实InputSystem指针、教学/购物/三台、三槽、重返/Closing恢复、结局/成长/音效去重及Hub往返；不通过调用按钮监听器代替操作。未执行全量测试，未新增Windows/Android构建或实物Xbox/手机验证；历史包不包含本轮修改。

同步README、实施状态、module、rules-baseline、运行手册以及公共场景/工具导航；总体AGENTS/CLAUDE已包含临时Builder清理、有效测试和新原型单轨规范，本轮无新增协作规则，不重复改写。代码在Hotfix Demo与Hotfix.Editor，未扩大Core、其他Demo或生产程序集。字体动态补字、UnitySkills/vTabs、平台设置和.blend1等无关修改保留。

本闭环提交名「refactor(jinx): 删除旧房间面板与原型资源」，实际SHA由Git提交记录和交付回复记录。四张简化稿仍待用户确认，正式Hub/Loading美术与三机台画面重做尚未开始；S1和Goal不标记完成。下一步拆出玩家交互并按真实职责移走其余Adapters，视觉确认后再制作正式资源。

## 2026-10-03：具体玩家职责与Adapters目录迁出

前一个旧流程闭环已推送80d0194。本轮把剩余8份Controller partial收成根目录单文件Controller；Game仍唯一持有冒险/存档/成长，场景入口装配Player与Settings、等待正式导航后激活输入、同步区域与恢复能力、保存和释放。PlayerInteraction是具体普通类，唯一消费Core输入并推进表现时钟/领域/相机聚焦，拥有桌面及柜台共用焦点与目标订阅。TutorialGuide独立观察真实动作和展示结果，ExitInteraction管理现场距离/视口/遮挡与撤离意图；没有新接口、工厂或Adapter，不在Controller补旧属性转发。退出改为先Flush/Save，再Dispose交互，避免先清未上报位移后保存。

LocalSettings独立管理Store、Value副本、Warning与Changed；构造不读盘，加载/预览/保存显式调用，失败不写值或通知。Controller订阅一次把设置应用到公共InputRouter与场景AudioDirector，设置UI管理自己的触控布局。旧LocalPreferences partial删除；沿用原测试类并补构造不隐式读盘、警告保留、副本隔离和通知/非法输入回归，无新测试文件或程序集。

33份现存源码通过Editor按职责移动并校验GUID：根Controller/GameSettings，Interaction为机台/玩家/教学/离场/柜台/区域，Presentation为三台表现/协调器/时钟/环境/音频，UI为保存HUD/View与设置。原脚本GUID全部相同，31份meta字节一致、2份仅LF/CRLF区别；旧Adapters目录及meta删除，无旧类型映射。场景/配置/HUD在Editor通过SerializedObject同步类型标识，避免普通保存仍留旧namespace；实际控件布局和物件引用保留。既有MvcBind重新生成Component并保持手写View，Presenter槽及bindingKey改为当前UI全名，输出移至UI/JinxCasinoImmersionHudView/View。两个触控区域同步为Core.Runtime.Inputs.TouchInputPad后删旧MovedFrom；Core仅移除这条旧赌场类型映射，输入功能仍是公共框架能力。一次性迁移源码/meta已删除，Editor目录仍只有正式平台构建入口。

首轮编译发现入口测试仍引用旧IsAdventureInputBlocked，迁到Player.IsMenuOpen后0错误。c5c466d1本机设置EditMode12/12，c8b17ee9保存场景3/3通过。首轮PlayMode834f3f30全部10项因启动HUD钱包读取空旅程而失败；源文件已改为无旅程时显示0，但最后修改后漏触发正式编译，Library/ScriptAssemblies/Hotfix.dll比该源文件旧32秒。test_cancel确认当前TestRunnerApi不支持取消，保留同一live任务等待自然完成（441秒），未在运行中改C#、退出Play或另起测试。随后正式重新编译并等待恢复，重跑直接相关入口，不删除有效断言规避失败。

最终入口06e8cae4 PlayMode10/10通过，共享触控5ddeb3a3 PlayMode3/3通过；连同前述本机设置12与保存场景3，共28项相关回归通过，最终Console 0错误、Editor已退出测试且无编译。未执行全量测试，未生成新Player或验证实物Xbox/Android。提交名「refactor(jinx): 拆分玩家职责并移除适配器目录」，实际SHA由Git提交记录和交付回复记录。四稿仍待确认，Hub/Loading及三台的新正式美术尚未制作；本轮重构与资源接线不代表S1体验、真机或Goal完成。
