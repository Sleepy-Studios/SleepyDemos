# 维护小小搬豆工视觉

可编辑源稿现位于独立美术目录，本文中的源路径相对此目录。恢复与导出步骤见 [维护独立美术源稿](../../../runbooks/maintain-art-sources.md)。

## 入口与资源

从 AppEntrance 运行，经 Hub 进入 Demo。界面直接编辑 `Assets/LoadResources/Demos/block_porters/Prefabs/UI/BlockPortersHudView.prefab`；场景、美术、网格均在同 Demo 目录中。原图、概念图和本轮生成提示词在 `Assets/Settings/BlockPorters/ArtSource/`，不进入 YooAsset 运行资源。

- `Art/UI/SiliconePanel.png`：透明底软胶面板，Sprite Editor 保存一个裁剪 Sprite 与九宫格边框。Image 使用 Sliced，`pixelsPerUnitMultiplier` 控制边缘厚度。
- `Art/UI/ToyIcons.png`：设置、声音、重开、返回大厅四个 Sprite；按 Alpha 范围切片，原图中的四个区域各保留透明间距。导入上限 1024、无 Mipmap。
- `Art/UI/ContactShadow.png`：程序生成的柔和投影渐变，供 3D Unlit 透明材质使用，Importer 为 Default。
- `Art/UI/TileSocket.png`、`TileFace.png`：当前统一凹槽底座与可着色凸起内容面。`Prefabs/UI/TileBase.prefab` 为所有队伍与任务槽的嵌套基座；旧 Reference 素材仅保留制作历史，不再驱动方格。
- `Art/UI/ProgressTrough.png`：专用浅米色凹槽胶囊，Sprite Editor 裁剪透明边距，九宫格伸缩。`ProgressCapsule.png` 是代码生成的圆角遮罩，不带装饰或文字。
- `Art/UI/Backgrounds/ToyTable.png`、`Garden.png`、`Seaside.png`、`StarrySky.png`：四套主题正式背景。均为 600×1080，使用同一玩具桌棋盘框像素合成。背景不包含坑口、方块或 UI。世界相机后景 Quad 使用 ToyTable 为场景回退，其余主题按地址加载。
- `Art/UI/AdUnlockIcon.png`：透明视频／锁图标，左右模拟广告入口共用；解锁后隐藏。文字由 TMP 绘制。
- `Data/ChamferBlock.asset`：共享倒角方块网格。坑口、波纹、底面与内壁网格也保存在 Data；不要在方块上增加物理组件。
- `Art/Materials/TrayCream.mat`、`TraySurface.mat`、`Tabletop.mat`：静态场景配色。`PorterColor0.mat` 是当前关卡动态材质模板，色表由 Controller 赋值。

如果在编辑器同时打开 Hub 和 Demo，画面可能比正式运行更亮。正式入口会暂时停用其他场景的已启用灯光，离场恢复。以 Hub 导航后的运行画面为准；不要为了修正叠加灯光而更改关卡色表。

## 修改界面

1. 保持 PortraitContent 为 600×1080 的内容根，安全区缩放由 HUD 管理。修改顶部、任务位和队伍卡片时同时检查场景相机，防止遮挡坑口或搬运路线。
2. 字体与人数使用 TMP；色号、人数、进度和玩法颜色不能烘焙进生成图片。队伍卡片取消立方块图标，使用大人数及小色号。内部内容面读取原始关卡牌色，按亮度自动选择深／浅文字；所有按钮关闭 ColorTint，禁用及预览不能改灰牌色。
3. 五列各一个当前 Button 和三个 Preview Image，共五个按钮、十五个预览。所有外框与热区均为 80×80，内边距 8、内容面 64×64、圆角约 12、厚度约 6、影子下移约 4；横向间距 16、纵向间距 8。首排中心 y=752，预览中心 y=840／928／1016，列中心 x=108／204／300／396／492（设计图从顶部计）。字体人数 32 粗体、色号与状态 11。预览与装饰关闭 Raycast Target。递补由 HUD View 统一推进 0.2 秒，整列向上移动，新末排在结束后补入，同列暂时禁点；暂停、重开、切关和解绑恢复固定布局并清理输入锁。
4. 设置入口为 Pause；Sound、Restart、Exit 位于 SettingsCard。SettingsContinue/SettingsClose 关闭设置，保留打开前的暂停状态。设置与结算遮罩必须可拦截射线。
5. 新增或改变绑定时使用 UIBind；自定义 Module 输出目录选择 `Assets/Scripts/Hotfix/Demos/BlockPorters/UI`，工具会追加 View 名和 View 目录，不要再次手工追加。生成文件不得手改。
   Image 的 Source Image 必须指向导入后的 Sprite 子资产，不能指向原始 Texture2D；在 Inspector 中从展开的图片资产选择 Sprite。设置与结果使用独立 View 和 Prefab，通过 Tip/Modal 显示在 HUD 之上；各自遮罩覆盖宿主并拦截射线，不依赖 HUD 内的节点排序。
6. 不新建 Demo 重建工具；常规调整直接保存 Prefab、场景和资产。小人 CarryAnchor、方块根 Renderer、坑口交付坐标保持不变。

进度节点为 `ProgressRoot/ProgressTrack/ProgressClip/ProgressFill`，数量文字 `Progress` 同属 ProgressRoot。背景压缩为 328×20；Clip 使用圆角 Sprite 和 Mask，横向内边距 7、纵向内边距 5，Fill 撑满 Clip 并从左到右填充。不能再给 Fill 设置独立屏幕坐标或旧的 520 宽度。已入坑数量驱动进度，正常交付平滑增长，通关立即满格，新会话立即归零；不会把抬起数量当作交付数量。

`BlockPortersScreenLayout` 同时约束 HUD 与相机：图案中心 (300,304)、最大宽度 416，背景框约 (24,104,552,400)。世界投影每设计单位为 65 像素／世界单位。背景贴图用无光照材质位于玩法后方，私有 TabletopBackground Shader 延展长屏空余区域；不要新增覆盖立体玩法的不透明 HUD 背景。坑口保留原世界中心和交付点，整体视觉缩为旧尺寸约 45%，波纹与粒子归入坑口缩放层级。

深处坑壁与坑底使用 Demo 私有 `Art/Shaders/PitInterior.shader`，沿正交视线投回坑沿平面裁剪孔外片元；后景图片不承担原三维地面的遮挡。开口半径由坑沿内圈确定，`pitOpening` 引用真实 PitRim 平面（当前根内偏移 y=.04，根 y 缩放 .45），不再使用逻辑交付点的 y=0。Controller 在相机适配时用 MaterialPropertyBlock 同步孔心、视线及向孔内半设计像素的半径余量。内壁和底面为柔和暖灰棕渐变。修改坑沿网格时需同步 `pitApertureRadius`，不能只缩坑沿或删除坑底。此 Shader 只用于坑内材质，不是全屏后处理。

免费槽固定为 TaskSlot0–4，中心 y=652；左右 TaskSlot5/6 位于 (80,554)／(520,554)。所有槽均为 80×80，共用同一个 TileSocket，不再切换广告专用按钮底座；广告仅增加图标和文案。它们始终显示，锁定时为模拟广告按钮，开放后原地变为任务槽。规则状态按物理槽编号绑定，不能用 `i < Capacity` 判断可见／开放：只开放右侧时 Capacity=6，实际额外槽是编号 6。

世界相机当前斜俯视角为 55°，场景自身的 Sun 开启 Soft 阴影。是否实际使用软阴影由当前 URP Asset 决定：PC 配置支持，现有 Mobile 配置不支持软阴影，未在本次修改全局管线。调整相机或坑沿尺寸后需重新检查 UI 遮挡及小人入坑演出。

## 规格与主题维护

`Data/UiStyle.asset` 是布局与字号的唯一规格；HUD View 在 OnGameObjectInitialize 时读取格子尺寸、内边距、排距和字体，Controller 读取相同投影中心与比例。不要单独修改某一格的 RectTransform 来改变尺寸。共用底座或内容面材质感直接编辑 TileBase／两个 Sprite；改变绑定使用 UIBind。

`Data/ThemeCatalog.asset` 的主题包含唯一 ID、名称和不带扩展名的资源地址。添加主题只添加 PNG 及此目录项，不修改关卡、控制器或场景常驻引用。默认 ToyTable 是唯一场景直接引用的大图；主题目录使用字符串地址，不把四张图同时挂入场景。

首次进入、新关开始及离开 Hub 后重新进入随机选择，排除上次成功应用的主题。重开、暂停、广告解锁保持当前主题；随机源与关卡生成种子分离。`BlockPortersThemeLoader` 每请求创建一个 IResourceLoader；成功应用后释放旧图所属加载器。失败保留已可用背景，旧请求完成后自行释放，不能覆盖新的请求；退出立即释放已应用资源，在途请求自然完成再释放，不 Dispose 正在 await 的 YooAsset 句柄。

背景和布局 UV 共用一个 MaterialPropertyBlock，切换只改 `_BaseMap`，保留 `_BaseMap_ST`，不写共享材质。长屏通过私有 `TabletopBackground.shader` 平滑过渡至边缘中部底色，避免将角落装饰拉成竖条；同一主题的棋盘框不得纵向拉长。新原稿保存在 ArtSource，正式图在 Unity 内按模板合成；所有背景的棋盘框像素相同。

## 验证

通过 Unity Test Runner 运行 `Tests.Demo.BlockPortersAssetTests`、`Tests.Demo.BlockPortersThemeTests` 与 `Tests.Demo.BlockPortersFlowTests`。后者回放全部八关实际小人，并包含设置恢复暂停、射线阻挡、十二色、正式关卡参考解、5/7 位、32×32 和 56 人在途。

检查 `Library/BlockPorters/Evidence/` 中的 FiveColumnsInitial、FiveColumnsCarrying、FiveColumnsRightUnlocked、FiveColumnsBothUnlocked、FiveColumnsTall、Progress0、Progress25、Progress50、Progress100、SettingsTall、TwelveColors、Stress56Transport 截图。解锁/布局用例为 `FiveColumnsAndIndependentExtraSlotsRespectRewardsAndRestart`，进度/递补用例为 `ProgressAndQueueAdvanceStayAlignedAcrossPauseAndRestart`。不同分辨率下检查按钮热区、数字、色号、九宫格边缘与路线遮挡。截图由实际运行生成，不以概念图代替运行结果；Editor 性能不是手机性能。

新增主题验收截图为 Theme_toy_table、Theme_garden、Theme_seaside、Theme_starry_sky 和 Theme_Tall。ThemeTests 覆盖随机复现、排除上次、加载失败、乱序完成、退出隔离和句柄释放；FlowTests 通过实际 YooAsset 加载、检查 MPB 与重开保持，再返回 Hub 重新进入。

Demo 补字字体为 `Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CNBlockPorters.asset`，仅此 Demo 引用。新增标题用字不足时，用现有 TMP Font Builder 和 `ArtSource/BlockPortersCharacters.txt` 更新该字体：源 HarmonyOS_CN.ttf、后缀 BlockPorters、CN 目录、采样 54、padding 8、atlas 512、保留已有 fallback、最优打包、外部 atlas。不要重建公共默认字体；更新保留字体、材质与 atlas 的 GUID。
