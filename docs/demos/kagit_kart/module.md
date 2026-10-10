# 纸艺卡丁车模块

## 当前职责

独立规则归 `Hotfix.KagitKart`，不依赖场景、UI、全局 Time 设置或浏览器运行环境。不新增程序集，不修改其他 Demo 业务。公共输入补齐标量轴读取与门闩、摇杆轴图标以及横向滑条能力，职责仍在 Core.Runtime。

| 类型 | 当前职责 |
|---|---|
| `KagitKartRandom` | 原作 Mulberry32 与 UTF-16 FNV-1a 命名分流，避免表现消耗 AI、道具、网格的随机序列。 |
| `KagitKartSimulationClock` | 1/120 秒模拟、单帧 0.1 秒钳制、最多八步与丢弃积压；暂停不追赶。 |
| `KagitKartCountdown` | 360 个整数步倒计时与最后油门按下时机判定，只触发一次起跑。 |
| `KagitKartRaceProgress` | 相对起跑线距离、顺序检查点、最大进度限速、正反圈线穿越、圈速及逆行显示门闩。 |
| `KagitKartGhostRecorder` / `GhostPlayback` | 20 Hz 规则位置记录、保存量化、快照校验与最短角度插值；不参与碰撞。 |
| `KagitKartCatalog` | 赛道、角色、道具的中文名称及原版名次道具抽样顺序，剪刀不可用时重新归一化。 |
| `KagitKartTrackData` | 解析当前导出的赛道采样，校验版本、长度和必需数组；场景宿主使用公共 Loader 加载。 |
| `KagitKartSourceCatalog` | 解析原作参数和角色属性，保留数据字段与稳定标识。 |
| `KagitKartTrackQuery` | 原作高度加权最近点、空间网格、捷径换段、坡度、水面、跳台、加速带及网格定位。 |
| `KagitKartVehicle` | 原作运动、手感、漂移、跳台特技、碰墙、保护与纸鹤救援；规则位置与场景位置分开。 |
| `KagitKartCollisions` | 原作按质量分离互撞、护盾撞击、尾流蓄力及冷却。 |
| `KagitKartItems` | 道具箱、轮盘、八种道具的固定步效果、泡泡糖防御与投放、投射物、预警及对象复用。 |
| `KagitKartRace` / `GrandPrix` | 多车排名、十二秒完赛等待与估算、幽灵分段、结算、四章积分与本场重赛回滚。 |
| `KagitKartRecordStore` | 原作按赛道、组别、角色保存独立计时记录，单圈和全程独立更新，只有更快全程替换幽灵。 |
| `KagitKartAiPlan` / `Ai` | 原版静态线路、弯道、捷径探测与实时车手决策；技能和性格、漂移、道具、追赶、避障与救援均保留。 |
| `KagitKartSession` | 网格、独立随机流、完整固定步模拟顺序、公共暂停门闩及本场实例释放。 |
| `KagitKartInput` | 正式动作资产副本、原作按键和单轴死区、触控命令、自动油门、短按锁存和设备切换清理。 |
| `KagitKartSceneryAsset` / `SceneryWorld` | 四章原生场景数据、实例纸景、折起弹簧与跳台姿态；印刷动画使用宿主的表现时间。 |
| `KagitKartWorld` | 等待公共启动与场景导航稳定，持有本场 Loader、Session、赛车、幽灵和页面，返回前关闭具体 View。 |
| `KagitKartData` / `Action` / `Handler` | 核对页面来源，提交暂停、重赛、下一章和返回；UI 读取唯一 Session。 |
| `KagitKartCameraRig` | 原作镜头半衰期跟随、加速视野、回头和撞击震动，随本场解除规则订阅。 |
| `KagitKartMinimap` | HUD 铅笔路线与本场车辆位置，不复制规则。 |
| `KagitKartPostProcess` | 克隆本场纸印材质，临时安装官方全屏 Pass；世界 Base 相机单独描边与错版，切换管线或退出时恢复 Renderer。 |

## 纸印后处理

`World` 通过已有 Loader 加载正式 `Art/Materials/Post.mat`，菜单和领奖台使用原作书本墨色，比赛读取章节材质的线性墨色。`Post.shader` 保留 `post.js` 的倒数深度 Laplacian、阈值与远距离淡出、整像素颜色错版、两层纸纤维和暗角。颜色保持线性，由 URP 最终输出转为 sRGB；UI Overlay 不参与纸印。

宿主复用 URP 的 `FullScreenPassRendererFeature` 和 `CopyDepthPass`，在透明绘制后补充最终深度，避免 SSAO 提前复制而漏掉透明幽灵的深度。运行时只安装到当前管线 Renderer，退出或切换管线时移除自有 Feature、恢复深度复制模式，不保存共享管线资产。相机过滤与整像素线宽、生命周期测试通过 2/2（`88220e62`）；GPU 的倒数深度平面、台阶墨边与屏幕错版方向复验 1/1（`9a87b8b2`），纵向方向按真实 GPU 结果修正，未改测试预期。

四章正常驾驶、赛道切换和退出测试通过 1/1（`f153aef9`），并保存画面与性能记录。当前验证为 Windows Editor 的 RenderGraph；兼容模式明确关闭效果并提示，Vulkan、Player 和 Android 未验证。最佳幽灵的实际透明轮廓及公共画质切换仍需结合后续页面验收。

## 数据与边界

`Data/Tracks/*.json` 完整保存原版赛道定义与 `sampleTrack` 的采样结果，含道路、左右边缘、坡度、表面、跳台和加速带索引。文件按项目规范命名为 `Meadow`、`Bosphorus`、`Glacier`、`Desk`，内部 ID 不变。`SourceCatalog.json` 的角色属性与赛车、速度组别和道路参数已经接入运动规则。

`KagitKartVehicle.Position`、道路查询和幽灵数据保留原作坐标。表现层通过 `ToUnity` 反射 X；角色网格、法线、骨骼绑定及朝向同时转换，不能将规则位置直接赋给 Transform。沿路距离使用原作双精度，不能提前量化成 float，否则刚到捷径入口时的正负判断会改变；AI 的道具箱和障碍坐标缓冲仍按原作保存为 float32。刹车延迟、救援阶段使用原作双精度固定步时钟，避免单个规则步的边界偏移。

`Prefabs/Characters/` 保存八个原作骨骼 Prefab，`Data/CharacterMeshes/` 保存原生 Mesh，`Art/Textures/Characters/` 保存图集，`Art/Materials/Characters/` 使用 Demo 的原作 Paper Shader。Mesh `.asset` 按现有矩阵归入 Data，不改公共规范。每角色单个 SkinnedMeshRenderer，保留折痕、纸纹、动物和车型差异；比赛与菜单已接入原作骨链动画。

读取后由场景会话持有规则实例。未来输入适配只能提交赛车命令，页面只读取状态，不直接改变圈数。原作显示名不直接用于 Unity 页面，内部 ID 保持；已建立的中文名称见 `KagitKartCatalog`。

## 规则接入阶段验证

下列记录保留规则接入阶段的验证范围；正式场景、页面及后续定向验收见本文对应章节。

2026-10-10：Unity Editor 正式编译成功，零编译错误；通过 Unity Test Runner 执行 `Tests.Demo.KagitKartRuleTests`，15/15 通过。覆盖原作随机序列、起步窗口、暂停时钟、四条实际路线的三圈进度、跳过检查点、逆向穿线、逆行、幽灵插值与损坏拒绝、道具抽样。

本轮新增 `KagitKartMotionTests`，12/12 通过：四赛道 148 条实际查询与固定提交 JS 对照，包含三速度组别、刹车倒车、漂移、火箭加速和救援的七组固定步运动样本，以及重定位清理、坐标方向、质量互撞、护盾和尾流冷却。查询对照双方接收相同 float32 输入坐标；车辆位置误差阈值 0.035 米、速度误差 0.025 米每秒，不声明跨语言逐位一致。

`KagitKartCharacterAssetTests` 9/9 通过，检查正式 Prefab 的骨骼、顶点权重、绑定姿态、法线、UV、图集，以及本 Demo 资源的项目命名与必需 Label。八个角色已用 Unity 原生预览逐个检查；一次性装配和预览脚本、菜单及 `.meta` 已清理。本轮复验规则 15/15、运动 12/12、角色资源 9/9，总计 36/36 定向用例，最新正式编译零错误，Console Error 为零。

道具、比赛、大奖赛和 AI 已接入完整规则模拟会话；正式场景、三端触控布局、角色、纸鹤、音频及 Hub 进入返回现已接入。完整单人内容与 Editor 验收仍在推进。

`KagitKartItemTests` 14/14 通过，包括 64 个原作 JS 投射物轨迹参考帧、三连火箭单次边沿、泡泡糖防御与释放、墨水按投掷时名次确定目标、护盾、剪刀目标变更与加速躲避、箱子和轮盘、近领跑者概率限制、剪刀并发门闩及退出清理。该测试不等于道具模型、声音或操控体验验收。

`KagitKartRaceTests` 9/9 通过：四条实际路线的三圈规则、十二个幽灵分段、有效完赛快照、起步、等待与估算、四章积分累计、重赛回滚、同分排序和退出订阅释放。轨迹是沿路合成输入，只验收规则会话，不是 AI 或实际操控。该范围发现并修复了原作分段毫秒量化导致末段略高于未量化完赛时间的校验误判；容许量化半毫秒，同时仍拒绝明显越界分段。

`KagitKartRecordStoreTests` 5/5 通过：空档不写默认值、身份隔离、最佳单圈与全程独立更新、保存后幽灵插值、损坏幽灵保留时间、非法快照拒绝，以及 Windows 文件占用导致原子替换失败时保留旧档。基础规则加入毫秒量化回归后复验 16/16。没有执行全量测试，也没有把存档测试当作页面错误提示已接入的证据。

本轮正式编译零错误，运动对照复验 12/12；当前这轮直接相关 EditMode 范围共 56/56（基础 16、运动 12、道具 14、比赛 9、记录 5）。此前角色资源 9/9，角色资产未在这轮修改。未执行 PlayMode、实际驾驶、三端输入、场景画面或性能验收。

后续 `KagitKartAiTests` 29/29 通过：十二组原作决策状态带，共 453 个八角色命令帧，保持原作车手奇偶漂移检查调度；四赛道 × 三个速度组别的十二次自动驾驶实际车辆运动均完成三圈并形成有效幽灵；四次一玩家加七 CPU 比赛正常使用道具和完整结算；公共暂停门闩冻结整场模拟，恢复不追赶，退出清空命令和订阅。这里的“实际车辆运动”指完整固定步规则模拟，不是合成轨迹，但仍没有验收键盘/手柄/触控或场景画面。用户手动驾驶、PlayMode 交互与性能仍待验证。

新增 `FullSimulationGrandPrixRunsAllChaptersAndReversesStandingsGrid` 单项通过：使用完整八车模拟连续完成四赛道，核对累计积分、时间和第二场起的逆积分出发顺序。AI 范围现共 30 个永久用例，按改动定向执行；沿路距离精度修复后，运动 12/12、道具 14/14、比赛 9/9 已复验通过。该规则验证时尚未创建正式场景或页面，四场模拟成功不等于菜单与场景切换流程已验收。

## 道具接入

比赛会话每步按原作顺序更新：输入 → AI → 赛车运动 → 互撞和尾流 → 道具 → 比赛排名和圈数 → 幽灵采样。`KagitKartItems.Advance` 读取当前步更新排名之前的名次，并接收独立比赛时间与倒计时状态。`Vehicle.Place`、`Progress`、`Finished` 由比赛规则维护，`Item` 等道具状态仅由本场道具实例维护。输入仍提交统一 `Controls`，道具实例自行记录使用键边沿；泡泡糖保留原版按住防御、松手投放语义。

`Boxes`、`Hazards`、`Projectiles` 的位置是源坐标，场景按同一 X 反射转换并使用前后位置插值。`Signaled` 提供道具获取、使用、投射物阶段、早晚预警和瞬时视觉事件，不在规则中生成模型或播放声音。暂停、失焦和设备切换清空输入，恢复后使用中性输入门闩；退出比赛调用 `Dispose`，清理道具引用、库存、轮盘和订阅。

## 计时记录

`KagitKartRecordStore` 复用 [公共本地存储](../../modules/local-data.md)，文件为 persistentDataPath/KagitKart/Records/<赛道>-<组别>-<角色>.json，路径集中在 Hotfix 的 `LocalDataKeys`。读取不自动写默认值；损坏幽灵丢弃，合法时间保留。保存先校验真实三圈快照，最好单圈独立更新，只有更快全程替换幽灵；原子保存成功后才返回候选。实际 IO 失败继续抛给场景宿主，由中文页面提示，不提交内存中的保存成功状态。

## 本场模拟与 AI

### 三端输入

正式资产为 `Data/PlayerInput.asset`，比赛使用 Drive，全局快捷键使用 Global；键盘、标准手柄和触屏最终都输出 `KagitKartControls`。菜单确认和返回使用 Core 的 `UIMenuScope`，不新增菜单输入底座。操作提示绑定同一会话动作副本，手柄单轴按原作 0.18 死区读取，方向键覆盖摇杆。

宿主每帧先 `CaptureFrame`，然后用缓存的读取委托调用 `Session.AdvanceInputFrame`，在每个固定步 `ReadControls(是否倒计时)`。零步帧不丢短按，追赶步不重复消费短按；按住泡泡糖与漂移保留持续状态。触控通过公共 `TouchInputPad`、`InputCommandButton.HoldChanged` 接入。自动油门默认关闭，倒计时始终手动油门，主动刹车优先。

输入设备切换清空旧触控与锁存命令；交付的触控命令按原作与当前物理命令合并，转向取绝对值较大者、油门和刹车取较大者、按钮取或。显式开启触控不伪造设备识别，鼠标也可操作保存的触控组件。隐藏触控区会释放保持按钮与转向值。`InputCleared` 由宿主连接本场命令和触控组件清理；失焦、后台、手柄断连同时调用 `ResetInput` 和公共暂停门闩。退出释放输入副本和设备订阅。

### 设置与操作说明

模式页和暂停页均可打开独立 `KagitKartSettingsView` / `KagitKartControlsView`，各自保存正式 Prefab 与生命周期。设置复用公共 `LocalDataManager`，键为 `LocalDataKeys.KagitKartPreferences`；音乐默认 0.7、音效 0.9、自动油门和帧率默认关。保存成功才替换场景偏好，损坏记录回退默认而不覆盖存储，失败显示中文反馈。画面行按需打开已有 `DlssSettingsView`，不复制公共画质实现。

音乐/音效和静音交付现有音频宿主，自动油门仍允许主动刹车，触控显示独立于真实输入设备。帧率按真实 `Time.unscaledDeltaTime` 每半秒采样，独立于固定步比赛时钟。低动态显式开关覆盖纸卡、立体书、角色、纸景、赛前与冲线演出，并关闭镜头震动；切换赛前路线保留原有进度。`ReducedMotion=auto` 通过 Core.Runtime 的 `SystemMotionPreference` 在启动及修改该选项时读取：Windows 使用原生客户区动画开关，Android 使用系统动画尺度是否为零；查询失败使用正常动态，不修改系统偏好。Windows 真实读取 Test Runner 1/1（`09ae1204`），设置回归 2/2（`71fa701a`）。Android 条件分支尚未在 Player 或真机执行。首次四行操作卡与五类驾驶提示接入独立非交互 Widget，不创建输入作用域。

暂停页与设置页在叠加 Modal 的 `OnHide` 时保留场景来源，在销毁时解除。UIManager 关闭上层 Modal 后只重新显示下层实例，不再交付 SetData，故隐藏时清空 world 会使恢复后的按钮失效。操作说明读取本场动作副本及 override，设备图标沿用公共绑定提示，菜单确认和返回仍走公共作用域。

直接相关 Test Runner 检查：偏好默认值及存储 2/2（`646ad162`），输入与真实绑定 9/9（`f914479c`），赛前路径 2/2（`61f309fd`），书本生命周期及低动态切换 2/2（`c5b84851`）。菜单设置、操作说明、暂停 → 设置 → 公共画面 → 设置 → 暂停同实例恢复及真实继续按钮通过 1/1（`c27c5f14`），关闭分别使用键盘返回、触屏轻触和手柄返回；暂停时钟保持不动。弹窗关闭复用输入会话的松键门闩，避免同一次 Esc 继续触发比赛恢复。模式纸卡保存显式连接，公共自动导航继续服务其他控件；手柄选赛与三指组合驾驶、暂停及继续复验 1/1（`2b9bd703`）。正式资源命名与标签检查 1/1（`2f52ae2a`）。截图保存为外部 `Previews/Settings.png` 与 `Controls.png`。完整单人 Demo 尚未验收；永久测试仍只在项目现有测试程序集。

暂停、失焦、切换设备与退出时复用公共输入清理，防止保持动作残留。后续优先完成正常菜单与比赛流程，不扩展持续按住旧设备等特殊切换用例。正式资产保存后已清理一次性输入装配脚本、菜单、文件夹及 `.meta`；运行时不生成默认动作资产。

`KagitKartInputPlayModeTests` 7/7 通过：保存的键盘组合键、手柄标量死区与方向键优先、触控短按锁存、自动油门、保持输入释放、真实 Input System + EventSystem 三指同时转向/加速/漂移，以及固定步只消费一次短按。测试动态创建输入夹具，不代表正式 HUD、完整菜单、真实手柄或 Android 真机已验收。

### 规则链路

`KagitKartSession` 使用 grid / ai / items 独立随机流生成原版网格，后续大奖赛按积分排名逆序出发。每帧只接收赛车命令和未缩放时间，遵循原作 120 Hz 链路；暂停使用公共 `LocalPauseState`，不修改全局 Time。场景宿主读取 `Karts`、`Race`、`Items` 和 `Interpolation` 更新表现。退出先解除暂停回调，再释放 AI 的道具预警订阅、道具对象和比赛事件。

静态路线直接由原作的松弛线路、弯道合并、跳台区域和多条件捷径探测生成；运行时车手按原作技能随机分配、性格和角色操控属性生成速度曲线。保留有机会失误的驾驶、尾流靠拢、抢道具箱、避泡泡糖、火箭捷径、三级漂移、跳台特技、按名次使用道具、剪刀预警躲避及卡住/逆行救援。原作仅供测试的参数覆写和调试日志不作为玩家功能接入。

## 四章场景表现

正式资源位于 `Prefabs/Tracks/`、`Data/Worlds/`、`Data/TrackMeshes/` 与 `Art/Textures/Tracks/`、`Art/Materials/Tracks/`。四章保持源层级、顶点色、多材质面组、UV、道路专属属性和实例色。网格、法线、绕序和实例矩阵统一反射 X，所有运行时资源均为原生 Unity 保存资产。

`Paper.shader` 转译原作道路、越野带、书页、加速贴纸、水波和纸洞的印刷算法；`PaperSky.shader` 保留七段天空印刷与纸纤维。纸洞使用 Unity 裁剪空间和 `SV_Depth`，不直接套用 WebGL 深度换算。Shader 编译与原生渲染均已核对，仍需在真实驾驶过程中检查视觉细节与性能。

`SceneryWorld.Render` 由场景宿主每帧调用，显式传入比赛摄像机、赛道距离、表现时间、暂停后的 dt 和回头状态。实例矩阵属于各场世界，不修改共享数据或材质；场景静态部分使用保存的 MeshRenderer，纸景按原作沿程分批实例绘制。折起沿用 70–110 米触发、身后 60 米折回、回头保持、弹簧过冲和纸片颤动；跳台提前 125 米立起。天空材质由宿主绑定本场摄像机的 Skybox，不修改项目全局 RenderSettings。

Unity Test Runner 的 `KagitKartSceneryTests` 4/4 通过（`8df7f53d`），覆盖四章保存引用、实例批次、纸景连接、跳台暂停冻结和共享资产保持。`KagitKartSceneryDynamicsTests` 的四章 16 组源码快照 4/4 通过（`8206ebdc`），覆盖风车、云与吊线、渡轮与烟、缆车和蒸汽；`Data/Worlds/*Dynamics.json` 保留原作动态数据，解析复用 Newtonsoft 以保持可选对象的 null 语义。四章路牌图集已使用中文，保留网格与资源 GUID。

纸纤维是线性数值纹理，五份原作 grain 导入均采用非 sRGB、三线性与各向异性 8。原生预览保存在外部美术目录 `ArtSource/kagit_kart/Previews/`。这些规则与数据检查不代替完整驾驶和所有 GPU 视觉验收；原作屏幕描边与错版后处理现已接入，原生 GPU 与生命周期证据见本文纸印后处理章节。

本次未执行全量测试、完整菜单与驾驶交互验收、Player 构建或 Android 真机验证；现有 PlayMode 输入夹具测试不能替代这些验收。

## 场景与页面生命周期

`World` 等待 `DemoIslandEditorBootstrap.EnsureReadyAsync` 和 `GameSceneNavigator.WaitUntilStableAsync`，通过公共 Loader 加载原作配置、赛道、AI 路线和角色资产。比赛 Loader 每场独立释放，退出同时解除输入、暂停与镜头订阅；不在 MonoBehaviour 字段初始化中读取本地存储路径。大奖赛下一章使用现有积分状态，重赛调用原作回滚；计时赛读取最佳幽灵并在实际完赛后原子保存。

HUD、暂停和结算各自持有 View、Prefab 和生成绑定。HUD 不创建全局 Canvas/EventSystem，触控按钮复用 `InputCommandButton`，横向滑条复用 `TouchInputPad`，提示绑定实际动作并复用 `InputBindingPrompt` 和项目图标目录。驾驶使用公共 `MenuInputScope.Interaction` 关闭菜单 Submit，暂停和结算通过 `UIMenuScope` 接管焦点，避免漂移 Space 同时确认 HUD 按钮。

页面显示前由SetData交付场景来源；嵌套Modal遮挡时保留来源，关闭或销毁时解除订阅与引用，退出前等待关闭本场具体 View，不在 `OnDestroy` 发起按类型异步关闭。暂停同时冻结规则与纸景、镜头的表现时间，不改变全局 Time 设置。场景切换回滚时恢复本场页面并保留明确继续门闩。

入口已改为原作选赛流程：封面、模式、纸张克重、角色、章节分别使用独立 Page View、正式 Prefab 和 UIBind 绑定。选择通过 `KagitKartAction` 提交，World 保存同一份选择；角色和章节焦点按原作即时更新选择与 3D 预览，确认才推进页面或比赛。大奖赛确认车手直接加载首章，单场与计时赛确认章节后开赛。返回上一步保留选择，返回模式时释放本场资源；重赛或选赛加载失败恢复可操作菜单。

菜单复用公共 `UIMenuScope` 与 Core EventSystem，卡片只桥接原作焦点/鼠标预览事件，不接管确认或返回输入。静止鼠标在页面出现时产生的 Enter 不覆盖键盘/手柄焦点；实际移动鼠标可预览，触屏点击可确认。纸张克重保留一层薄纸、三层横线笔记本纸和八层纸板；五项属性按原作四舍五入为五格。

`KagitKartBookStage` 接入原作桌面立体书：封面开合、书页呼吸、曲面翻页、纸景弹簧铰链、车手切换与转盘、封面/选择镜头过渡。每实例克隆动态 Mesh，退出释放，不写共享资源。中文四章印刷与封面保存于 `Prefabs/BookStage.prefab`；纸卡布局露出舞台，`KagitKartMenuPaperMotion` 保留入场曲线、焦点抬升与书签。原 JS 几何对照 1/1（`abfe5fde`）、舞台生命周期 1/1（`ed6c7209`）、新版完整菜单三端场景流程 2/2（`23bcab93`）通过。

`KagitKartCharacterVisual` 直接迁移 `characters/animate.js`，菜单与比赛共用八角色骨链、轮转、方向盘、漂移、打转、特技、救援、幽灵和情绪表现；宿主调用 `Bind` / `Advance`，暂停传零。材质使用原作 Paper Shader，实例属性块保存环境、发光与无敌闪烁。固定提交 JS 直接导出的 50 组姿态由八角色测试逐骨比对，8/8 通过（`4a486885`）。

`KagitKartItemVisuals` 使用保存的 11 类源网格和专用 Shader，按源事件表现箱体折叠、钢笔点火抛落、口香糖气泡/拖线、飞机轨迹、墨水、剪刀和透明纸壳。原 JS 的 14 场景 / 56 实例快照对照 14/14 通过（`fb6ae50e`）；GPU 与完整赛道比赛另行验收。

`KagitKartRaceCinematics` 将赛前时间与比赛模拟分开，六秒镜头结束或键盘/手柄/触屏跳过后才倒计时。中文章节卡复用统一副标题。暂停不推进演出；冲线环绕沿用原作构图，为结算表让出玩家位置。赛前曲线源码对照 1/1 通过（`45f13610`），键盘与模拟手柄、多指触控流程各 1/1 通过（`e59d0239`、`938119b0`）。

音频接入 `KagitKartAudio` / `AudioBank`，包含 97 个原作 Clip、连续加速配方、实时引擎和两级总线 DSP；旧的 14 个变速引擎循环已移除，新增原作粉噪声。音乐离线保留滤波前采样，由运行时处理一次正常/暂停低通。算法、原生 WebAudio 采样及资产契约 11/11 通过（`912553ee`），含新 2x 软削波对照。赛前镜头期间关闭引擎，结束才启用并播放第一声倒计时。实际听感与性能仍需验收，见 [音频维护](runbooks/audio.md)。

`KagitKartRaceFx` / `KagitKartRescueCrane` 接入原作纸屑、尘迹、速度线、落地与碰撞纸卡、漂移和纸鹤救援。固定源码快照对照 20/20 通过（`846bde10`）；接入后的键盘比赛场景 1/1 通过（`824f826a`）。全特效 GPU 表现及真实掉落救援仍需实操验收。

`KagitKartPodiumStage` 接入原作三层纸台、前三车手落台、失落玩家、纸屑和推进镜头；最后一场先显示本场成绩和独立总积分榜，再由积分榜按钮进入颁奖，结束后显示独立 `KagitKartPodiumView`，可返回封面。正常、减少动态、窄屏与角色回退四组原作快照 4/4 通过（`a89d7a35`）；四章三圈、逐场积分、领奖与返回封面的 Editor 流程 1/1 通过（`b0e63dc7`），使用原作 AI 与加快推进的正式固定步模拟，包含前三纸卡与返回按钮完全显示断言。书本切换及共享网格释放回归 1/1 通过（`9896c879`）。这不是人工驾驶或真实设备验收。

设置、操作说明及原作后处理现已接入；完整 HUD 已接入并通过后续定向验证，完整 Editor 验收仍未完成。资源命名与标签定向检查 1/1 通过（`67cb0b7a`）。上述检查不包含全量测试、Player 构建或 Android 真机。

四章动态与实时音频接入后的大奖赛完整流程 1/1 通过（`50adbd88`），键盘驾驶、暂停、重赛与返回 Hub 1/1 通过（`beecf0dd`）。四章正常运行及退出释放采样 1/1 通过（`4d64beb6`），实际数据见 [Editor 性能记录](runbooks/editor-performance.md)。

`KagitKartScenePlayModeTests.HubRaceKeyboardPauseRestartAndReturn` 在入口改为菜单后 1/1 通过（`a16c4581`）：实际键盘逐页选择单场比赛、加载八车比赛、倒计时、油门＋转向＋漂移、暂停冻结、恢复、重赛和返回大厅清理 Data/UI。

新版 `KagitKartScenePlayModeTests` 2/2 通过（`23bcab93`）：从 3D 封面按键开书、键盘逐页选赛与驾驶/暂停/重赛/Hub；模拟手柄选择书写桌面计时赛，焦点同步预览与返回；多指触屏选择墨水章鱼大奖赛并同时转向＋油门＋漂移与暂停恢复。实际菜单截图保存于外部美术目录 `Previews/Menu*.png`。此范围不证明四章三圈与整届大奖赛、全部设置、真实手柄或 Android 真机已验收。

公共取消转发的最小回归测试与动态导航/嵌套作用域既有测试分别 1/1 通过（`70ba2c5f`、`a8f54a9f`），未执行全量测试。

运行截图暴露的起点纸景遮挡已定位到原作章节 updater 未初始化：导出时先推进零时刻 updater，再保存真实云、吊线和风车附属部件姿态；运行时云和吊线继续朝向摄像机，折起触发使用摄像机对应赛道距离。小地图显式保存 CanvasRenderer，防止只保存 Graphic 脚本导致没有实际 UI 网格。

场景保存对话框解除后，同一驾驶流程含小地图网格断言复验 1/1 通过（Unity Test Runner 作业 `141ee108`）；实际截图确认路线与车手标记可见。临时页面装配使用隔离 Preview Scene，避免在当前编辑场景创建和删除临时节点而标记其修改；正式页面资源保存与定向验证完成后，本轮七份一次性UI装配脚本及其.meta、临时目录.meta已清理，不作为维护入口保留。

## 首次教程与驾驶提示

`KagitKartHints` 直接迁移 `hints.js`：倒计时3打开首次四行卡，倒计时不扣七秒，Running累计七秒后写既有设置的 `SeenTutorial`；暂停传零表现时间，重赛与离场取消卡片且不记完成。Corner/Tier/Ramp/Item/Portrait提示沿用原阈值、队列与3.8/4.5秒时长，真正显示时才写 `KagitKart.UiHints`，缺失或损坏使用空历史，写入失败只警告并保留本场一次性状态。

`KagitKartHintsView` 使用 Foreground Widget 正式 Prefab、UIBind、中文字体和真实绑定提示；没有按钮、菜单作用域或射线阻挡。纸卡沿底边弹起复用 `KagitKartMenuPaperMotion`，教程0.62秒、情境卡0.5秒，低动态直接显示终态。Widget跨菜单/重赛保留，离开Demo精确关闭本实例，切场失败恢复。原网页软件渲染器的浏览器硬件加速说明没有Unity对应条件，因此不显示该浏览器专属说明。

教程规则 Test Runner 4/4（`80aa1588`），覆盖倒计时/暂停、退出不提前标记、源码队列与阈值、提示历史及竖屏时长；真实场景 Test Runner 1/1（`2513c7c4`）验证跳过电影、倒计时/暂停冻结、累计七秒保存、下场不重复及实际中文纸卡可见；截图保存为外部 `Previews/TutorialKeyboard.png`。一次性教程 Bake 及其 `.meta` 已清理。该检查不代表真实手柄、Android 或全量回归通过。
## HUD 屏幕墨水

`KagitKartHudInk` 直接迁移源三团曲线、触须、六条滴落与最后35%的整层渐隐，表现随机独立于比赛。墨斑先在活跃期间的单张局部RT按源sRGB数值合成，UGUI仅应用一次整层透明度，避免重叠墨斑渐隐变黑。`LastInkDuration`保存本次事件时长，规则 `InkTime` 仍取max；连续中墨重新生成全部形状。

HUD使用 Foreground Widget，先创建的教程在其下方、Pop暂停在其上方。墨水子Canvas只改变自身排序，位于Foreground之前，因此遮挡3D但不遮挡教程、HUD、触控或暂停；不改公共UI层定义。Canvas尺寸按根Canvas缩放取实际像素，满屏不受safe-area内缩影响。原作CSS滴落按墙钟继续，暂停只冻结规则剩余时间及渐隐；低动态取消滴落、显示完整形状。结束/隐藏释放RT，销毁释放Mesh与自有材质，换场解除玩家订阅。

原JavaScript曲线/滴落/渐隐fixture Test Runner 1/1（`0d38c85c`），真实Camera+Canvas GPU单层/重叠/高光/UV/整体渐隐与释放1/1（`18af6422`）。层级调整后实际比赛 Test Runner 1/1（`568e176b`），验证玩家事件、连续中墨、暂停剩余时间、教程/HUD排序与重赛释放，并已检查 `Previews/RaceInk.png`；覆盖排序在OnShow进入实际UI层后设置。手柄完整选赛及三指触控/暂停/继续复验1/1（`d72d6811`）；未执行全量测试、Player构建或Android真机。
## 原生计时赛与幽灵回放

幽灵遵循原 `ghost.js` 的显隐：创建时隐藏，非倒计时阶段（包含赛前电影）按比赛时间采样，倒计时保留上一位置与可见状态；越过最后有效采样后隐藏。表现始终独立于车手规则数组，不参加排名与碰撞。

真实场景 Test Runner 1/1（`900a688b`）使用正式原作AI与固定步完成三圈，通过实际 `raceEnd` / `SaveBest` 写入，再以同赛道、组别、角色重开并检查原生幽灵位置、朝向及末端隐藏。等待后完成更慢的第二场，确认全程最佳与幽灵不覆盖，单圈最佳仍独立更新。测试仅注入已有RecordStore的临时目录并恢复偏好，不写用户原有成绩；使用自动驾驶，不是人工三圈驾驶验收。带实际回放截图的复验1/1（`cd556efa`）通过，已检查外部 `Previews/TimeTrialGhost.png`；后续完整 HUD 接入及定向证据见本文原作比赛 HUD 章节。
## 原作比赛 HUD

`KagitKartItemSlot`显示真实库存的八张原作图标、九格轮盘（八种加第一格）、数量与持有纸框；道具卡也使用公共`InputCommandButton`，只在触屏驾驶时交付道具保持。`KagitKartMinimap`复用固定源`buildMap`直接导出的四章底图，实时车手标记独立逐帧绘制，保留源SVG坐标方向、箭头角度与小数舍入；底图不参与标记纹理采样。

`KagitKartHudNotice`订阅本场比赛、道具与玩家事件，显示倒计时、起步评级、最终圈、逆行、冲线及等待、幽灵分段差、换圈纸页和来袭预警。比赛时间与结算成绩按原百分秒向下截取；单人隐藏`/1`，多人显示实际车手总数。JS通知寿命与轮盘使用暂停感知秒数；CSS折卡、盖章、摇摆与离场继续墙钟。菜单、规则与公共Canvas定义不因此改变；离场取消三处订阅并销毁局部通知。

源码计时、预警方位及四章地图坐标 Test Runner 2/2（`8f665460`）。正式场景库存/倒计时测试1/1（`fd5b561d`）：计时赛初始三火箭、真实道具使用与口香糖持有/释放、真实道具箱触发轮盘并显示实际结果，暂停不消耗Go寿命，实际Hub退出释放HUD。测试移车至箱体及口香糖库存是前置样本，不代表人工驾驶采集。正式Prefab通知表现测试1/1（`2c7ededa`）验证实际TMP折卡网格、通知寿命、预警方位、暂停双时钟及重复绑定/清理；这些事件由表现样本构造，不证明比赛规则触发。已检查`HudNativeTimeTrial3`、`HudFixtureFlipThreatAndSigns`、`HudFixtureFinishAndWait`实际截图；原倒计时字形及触屏槽布局已继续补齐，完整比赛视觉验收另行记录。

## 菜单退出与长驻帧率

`KagitKartMenuPaperTransition`只将已有纸卡动效接入公共退出等待链：四个选赛页与从菜单打开的设置/操作表单按源0.16秒ease-in折下，减少动态直接结束；暂停子页按源`panels.cleanup`直接关闭。入口仍由`SetData.Play`弹起，回滚恢复完整纸卡。项目FIFO导航先退出后进入，与原DOM同时追加新页的组织不同；不因此新增平行UI导航。缓动共用`KagitKartHudMotion`。

原根节点FPS现在复用长驻`KagitKartHintsView`局部区域，唯一采样仍由World每0.5秒未缩放时间提供，刚开启先空白，关闭重置。FPS子Canvas在`OnShow`按Tip的层和排序+1恢复，不遮挡输入；HUD不再重复保存FPS文本。折卡契约 Test Runner1/1（`74e4d8ab`），覆盖未缩放时间、原无额外淡出、回滚和低动态/暂停来源免等待。正式设置按钮与FPS场景测试1/1（`a308ddc2`），覆盖标题/模式/比赛/暂停、菜单和暂停的真实开关、字体/网格/排序可见及退出释放，恢复原偏好。

倒计时字形改为直接源导出的三张Sprite，`KagitKartCountdownCard`按完整em边界变形四顶点，替代项目字体数字；中文Go保留TMP投影。真实字形折卡网格及通知复验1/1（`6d77f4e2`），实际3/2/1截图与库存复验1/1（`f07d8a16`），已查看`HudNativeTimeTrial3.png`确认红错版、网点和硬阴影。道具卡按源全局em、安全区与触屏/短屏窗口布局，名字按内容宽度淡入；持有边框保留原CSS在低动态下仍继续的选择器行为。

最终HUD去掉原作不存在的常驻底部操作条，避免遮挡来袭预警；教程、操作说明和情境提示仍由真实绑定提供。该正式Prefab通知复验1/1（`7235e63f`），手柄完整选赛与三指触屏/暂停恢复及实际道具槽尺寸复验1/1（`10dec2c8`），资源命名与标签1/1（`77d35b87`）。本阶段没有修改Core.Runtime，不执行全量测试、Player构建或Android真机验证；结算/GP积分榜与暂停返回已在下述阶段补齐。

## 暂停返回与子页快捷键

暂停页的“返回菜单”复用Demo标题路由，并清除杯赛；“返回大厅”仍是独立项目入口。GP先显示杯赛进度丢失提示，第二次选同一行才退出；移焦到其它行或打开子页取消确认，复用已有纸卡Focused事件，不新增通用确认弹窗。计时赛/普通比赛一次选择返回标题。

World在Demo设置/操作子页仍处理P/Start继续，先关闭子页再恢复比赛；Esc/B继续交给公共菜单只退一层。TT的R仅在非赛前电影的倒计时、Running、Finishing有效，仍沿已有重赛/资源关闭链；不扩大为任意页面热键。项目额外的公共画面设置Modal保持自己的关闭契约。正式场景 Test Runner1/1（`5ef2130b`）验证GP确认/移焦复位、标题与Hub区别、键盘/模拟手柄子页继续和返回、TT电影期间R无效与倒计时子页R实际重赛。截图`PauseGpQuitConfirmation.png`来自正式暂停Prefab。

暂停赛前演出时，“跳过演出”的按钮及阴影按完整子组一起隐藏，避免残留黑纸片；同一路由场景复验1/1（`61e8c7bd`）通过。该阶段仅改KagitKart Hotfix、暂停正式Prefab及直接相关场景测试；公共Core输入和导航未修改，未执行全量测试或构建。

## 本场成绩与总积分榜

独立 `KagitKartResultsView` / `KagitKartStandingsView` 使用正式 Prefab、UIBind 和共享行子Prefab。普通比赛显示真实八名车手的名次、头像、全程与最佳圈；GP补本场积分与累积积分，先显示本场成绩，再由按钮打开总积分榜，最后进入下一章或颁奖。计时赛显示三圈与全程、当场最佳标签和已保存全程的新纪录印记。返回菜单走标题路由，Results源FocusList无onBack，因此不把Esc/B作为退出比赛。

`KagitKartWorld.Results`只管理这段导航和显示名次历史，比赛积分仍归GrandPrix。积分榜升降按源开赛前积分稳定排序，同分沿首场插入顺序；当前总榜依次按真实累计积分、最近一场名次、总时间排序。`KagitKartResultRow/Shape`表现原纸色玩家高亮、斜积分条、箭头及延迟入场，不保存另一份赛果。View由World交付表现帧与横屏安全区；公共Core/UIManager没有修改。

进入Done按源清空并隐藏HUD通知/屏幕墨水。结束事件正在分发时，即使解除订阅，已复制的旧回调仍可能被调用；Notice三个入口仅在本场绑定有效时处理，避免结算时空引用。

扩展现有原生大奖赛测试1/1（`855a98e9`）：实际AI与比赛固定步连续完成四章三圈，核本场表格、独立积分榜、真实GPU积分条、升降、下一章与领奖台/标题。原生计时存取/回放/慢场不覆盖测试1/1（`f1ecf9de`），同时核三圈表、最佳标签、新纪录、隐藏按钮和实际HUD清理。已检查 `GrandPrixRaceResults.png`、`GrandPrixStandings.png`、`TimeTrialResults.png`。源表格规则另以固定提交JS执行核对，非C#自生成期望值；场景测试为自动驾驶，不冒充人工完整驾驶。

单场真实三圈、再赛一场和再次完赛后返回封面 Test Runner1/1（`b01a3de2`），同时核八行无GP积分、最佳圈保留、HUD/Ink隐藏、新场和旧页释放。按钮复用公共UIState/UIStateInteraction五态，主纸底使用既有White Sprite保持源墨色，焦点抬纸、红内沿与硬影；Pressed中性压暗、Disabled淡出。实际键盘选中/移焦复位及单场完整路由复验1/1（`2e76608f`）；已检查更新后的 `SingleRaceResults.png`，消除暖黄Tint与暖纸纹双重乘色。新增资源命名/标签1/1（`10658471`），本阶段Editor编译成功、Console错误0；未运行全量测试、Player或Android验证。

## 原生救援、世界道具与音频路由收口

`NativeFallSummonsPaperCraneAndReturnsToRoad` 1/1（`72715ba7`）在正式冰川场景记录真实安全点后，将车辆置于源Query确认的落空边缘；正式重力触发提起、搬运、放下、落地四信号，核真实纸鹤网格、回到安全地面、保护时间及离场引用/音频载体释放。已检查五张 `NativeRescue*.png`。原草原只定义围栏，不能用不存在的悬崖做前置；安全点等待规则实际满足，不把固定墙钟等待当模拟已推进。

`NativeEightItemsRenderAndReachUnityAudioOutput` 1/1（`19bc564c`），覆盖全部八件正式库存、键盘E使用/口香糖保持及松开、源世界网格、追踪目标、墨瓶爆墨、剪刀俯冲/剪断、保护及弹体清理，实际use_*声路由、最终混音与退出释放。摆车为明确表现前置，全部位于源圈线检测的前60米内，排名仍由Race更新，不直接改名次。已检查14张 `NativeItem*.png`；非自然抽奖、人工驾驶或人工集中道具压力测试。AudioListener实际输出峰值0.6655096，采样率48000；原静音载体的AudioSource采样为零，最终Listener采样能得到DSP后输出。该结果证明链路工作，不声称逐件隔离音色或人工听感。

源码回查补齐倒计时0.9秒退赛前曲、每车撞墙0.2秒再共享world桶0.02秒，以及tier/threat/rouletteTick按源配方共用桶。原全局P/R/M不追加UI click，已暂停P继续与UI按钮保留click。`NativeAudioMixerHasRealOutput` 1/1（`9c1f1c0d`），核真实Intro→Countdown→Race、P暂停/菜单Enter继续路由及最终混音，峰值0.6997126；未修改全局Editor静音、项目AudioManager或用户偏好。

纸景原先缺少实际Popup音频出口，现由SceneryWorld的真实目标false→true发源最大尺寸/0.18秒波次和坡台位置，World订阅/StopRound退订，Audio聚合rustle。四World资产仅补存源1401项size（766/227/363/45），逐项核源版本/顺序/位置/尺度，GUID保留；一次性补存脚本及.meta已清理。正常行驶中的纸景声与四章长圈性能另在后续记录，未把源码接线当长期性能通过。本阶段仅Hotfix、Demo数据资源、现有PlayMode测试与文档；Editor编译成功、Console错误0，未执行全量测试、Player/Android或人工听音。
