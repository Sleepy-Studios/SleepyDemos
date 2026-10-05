# Flux / UIBind 重构实施记录

本轮覆盖 Core 的必要公共能力、Hotfix 全局业务、五个 Demo、LoopScroll 宿主示例、相关资源、测试和文档。玩法数值、页面布局、资源地址、DemoId 和存档格式保持原有契约，独立 SleepyLoopScroll 包未修改。

## 分阶段提交

| 阶段 | 提交 | 主要结果 |
|---|---|---|
| 公共基础 | f5b1c8c | UIBind 更名、注册实例保护、View 自动订阅与公共输入回调 |
| Hub / 搬豆工 | 971ae2b | MainMenuData、BlockPorters Action/Data/Handler，三个整页 Presenter 合回 View |
| 无人机 | 2b2374e | 选择、装备、帮助、调试与遥测接入 Flux；页面 ControlsPresenter 合回 HUD |
| DLSS | 243278e | 控制页进入 UIManager，Demo 和公共画面设置接入 Flux |
| 赌场 | 21b5402 | 冒险、存读档、设置、教学和页面请求接入 Flux，整页 Presenter 合回 View |
| 渔力全开 / 示例 | 本记录所在提交 | 交易、任务、奖励、生命/饱食、存档和页面状态收口 Handler；七个页面合并；LoopScroll 示例更名 |

现有规则 Session 和存档对象继续作为进度真源。Controller / World 保留实体、物理、演出和场景资源；View 通过 BindData 刷新，通过具体 Action 提交业务请求。连续输入、物理 Tick 和局部 HUD 组件保留原来的运行职责。

## 自动化验证

所有测试均通过 Unity Test Runner 串行执行，只使用 Tests.EditMode / Tests.PlayMode；未运行全项目或第三方测试。结果文件位于本地 Library/FluxRefactor，以下是本轮结果，历史文档中的通过不计为本轮证明。

| 范围 | 已通过的直接相关检查 |
|---|---|
| Core Flux / UI | GlobalData 2；View 生命周期 23；UIManager 导航 51；UIRoot 7；过渡 9；世界过渡 13；UIBind 生成/发现/自定义输出 24；其余类型、栈和契约检查见对应结果文件 |
| Hub / 搬豆工 | MainMenu 导航 6、资产 8、主题 6、流程 6，共 26 |
| 无人机 | UI 契约 4、HUD 绑定 1、统一输入 6、机型选择 3、相机生命周期 6、钓鱼任务 2，共 22 |
| DLSS | 控制页绑定/释放、公共设置阻断与返回、Editor 直接启动各 1；Streamline 设置 4 |
| 赌场 | EditMode 73；PlayMode 20，共 93 |
| 渔力全开 / LoopScroll | 渔力全开资产、规则和存档各 16，共 48；LoopScroll 展示 7及更名场景的UIBind偏移/取消/拖动专项79e82386（1/1）；三槽、设置重绑、服装选择和服装奖励专项通过 |

渔力全开整类首轮 884cbeb2 为 34/49 通过。随后保留原业务断言，修正测试的页面等待、鱼竿选择、随机重量尺寸及共享鱼池假设；Drip 外观、链爆、水下炸鱼、沙漠任务、火山任务与声音暂停恢复的精确重跑通过。整类没有在修正后重新宣称全绿。测试修改曾引入一处编译错误，已修正；因此造成的零用例启动超时不计入通过或失败断言。随后测试遗留的临时场景触发保存确认并导致 Test Runner 清理异常，已丢弃该临时状态、恢复 AppEntrance 并重新发现目标用例；后续以实际执行结果计入。

## 尚未通过或未验证

- 渔力全开仍有6条已执行但未通过的用例：Attachments_BuyAimReloadRejectAndResume、HubEntry_KeyboardGamepadFishingTradeSaveAndReturn、Forest_LeechProgressResumesAndBossDropsCoordinates、DesertPufferfish_ReelFightWithSmgAndDropFin、Rocks_ReelTunaAndDefeatAlbatrossWithSniper、Volcano_ReelShootCarryWhaleAndDefeatMutation。分别涉及配件瞄准、首岛手持鱼获攻击、森林/河豚/信天翁战斗及鲸鱼投掷至变异战斗。失败报告保留，不降低生产生命、伤害、逃脱时限或删除战斗断言；不能把这些失败一概认定为历史问题。
- 普通付费饵原测试要求两次钓鱼后必定归零，并按所在岛定位鱼获，与当前概率损耗及共享鱼池配置不一致；改为检查单次至多扣一份、处理/出售不重复扣饵、真实余量续档，以及按本次鱼饵定位鱼获。标准饵17ee507b、专业饵18844417与科学饵bc9a1ecb各1/1通过。
- DLSS FormalStartupModesAndHubReturn 最新4c951732在 Off 世界画面的像素变化断言失败（0.0，要求大于0.1）；本轮 Game View 捕获为黑帧，原因尚未确认。已有模式状态/相机及面板生命周期检查不能替代此视觉验收。
- 实体键鼠/触屏/手柄、Player / IL2CPP 和全部页面外观尚未验收；自动化输入使用虚拟设备。

## 资源与清理

资产通过 AssetDatabase 移动或更名；保留脚本 GUID 及 LoopScroll 场景 GUID。整页 Presenter 的引用迁入 UIBind 索引并重新生成 Component；相关资产测试检查类型、引用、BindingKey 和缺失脚本。当前资源无已删除 Presenter GUID 引用。

临时 FluxMigrationTools 及 meta 已通过 AssetDatabase 删除。原字体未提交内容按迁移前备份保留，测试添加的动态字体内容不纳入提交。当前源码、资源和维护文档的旧工具名称已清理；原始 Goal 留作历史记录。

最终 Unity 编译检查为非编译/非更新状态，Console 0错误；git diff --check 通过。测试产生的 runInBackground 临时设置已还原，场景恢复为 AppEntrance，生成的 InitTestScene 已由 Test Runner 清理。
