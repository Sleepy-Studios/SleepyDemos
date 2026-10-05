# 渔力全开 · 单人复刻

内部标识：`HowToFish` / `how_to_fish`。五岛主线和结局已有可运行初版与分段验证；完整内容、连续新档通关及正式美术尚未完成。

- [实施计划与验收](plan.md)
- [模块与生命周期](module.md)
- [参考与内容清单](reference-catalog.md)
- [逐项内容与验收清单](content-checklist.md)
- [装备外观预设核对表](skin-catalog.md)
- [人物服装与解锁依据](outfit-catalog.md)
- [生物公开数据快照](creature-source-data.tsv)
- [模型契约](model-contract.md)
- [实际验证记录](validation.md)
- [运行五岛单人原型](runbooks/play-prototype.md)
- [原始需求](../../agent/prompts/demos/how_to_fish/original-goal.md)

目标：Windows 离线单人，键鼠与手柄，完整岛屿主线、收集、正式自制模型与 UI、三个存档槽，并接入现有 Hub。首岛不是最终交付。

代码归属 Hotfix/Demos/HowToFish，资源归属 LoadResources/Demos/how_to_fish，专属编辑工具归属 Hotfix/Editor/HowToFish。复用现有 Core UI、UIBind、YooAsset、GameSceneNavigator 和两套测试程序集。
