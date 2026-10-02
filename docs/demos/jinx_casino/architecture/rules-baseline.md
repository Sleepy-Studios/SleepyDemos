# 单机规则原型基线

此文记录沉浸重做前可复用的规则与持久化边界，不能作为体验完成证明。

PrototypeV2只保证新版本。冒险已改为SchemaVersion=4，三槽和档案默认使用PrototypeV2目录；不迁移v1/v2/v3，也不保存旧迁移原件。本机偏好使用独立PrototypeV2键，只读当前完整记录。旧局认领已删除，旧房间、旧面板及Controller职责拆分仍待后续清理，不能将存档闭环扩大为全部旧链已删除。

## 入口与职责

- `Rules/CasinoSession.cs`：早期三机台资金原型；保留历史适配依赖。
- `Rules/MiniGames/CasinoMiniGameRound.cs`：17款独立规则、操作路由、随机状态和公开表现数据。
- `Rules/Adventure/CasinoAdventureSession.cs`：阶段、钱包、库存、事件、活动局、请求去重与结算。
- `Rules/Progression/`：永久成长和解锁目录；练习不计正式战绩。
- `Persistence/`：三槽冒险、永久档案的原子写入与损坏恢复，本机偏好单独保存。

表现读取公开数据，不读取隐藏牌、密码或秘密报价。动画和物理不能决定收益。所有扣款、实际成交价、奖励、消耗通过规则入口执行；已提交操作不能因关闭界面或重返机台重复执行。

## 重构约束

当前冒险快照版本4保存机台定位与教学检查点，只接受明确声明的当前版本。重复恢复不改变资金和随机状态；不搜索旧数据目录、不把旧版本补成新版本。新桌面按具体机台定位同类固定桌和轮换桌。

规则文件不依赖输入设备、场景对象或网络SDK。输入与桌面表现适配留在Demo，公共框架不依赖赌场类型。

## 已有验证

2026-10-02 原型 Unity Test Runner 记录：小游戏35项、冒险23项、内容11项、三槽4项、公开表现13项、秘密竞价5项、成长7项、档案6项、协作帮助7项通过。对应作业记录保留在原型验证报告中；此处不是新的执行记录。

这些检查证明规则分支与存档断言，不证明实体桌面、真实设备输入、美术或完整Player体验。沉浸重构按受影响范围补验，禁止以测试数量替代用户试玩。

## 当前机台定位与保存

CasinoAdventureState现在保存ActiveStationId和LastStationId。新桌面BeginGame/Act必须传稳定实例ID，同玩法的其它机台请求被拒绝；指纹包含机台ID，相同requestId不能改成另一张桌。结算把活动定位移至最近结果定位，退出桌面不调用取消或重新Begin。

Restore只接受版本4，缺失版本同样拒绝；当前数据在Application.persistentDataPath/JinxCasino/PrototypeV2/save-N.json。BindActiveStation、BindAdventureStation、桌面认领状态及命令已删除；沉浸入口读取活动局时必须提供当前区域已保存的准确机台ID。缺失或错误ID不能按同类游戏补绑定，拒绝读取不改变当前旅程或存档。

三槽Save保留上一个不同的当前版本快照到普通.bak；重复保存不滚动备份，损坏主文件不能覆盖有效备份。读取不写盘，不创建.v1/.v2迁移原件。存档封装Version仍是1，内层冒险SchemaVersion为4；小游戏局和成长档案有自己的版本，不能统一替换所有SchemaVersion。

永久档案使用PrototypeV2/Profile/profile.json，保持独立原子保存、完整校验、RunId登记去重和损坏恢复。原JinxCasino目录不读取也不删除，开发测试仍可注入Library下的独立目录。

## 当前互动教学检查点

`CasinoAdventureTutorial`与`CasinoTutorialContracts`位于现有Rules/Adventure，Teaching嵌入冒险快照。StartTutorial仅允许新建的单人Practice且未推进时钟、下注、购物或库存操作；宿主必须在创建练习的同次调用立即启动教学。教学不改Practice模式、不补发筹码，也不计正式成长。

ObserveTutorial报告已经发生的动作，以具体机台ID、真实结算序号、新成功购买/使用回执验证推进。视角/移动用已应用的整数累计量，重复观察不加请求记录；宿主必须过滤暂停、相机聚焦和无实际位移，不能拿输入轴值当行为完成。RoundPresented须等物件结果真正展示后上报，规则验成功不等于画面已可读。

教学保存Look→Walk→水果机投入与结算→离桌→二十一点→买/用扳手→协作拉杆→Ready检查点；Ready须玩家明确Continue才完成。天然二十一点允许正常自动结算，拉杆超时或NPC单独动作不能完成教学。Skip只停引导，不取消已投入局、退款或改随机。

外部Restore只将未保存的水果机筹码/确认草稿回退到AddChips；已结算水果机与活动牌局/拉杆保持原局。失败事务回滚不调用此归一化，避免一次无效操作擦掉教学进度。购买回执按提交Revision比较，避免旧回执裁剪导致索引错认。
