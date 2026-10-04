# 人物服装目录

当前状态：18套源模型、透明图标与服装选择代码已加入；原生装配和运行验证待完成，尚未宣称视觉验收通过。

## 来源与版本边界

[Corpus 外观目录](https://corpus.gg/games/how-to-fish/appearance)的17款页面已逐项读取，`?source=1`只提供`_defaultUnlocked`；前四款为1，其余13款为0，没有直接公开成就映射。模型参考图已逐项查看，复用相同自制人体比例，保留不同衣帽轮廓；中性脸、站姿及不可见背面为推定。

下表非默认款的条件来自[社区成就表](https://how-to-fish.org/achievements/)（自称v1.0.10），属于二级证据，不冒称原始字段。剧情条件优先结合当前已核实的实际任务规则。资料缺口继续记录，不把17款当作完整总数。

| 稳定ID | 显示名 | 解锁依据 |
|---|---|---|
| Badman | 泳者 | 原始字段默认解锁 |
| Bikini | 比基尼 | 原始字段默认解锁 |
| Fisherman | 渔夫 | 原始字段默认解锁 |
| Sailor | 水手 | 原始字段默认解锁 |
| LighthouseKeeper | 灯塔看守人 | Who stole my beer：击败蜘蛛蟹 |
| SwampMan | 沼泽男子 | Getting an upgrade：首次购买马达升级 |
| SwampLady | 沼泽女士 | Dinnertime：击败巨型食人鱼 |
| KioskLady | 售货亭女士 | Yummy in my tummy：吃烧焦生物；社区写Cookness≥1.5，不能直接套入项目归一化字段 |
| Tourist | 游客 | Vacation：击败河豚 |
| GrillMaster | 烧烤师 | Grillmaster：交付鲨鱼并解锁烧烤；新资料也接受哥布林鲨 |
| Andrei | 安德烈 | All in：轮盘押绿获胜 |
| Jacob | 雅各布 | GOLD GOLD GOLD：老虎机获得Legendary皮肤 |
| GunstoreClerc | 枪店伙计 | Fully equipped：同枪集齐瞄具、伤害升级、枪口、激光和扩容 |
| ScaredGuyInShorts | 短裤男子 | Terrorizing bird：击败信天翁 |
| StoreGrandma | 商店奶奶 | I am speed：最高档马达 |
| Military | 军人 | Deadliest catch：击败最终鲸类首领 |
| Scientist | 科学家 | We are so back：完成离岛结局 |
| Bean | 豆豆 | Bean：一小时内完成最终离岛互动；社区明确列出皮肤奖励 |

[Bean计时说明](https://corpus.gg/blog/how-to-fish/bean-under-one-hour-route)：使用世界累计游玩时间，读档不重置、暂停不停止；击杀鲸鱼本身不算完成，仍须交鱼鳍、领取钥匙、操作最后的船。原始比较符未公开，精确边界需标明推定。Bean没有取得可靠全身或选装图，不能据名字称作Mr Bean西装；当前准备绿色防水服与豆形徽记的自制主题候选，造型完全属于资料缺口推定。

独立本地档案与跨世界解锁参考[保存说明](https://corpus.gg/blog/how-to-fish/saves-steam-cloud-missing-items)。所选服装在单人第一人称袖子、死亡遗体上的表现没有可靠原作证据，后续接入需明确为本项目约定。

## 本项目接线约定

- 在暂停菜单的衣柜查看18套全身图标、解锁条件并穿戴；锁定卡片仍可通过手柄聚焦。默认款为前四种，首次及旧档默认穿渔夫属于项目约定。
- `PlayerSkins.json` 复用原子写入与备份恢复，保存跨槽解锁和全局当前服装；每槽只镜像解锁，旧槽不得覆盖全局选择。选装先保存成功，再改变当前角色表现。
- 第一人称继续使用已有持握模型，从所选服装读取前臂和手掌材质；短袖或裸臂隐藏袖口，不改写武器皮肤与烹饪颜色。死亡遗体替换视觉子树，保留原物理根和独立 `outfitId`，后来换装不会改变已有遗体。
- 首领、马达和同枪配件的持久进度可为旧档补授；烧焦进食、轮盘押绿与传奇中奖只由实际事件授予，重复传奇也满足条件。人物奖励直接合入共享档案，不在首领死亡回调中抢拍等待清理的世界实体；共享写入失败保留本次内存奖励并提示再次保存。
- 烧焦沿用项目已有 `IsBurnt`（归一化受热程度≥0.9），不是把社区的1.5直接代入。世界计时改为包含暂停；新航程标记完整计时，旧档不能补回历史暂停时长，因此不凭旧档短时长授予豆豆。使用 `<3600` 且必须首次完成最后离岛互动，精确边界为推定。
