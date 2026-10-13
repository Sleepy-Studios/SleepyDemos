# 纸艺卡丁车需求与验收对照

本清单用于收口和后续定向回归。来源固定为 `da100b21ae1c5332d2843d21a9bb7b68b97e9c2a` 的 [Kâğıt Kart](https://github.com/BurakErdemci/kagit-kart/tree/da100b21ae1c5332d2843d21a9bb7b68b97e9c2a)；[原始 Goal](../../../agent/prompts/demos/kagit_kart/original-goal.md) 只追溯需求，逐页效果图审批已取消。当前实现详见 [模块说明](../module.md) 和 [源模块对应](source-migration.md)。截至 2026-10-11，完整单人内容已接入并有下表定向证据；仍保留末节的未验证项，不能据此声称百分之百像素一致或完成全部平台验收。

正式运行代码为 `Assets/Scripts/Hotfix/Demos/KagitKart/`，资源为 `Assets/LoadResources/Demos/kagit_kart/`。下表使用当前文档记录的 Unity Test Runner 实际结果，不将外部候选、正在运行的作业或离线公式等同于原生玩法通过。测试均沿用 `Tests.EditMode` / `Tests.PlayMode`，没有执行全量测试。表内 job 与通过数量保留相应日期证据，不证明后续治理修改已经重验；当前本地存储只维护完整当前记录，读取失败视为暂无数据，最新契约见 [模块说明](../module.md#计时记录)。

2026-10-13 的 AA、阴影与纸纤维参数核对及 Paper / Items 屏幕网点缩放修正见[画质维护](source-migration.md#画质参数与平台转换)。当前两份 Shader 已保存平台换算，但多 renderScale 的实际 GPU 视觉验收尚未完成；下表历史 Shader 结果不能替代本次修正复验。

## 需求 → 实现 → 证据

| 需求范围 | 当前实现 | 实际证据与准确边界 |
|---|---|---|
| 固定源码参数、随机、120Hz 独立模拟与插值、暂停不追赶 | SourceCatalog、Random、SimulationClock、TrackQuery、Vehicle、Session | 基础16、运动12、道具14、比赛9、记录5，共56/56；含四条实际查询、三级漂移、起步、尾流、跳台、加速带、表面、碰撞和救援规则。属于源码/规则对照，不证明每种画面均人工看过。 |
| 四赛道、路线/地形/捷径与章节特色；八角色/属性；80g/120g/200g | Tracks / Worlds / AiPlans、八份角色 Prefab、SceneryWorld、CharacterVisual | 四章资源4/4（`8df7f53d`）、章节动态4/4（`8206ebdc`）；角色资源9/9，八角色50组骨姿态8/8（`4a486885`）。AI 29/29及后续四场GP单项通过，包含四章×三组别完整运动三圈；固定步模拟不是人工驾驶。 |
| 一玩家+七AI、单场三圈、四场GP、本场成绩→总榜→下一章/领奖 | Race、GrandPrix、Results/Standings/Podium 正式 View/Prefab | 正式AI四章三圈流程1/1（`855a98e9`）；单场实际三圈、再赛和回标题1/1（`b01a3de2`），按钮五态及路由复验1/1（`2e76608f`）。最终GP四场三圈→成绩/总榜→领奖→封面复验1/1（`3c1f80d5`），含主按钮墨底浅字、焦点红内沿、抬纸和移焦复位。用正式规则和按钮，不是合成赛果；仍是自动驾驶场景验收。 |
| 计时三圈、最佳圈/全程、新纪录、同身份幽灵与慢场不覆盖 | RecordStore、GhostRecorder/Playback、World真实保存、TT结果表 | 原生存取/回放1/1（`f1ecf9de`），最终复验1/1（`7488e02f`）：实际 raceEnd 保存、同身份回放、位置/朝向/末端隐藏、较慢第二场保留原记录；正常起步后立即保存帧像素，已检查 `TimeTrialGhost.png` 的分离透明幽灵与纸印轮廓。只使用临时记录目录，未写用户成绩。 |
| 键鼠、手柄、触屏同一命令；左右保持转向、多指、保持/短按、自动油门与清输入 | PlayerInput.asset、KagitKartInput；HUD 左右按钮复用 Core InputCommandButton，原横向滑条不再作为当前入口 | 既有绑定9/9（`f914479c`）、菜单三端2/2（`23bcab93`）及HUD设备模拟1/1（`10dec2c8`）为此前阶段记录，不能代替本次左右按钮迁移复验。真实手柄、Android触摸与多指手感仍需设备验证。 |
| 全中文菜单/HUD/教程/错误反馈与统一译名 | Catalog、HarmonyOS_CN、13页正式 Prefab/UIBind、中文书本印刷/路牌 | 原生各页截图及上述流程；本轮只读核13页 `m_text`、8角色/4赛道/8道具映射及动态 caller，未发现源 raw name/exception.Message 进入玩家文字；异常细节只写日志。按键符号、克重单位不计为漏译；所有屏幕比例缺字/溢出并未穷举。 |
| 设置/操作说明、减少动态系统来源、镜头抖动/音量/静音/触控/FPS与保存异常 | Settings/Controls、World.Options、Core.SystemMotionPreference；公共画面 Modal | 偏好2/2（`646ad162`）、Windows真实系统读取1/1（`09ae1204`）、设置/公共画面多层恢复1/1（`c27c5f14`）。Android分支未在Player或真机执行。 |
| 首次教程、五类驾驶提示；完整原HUD、轮盘、库存、名次、百分秒、倒计时、分段/预警/最后圈/冲线/等待/翻页/地图 | Hints、ItemSlot、HudNotice、CountdownCard、Minimap | 教程1/1（`2513c7c4`）；真实初始三火箭、E/held gum、箱体轮盘1/1（`f07d8a16`）；正式通知/投影/暂停双时钟/退订1/1（`7235e63f`）。后者使用明确表现事件样本，不能声称每种通知都在自然比赛触发。数字3/2/1及地图直接执行源算法导出。 |
| 墨水形状、滴落/整体渐隐、暂停与低动态、HUD/教程/暂停层次 | HudInk局部RT合成、独立表现随机、源曲线/滴落、实际玩家Ink事件 | 源曲线1/1（`0d38c85c`）、GPU重叠/高光/UV/整层渐隐与释放1/1（`18af6422`）、实际比赛和重赛释放1/1（`568e176b`）。CSS滴落按墙钟继续，规则剩余时间/渐隐暂停；不额外改变原暂停行为。 |
| 开书/卷页/折纸、选择预览、赛前可跳过、冲线和领奖演出；菜单退出/FPS | BookStage、RaceCinematics、PodiumStage、MenuPaperMotion/Transition | 源几何/镜头/领奖快照（`abfe5fde`、`45f13610`、`a89d7a35`）和原生生命周期（`ed6c7209`、`9896c879`）；未缩放折卡/回滚/低动态1/1（`74e4d8ab`）、真实设置与长驻FPS1/1（`a308ddc2`）。FIFO导航适配项目实现，不声称与DOM追加页面的瞬间逐像素相同。 |
| 纸纹、网点、硬阴影、深度描边与整像素错版 | Paper/Road/Water/Sky/Post源Shader、URP本场Feature与透明后CopyDepth | 后处理过滤/线宽/生命周期2/2（`88220e62`），真实GPU深度/墨边/错版1/1（`9a87b8b2`）；四章实际驾驶与切换1/1（`f153aef9`）。Windows Editor RenderGraph路径；兼容模式明确关闭效果，Vulkan/Player未验。正常起步后的透明幽灵与纸印轮廓已实拍确认（`7488e02f`），不覆盖所有距离和角度。 |
| 八世界道具、轨迹/命中/保持/释放与比赛特效 | ItemVisuals、RaceFx正式源Mesh/Shader/材质，订阅真实规则事件 | 源道具14场景/56实例14/14（`fb6ae50e`），FX20/20（`846bde10`）；原生八道具1/1（`19bc564c`）：真实E、held/drop、目标、墨瓶爆墨、剪刀俯冲/剪断、护盾、弹体清理与14图。库存/摆车是明确前置，不证明自然抽奖或所有特效同时出现的性能。 |
| 真实落空与纸鹤四阶段、安全回路、保护及释放 | Vehicle正式重力/Query、RaceFx、RescueCrane | 原生冰川掉落1/1（`72715ba7`）：先真实安全点，再合法无地面位置，重力触发提/搬/放/落；五张NativeRescue图、回路保护、鹤引用和音频载体释放。没有直接StartRescue；草原围栏不能作为悬崖前置。 |
| 原章音乐/音效/实时引擎、漂移/道具/救援/终点、暂停混音、纸景声 | 97 Clip、Audio/EngineSynth/BoostSynth/AudioDsp、Scenery真实Popup与源1401项size | 算法/原生WebAudio/资产11/11（`912553ee`）；原生Intro→Countdown→Race及真实P/UI继续1/1（`9c1f1c0d`），Listener最终PCM peak=0.6997126；八道具新use_*路由及PCM peak=0.6655096（`19bc564c`）。载体AudioSource历史采样为零，最终Listener采样有效；总线非零不等于孤立SFX或人工听感，详见[audio.md](audio.md)。 |
| GP暂停退出确认、Demo标题/Hub区分、子页P/Start继续、Esc/B返回、TT R阶段 | PauseView、World.PauseRoutes与已有公共菜单/Cancellation | 原生1/1（`61e8c7bd`）：GP二次确认/移焦取消、正常标题返回与独立Hub入口、设置/操作子页继续、电影期间R无效、倒计时子页实际重赛，SkipIntro整组隐藏。没有新公共导航框架。 |
| Hub/直启、自包含加载、损坏/占用存档、页面/资源生命周期与工具收口 | Main.unity / GameSceneId.KagitKart、Loader、Bootstrap、World.StopRound、公共本地存储 | RecordStore5/5，以上Hub/GP/TT/重赛/多层Modal及原生释放；资源命名/标签1/1（`10658471`）。阶段编译绿/Console0见module；一次性Bake及meta已清理。没有Player/IL2CPP构建证据。 |
| 正常比赛帧分布/峰值/内存与最终Console | 既有四章起步采样、正常道路长圈方法及外部EditorTiming JSON | 四章正常长圈前两章`a975b049`、后两章`62f99115`均终态1/1，每章至少45秒且实际完成一圈，帧分布、自然事件、GC和内存已记入[性能记录](../../../agent/archive/kagit-editor-performance.md)。测量期间临时关闭独立Game View VSync并finally恢复原值；旧`a399f34a`含Editor呈现等待，不作当前性能依据，也不称项目/Player问题已修复。草原74.96ms单帧尚未精确定因，不据窗口通过声称整场三圈或所有特效组合稳定60帧。 |

## 原作与真实 Unity 画面对照

原作图均来自固定参考仓库 `docs/shots/`；Unity图均来自正式场景的外部 `Previews/`，本页已逐图查看。没有AI效果图、重画图或生成概念图作验收证据。原作图为1280×720，当前Unity图为1920×1080；状态、拍摄时间、参赛名次与取景并非相同，比较原网格/纸艺构成、主题与页面职责，不做像素差值或百分之百一致断言。

| 对照 | 原作截图 | Unity实际截图 | 本图可证明与限制 |
|---|---|---|---|
| 模式菜单 | ![原作模式菜单](https://raw.githubusercontent.com/BurakErdemci/kagit-kart/da100b21ae1c5332d2843d21a9bb7b68b97e9c2a/docs/shots/menu.jpg) | ![Unity模式菜单](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/MenuMode.png) | 原书本、剪纸景物、车手、三个模式卡与中文显示；焦点、取景和布局适配不同，旧截图不替代全部纸卡五态/退出测试。 |
| 草原赛道/中文路牌 | ![原作草原起点](https://raw.githubusercontent.com/BurakErdemci/kagit-kart/da100b21ae1c5332d2843d21a9bb7b68b97e9c2a/docs/shots/meadow-grid.jpg) | ![Unity草原真实长圈](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/EditorTiming_meadow_LongRoad_20261010_225412.png) | 原纸景/角色、当前HUD/地图与中文显示；Unity为真实长圈返回起点、原作为倒计时，不能当同一瞬间/同一车数。此图对应最终草原45秒采样，性能数字以JSON和性能记录为准。 |
| 领奖台 | ![原作领奖台](https://raw.githubusercontent.com/BurakErdemci/kagit-kart/da100b21ae1c5332d2843d21a9bb7b68b97e9c2a/docs/shots/podium.jpg) | ![Unity实际大奖赛领奖](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/GrandPrixPodium.png) | 原1/2/3纸台、前三角色、纸屑、书本与中文真实积分；名次/车手不同属实际赛果。返回菜单已修为源主按钮墨底浅字，当前图可见焦点红内沿；`3c1f80d5`实际GP复验同时验证抬纸和移焦复位。 |

另外的真实证据保留在同一 `Previews/`：`MenuCover/Class/Character/Track`、`Settings/Controls/TutorialKeyboard`、`HudNative*`、`HudFixture*`、`RaceInk`、`GrandPrixRaceResults/Standings`、`TimeTrialGhost/Results`、`SingleRaceResults`、`NativeRescue*`、`NativeItem*`。`HudFixture*`为明确表现样本；`Race_*.png`和部分早期HudNative图含旧HUD，不能当最终布局证据。图片来源按 [独立美术源规范](../../../runbooks/maintain-art-sources.md) 管理：原作使用固定 commit 的公开图片链接，Unity 证据使用游戏仓库旁 `SleepyDemos-ArtSource` 的相对链接。换机器时按相同相邻目录布局提供美术源仓库；缺少它只影响证据图片预览，不影响 Unity 运行和打包。没有将大图复制入游戏仓库。

## 仍需明确记录的收口项

1. 四章正常长圈已完成；草原74.96ms单帧未精确定因，窗口不覆盖完整三圈或所有特效/演出同时出现。Editor呈现等待的Game View VSync对照不代表Player性能修复。
2. 四章开场、倒计时、漂移三档、警报、救援、最后圈、终点、暂停/继续的人工听感尚未检查；真实PCM与源码配方只能证明软件链路/算法范围。
3. 未穷举所有自然比赛特效组合、横屏比例/安全区及跨设备切换场景；幽灵分离、透明与纸印轮廓已在正常起步实拍确认，不能据单帧覆盖所有角度和赛道。只按具体未解决风险补验，不重新跑所有已绿流程。
4. 本轮范围仅Windows Unity Editor。真实手柄、Android设备/系统分支、Player/IL2CPP、Vulkan及全量测试均未验证，不将它们记为当前Editor通过的推论。

本文与README导航是文档改动，基础检查仅为文件/链接和diff检查，不需要启动Unity；后续新增实际结果更新对应行和图片来源，保留此前证据的范围。
