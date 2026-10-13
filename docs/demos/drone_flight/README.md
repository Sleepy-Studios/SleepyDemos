# 无人机飞行仿真

内部名称 `DroneFlight`，资源 ID `drone_flight`。长期文档统一从这里进入。

- [模块职责与生命周期](module.md)
- [公共设置与表现](../../modules/settings-presentation.md)、[公共玩法输入](../../modules/gameplay-input.md)
- [实现原理与架构](architecture/design.md)
- [界面美术与控件外观](architecture/art-direction.md)
- [正式模型契约](model-contract.md)
- [设计演进与决策记录](../../agent/archive/drone-flight-history.md)
- [调试与整定](runbooks/tune.md)
- [独立场景接入](runbooks/standalone.md)
- [迁移到正式项目](runbooks/migrate.md)
- [原始 Goal，仅供追溯](../../agent/prompts/demos/drone_flight/original-goal.md)

基础 UI 风格库：`Assets/LoadResources/Demos/drone_flight/Prefabs/UI/Foundation/`，以 `Drone` 前缀按用途命名，完整公共控件与本 Demo 组合子变体均从这里复用；准确字号、业务参数与页面布局保留。固定 UI/HUD 源码颜色在 `UIColors.DroneFlight`，规则与清单见[界面美术维护](architecture/art-direction.md)及[基础资源维护](../../modules/ui-foundation-assets.md#四-demo-完整风格库)。
