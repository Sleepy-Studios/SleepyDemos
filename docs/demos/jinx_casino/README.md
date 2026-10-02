# 倒霉蛋俱乐部

Windows / Android 原创单机赌场冒险。内部标识 JinxCasino / jinx_casino。

当前执行用户批准的**新原型精简与视觉优化计划（PrototypeV2）**：简化复古俱乐部、赌场代码职责整理、独立Hub及加载UI优化；PC/Android共用机台和UI，保留自由探索。四张参考稿确认后制作正式资源，Hub和三款S1体验确认后再扩展。只保证新版本，不维护旧原型/旧档兼容；联网移出本轮。

- [当前计划与验收门槛](implementation.md)
- [PrototypeV2 四张简化参考稿（待确认）](architecture/prototype-v2-visual-review.md)
- [上一轮体验与美术提案（已被新计划替代）](architecture/experience-redesign-proposal.md)
- [当前进度与提交记录](immersion-progress.md)
- [视觉与交互规范](architecture/immersion-design.md)
- [可复用规则与存档边界](architecture/rules-baseline.md)
- [模块及生命周期](module.md)
- [模型与装配合同](architecture/model-contract.md)
- [运行及构建](runbooks/run.md)
- [历史原型进度](progress.md)、[原型验证报告](verification.md)、[P0记录](verification-p0.md)
- [最初多人需求归档](../../agent/prompts/demos/jinx_casino/original-goal.md)
- [本轮批准计划归档](../../agent/prompts/demos/jinx_casino/prototype-v2-goal.md)

旧P1–P4仅为功能原型：可复用规则和资源保留，旧场景/构建的历史事实保留，用户未认可沉浸感、美术和整体体验。旧报告中的正式资源/完成措辞不适用于当前验收。

旧构建仅保留历史验证事实，不再要求当前版本支持旧入口、旧房间、旧面板或旧数据。当前已接通具体Game与直接机台会话、版本4存档；旧入口/面板/场景及构建兼容已删除，场景Controller剩余玩家交互职责仍待拆分，实际迁移与测试记录见实施记录。

代码在 Assets/Scripts/Hotfix/Demos/JinxCasino/，正式平台构建在 Hotfix/Editor/JinxCasino/（一次性装配工具已清理），资源在 Assets/LoadResources/Demos/jinx_casino/。当前模块文档描述原型实际结构；沉浸能力随实现更新，不把设计当已实现。
