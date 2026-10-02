# 倒霉蛋俱乐部

Windows / Android 原创单机赌场冒险。内部标识 JinxCasino / jinx_casino。

当前执行**单机沉浸重做 S0–S5**：第一人称探索、聚焦实体桌面、完整教学、统一视觉、键鼠/触屏/Xbox手柄。先完成水果机、二十一点、协作拉杆三款体验样板，由用户确认后再扩展。联网移出本轮，选型待定。

- [当前计划与验收门槛](implementation.md)
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
