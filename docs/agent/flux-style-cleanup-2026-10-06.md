# Flux 代码风格与职责整理记录

## 实施范围

按钓鱼项目的业务写法整理 SleepyDemos：Action 使用 PascalCase 公开参数字段和显式构造函数，结果由 Handler 同步回填；删除逐动作静态 Send。格式与注释规则由根 .editorconfig、文档维护规范及两侧 gen-module 技能共同约束。本地模板可以独立编译，不生成网络占位。目录、命名空间、资源与存档契约保持原有边界。

| 阶段 | 提交 | 内容 |
|---|---|---|
| 规范与参考 | 494314b | 格式配置、技能与协作规则同步，搬豆工三件套参考 |
| Hub 与三个 Demo | 75d1396 | 大厅、搬豆工、无人机、DLSS 和公共画面设置的字段、注释与业务入口 |
| 赌场与渔力全开 | 本记录所在提交 | 显式派发与结果回填、纯窗口状态、Handler 命令与 Data 查询、清理边界 |

场景初始化、依赖配置、物理采样和释放仍保留具体生命周期入口；页面反馈、离场和导航恢复采用带来源的 Action。大厅导航增加清理版本检查。赌场窗口对象不再执行 IO 或教学命令，嵌套规则事件在完整操作结束后发布。渔力全开直接表达 State 修改，View 通过 Data 读取业务状态，SetData 在显示前完整交付场景与 Data。

## 验证方式

仅使用既有 Tests.EditMode / Tests.PlayMode 和 Unity Test Runner，串行运行直接受影响类或精确方法，没有全项目或第三方测试。临时源码整理使用已安装 Roslyn 的解析与格式能力，空白整理校验 token 不变；没有构建 Unity sln/csproj。生成 Component 未修改，脚本 meta 与任务前逐字节一致。

当前结果文件在 Library/FluxStyleCleanup。新增测试须重新执行原生发现，旧缓存的数量不能证明新方法已执行。实际结果见下面的通过范围及未通过列表。

## 修复与限制

- 整理时遗漏了一处旧测试 Handler 调用，并有一处分支插入位置错误；均已修正，正式编译后验证。
- 拆除静态 Send 后，四个调用文件需要 Core.Runtime 引用，已补齐。
- 设置页面的 SetData 初次整理漏保存 Data，导致打开前查询空引用；已检查同类页面并修正。设置预览、取消、重绑与持久化精确重跑通过，服装页面与交易通过。
- 一次 EditMode 全局 Clear 测试在前一轮 PlayMode 退出后访问了已销毁的 Hub 列表。View.BindData 仍直接订阅页面回调，跨运行的旧页面订阅问题已暴露。当前计划限定 Core 只整理格式与注释，已询问是否纳入公共销毁退订保护，尚未得到范围确认，因此本轮没有修改 Core 行为。断言未删除，异常未忽略；正式重新编译后的同一 GlobalDataTests 三条全部通过，不能据此宣称该跨运行问题已经修复。
- DLSS FormalStartupModesAndHubReturn 本轮 631ab516 仍在关闭模式世界画面像素变化断言失败，实际 0.0、要求大于 0.1，与上轮黑帧检查一致。模式和页面状态通过不能替代此视觉验收。
- 上轮六条渔力全开交互/战斗失败保留并精确重跑，结果在最终表记录；不调整生命、伤害、逃脱时限，不删业务断言。
- 实体输入设备、全部页面外观和 Player / IL2CPP 未验证。

## 已通过范围

| 范围 | 本轮通过 |
|---|---|
| Flux / View | GlobalData 3、View 生命周期 23 |
| Hub / 搬豆工 | 大厅导航 6、搬豆工资产 8、流程 6 |
| 无人机 | UI 契约 5（含旧来源/恢复新增断言）、机型选择 3、统一输入 6 |
| DLSS / 公共画面设置 | 控件绑定与保持释放 1、设置阻断及返回 1、Streamline 设置 4 |
| 赌场 | 规则 25（含纯重置新增断言）、存档 4、教学 16、偏好 12；进入/窗口 11、机台交互 7、暂停 2 |
| 渔力全开 | 规则 17（含同步结果/旧会话新增断言）、存档 16、资产 16；三槽、设置、服装、轮盘、音效、标准饵交易、炸药链路各 1 |

按最新结果去重，以上 199 条通过；新增方法的精确运行已包含在所属测试类，不重复计数。没有运行 HowToFishRuntimeTests 整类或全项目测试。

## 仍未通过的精确检查

| 用例 | 本轮 job | 结果 |
|---|---|---|
| Attachments_BuyAimReloadRejectAndResume | a1f4550c | 瞄准了 ExtendedMag，期望 RedDotSight |
| HubEntry_KeyboardGamepadFishingTradeSaveAndReturn | 8a444aca | 实际手持鱼获攻击未完成击杀 |
| Forest_LeechProgressResumesAndBossDropsCoordinates | fff4b800 | 实战未完成首领击杀 |
| DesertPufferfish_ReelFightWithSmgAndDropFin | 8fed4810 | 实战未击败河豚并获得掉落 |
| Rocks_ReelTunaAndDefeatAlbatrossWithSniper | a764f400 | 实战角色死亡，未完成流程 |
| Volcano_ReelShootCarryWhaleAndDefeatMutation | ee750a2d | 本次推进到后续战斗，角色死亡后未完成流程 |
| DlssDemoFlowTests.FormalStartupModesAndHubReturn | 631ab516 | 关闭模式世界画面仍为黑帧 |

这些与上轮未通过的六个 HowToFish 方法和一个 DLSS 方法对应，但不能把失败原因一概认定为历史或环境问题，也不能因方法名相同声称每次失败阶段完全一致。本轮保留结果和业务断言，尚未达到全部验收全绿。

收尾再次把菜单“继续存档”的状态检查收口到 Handler，保持点击时检查最新磁盘状态；三槽、恢复与重开用例 645d44f9（1/1）通过。最终场景为干净 AppEntrance，Unity 非编译/非更新，Console 0 错误，git diff --check 通过。临时整理工具未进入 Assets 或 Git；递归及逐文件清理均被自动审批拦截（blocked by policy），因此保留在忽略的 Library/FluxStyleCleanup 下；原字体未提交内容按任务开始备份逐字节恢复。
