# 纸艺卡丁车模块

## 当前职责

独立规则归 `Hotfix.KagitKart`，不依赖场景、UI、全局 Time 设置或浏览器运行环境。不新增程序集，不修改其他 Demo 业务。标量轴读取与门闩、摇杆轴图标及触控保持组件复用 Core.Runtime，Demo 只转换驾驶命令。

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

## 公共时间与模拟上限

`World.Update` 将公共 `GameTimeService.DeltaTime` 交给本场固定步时钟，规则不再重复乘倍率。公共作用域与冻结生命周期见[设置与表现模块](../../modules/settings-presentation.md)，不另建 Demo 时间服务。

作者的单帧最多八步与丢弃积压仍保留，实际快进受帧率限制：理论最大比赛推进为实际 FPS × 8 / 120。不能保证低帧率下任意请求倍率完整兑现；比赛、圈时和 Ghost 共用模拟时钟，不为快进绕过八步保护。

## 纸印后处理

`World` 通过已有 Loader 加载正式 `Art/Materials/Post.mat`，菜单和领奖台使用原作书本墨色，比赛读取章节材质的线性墨色。`Post.shader` 保留 `post.js` 的倒数深度 Laplacian、阈值与远距离淡出、整像素颜色错版、两层纸纤维和暗角。颜色保持线性，由 URP 最终输出转为 sRGB；UI Overlay 不参与纸印。

宿主复用 URP 的 `FullScreenPassRendererFeature` 和 `CopyDepthPass`，在透明绘制后补充最终深度，避免 SSAO 提前复制而漏掉透明幽灵的深度。运行时只安装到当前管线 Renderer，退出或切换管线时移除自有 Feature、恢复深度复制模式，不保存共享管线资产。兼容模式关闭效果并提示；其它后端的实际画面和资源释放按对应验收记录判断。

本 Demo 的 Paper、Items、Sky、FX 和 HUD Shader 不读取 AO 或 `_CameraOpaqueTexture`，纸印只读取 Blit 颜色与场景深度。宿主在 `beginCameraRendering` 匹配本场世界相机时，临时关闭颜色复制并停用原本活跃的 SSAO 实例；在 `endCameraRendering` 恢复该次渲染前的 `requiresColorOption` 原枚举和具体 SSAO 实例状态。其它相机不进入这段作用域，切管线、重绑或卸载也会收回未结束的临时改动；共享 Feature 不销毁，公共设置仍维护相机原偏好。保留 `FinalDepthPass` 与透明后深度，确保透明幽灵轮廓和公共景深需求。

## 数据与边界

`Data/Tracks/*.json` 完整保存原版赛道定义与 `sampleTrack` 的采样结果，含道路、左右边缘、坡度、表面、跳台和加速带索引。文件按项目规范命名为 `Meadow`、`Bosphorus`、`Glacier`、`Desk`，内部 ID 不变。`SourceCatalog.json` 的角色属性与赛车、速度组别和道路参数已经接入运动规则。

`KagitKartVehicle.Position`、道路查询和幽灵数据保留原作坐标。表现层通过 `ToUnity` 反射 X；角色网格、法线、骨骼绑定及朝向同时转换，不能将规则位置直接赋给 Transform。沿路距离使用原作双精度，不能提前量化成 float，否则刚到捷径入口时的正负判断会改变；AI 的道具箱和障碍坐标缓冲仍按原作保存为 float32。刹车延迟、救援阶段使用原作双精度固定步时钟，避免单个规则步的边界偏移。

`Prefabs/Characters/` 保存八个原作骨骼 Prefab，`Data/CharacterMeshes/` 保存原生 Mesh，`Art/Textures/Characters/` 保存图集，`Art/Materials/Characters/` 使用 Demo 的原作 Paper Shader。Mesh `.asset` 按现有矩阵归入 Data，不改公共规范。每角色单个 SkinnedMeshRenderer，保留折痕、纸纹、动物和车型差异；比赛与菜单已接入原作骨链动画。

读取后由场景会话持有规则实例。未来输入适配只能提交赛车命令，页面只读取状态，不直接改变圈数。原作显示名不直接用于 Unity 页面，内部 ID 保持；已建立的中文名称见 `KagitKartCatalog`。

## 道具接入

比赛会话每步按原作顺序更新：输入 → AI → 赛车运动 → 互撞和尾流 → 道具 → 比赛排名和圈数 → 幽灵采样。`KagitKartItems.Advance` 读取当前步更新排名之前的名次，并接收独立比赛时间与倒计时状态。`Vehicle.Place`、`Progress`、`Finished` 由比赛规则维护，`Item` 等道具状态仅由本场道具实例维护。输入仍提交统一 `Controls`，道具实例自行记录使用键边沿；泡泡糖保留原版按住防御、松手投放语义。

`Boxes`、`Hazards`、`Projectiles` 的位置是源坐标，场景按同一 X 反射转换并使用前后位置插值。`Signaled` 提供道具获取、使用、投射物阶段、早晚预警和瞬时视觉事件，不在规则中生成模型或播放声音。暂停、失焦和设备切换清空输入，恢复后使用中性输入门闩；退出比赛调用 `Dispose`，清理道具引用、库存、轮盘和订阅。

## 计时记录

`KagitKartRecordStore` 复用 [公共本地存储](../../modules/local-data.md)，文件为 persistentDataPath/KagitKart/Records/<赛道>-<组别>-<角色>.json，路径集中在 Hotfix 的 `LocalDataKeys`。读取失败或完整记录校验失败统一返回空记录，不局部修补成绩或幽灵，不自动写默认值。保存先校验真实三圈快照，最好单圈独立更新，只有更快全程替换幽灵；写入成功后才返回候选。保存失败交给场景宿主提示，不提交内存中的保存成功状态。

幽灵分段保存按原作毫秒量化，完整快照校验允许半毫秒量化误差，同时拒绝明显越界分段；不能把不合法幽灵从记录中剥离后保留成绩。

## 本场模拟与 AI

### 三端输入

正式资产为 `Data/PlayerInput.asset`，比赛使用 Drive，全局快捷键使用 Global；键盘、标准手柄和触屏最终都输出 `KagitKartControls`。菜单确认和返回使用 Core 的 `UIMenuScope`，不新增菜单输入底座。操作提示绑定同一会话动作副本，手柄单轴复用公共会话的死区参数，方向键覆盖摇杆。

宿主每帧先 `CaptureFrame`，然后用缓存的读取委托调用 `Session.AdvanceInputFrame`，在每个固定步 `ReadControls(是否倒计时)`。零步帧不丢短按，追赶步不重复消费短按；按住泡泡糖与漂移保留持续状态。触控通过公共 `InputCommandButton.HoldChanged` 接入，转向由 Left / Right 两个保持命令交付，不再使用横向滑条。自动油门默认关闭，倒计时始终手动油门，主动刹车优先。

输入类别切换或更换手柄时清空旧触控与锁存命令；键盘和鼠标互切只更新设备提示，保留持续驾驶和已缓存短按。交付的触控命令按原作与当前物理命令合并，转向取绝对值较大者、油门和刹车取较大者、按钮取或。显式开启触控不伪造设备识别，鼠标也可操作保存的触控组件。隐藏触控区会释放保持按钮与转向值。`InputCleared` 由宿主连接本场命令和触控组件清理；失焦、后台、手柄断连同时调用 `ResetInput` 和公共暂停门闩。退出释放输入副本和设备订阅。

### 设置与操作说明

公共偏好由启动和直启的同一设置入口准备，赛车保留自动油门、触控布局及本地音量。普通输入参数和改键来自公共会话；操作提示绑定实际动作，键鼠使用 Kenney 键帽，说明文案在页面绑定时读取当前语言。隐藏公共说明提示不影响触控操作区。

主音量与音乐/音效分类增益在 DSP 各分类混音处与本地增益相乘，静音保留播放位置。全局减少动态和禁用相机抖动优先于赛车选项。规则推进、立体书和赛前/领奖表现使用公共玩法时长；UI、输入采样、帧率及 DSP 时钟保留未缩放语义，冻结期间暂停与退出仍可操作。API 与配置真源见[公共设置与表现](../../modules/settings-presentation.md)。

模式页和暂停页均可打开独立 `KagitKartSettingsView` / `KagitKartControlsView`，各自保存正式 Prefab 与生命周期和纸艺设计。专属设置复用公共 `LocalDataManager`，键为 `LocalDataKeys.KagitKartPreferences`；音乐默认 0.7、音效 0.9、自动油门和帧率默认关。保存成功才替换场景偏好，损坏记录回退默认而不覆盖存储，失败显示本地化反馈。公共显示与画质只在 Hub 设置，赛车专属设置不提供公共画质入口。

音乐/音效和静音交付现有音频宿主，自动油门仍允许主动刹车，触控显示独立于真实输入设备。帧率按真实 `Time.unscaledDeltaTime` 每半秒采样，独立于固定步比赛时钟。低动态显式开关覆盖纸卡、立体书、角色、纸景、赛前与冲线演出，并关闭镜头震动；切换赛前路线保留原有进度。`ReducedMotion=auto` 通过 Core.Runtime 的 `SystemMotionPreference` 在启动及修改该选项时读取：Windows 使用原生客户区动画开关，Android 使用系统动画尺度是否为零；查询失败使用正常动态，不修改系统偏好。

暂停页与设置页在叠加 Modal 的 `OnHide` 时保留场景来源，在销毁时解除。UIManager 关闭上层 Modal 后只重新显示下层实例，不再交付 SetData，故隐藏时清空 world 会使恢复后的按钮失效。操作说明读取本场动作副本及 override，设备图标沿用公共绑定提示，菜单确认和返回仍走公共作用域。

暂停、失焦、切换设备与退出时复用公共输入清理，防止保持动作残留；运行时不生成默认动作资产。

### 规则链路

`KagitKartSession` 使用 grid / ai / items 独立随机流生成原版网格，后续大奖赛按积分排名逆序出发。每帧接收赛车命令和公共玩法时长，遵循原作 120 Hz 链路；暂停使用公共 `LocalPauseState`，不直接写入全局 Time。场景宿主读取 `Karts`、`Race`、`Items` 和 `Interpolation` 更新表现。退出先解除暂停回调，再释放 AI 的道具预警订阅、道具对象和比赛事件。

静态路线直接由原作的松弛线路、弯道合并、跳台区域和多条件捷径探测生成；运行时车手按原作技能随机分配、性格和角色操控属性生成速度曲线。保留有机会失误的驾驶、尾流靠拢、抢道具箱、避泡泡糖、火箭捷径、三级漂移、跳台特技、按名次使用道具、剪刀预警躲避及卡住/逆行救援。原作仅供测试的参数覆写和调试日志不作为玩家功能接入。

## 四章场景表现

`Data/Worlds/*Dynamics.json` 保存四章原作动态数据，解析复用 Newtonsoft，保留可选对象的 null 语义。四章路牌使用中文图集，保留网格与资源 GUID；纸纤维的五份 grain 纹理按线性数值导入，采用非 sRGB、三线性与各向异性 8。

正式资源位于 `Prefabs/Tracks/`、`Data/Worlds/`、`Data/TrackMeshes/` 与 `Art/Textures/Tracks/`、`Art/Materials/Tracks/`。四章保持源层级、顶点色、多材质面组、UV、道路专属属性和实例色。网格、法线、绕序和实例矩阵统一反射 X，所有运行时资源均为原生 Unity 保存资产。

`Paper.shader` 转译原作道路、越野带、书页、加速贴纸、水波和纸洞的印刷算法；`PaperSky.shader` 保留七段天空印刷与纸纤维。纸洞使用 Unity 裁剪空间和 `SV_Depth`，不直接套用 WebGL 深度换算。后端编译、驾驶视觉和性能需按实际执行范围验证，不能由源码转译推断通过。

Paper / Items 的屏幕 halftone 将输出像素间距按实际渲染目标高度换算，坐标和 `aa` 共用 `dotPx`，避免降低 renderScale 后输出网点被放大。AA 仍由公共显示设置处理，主光阴影尺寸和纸纤维保留当前契约；参数核对、再导入 Shader 的必要转换及视觉边界见[画质维护](runbooks/source-migration.md#画质参数与平台转换)。

`SceneryWorld.Render` 由场景宿主每帧调用，显式传入比赛摄像机、赛道距离、表现时间、暂停后的 dt 和回头状态。实例矩阵属于各场世界，不修改共享数据或材质；场景静态部分使用保存的 MeshRenderer，纸景按原作沿程分批实例绘制。折起沿用 70–110 米触发、身后 60 米折回、回头保持、弹簧过冲和纸片颤动；跳台提前 125 米立起。天空材质由宿主绑定本场摄像机的 Skybox，不修改项目全局 RenderSettings。

## 场景与页面生命周期

`World` 等待 `DemoIslandEditorBootstrap.EnsureReadyAsync` 和 `GameSceneNavigator.WaitUntilStableAsync`，通过公共 Loader 加载原作配置、赛道、AI 路线和角色资产。比赛 Loader 每场独立释放，退出同时解除输入、暂停与镜头订阅；不在 MonoBehaviour 字段初始化中读取本地存储路径。大奖赛下一章使用现有积分状态，重赛调用原作回滚；计时赛读取最佳幽灵，并在实际完赛后按当前完整记录契约保存。

HUD、暂停和结算各自持有 View、Prefab 和生成绑定。HUD 不创建全局 Canvas/EventSystem，触控按钮复用 `InputCommandButton`，左右转向按钮从本 Demo Foundation 嵌套并通过正式 UIBind 生成引用；提示绑定实际动作并复用 `InputBindingPrompt` 和项目图标目录。驾驶使用公共 `MenuInputScope.Interaction` 关闭菜单 Submit，暂停和结算通过 `UIMenuScope` 接管焦点，避免漂移 Space 同时确认 HUD 按钮。

页面显示前由SetData交付场景来源；嵌套Modal遮挡时保留来源，关闭或销毁时解除订阅与引用，退出前等待关闭本场具体 View，不在 `OnDestroy` 发起按类型异步关闭。暂停同时冻结规则与纸景、镜头的表现时间，不改变全局 Time 设置。场景切换回滚时恢复本场页面并保留明确继续门闩。

入口使用原作选赛流程：封面、模式、纸张克重、角色、章节分别使用独立 Page View、正式 Prefab 和 UIBind 绑定。选择通过 `KagitKartAction` 提交，World 保存同一份选择；角色和章节焦点按原作即时更新选择与 3D 预览，确认才推进页面或比赛。大奖赛确认车手直接加载首章，单场与计时赛确认章节后开赛。返回上一步保留选择，返回模式时释放本场资源；重赛或选赛加载失败恢复可操作菜单。

菜单复用公共 `UIMenuScope` 与 Core EventSystem，卡片只桥接原作焦点/鼠标预览事件，不接管确认或返回输入。静止鼠标在页面出现时产生的 Enter 不覆盖键盘/手柄焦点；实际移动鼠标可预览，触屏点击可确认。纸张克重保留一层薄纸、三层横线笔记本纸和八层纸板；五项属性按原作四舍五入为五格。

`KagitKartBookStage` 接入原作桌面立体书：封面开合、书页呼吸、曲面翻页、纸景弹簧铰链、车手切换与转盘、封面/选择镜头过渡。每实例克隆动态 Mesh，退出释放，不写共享资源。中文四章印刷与封面保存于 `Prefabs/BookStage.prefab`；纸卡布局露出舞台，`KagitKartMenuPaperMotion` 保留入场曲线、焦点抬升与书签。

`KagitKartCharacterVisual` 直接迁移 `characters/animate.js`，菜单与比赛共用八角色骨链、轮转、方向盘、漂移、打转、特技、救援、幽灵和情绪表现；宿主调用 `Bind` / `Advance`，暂停传零。材质使用原作 Paper Shader，实例属性块保存环境、发光与无敌闪烁。

`KagitKartItemVisuals` 使用保存的 11 类源网格和专用 Shader，按源事件表现箱体折叠、钢笔点火抛落、口香糖气泡/拖线、飞机轨迹、墨水、剪刀和透明纸壳。

`KagitKartRaceCinematics` 将赛前时间与比赛模拟分开，六秒镜头结束或键盘/手柄/触屏跳过后才倒计时。中文章节卡复用统一副标题。暂停不推进演出；冲线环绕沿用原作构图，为结算表让出玩家位置。

音频接入 `KagitKartAudio` / `AudioBank`，包含 97 个原作 Clip、连续加速配方、实时引擎和两级总线 DSP；引擎使用原作粉噪声与实时合成。音乐离线保留滤波前采样，由运行时处理一次正常/暂停低通。

`KagitKartRaceFx` / `KagitKartRescueCrane` 接入原作纸屑、尘迹、速度线、落地与碰撞纸卡、漂移和纸鹤救援。

`KagitKartPodiumStage` 接入原作三层纸台、前三车手落台、失落玩家、纸屑和推进镜头；最后一场先显示本场成绩和独立总积分榜，再由积分榜按钮进入颁奖，结束后显示独立 `KagitKartPodiumView`，可返回封面。

源章节 updater 在导出时先推进零时刻，再保存真实云、吊线和风车附属部件姿态；运行时云和吊线继续朝向摄像机，折起触发使用摄像机对应赛道距离。小地图显式保存 CanvasRenderer，防止只保存 Graphic 脚本导致没有实际 UI 网格。

## 首次教程与驾驶提示

`KagitKartHints` 直接迁移 `hints.js`：倒计时3打开首次四行卡，倒计时不扣七秒，Running累计七秒后写既有设置的 `SeenTutorial`；暂停传零表现时间，重赛与离场取消卡片且不记完成。Corner/Tier/Ramp/Item/Portrait提示沿用原阈值、队列与3.8/4.5秒时长，真正显示时才写 `KagitKart.UiHints`，缺失或损坏使用空历史，写入失败只警告并保留本场一次性状态。

`KagitKartHintsView` 使用 Foreground Widget 正式 Prefab、UIBind、中文字体和真实绑定提示；没有按钮、菜单作用域或射线阻挡。纸卡沿底边弹起复用 `KagitKartMenuPaperMotion`，教程0.62秒、情境卡0.5秒，低动态直接显示终态。Widget跨菜单/重赛保留，离开Demo精确关闭本实例，切场失败恢复。原网页软件渲染器的浏览器硬件加速说明没有Unity对应条件，因此不显示该浏览器专属说明。

## HUD 屏幕墨水

`KagitKartHudInk` 直接迁移源三团曲线、触须、六条滴落与最后35%的整层渐隐，表现随机独立于比赛。墨斑先在活跃期间的单张局部RT按源sRGB数值合成，UGUI仅应用一次整层透明度，避免重叠墨斑渐隐变黑。`LastInkDuration`保存本次事件时长，规则 `InkTime` 仍取max；连续中墨重新生成全部形状。

HUD使用 Foreground Widget，先创建的教程在其下方、Pop暂停在其上方。墨水子Canvas只改变自身排序，位于Foreground之前，因此遮挡3D但不遮挡教程、HUD、触控或暂停；不改公共UI层定义。Canvas尺寸按根Canvas缩放取实际像素，满屏不受safe-area内缩影响。原作CSS滴落按墙钟继续，暂停只冻结规则剩余时间及渐隐；低动态取消滴落、显示完整形状。结束/隐藏释放RT，销毁释放Mesh与自有材质，换场解除玩家订阅。

## 原生计时赛与幽灵回放

幽灵遵循原 `ghost.js` 的显隐：创建时隐藏，非倒计时阶段（包含赛前电影）按比赛时间采样，倒计时保留上一位置与可见状态；越过最后有效采样后隐藏。表现始终独立于车手规则数组，不参加排名与碰撞。

## 原作比赛 HUD

`KagitKartItemSlot`显示真实库存的八张原作图标、九格轮盘（八种加第一格）、数量与持有纸框；道具卡也使用公共`InputCommandButton`，只在触屏驾驶时交付道具保持。`KagitKartMinimap`复用固定源`buildMap`直接导出的四章底图，实时车手标记独立逐帧绘制，保留源SVG坐标方向、箭头角度与小数舍入；底图不参与标记纹理采样。

`KagitKartHudNotice`订阅本场比赛、道具与玩家事件，显示倒计时、起步评级、最终圈、逆行、冲线及等待、幽灵分段差、换圈纸页和来袭预警。比赛时间与结算成绩按原百分秒向下截取；单人隐藏`/1`，多人显示实际车手总数。JS通知寿命与轮盘使用暂停感知秒数；CSS折卡、盖章、摇摆与离场继续墙钟。菜单、规则与公共Canvas定义不因此改变；离场取消三处订阅并销毁局部通知。

## 菜单退出与长驻帧率

道具卡按源全局 em、安全区与触屏/短屏窗口布局，名字按内容宽度淡入；持有边框保留原 CSS 在低动态下继续的选择器行为。

`KagitKartMenuPaperTransition`只将已有纸卡动效接入公共退出等待链：四个选赛页与从菜单打开的设置/操作表单按源0.16秒ease-in折下，减少动态直接结束；暂停子页按源`panels.cleanup`直接关闭。入口仍由`SetData.Play`弹起，回滚恢复完整纸卡。项目FIFO导航先退出后进入，与原DOM同时追加新页的组织不同；不因此新增平行UI导航。缓动共用`KagitKartHudMotion`。

FPS复用长驻`KagitKartHintsView`局部区域，唯一采样仍由World每0.5秒未缩放时间提供，刚开启先空白，关闭重置。FPS子Canvas在`OnShow`按Tip的层和排序+1恢复，不遮挡输入；HUD不再重复保存FPS文本。

倒计时字形改为直接源导出的三张Sprite，`KagitKartCountdownCard`按完整em边界变形四顶点，替代项目字体数字；中文Go保留TMP投影。

HUD不保留原作不存在的常驻底部操作条，避免遮挡来袭预警；教程、操作说明和情境提示仍由真实绑定提供。

## 暂停返回与子页快捷键

设置页的两态/三态选项由 `KagitKartSettingsView.BindChoice → ChangeSetting → SaveSetting` 保存真实偏好，Flux 刷新后逐组应用唯一 Selected，其余为 Normal；保存失败仍显示原值和反馈。选择外观由 `KagitKartChoiceButton` 风格 Variant 维护，Normal 必须关闭旧 SelectionMark 并恢复纸面/文字，业务 Selected 使用完整红纸卡；操作说明的三个设备页签复用同一状态契约。具体样式与确认图见[美术维护](architecture/art-direction.md#可读性与换风格)。

暂停页的“返回菜单”复用Demo标题路由，并清除杯赛；“返回大厅”仍是独立项目入口。GP先显示杯赛进度丢失提示，第二次选同一行才退出；移焦到其它行或打开子页取消确认，复用已有纸卡Focused事件，不新增通用确认弹窗。计时赛/普通比赛一次选择返回标题。

World在Demo设置/操作子页仍处理P/Start继续，先关闭子页再恢复比赛；Esc/B继续交给公共菜单只退一层。TT的R仅在非赛前电影的倒计时、Running、Finishing有效，仍沿已有重赛/资源关闭链；不扩大为任意页面热键。公共画面设置仅在 Hub 打开，不把旧赛车公共 Modal 路由作为本场契约。

## 本场成绩与总积分榜

主按钮复用单个公共 `UIState` 的五态，White Sprite 保持源墨底浅字；焦点抬纸、红内沿与硬影，Pressed 中性压暗、Disabled 淡出，移焦复位。领奖父卡不再重复抬升。

独立 `KagitKartResultsView` / `KagitKartStandingsView` 使用正式 Prefab、UIBind 和共享行子Prefab。普通比赛显示真实八名车手的名次、头像、全程与最佳圈；GP补本场积分与累积积分，先显示本场成绩，再由按钮打开总积分榜，最后进入下一章或颁奖。计时赛显示三圈与全程、当场最佳标签和已保存全程的新纪录印记。返回菜单走标题路由，Results源FocusList无onBack，因此不把Esc/B作为退出比赛。

`KagitKartWorld.Results`只管理这段导航和显示名次历史，比赛积分仍归GrandPrix。积分榜升降按源开赛前积分稳定排序，同分沿首场插入顺序；当前总榜依次按真实累计积分、最近一场名次、总时间排序。`KagitKartResultRow/Shape`表现原纸色玩家高亮、斜积分条、箭头及延迟入场，不保存另一份赛果。View由World交付表现帧与横屏安全区；公共Core/UIManager没有修改。

进入Done按源清空并隐藏HUD通知/屏幕墨水。结束事件正在分发时，即使解除订阅，已复制的旧回调仍可能被调用；Notice三个入口仅在本场绑定有效时处理，避免结算时空引用。

## 救援与音频事件

真实落空由车辆重力与源 TrackQuery 触发纸鹤提起、搬运、放下、落地四阶段；返回实际安全地面后应用保护。原草原定义围栏，不能将其当作不存在的悬崖；安全点和等待按比赛模拟判断，不用固定墙钟代替。退出释放纸鹤、特效引用与音频载体。

倒计时0.9秒退出赛前曲，每车撞墙0.2秒门闩后再进入共享 world 0.02秒桶；tier、threat、rouletteTick按源配方使用共享桶。原全局P/R/M不追加UI click，暂停P继续与UI按钮保留click。听感维护见 [音频手册](runbooks/audio.md)。

`SceneryWorld` 的真实目标 false→true 发布源最大尺寸、0.18秒波次与坡台位置，World订阅并在StopRound退订，Audio汇聚rustle。四World资产保存源1401项size（766/227/363/45），保持来源版本、顺序、位置、尺度和GUID；不能由接线正确推断长期性能通过。

静止纸景和坡台在弹簧到达目标且速度归零后跳过姿态重算，目标变化或 flutter 立即唤醒。实例局部姿态变化时标记对应批次，根变换变化时重算所有世界矩阵；章节动态、摄像机相关更新、材质时间与逐帧绘制继续执行。不能通过冻结动态纸景或减少可见更新频率取得性能收益。

输入直接使用完整动作名常量，保持公共输入绑定和短按锁存。HUD 数字复用 TMP 格式化，数量和静态文本只在值变化时提交，时间仍逐帧按百分秒向下截取；Editor 的 TMP Inspector 字符串与 Player 热路径分配分别计量。音效缓存、加速双缓冲和音频线程参数交接见 [音频维护](runbooks/audio.md)。

## 验证与维护边界

- 当前玩法、源算法、坐标转换、资源地址和生命周期以上述正文与 [源码维护](runbooks/source-migration.md) 为准。存档只遵守“计时记录”中的当前完整记录契约，不恢复旧部分修补行为。
- 定向验收覆盖与尚未验证范围见 [验收对照](runbooks/acceptance.md)；设备模拟、合成轨迹、自动驾驶、GPU样本和人工驾驶分别记录，不相互替代。
- 2026-10-10/11阶段数量、job、早期结构与“未完成”原文保存在 [验证历史](../../agent/archive/kagit-validation-history.md)；旧结果不能证明本次治理后的代码已重新验证。
- 长圈流程复用 [现有两项长期采样入口](runbooks/editor-performance.md)，CPU/GC 定因与比较使用 [正式性能工具](../../modules/performance-tools.md)，历史数字独立归档。具体慢帧需实际定因，不把窗口均值当整场或Player性能保证。
- 独立Player/IL2CPP、Android、真实手柄/触屏、全部屏幕比例及人工听感以各自实际记录为准，不从Editor通过推断。
