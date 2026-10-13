# 纸艺卡丁车

`KagitKart` / `kagit_kart` 是 Kâğıt Kart 的中文 Unity 单人 Demo，包含四条原版赛道、八角色、三种模式与键盘/手柄/多指触屏操作。保留源码规则、3D立体书、纸景动画、文具道具、纸鹤救援、章节音乐和纸印效果，已完成定向规则对照、原生场景流程、关键画面及四章长圈性能验收。实际证据和未验证项见[验收对照](runbooks/acceptance.md)；不声明像素完全一致、Player构建或Android真机通过。

- [模块与当前能力](module.md)
- [界面美术与控件外观](architecture/art-direction.md)
- [原作对应与源码维护](runbooks/source-migration.md)
- [音频维护与保真边界](runbooks/audio.md)
- [完整需求与实际验收对照](runbooks/acceptance.md)
- [Editor 性能记录](runbooks/editor-performance.md)
- [原始 Goal](../../agent/prompts/demos/kagit_kart/original-goal.md)：只用于需求追溯。

代码：`Assets/Scripts/Hotfix/Demos/KagitKart/`。数据：`Assets/LoadResources/Demos/kagit_kart/Data/`。测试：`Tests.Demo.KagitKartRuleTests`。

画面沿用原作立体书、纸景、剪纸角色、文具道具和硬阴影。玩家可见内容统一简体中文；触屏保留原作横向转向滑条。用户已确认中文比赛 HUD 方向；后续直接忠实迁移原版 UI，不再要求逐页效果图确认。比赛 HUD、暂停和结算均使用正式 Prefab 与 UIBind 生成绑定。

选赛使用原作桌面立体书：封面开合、曲面卷页、纸景铰链弹出、车手切换与转盘、镜头过渡均已接入。纸卡入场与焦点表现保留原作时序，布局露出 3D 舞台。已有键盘、模拟手柄与多指触屏的场景流程记录；范围与限制见验收对照，阶段数量与job只保留在 [验证历史](../../agent/archive/kagit-validation-history.md)。

场景入口：`Assets/LoadResources/Demos/kagit_kart/Scenes/Main.unity`，登记为 `GameSceneId.KagitKart`。Hub 显示“纸艺卡丁车”，Editor 直启复用公共 Bootstrap。进入后先选择模式、80g／120g／200g 与八名车手；大奖赛从首章开始，单场与计时赛继续选择四条赛道。角色和章节焦点按原作同步当前选择与 3D 预览，确认才推进页面或开赛。

公共输入、资源加载、UI、导航与测试继续以项目公共文档为真源，不建立第二套框架。
