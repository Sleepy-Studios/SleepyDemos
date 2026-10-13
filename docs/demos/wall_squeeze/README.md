# 夹爆它

`WallSqueeze` / `wall_squeeze`：2D 固定俯视、实时移动滑墙的动作解谜原型。玩家赶拢并夹爆红色怪物，同时保护蓝色住户。偏爽快动作，空间解谜负责产生变化。

## 当前状态

2026-10-06：扩展为六关验证版，加入顶墙怪、钻缝怪、固定隔墙与限时救援，HUD 按已确认概念图恢复黑框分格与怪物图例。正式 Scene、World、HUD / Modal、输入和配置资产为真源。实际测试范围见验证记录；真实设备操作手感和玩法乐趣仍需人工体验。

## 文档

- [最小 MVP 验证案](architecture/mvp-validation.md)：当前规则、参数、地图与验收标准。
- [模块维护](module.md)：规则、输入、表现、Flux 与 UI 生命周期。
- [界面美术与控件外观](architecture/art-direction.md)：纯色直角、控件继承与页面覆盖边界。
- [验证历史](../../agent/archive/wall-squeeze-validation.md)：已记录的测试结果、截图及当时未验证项，不代表最新代码已重验。
- [开发与验证](runbooks/start-mvp.md)：新对话启动提示词、开发入口和操作说明。
- [已确认的美术概念图](art/gameplay-concept.png)：HUD 分格、方向箭头和怪物识别的视觉依据。顶墙怪用侧面护甲提示只能上下夹；钻缝怪用薄体与双横纹提示快速穿缝。
- [六关版实际运行图](art/mvp-expanded-six.png)：完整 HUD、三种怪和限时救援；[第一关 HUD](art/mvp-expanded-first.png)。

## 入口约定

- 运行时代码：`Assets/Scripts/Hotfix/Demos/WallSqueeze/`。
- 专属资源：`Assets/LoadResources/Demos/wall_squeeze/`。
- 从 Hub 的“夹爆它”入口进入，暂停或结果菜单返回 Hub。Editor 可直接打开保存的 `Scenes/Main.unity` 后 Play。
- 现有 [新增 Demo](../../runbooks/add-demo.md)、[公共输入](../../modules/gameplay-input.md)、[公共设置与表现](../../modules/settings-presentation.md)、[测试架构](../../architecture/testing.md) 继续作为公共真源。

基础 UI 风格库：`Assets/LoadResources/Demos/wall_squeeze/Prefabs/UI/Foundation/`，以 `Wall` 前缀按用途命名，完整公共控件与本 Demo 组合子变体均从这里复用；准确字号、业务参数与页面布局保留。固定 UI/HUD 源码颜色在 `UIColors.WallSqueeze`，规则与清单见[界面美术维护](architecture/art-direction.md)及[基础资源维护](../../modules/ui-foundation-assets.md#四-demo-完整风格库)。
