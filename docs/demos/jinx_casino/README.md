# 倒霉蛋俱乐部

Windows / Android 原创单机赌场冒险。内部标识 JinxCasino / jinx_casino。

当前按用户要求**暂停实现，先复核手机交互、美术与代码职责**。原单机沉浸重做S0–S5目标保留，S1画面和手机操作尚未获认可；新提案待审阅，不自动继续扩展。联网移出本轮，选型待定。

- [当前计划与验收门槛](implementation.md)
- [体验与美术重做提案（待审阅）](architecture/experience-redesign-proposal.md)
- [当前进度与提交记录](immersion-progress.md)
- [视觉与交互规范](architecture/immersion-design.md)
- [可复用规则与存档边界](architecture/rules-baseline.md)
- [模块及生命周期](module.md)
- [模型与装配合同](architecture/model-contract.md)
- [运行及构建](runbooks/run.md)
- [历史原型进度](progress.md)、[原型验证报告](verification.md)、[P0记录](verification-p0.md)
- [最初多人需求归档](../../agent/prompts/demos/jinx_casino/original-goal.md)

旧P1–P4仅为功能原型：已有规则、场景、资源和构建事实保留，用户未认可沉浸感、美术和整体体验。旧报告中的正式资源/完成措辞不适用于当前验收。

旧P4包仍可恢复：Windows位于 Builds/JinxCasino/P4/StandaloneWindows64/20261002031541-0ee336b5/，只验证解压启动至Hub；Android位于 Builds/JinxCasino/P4/Android/20261002032344-2a4f64cd/Player/，构建检查通过但未真机运行。它们不是新的S1样板包。

代码在 Assets/Scripts/Hotfix/Demos/JinxCasino/，正式平台构建在 Hotfix/Editor/JinxCasino/（一次性装配工具已清理），资源在 Assets/LoadResources/Demos/jinx_casino/。当前模块文档描述原型实际结构；沉浸能力随实现更新，不把设计当已实现。
