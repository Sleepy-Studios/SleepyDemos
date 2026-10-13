# DLSS 体验 Demo

内部名称 `Dlss`，资源 ID `dlss`。该目录只维护体验场景自身；公共 DLSS / Streamline 渲染能力仍归公共模块。

偏好、输入参数和实际改键来自[公共设置与表现](../../modules/settings-presentation.md)，只在 Hub 修改。提示绑定本场动作副本，使用 Kenney 键鼠/手柄图标；说明文案在绑定时读取当前语言。实验室不提供公共设置入口。世界运动使用公共玩法时钟，冻结期间仍可重置视角和返回 Hub。

- [体验场景职责与运行](module.md)
- [界面美术与控件外观](architecture/art-direction.md)
- [公共设计与路线](../../architecture/streamline-integration.md)
- [公共模块状态](../../modules/streamline.md)
- [公共构建与验证](../../runbooks/use-streamline.md)

基础 UI 风格库：`Assets/LoadResources/Demos/dlss/Prefabs/UI/Foundation/`，以 `Dlss` 前缀按用途命名，完整公共控件与本 Demo 组合子变体均从这里复用；准确字号、业务参数与页面布局保留。本 Demo 当前固定外观由 Variant 保存，源码色板保留 DLSS 区域说明，无需未使用的运行时字段；规则与清单见[界面美术维护](architecture/art-direction.md)及[基础资源维护](../../modules/ui-foundation-assets.md#四-demo-完整风格库)。
