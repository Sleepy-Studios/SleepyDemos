# 外观预设核对表

2026-10-04：逐页读取 Corpus 的 v1.0.12 外观记录；这里只记录名称、稀有度与效果标记，不复制原作材质或着色器。外观的具体配色、纹理和动画另行自制、对照验收。已实现九类共127条外观目录，其中114条进入奖励池、13条为默认类别记录；颜色与程序图案为自制候选，尚非原作视觉验收。

| 类型与来源 | Common | Rare | Legendary | 其它 Default 类预设 |
|---|---|---|---|---|
| [Pistol](https://corpus.gg/games/how-to-fish/appearance/pistol-skins) | Wood, Pink, White, Blaze, Bluze | Emerald, Black and White, Inverted Diamond, Galaxy, Ruby | Diamond, Gold | Default, Missing |
| [Boat](https://corpus.gg/games/how-to-fish/appearance/boat-skins) | Winter, Red, Orange, Blue | Camouflage, Emerald, Ruby, Polka, Blue Fade | Rainbow, Gold, Diamond | Default, Fire |
| [BrassKnuckles](https://corpus.gg/games/how-to-fish/appearance/brass-knuckles-skins) | Polka, Chess | Camouflage, Wood, Emerald, Hazard, Ruby | Gold, Rainbow, Diamond | Default |
| [FishingRod](https://corpus.gg/games/how-to-fish/appearance/fishing-rod-skins) | Orange, Winter, Cow, Hazard, Wood | Asiimov, Camouflage, Emerald, Ruby, Missing | Galaxy, Gold, Diamond | Default |
| [Knife](https://corpus.gg/games/how-to-fish/appearance/knife-skins) | Camouflage, Chess, Tiger, Hazard, Missing | Blaze, Emerald, Wood, Polka, Rainbow, Galaxy, Ruby | Diamond, Gold | Default |
| [Shotgun](https://corpus.gg/games/how-to-fish/appearance/shotgun-skins) | Orange, Redwood, Greyscale | Hazard, Wood, Bronze, Emerald, Ruby, GoldStriped | Rainbow, Diamond, Gold | Default |
| [SMG](https://corpus.gg/games/how-to-fish/appearance/smg-skins) | White, Blue, Pink, Red, Brown, Hazard | Wood, Emerald, Ruby, Purple, Tiger, Cow | Rainbow, Diamond, Gold | Default |
| [SniperRifle](https://corpus.gg/games/how-to-fish/appearance/sniper-skins) | Wood, Brown, Light Gray, Winter, Camouflage, Cow, Blaze, Sponge | Asiimov, Emerald, Ruby, Tiger, Galaxy | Gold, Rainbow | Default |
| [AssaultRifle](https://corpus.gg/games/how-to-fish/appearance/assaultrifle-skins) | White | Tiger, Hazard, Wood, Emerald, Ruby, Pink, Cow | Rainbow, Diamond, Gold | Default, Orange, Blue |

Rainbow效果标记：Boat/BrassKnuckles/Shotgun/SMG/AssaultRifle 的 Rainbow；FishingRod 的 Asiimov、Galaxy；Knife 的 Rainbow、Galaxy；SniperRifle 的 Asiimov、Galaxy、Rainbow。其余表内记录标为 Standard。不能只凭名称推断效果或稀有度，例如手枪 Galaxy 为 Rare / Standard，手枪没有 Rainbow 预设。

[老虎机规则来源](https://corpus.gg/blog/how-to-fish/skins-slot-machine)：交付玩家拿过并已放手的死 Drip 鱼，普通鱼不收；部分非生物任务物品也可投。一次消耗一个实体，普通/稀有/传奇类别概率70%/20%/10%，可重复中奖，重复不退款。奖励皮肤归本地玩家，当前物品所用皮肤属于物品状态。默认类别记录不能直接当作老虎机奖励或独立解锁证明。

| 岛 | 奖励类型 |
|---|---|
| 灯塔 | 指虎、小刀 |
| 森林 | 指虎、小刀、手枪、普通鱼竿、霰弹枪、船 |
| 沙漠 | 手枪、SMG、普通鱼竿、霰弹枪、船 |
| 岩石 | 普通鱼竿、SMG、狙击枪、船 |
| 火山 | 普通鱼竿、突击步枪、狙击枪、船 |

人物服装与武器皮肤分开。外观目录列出NPC衣着不等于确认玩家可解锁该衣着；角色奖励需要另核实对应里程碑。单人版不接入Steam服务。

## 单人实现与持久化

`HowToFishSkinCatalog` 保存名称、稀有度、效果和五岛奖励池；`HowToFishSlotMachine` 接收投料并播放两秒自制滚轮动画，`HowToFishWorld.TryPlaySlotMachine` 负责消费、开奖和立即保存。动画受暂停控制，奖励不依赖动画播放完毕。保存失败保留鱼尸并撤销当次新解锁，活动首领期间遵守既有禁存档边界，拒绝投料。

当前仅收已被玩家持握、已经放手且死亡的 Drip 生物，普通鱼、活物、未拿过或仍在手中的实体不收。非生物任务物品的资格名单尚未可靠核实，暂未实现。类别概率按70/20/10；类别内部每个预设等权是本项目待校准的假设，不声称已确认原作的类型选择权重。

键盘 C / 手柄右摇杆按下默认绑定同一个 ChangeSkin 动作：驾驶时循环船外观，否则切换手中对应装备。解锁列表与装备 `skinId`、船 `boatSkinId` 分开保存；物品丢下和拾回保留其选择，拾到带皮肤的实例不额外解锁该皮肤。根据[存档来源](https://corpus.gg/blog/how-to-fish/saves-steam-cloud-missing-items)，解锁属于跨新世界保留的本地玩家档案。本项目使用同一保存目录中的共享外观档案，让三个槽和新档继承解锁，不使用 Steam；各槽仍镜像解锁集合以便中断后恢复。旧档缺少字段按 Default / 无解锁读取。

`HowToFishSkinView` 使用序列化引用的 URP Shader 和对象坐标程序纹理，不要求模型 UV。每个外观实例拥有独立材质，恢复默认和销毁时释放；不改原材质、不覆盖手部、瞄具与购买配件，烹饪在换肤后重新叠色。动画只读取目录 Rainbow 标记。人物服装和轮盘仍是单独的未完成功能。
共享写入顺序：先原子提交槽快照（奖励及鱼尸消费在同份快照），再写 `PlayerSkins.json`。第二步失败不回滚已提交的槽，从三个有效槽的解锁并集在下次进入时补齐；共享主档损坏时从校验通过的备份恢复并保留损坏文件，主备均坏拒绝覆盖并提示。沿用既有槽损坏显式恢复流程。

Drip投料辨识：拾取焦点和手持鱼获信息显示彩色Drip标记，普通鱼没有该标记。实际生物特殊材质/饰物仍未核实，不用统一金色推断全部Drip外观。

Shader验证中统一跨Pass常量缓冲：自定义Forward与直接UsePass的URP/Lit ShadowCaster、DepthOnly布局不同，改为三个自有Pass共用唯一UnityPerMaterial。首次截图青色不能直接归因该布局：Unity Editor在异步编译变体时使用纯青色占位Shader，测试须等待ShaderUtil.anythingCompiling结束再取图；配色保持原值，不能把CPU读取材质属性当作渲染颜色证明。
