# 纸艺卡丁车

`KagitKart` / `kagit_kart` 是 Kâğıt Kart 的中文 Unity Demo，包含四条原版赛道、八角色、单人三种模式，以及玩家 Host 的单场/大奖赛联机，支持键盘、手柄与多指触屏操作。保留源码规则、3D立体书、纸景动画、文具道具、纸鹤救援、章节音乐和纸印效果，已有定向规则对照、原生场景流程、关键画面及历史四章长圈证据。历史 AA、UI 与优化复测见[2026-10-13 性能收口](runbooks/performance-2026-10-13.md)；联机当前实现、操作及待验收范围见[玩家 Host 联机](runbooks/multiplayer.md)。实际证据和未验证项见[验收对照](runbooks/acceptance.md)，不声明像素完全一致、Player构建或Android真机通过。

- [模块与当前能力](module.md)
- [创建、加入与维护玩家 Host 房间](runbooks/multiplayer.md)
- [界面美术与控件外观](architecture/art-direction.md)
- [原作对应与源码维护](runbooks/source-migration.md)
- [音频维护与保真边界](runbooks/audio.md)
- [完整需求与实际验收对照](runbooks/acceptance.md)
- [Editor 性能记录](runbooks/editor-performance.md)
- [2026-10-13 性能收口与条件边界](runbooks/performance-2026-10-13.md)
- [2026-10-14 开声音频与事件性能记录](runbooks/performance-2026-10-14.md)
- [原始 Goal](../../agent/prompts/demos/kagit_kart/original-goal.md)：只用于需求追溯。

代码：`Assets/Scripts/Hotfix/Demos/KagitKart/`。数据：`Assets/LoadResources/Demos/kagit_kart/Data/`。测试：`Tests.Demo.KagitKartRuleTests`。

画面沿用原作立体书、纸景、剪纸角色、文具道具和硬阴影。玩家可见内容统一简体中文；触屏按已确认局部设计使用左右保持转向按钮。用户已确认中文比赛 HUD 方向；后续直接忠实迁移原版 UI，不再要求逐页效果图确认。比赛 HUD、暂停和结算均使用正式 Prefab 与 UIBind 生成绑定。

选赛使用原作桌面立体书：封面开合、曲面卷页、纸景铰链弹出、车手切换与转盘、镜头过渡均已接入。纸卡入场与焦点表现保留原作时序，布局露出 3D 舞台。已有键盘、模拟手柄与多指触屏的场景流程记录；范围与限制见验收对照，阶段数量与job只保留在 [验证历史](../../agent/archive/kagit-validation-history.md)。

场景入口：`Assets/LoadResources/Demos/kagit_kart/Scenes/Main.unity`，登记为 `GameSceneId.KagitKart`。Hub 显示“纸艺卡丁车”，Editor 直启复用公共 Bootstrap。封面开始后选择单人或联机；单人继续选择模式、80g／120g／200g 与八名车手，大奖赛从首章开始，单场与计时赛可选四条赛道。联机通过创建房间或按地址加入，容量为2／4／8人，至少两名真人且全部准备后由房主开赛，其余赛车由AI补齐八车网格。默认使用UDP `7777`；IP可达与端口映射见联机手册。角色和章节焦点按原作同步当前选择与3D预览，确认才推进页面或开赛。

联机复用公共 FishNet AOT 会话，以120Hz权威固定步推进、30Hz共享快照和官方拥有者预测/校正运行。2026-10-14 同进程真实UDP公共契约、独立双Editor单场与四章GP、四组UDP抖动/丢包及失败恢复均已通过定向验收；两端完整赛果、积分和后续网格一致，新杯清零及Host退出已验证。实际原生RTT、job与报告见[联机验收记录](runbooks/multiplayer.md#2026-10-14-联机验收)。独立Player/IL2CPP和真实双机仍未验证。

公共输入、资源加载、UI、导航与测试继续以项目公共文档为真源，不建立第二套框架。

基础 UI 风格库：`Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/Foundation/`，以 `KagitKart` 前缀按用途命名，完整公共控件与本 Demo 组合子变体均从这里复用；准确字号、业务参数与页面布局保留。固定 UI/HUD 源码颜色在 `UIColors.KagitKart`，规则与清单见[界面美术维护](architecture/art-direction.md)及[基础资源维护](../../modules/ui-foundation-assets.md#四-demo-完整风格库)。
