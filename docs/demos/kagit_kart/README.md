# 纸艺卡丁车

`KagitKart` / `kagit_kart` 是 Kâğıt Kart 的中文 Unity 迁移，目标为 Windows 与 Android 横屏单人游戏。源码规则、八角色动画和四章纸艺赛道已迁移；中文封面、3D 选赛、赛前镜头、道具表现、纸鹤救援与比赛特效、音频、HUD、暂停、结算及大奖赛领奖台已接入。最终移植尚未完成，设置与操作说明、章节动态与中文路牌、渲染和音频差异及完整 Editor 验收仍在推进。

- [模块与当前能力](module.md)
- [原作对应与源码维护](runbooks/source-migration.md)
- [音频维护与保真边界](runbooks/audio.md)
- [原始 Goal](../../agent/prompts/demos/kagit_kart/original-goal.md)：只用于需求追溯。

代码：`Assets/Scripts/Hotfix/Demos/KagitKart/`。数据：`Assets/LoadResources/Demos/kagit_kart/Data/`。测试：`Tests.Demo.KagitKartRuleTests`。

画面沿用原作立体书、纸景、剪纸角色、文具道具和硬阴影。玩家可见内容统一简体中文；触屏保留原作横向转向滑条。用户已确认中文比赛 HUD 方向；后续直接忠实迁移原版 UI，不再要求逐页效果图确认。比赛 HUD、暂停和结算均使用正式 Prefab 与 UIBind 生成绑定。

选赛使用原作桌面立体书：封面开合、曲面卷页、纸景铰链弹出、车手切换与转盘、镜头过渡均已接入。纸卡入场与焦点表现保留原作时序，布局露出 3D 舞台。新版键盘、模拟手柄与多指触屏场景流程 2/2 通过；完整比赛视觉仍需与道具、演出一起验收。

场景入口：`Assets/LoadResources/Demos/kagit_kart/Scenes/Main.unity`，登记为 `GameSceneId.KagitKart`。Hub 显示“纸艺卡丁车”，Editor 直启复用公共 Bootstrap。进入后先选择模式、80g／120g／200g 与八名车手；大奖赛从首章开始，单场与计时赛继续选择四条赛道。角色和章节焦点按原作同步当前选择与 3D 预览，确认才推进页面或开赛。完整单人内容与演出仍在迁移，选赛可用不代表最终 Demo 已交付。

公共输入、资源加载、UI、导航与测试继续以项目公共文档为真源，不建立第二套框架。
