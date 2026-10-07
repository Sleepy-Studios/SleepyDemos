# MVP 验证记录

## v0.2 六关与 HUD 扩展

2026-10-06：普通、顶墙、钻缝三种怪，六个保存关卡、固定隔墙、35 秒限时救援和黑框分格 HUD 已完成。运行环境 Unity 6000.3.15f1；未执行全量测试。

| 本次范围 | 结果 | 证据 |
| --- | --- | --- |
| WallSqueezeSimulationTests | 24/24 通过 | 六个保存关卡参考路径、护甲正确轴、薄体真实通缝与倍率、固定墙支撑、超时/误伤/胜利优先、复位与非法保存数据 |
| 第六关加至六怪后定向复测 | 1/1 通过 | `SavedLevelsHaveWinningReferencePaths(6)`；属于前列用例的重复复测，不额外计数 |
| WallSqueezeRuntimeTests | 5/5 通过 | 保留键鼠/模拟手柄/触屏、UI 排除和 Hub 生命周期；增加固定墙选墙和点击排除 |
| WallSqueezeDirectStartTests | 1/1 通过 | 真实 Scene 直启、正常下一关到第六关、护甲与薄体保存模板、Renderer 高度、倒计时 HUD、暂停冻结与重试清时钟 |
| UIViewPrefabConventionTests | 1/1 通过 | 修改后的保存 HUD 继续满足公共 View Prefab 契约 |

本轮结果 JSON 保存在 `C:\Users\User\.codex\artifacts\wall_squeeze\upgrade\`。HUD 按钮修正了原有 `UIState` 的黑底状态配置，五态使用米白、浅灰、蓝色焦点和黄色按下反馈，均保持文字对比；修正后直启与交互接线用例复测通过。

公共导航没有新改动，因此未重复此前导航测试。新增规则测试保留用于长期回归；未增加生产测试程序集。真实硬件、首次可读性与玩法乐趣仍需人工体验。

实际运行图：[第一关](../art/mvp-expanded-first.png)、[第六关](../art/mvp-expanded-six.png)。已检查完整头尾黑框、计数、暂停/重来、三种怪物图例、两轴箭头、护甲与薄体轮廓。截图通过临时跳关取景，自动测试另经正式“下一关”入口验证六关接线，不代表人工手感验收。

本次只修改 Hotfix Demo、专属资源、对应测试和文档；未新增 Core 能力、场景路由或 BuildSettings。一次性升级脚本、菜单与临时截图已清理。测试期间公共动态字体自动更新，使用包含用户未提交内容的外部保护副本恢复原 SHA256 `BBE470E5B63BA80198562DA715F8FDDA655D37113A0A925E863FD37E48D051FB`；自动更新后的副本也保存在 `font-protection/HarmonyOS_CNSupplement.after-upgrade-validation.asset`，未用 Git 版本覆盖用户字体。

清理后正式编译完成（`isCompiling=false`、`isUpdating=false`），Console Error 为 0；`git diff --check` 通过，Demo 文档失效链接与资源缺失 `.meta` 均为 0。未 commit/push。

2026-10-07 提交前核对：手写 C# 与 Markdown 的 staged diff 检查通过。新纳入版本管理的 Unity 原生序列化文件含空字段尾部空格，保留 Unity 输出格式。沿用上述实际验证结果，本次 Git 交付未重跑 Unity 测试；用户公共字体改动不纳入提交。

## v0.1 首次 MVP

2026-10-06：三关 MVP 实现、正式资产保存与相关 Unity Test Runner 验证完成。以下为此前记录。

## 实际结果

| 范围 | 结果 | 说明 |
| --- | --- | --- |
| WallSqueezeSimulationTests | 14/14 通过 | 单接触、夹持与撤回、推链、独立行、同步死亡、住户优先、墙互阻、高速不同步长、复位和三关解法 |
| SavedLevelsHaveWinningReferencePaths 后续定向复测 | 3/3 通过 | 三个保存关卡；与上列用例重复，不另计独立测试数 |
| WallSqueezeRuntimeTests | 4/4 通过 | 真实保存动作资产、模拟设备、指针归属、UI 排除、暂停/重试及 Hub 往返生命周期；检查 Renderer 实际尺寸 |
| WallSqueezeDirectStartTests | 1/1 通过 | 加载实际保存场景，检查 Editor 直启场景标识、相机及 HUD 可见性 |
| GameSceneNavigatorTests | 7/7 通过 | 包含新增保存场景路径识别以及现有导航回归 |
| UIViewPrefabConventionTests | 1/1 通过 | 公共 UI Prefab 契约检查 |

最终 Editor 编译状态为 `isCompiling=false`、`isUpdating=false`，Console 错误查询为 0。测试结果留在 `C:\Users\User\.codex\artifacts\wall_squeeze\tests\`，包括规则结果 JSON、后续定向路径复测及各范围 XML。

资源命名检查中，本 Demo 路径问题为 0。全仓扫描仍报告其他目录的 162 项 Error、59 项 Warning，未在本任务修改；摘要见上述证据目录的 `AssetNamingSummary.txt`。

正式第三关 GameView 截图为 [mvp-gameplay-3.png](../art/mvp-gameplay-3.png)。已检查 HUD、暂停/重试按钮、红蓝方块、两轴箭头及黑灰把手可见；世界形状使用本 Demo 的单位 Sprite，未修改公共 White 导入设置。截图是实际运行画面，通过程序参考路径进入第三关，不代表人工操作手感验收。

## 保存与清理

- 场景、World Prefab、HUD/Modal Prefab、三关数据、参数、`PlayerInput.asset`、独立静态字体、Sprite 与音效均已保存。音效路径为 `Audio/SFX/Crush.wav`。
- 一次性 `Hotfix/Editor/WallSqueeze` Builder、其目录和 `.meta` 及临时截图已删除，仅保留正式文档图。没有新增生产测试程序集，没有修改 Core、BuildSettings 或原入口场景，也没有 commit/push。
- 用户已有 `HarmonyOS_CNSupplement.asset` 保持原 SHA256：`BBE470E5B63BA80198562DA715F8FDDA655D37113A0A925E863FD37E48D051FB`。
- 恢复的未保存入口场景已另存外部永久副本：`C:\Users\User\.codex\artifacts\wall_squeeze\scene-recovery\20261006-AppEntranceRecovered.unity`（含 `.meta`），SHA256 为 `72F34738CABBF8F985EB4103841DF9350C2CE43A8754D624FEF9BED0727CAD21`。项目内临时恢复资产已清理，原 `AppEntrance.unity` 未覆盖。

## 操作参考路径

- 第一关：选中纵墙，持续向右推进至轨道末端并保持夹击。
- 第二关：纵墙先右推，再选择横墙下推。
- 第三关：纵墙从 x=1 右推到 x=7.2，移开蓝色住户，再下压横墙夹红怪。

## 验证范围

规则类覆盖单面推动、持续夹持、单怪及推链撤回、同帧死亡、独立自由行不压扁、住户优先、墙互阻、高速/多步长、复位与保存三关解法。PlayMode 覆盖真实动作资产、键鼠/模拟手柄输入幅度、UI 指针阻挡、单指归属、暂停取消与 Hub 往返 UI 生命周期。

模拟 InputSystem 设备只能证明软件接线；鼠标拖动手感、真实触屏/手柄、箭头初见可读性、群杀爽感与保护住户乐趣必须由人工体验分别确认。
