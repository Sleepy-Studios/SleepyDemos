# 原作对应与源码维护

## 来源

- 仓库：<https://github.com/BurakErdemci/kagit-kart>
- 本机参考源码：`D:\Unity\ReferenceProjects\kagit-kart`。
- 基线提交：`da100b21ae1c5332d2843d21a9bb7b68b97e9c2a`，参考仓库处于该提交的 detached HEAD。
- 原作截图在参考仓库 `docs/shots/`；参考仓库不进入 Unity 依赖与资源打包。

## 源码对应

| 原作模块 | Unity 对应与当前边界 |
|---|---|
| `core/rng.js` | 已迁移 `KagitKartRandom`，测试值直接由固定提交的 JS 生成。 |
| `core/loop.js` | 已迁移正常速度与暂停固定时钟；原作测试用 timeScale 不作为玩家功能移入。 |
| `race/race.js` | 已迁移倒计时、检查点、圈线、逆行、稳定排名、十二秒完赛等待与估算、结算与大奖赛积分；会话、HUD与基本结算已接入，Editor自动驾驶四章三圈与大奖赛流程已验证；源结算细节和独立GP积分榜待补齐。 |
| `kart/ghost.js`、`main.js` 计时记录 | 已迁移录制量化、回放插值、分段与公共文件存储，已接入场景幽灵；正式场景最佳记录保存、同角色回放与较慢成绩不覆盖已定向验证。 |
| `track/defs/*.js`、`trackSampler.js` | 用原版 `sampleTrack(def, config.track)` 导出四条定义及采样；Unity 查询与原生四章场景已接入。 |
| `items/items.js` | 已迁移八行名次概率、近领跑者限制、道具箱及轮盘、八种道具效果、投射物碰撞、预警和复用；世界道具表现及HUD图标轮盘已接入。 |
| `core/config.js`、`characters/characters.js` | 保存参数与完整 roster，中文显示名统一由 Unity 目录提供。 |
| `input/input.js`、`ui/touch.js` | 正式 `Data/PlayerInput.asset` 与 `KagitKartInput` 保留原版键盘、标准手柄、横向滑条、保持按钮和短按锁存，复用公共输入；HUD、选赛、暂停与重赛三端流程通过定向场景测试，中文设置与三设备操作说明已接入，画面设置复用公共 Modal；系统低动态查询、首次教程及驾驶提示已接入并定向验证。 |
| `track/trackQuery.js` | 已迁移高度加权查找、空间网格、捷径换段、坡度、道路两侧、水面、跳台、加速带、表面定位及网格位置。 |
| `kart/kart.js`、`kart/collisions.js` | 已迁移车辆运动、漂移、特技、壁碰、救援、保护、质量互撞和尾流；独立规则事件已接入角色/道具/比赛特效与音频；真实掉落救援及完整听感另行验收。 |
| `characters/models.js`、`paperkit.js`、`atlas.js` | 直接执行固定原作的建模与绘图算法导出八角色，不重画造型。已转换为正式骨骼 Mesh、图集、材质与 Prefab，比赛与菜单骨链动画已接入。 |
| `ai/ai.js` | 原作路线规划保存为四份 `Data/AiPlans`；实时性格、技能、起步、漂移、追赶、道具、避障、捷径和救援迁移到 `KagitKartAi`，原作命令对照和完整规则模拟测试已通过。 |
| `items/visuals.js` | `KagitKartItemVisuals` 迁移箱体、钢笔、口香糖、飞机、墨水、剪刀、纸壳和世界虚线；原作 14 场景 / 56 实例快照对照通过，GPU 视觉另行检查。 |
| `render/materials.js`、`surfaces.js`、`renderer.js` 的天空片段、`post.js` | 已转译原生纸纹印刷、道路、加速贴纸、水波、纸洞深度与天空 Shader；原作倒数深度描边、整像素错版、双层纸纤维与暗角已接入本场世界相机。 |
| `scenery/*`、`track/trackBuilder.js` | 四章原作网格、纸景、材质与折起数据保存为原生 Prefab；章节 updater、风车、云、渡轮、烟、缆车、蒸汽及中文路牌已接入，四章源码矩阵对照通过。 |
| `characters/animate.js` | `KagitKartCharacterVisual` 接入八角色比赛和菜单动画；固定 JS 导出 50 组逐骨姿态，8/8 通过。 |
| `ui/menus.js`、`icons.js`、`nav.js` | 中文封面、模式、克重、八车手和四章节已接入；焦点按原作更新选择与 3D 预览，确认才推进，大奖赛跳过章节选择。中文设置与三设备操作说明已接入，画面设置复用公共 Modal；系统低动态查询、首次教程及驾驶提示已接入并定向验证。 |
| `cinematics/stage.js`、`popups.js`、`shots.js`、`campath.js` | `KagitKartBookStage` 接入开书、呼吸、翻页、车手弹出/旋转、镜头与中文印刷。`KagitKartRaceCinematics` 使用原作四章八网格、普通/减少动态赛前路径，时间曲线与向心样条对照通过。冲线和领奖镜头已接入。 |
| `cinematics/podium.js`、`ui/panels.js` 领奖部分 | `KagitKartPodiumStage` 保存原网格与纸台弹簧、角色落台、纸屑、失落车手；`KagitKartPodiumView` 显示中文奖杯标题与前三积分。四组源快照和完整四场流程通过定向测试。 |
| `fx/fx.js`、`fx/crane.js` | `KagitKartRaceFx` 与 `KagitKartRescueCrane` 接入原作纸艺特效与救援，源码快照 20/20 通过；全 GPU 变体与实际救援待验收。 |
| `audio/*` | 源码生成音乐与音效，`KagitKartAudio` 订阅真实比赛事件；`EngineSynth` 实时保留固定噪声谱和独立调制，音乐只低通一次。音频算法、采样与资产契约 11/11 通过，平台采样边界见[音频维护](audio.md)。 |

## 导出维护

使用本机已有 Node.js，无需安装 npm 依赖。原作 `trackSampler.js` 与 `track/defs/_path.js` 是纯数学模块，可以导入执行。四个定义各调用原作 `sampleTrack(def, config.track)`，将结果中的 TypedArray 转成普通数组后序列化；输出包保持 `sourceCommit`、`definition`、`data` 三个字段。

配置与角色基线包保持 `sourceCommit`、`config`、`roster`。只执行固定提交内的数据模块，不执行浏览器入口。导出命令已完成，无临时 Builder、菜单或生成器留在项目源码中。改变原作基线时重新导出并跑受影响规则测试，不手动重绘路线，也不将测试结果当成视觉和操控验收。

## 原作模型与对照数据

独立导出运行时位于 `D:\Unity\ReferenceProjects\kagit-kart-tools`，使用原作同版本 `three@0.186.0` 与 Canvas 实现。该目录的 npm 依赖不进入 Unity 项目；建模和图集绘制仍执行原作源码。

原始网格数组、骨骼、动画链与挂点保存到独立美术目录 `SleepyDemos-ArtSource/ArtSource/kagit_kart/SourceExports/`。正式 Mesh 的顶点和法线反射 X，三角绕序与骨骼绑定一并转换，PNG 使用原作 UV；Native Unity 预览保存于同级 `Previews/`。源导出及预览不作为 Player 运行依赖。

四章赛道与纸景直接执行 `trackBuilder.js`、`scenery.js` 和章节造型模块，外部维护入口为参考工具目录中的 `export-world.mjs`。完整参考数据与 PNG 保存在 `SourceExports/Worlds/<赛道>/`，保留顶点色、UV、自定义道路属性、材质参数、层级、实例矩阵、纸景折起距离与触发阈值。155 个独立几何、46 张贴图和 2926 个实例已装配为四章原生赛道资源。`KagitKartSceneryWorld` 迁移实际章节 updater，独立表现时间在暂停时冻结；源码动态数据保存到 `Data/Worlds/*Dynamics.json`，原 JS 参考位于 `Fixtures/OriginalSceneryDynamics.json`。四章路牌的中文绘图沿用项目字体与原作排版，替换图集内容时保留 GUID，不重建模型。

`export-paper-shader.mjs` 将固定原作的印刷算法转译为 Unity HLSL，保留原作计算，只替换纹理采样、GLSL 类型与 HLSL 保留字、源坐标和深度接口。正式 Shader 保存到 Demo 的 `Art/Shaders/`，不依赖外部工具运行。原生预览使用隔离 Preview Scene 与 URP Render Request，未修改用户场景；原生 Shader 变体的编译错误需在渲染后检查，不能只依赖 C# 编译通过。

`export-ui-art.mjs` 直接执行原作 `ui/icons.js`，将八个道具、八个角色头像、三个模式、四条赛道草图和四个操作图标保存为 27 张透明 PNG。正式资源位于 `Art/UI/Items/`、`Portraits/`、`Modes/`、`Tracks/` 和 `Controls/`，已通过 Unity 导入为单图 Sprite 并添加资源标签；模式、头像和章节插图已保存到选赛 Prefab，道具窗口、来袭预警与逆行图标已接入。保留原作剪纸和错版印刷绘图，不将土耳其语文字烘入这些图标。导入必须明确 `SpriteImportMode.Single`，避免空的多图模式导致资源无法取得 Sprite。

`export-stage.mjs` 直接执行原作 `cinematics/stage.js` 与 `print.js`，在内存中暴露原有变形与纸景数据，不修改参考仓库。独立参考输出位于 `SourceExports/Stage/book/`，保留封面与卷页基础顶点、29 个纸景/文具部件铰链数据和四章印刷。中文文字以项目 `HarmonyOS_CN.ttf` 替换拉丁字模。Unity 中保存 16 个 Mesh、12 个纸材质、立体书 Prefab 和精简动态数据；菜单开合、翻页、纸景与车手联动已接入并通过新版三端场景测试。

`export-intro.mjs` 直接执行原作赛前选路、障碍探测和镜头路径，为四章八网格保存普通/减少动态关键帧。`IntroPaths.json` 约 83 KB；运行时以 Unity `AnimationCurve` 保存原作单调时间切线，计算向心 Catmull–Rom，视野依横屏比例计算。源参考 `OriginalIntro.json` 的全网格曲线对照 1/1 通过（`45f13610`），不是 C# 自生成期望值。

`export-podium.mjs` 直接执行 `createPodium` 与 `podiumShot`；原生领奖台 Mesh、图集与配置保存在 Demo 独立目录，`Fixtures/OriginalPodium.json` 保存四场景 44 组源码快照。书本在颁奖时直接打开最终章，收起拱门与 `podiumFold` 纸景，镜头由领奖台接管。Unity 浮点四元数每步归一化，避免原作双精度旋转迁入后积累误差触发原生矩阵断言。中文纸卡沿用原作壁钟入场时序，不使用比赛固定时钟。

纸艺 Shader 的光照保留 Three.js `RE_Direct_Toon` / `RE_IndirectDiffuse_Toon` 调用的 `BRDF_Lambert`（1/π），避免沿用相同原作灯光强度时画面过曝。该修正同时影响四章赛道和书本舞台；外部 Shader 导出入口与正式 Shader 同步，未修改项目全局画面设置。

`Fixtures/OriginalMotion.json` 来自实际执行固定提交内的 JS 查询与赛车运动。它是跨语言迁移的行为参考数据，不是 C# 实现生成的期望值。查询输入先转为 float32，使两端从同一位置开始；刹车和救援的阈值仍保留原作时间精度。原作变化时重新运行 JS 生成参考，不修改参考值来掩盖 Unity 行为错误。

`Fixtures/OriginalItems.json` 直接执行同一提交的 `items.js`，只屏蔽浏览器表现模块，保留真实道路查询和道具模拟。参考覆盖纸飞机前后发射、追踪飞行与剪刀升空、追赶和俯冲。维护脚本位于外部参考工具目录 `export-item-fixtures.mjs`，不进入 Unity 运行依赖。

`export-ai-plans.mjs` 直接运行原作路线松弛、弯道合并和捷径多速度、多起点探测，保存四个 `Data/AiPlans/*.json`；共用静态线路，每个速度组别保留独立探测结果。运行时的角色与技能速度曲线仍由 `KagitKartAi` 按当前车手生成。该方式不需要 Unity 运行时加载 Node 或浏览器模块。`Fixtures/OriginalAi.json` 来自原作实时 AI 接收确定性状态带时的命令输出，覆盖四赛道、三个组别、八角色与八种库存及墨水输入；维护入口为外部 `export-ai-fixtures.mjs`。状态带隔离决策，实际运动与三圈完赛另由完整模拟会话测试，二者不能互相替代。

## 规则测试

按项目 [运行 Unity 自动化测试](../../../runbooks/run-unity-tests.md) 确认实例，再串行以 EditMode 运行直接受影响的 `Tests.Demo.KagitKartRuleTests`、`KagitKartMotionTests`、`KagitKartCharacterAssetTests`、`KagitKartItemTests`、`KagitKartRaceTests`、`KagitKartRecordStoreTests`、`KagitKartAiTests`、`KagitKartSceneryTests`。不要为这个 Demo 新建测试程序集、BatchMode 包装或验证菜单。

HUD四章小地图直接执行源`buildMap`保存底图，赛车图标沿源坐标逐帧绘制。倒计时3/2/1直接执行`letteringElement`，取`TYPE.light`及`PLATES`的纸色、网点、错版和硬阴影，保持完整字形BBox；Unity按导出的em尺寸显示，局部四顶点执行源透视折卡。中文“出发！”使用项目字体。导出入口`kagit-kart-tools/export-countdown.mjs`及`export-minimap.mjs`仍独立于Unity运行依赖，不修改源仓库；倒计时原SVG与em元数据保存在外部美术目录`SourceExports/UI/Countdown/`。
