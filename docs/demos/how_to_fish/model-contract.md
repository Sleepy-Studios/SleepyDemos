# 模型契约

用户已授权根据原作参考自主制定具体规格。2026-10-04，候选 FBX 的轴向门禁及 36 个模型结构测试已有通过证据；这不代表造型与原作一致性已验收。

## 坐标与资源

- 米制；Blender `-Y` 前、`+Z` 上，从自身朝向定义 `-X` 右；Unity `+Z` 前、`+Y` 上、`+X` 右；禁止负缩放。
- Blender 源文件保留在本 Demo 的源资产目录（位于 Assets 以外），仅导出 FBX、贴图等运行资源到 Demo Art 目录。
- 候选 FBX 必须包含前向、左右、上方标记和可动子节点；Unity Importer 证明朝向、尺度、局部轴正确后再固定导出参数。
- 不以运行时旋转补偿掩盖错误源坐标。每次导出保留版本、参数、源文件与哈希。

### 已验证的导出设置

共享入口为 `scripts/how_to_fish/fbx_export.py`。先保存原生 `.blend`，再仅在未保存的导出态对节点局部矩阵和 Mesh 使用 `RotationX(-90°)` 基变换。FBX 使用 `Z Forward / Y Up`、`bake_space_transform=true`、`use_space_transform=false`、米制文件单位。Unity Importer 启用 `bakeAxisConversion`，Scale 1，不导入灯光或相机。该设置避开 Blender 5.2 内置递归 Apply Transform 对孙节点的重复转换。

不能把 .json 中的 `unity_import_verified=false` 导出时默认值当成失败或成功结论；实际验证记录见 [验证记录](validation.md)，按 FBX 哈希对应。再次导出后需要重新运行受影响模型测试。

## 物理与表现

- 物理根保持单位缩放；视觉根、Collider、抓取与攻击挂点各有明确用途。
- 鱼类口部为 Hook 挂点；鱼体重心为物理参考，鱼鳍、尾巴、颌部按可动性制作，不把所有鱼种换色复用成同一轮廓。
- 工具以握持点为装备参考；枪口、鱼竿尖、抛壳点、换弹部件分别建节点。
- 船以船体中心为根；舵、驾驶位、上船落点、载物区域、浮力点独立于视觉网格。
- 人物手部需要握竿、持刀、抓取和持枪姿势；第一人称动画就地播放，角色位移由玩法控制。
- 使用 Unity 物理代理，不将渲染网格直接当作所有动态物体碰撞体。

## 画面基准

官方截图 SS00 已实际查看：明确的低多边形切面、细长人物、深色袖口与浅色手掌、木板商店实物陈列、茂密分层针叶树、蓝色海面和暖灰木屋。它是森林区域参考，不能直接当作灯塔首岛布局。

模型、关卡布局、灯光、材质和 UI 分别验收。正式场景不以方块、胶囊或通用鱼体替代成品；未完成项留在计划中。

新增候选 `CrabMeat`、`FishingBoat`、`Lighthouse`、`Keeper`、`RightHand` 保留独立 .blend 和哈希。船体按现有 2.1×4.5 米甲板与挂点建模，灯塔约 10.3 米、看守人坐姿约 1.72 米；这些尺度为本项目匹配物理的推定，不是原作测量值。右手额外检查袖口在 Unity 握持轴右侧。灯塔场景朝向 180° 是让门朝向玩家来路，模型根导入仍保持 identity。船的动力/浮力/离舵挂点与看守人交互代理不随视觉替换改变。

首船已依据 S6 实际游戏截图改为木船，左舵右油门；SS08 主菜单白色船不再用作首船依据，旧候选保留在源目录 iterations。新增 Beer、HotDog、Radar 与 Pine 候选均保留源文件；17 个模型加轴向门禁共 18 个结构用例，不等于视觉还原验收。

## 森林任务候选实现（2026-10-04）

新增 Leech、ForestLady、Piranha、GiantPiranha、PiranhaSkeleton 源模型，复用已验证导出坐标；水蛭长约 0.5 米，女士高约 1.94 米，普通鱼长约 0.5 米、首领约 2 米，均为适配交互的推定尺度。鱼颌和尾鳍保留独立可动节点，骨架使用独立肋骨与脊椎几何。模型候选板已查看，结构门禁不代替原作造型一致性验收。

## 森林枪械候选（2026-10-04）

新增 Pistol、Shotgun 和 ForestShop，源文件及导出仍走同一 Blender 管线。Pistol 保留 Slide、Magazine、Muzzle；双管 Shotgun 保留 Breech 和 Muzzle，运行时换弹分别移动弹匣和旋开枪管组。商店使用独立木板、柜台与商品板，碰撞代理由编辑器按可见静态网格边界生成。尺寸与店面布局为推定，NPC 和装饰尚未补齐；枪械及商店候选板已查看，不代表正式美术验收。

新增九种普通鱼候选：Mackerel、Gar、Pike、Cod、Goldfish、Perch、Triggerfish、Goby、Salmon，以及 FishingRod、BeginnerLure。鱼体分别保留长吻、下颌须、棘背鳍、菱形薄体、隆起眼位或斑纹，尺寸与造型细节为推定；不是仅换材质的统一鱼体。普通鱼竿使用独立纺车轮、木色握柄及 2.23 米前向竿尖；初级饵有独立鱼钩和尾饰。两组候选预览已查看，正式游戏造型对照与活鱼动作仍待验收。

## 沙漠候选（2026-10-04）

新增独立脚本 `scripts/how_to_fish/build_desert_models.py`，复用同一几何助手与 FBX 导出入口；17 个模型保留 `.blend`、FBX 和哈希。河豚 Ball 位于球心（Unity Y=.9），物理根单位缩放；体型随失血连续增长，同步扩大 Ball 与 SphereCollider，根节点保持单位缩放。蓝鲨 RollPivot 位于鱼体中心，翻滚不改变物理根缩放。SMG 保留独立弹匣和枪口节点。

Bowlfish 根据 S14 实机图改为透明开口鱼缸、内部小鱼与侧鳍；YellowBoxfish 是另一个有倒角、黑斑的箱形鱼种。URP 材质装配读取源材质 Alpha，鱼缸使用透明混合。两组八模型候选板及两种鱼的修订预览均已查看，结构和预览不等于正式造型还原验收。

完整模型类 `114df0e1` 为 54/54 通过（53 模型含本次 17 个，另有轴向门禁）。此前 `eff81ee2` 53/53 通过发生在 Bowlfish 修订及 YellowBoxfish 新增之前，应使用新结果。沙漠场景布局、地形、树木密度和动作视觉仍需运行验收。

## 岩石岛候选（2026-10-04）

新增 `scripts/how_to_fish/build_rocks_models.py`，沿用共享导出入口。Tuna、Albatross、AlbatrossHead、Islander、SniperRifle、RocksShop、ProfessionalLure、BirdDropping 共八项保留独立 `.blend`、FBX 及哈希；目前仅为候选资源，未写入鱼池、任务或场景。

金枪鱼有独立 Tail，信天翁保留 WingLeft/WingRight，狙击枪有 Magazine/Bolt/Muzzle。商店屋顶使用闭合楔形网格，已修正镜像侧面的绕序；装配时需要真实屋顶碰撞，不能用整栋包围盒封住门窗。约 2.6 米鱼长、4.4 米翼展及店面尺度均为适配玩法的推定。

最终八模型预览板已查看，完整模型测试 `48530d9a` 为 62/62 通过；包含 61 个候选模型与轴向用例。该门禁证明导入挂点、尺度和局部变换，不证明飞行动画、碰撞遮挡或正式还原效果。

## 专业鱼池候选（2026-10-04）

新增 `scripts/how_to_fish/build_rocks_fish_models.py`，生成 Bass、RedSnapper、Parrotfish、Tigerfish、FlyingFish、Sengarat、Eel、Halibut、Voxelfish、Dripper 的独立源文件。体素鱼、双鞋鱼体、鳗鱼长体、比目鱼顶眼、飞鱼宽鳍及鹦嘴鱼喙部分别制作；Sengarat 调整眼位后重新导出，使用最终 FBX 运行门禁。

完整模型类 `39f9735d` 为 72/72 通过。八鱼与两种特殊鱼预览板均已查看；导入尺度和挂点已验证，动作及原作配色仍为候选。Dripper 的动态碰撞体同时包含鱼体和鞋底，避免仅按高处鱼体生成包围盒而让鞋部穿地。信天翁另外给翅膀和头部建立可命中代理。

## 标准鱼池补齐（2026-10-04）
新增 build_standard_fish_models.py：Angelfish 薄体/高鳍/条带、Catfish 宽头/六须、SeaUrchin 独立棘刺、Clownfish 三道白带、Bluegill 鳃盖/黑耳斑。保持同一米制、Grip/Hook 和导出契约，源尺寸与配色为推定。侧向候选预览已查看；重新配置导入后模型类 23a15f84 为 98/98 通过（97 模型与轴向门禁），尚未作为最终原作造型验收。

可选遭遇候选：build_optional_fish_models.py 自制 Sunfish、OldPike、GoblinShark、BeginnerBossLure、ScientificBossLure，沿用米制/Grip/Hook 或 Forward、尾鳍 Tail 独立关节和固定 FBX 导出。翻车鱼约 3.4 米高，老狗鱼约 3.7 米长、哥布林鲨约 4.2 米长，均为适配玩法的推定。当前侧视预览已查看，尚待模型门禁和玩法装配；鱼饵双色环带外形为推定。

可选五模型门禁 40fc7faf：103/103 通过，随后已装配可选鱼和首领饵。新增 build_wildlife_models.py 的 BingBong/Seagull/Coconut/PlayerRemains 保留独立源模型，四项侧视预览已查看；目前尚未进入玩法资源。BingBong 使用眼球、四肢、双色帽和独立 Propeller，海鸥保留 WingLeft/WingRight/CarryPoint，玩家遗体沿用黑袖第一人称配色；尺寸和不可见背面均为推定，导入仍走同一坐标契约。

环境模型随后通过 f7493111 共107项门禁并已装配。装备候选 build_equipment_models.py 新增 BrassKnuckles、UpgradeAnvil、AmmoUpgrade；指虎保留四个真实开孔和独立掌撑，砧台有木桩/铁箍/砧角，弹药台有箱扣和独立弹头。指虎外观参照 S23 页面渲染图，其余工作台造型与尺寸为自制推定。三项预览已查看，0473fa07 为110/110模型检查通过，尚待场景运行验收。

配件候选由 build_attachment_models.py 制作六种附件和 AttachmentCrate；瞄准镜与枪口附件使用中空网格，外侧绕序有导出前断言。最终预览已查看，ecc25494 为117/117（116模型加轴向）通过。狙击枪源模型去掉预装光学镜并补机械瞄具，按 S23 原装候选图分离购买配件；枪身其它细节仍是早期候选，不能据此称造型完全还原。编辑器为每种枪保存实际配件对象和瞄准挂点；双管霰弹枪的枪口件各使用一对，扩容不装配到霰弹枪。扩容部件跟随原弹匣，枪口件跟随双管开膛节点，运行时不创建替代网格。

## 船只升级候选（2026-10-04）
`build_boat_models.py` 新增 SmallMotor、MediumMotor、BigMotor、BoatRadar，均保留独立源文件和FBX。小型使用方盖长轴，中型使用蓝灰圆罩，大型为并排两台中型；已查看对应参考图片及自制预览板。船载雷达没有找到独立外观记录，屏幕外壳、尺寸和位置为推定，使用世界空间Canvas显示实际解锁岛屿。螺旋桨使用独立子节点，叶片变换烘入网格，保持局部旋转和缩放契约。实际导入、装配及运行验证待补齐。

## 炸药候选（2026-10-04）
`build_dynamite_model.py` 新增Dynamite，独立.blend/.json/FBX保留。七根红色棱柱、两道深色箍带、多引线汇聚，Grip与Fuse分别用于握持与引信定位；尺寸约0.166×0.200×0.427米为自制校准。已实际查看Review-Dynamite.png，精确导入测试7b67a6d8为1/1通过，保持统一轴向、单位缩放和应用变换。当前总登记123项（122模型加轴向），未为单项新增重跑全部模型。

## 皮肤老虎机候选（2026-10-04）
`build_slot_machine_model.py` 自制SlotMachine，保留.blend/.json/FBX。宽1.164×深0.845×高1.8米，Intake/Display/Lever与三个独立Reel0~2；造型及尺寸为推定。Review-SlotMachine.png已实际查看。Intake源坐标(0,-.405,.58)，预期Unity(0,.58,.405)；Display预期(0,1.665,.36)。场景以Intake为触发区，身体用简单碰撞体，动画仅驱动真实滚轮和操纵杆。导入、运行验证结果见validation.md。

Unity正面截图发现顶屏/滚轮符号消失：原脚本符号面法线朝源+Y，Blender默认双面预览掩盖了背面剔除问题。现已反转六个面的绕序，源导出前断言normal.y<-.99，并在Unity导入门禁检查符号法线朝+Z；滚轮停转复位让符号朝前，不停在随机空白面。

## 轮盘桌候选（2026-10-04）
`build_roulette_model.py` 自制 `RouletteTable`，保留 `.blend/.json/FBX`。宽2.8、深1.8、高1.185米，桌面高1米；木制台体、金边、三色放物面和37格轮盘。轮心Unity(0,1.05,-.35)，白球直接挂台根，初始Unity(0,1.095,.05)；RedBet/BlackBet/GreenBet分别为(-.65/0/.65,1.005,.3)，Grip为原点。格0朝轮局部+Z，编号沿+Y正旋转，0绿/奇红/偶黑。脚本断言37格、颜色数量、上表面法线及应用变换；自制预览已查看并修正一次共面闪烁，实际导入精确用例8087d6ea为1/1通过，运行截图Roulette-InGame.png已查看；规则与运行证据见validation.md。

## 人物服装候选（2026-10-04）

`build_character_outfits.py` 生成18套 `Outfit<Id>`，保留独立 `.blend/.json/FBX`；图标通过 `--icons` 分支读取源模型渲染，384×512 RGBA，无文字和地面。前17套参考公开角色图的衣帽轮廓和配色，Bean的绿色防水服及豆形徽记为资料缺口下的自制主题。源预览及透明边界已检查，Unity装配与运行视觉结果另记在validation.md。

统一米制、前方+Z、原点Grip和FaceFront；各节点局部旋转归零、缩放为1。`ForearmSleeveRight`或`ForearmSkinRight`以及`PalmRight`提供第一人称材质，完整模型只用于服装资源和遗体视觉。Prefab不带Collider/Rigidbody，死亡遗体继续使用已有物理根；图标位于 `Art/Icons`，模型位于 `Art/Models`，服装Prefab位于 `Prefabs/Outfits`。
