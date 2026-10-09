# 双平台运行与依赖

## 前置依赖

1. Unity 6000.3.15f1 与当前项目子模块。
2. Windows IL2CPP 支持和构建工具。
3. 同一 Editor 的 Android Build Support、SDK / NDK Tools 与 OpenJDK。
4. 当前单机验收需要 Windows Player、Android 真机与 Xbox 手柄；设备证据单独记录。

联网不属于当前单机验收，选型待定；旧 Photon 配置不作为开发前置条件。

## S1保存资源维护（开发中）

当前工作分支的Hub赌场入口使用已保存的`Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity`和`Prefabs/UI/JinxCasinoImmersionHudView.prefab`。直接维护场景与Prefab中的布局、引用、按钮导航和交互组件；保留GUID、UIBind 绑定及人工调整；交互/表现/UI源码分别在Interaction/Presentation/UI，Controller仅保留场景职责，不重新运行临时生成/装配Builder。旧Main和通用HUD已经删除；新入口与完整流程仍需分别记录实际验证。

玩法范围维护独立`Data/ImmersionSettings.asset`，当前为一区、三机台且暂时关闭事件；旧AdventureSettings和SessionSettings已删除。后续接入事件设施时同步本资产与保存资源测试。本地胶囊射线层、操作目标和铭牌行高直接维护保存场景组件，核查序列化引用及`JinxCasinoImmersionSceneTests`结果。

样板初始开放Slots、Blackjack、CooperativeLevers；`InitiallyAvailableGames`仍受`AllowedGames`限制，只读取当前版本配置。现有自动化覆盖保存引用，不代替三输入实玩、画面或S1用户验收。

实体补给柜台已保存在Immersion场景中，维护三件商品、报价牌、购买与库存目标、商品缩放及聚焦挂点。新局商品范围由ImmersionSettings限制为双人扳手、重抽牌、止损券，当前版本存档保留自身配置。柜台复用唯一游戏相机，不创建资金或库存服务。

非模态教学提示维护于 JinxCasinoImmersionHudView；教学选择与确认维护于 JinxCasinoTutorialView，暂停入口维护于 JinxCasinoPauseView。每个页面均有独立 Presenter、UIBind View 和同名 Prefab，生成代码位于 UI/<View名>/View；使用既有 UIBind 更新绑定，不手改生成字段。

开发样板从主菜单选择“互动教学”，按实际动作依次体验水果机、二十一点、柜台扳手和协作拉杆；普通练习入口保持自由游玩。教学最后离开桌面再选择完成，稍后也可从暂停菜单回到教学结果。此练习不推进标准冒险区域，标准局阶段推进和结局仍需单独实玩验证。

主菜单“继续存档”维护于 JinxCasinoMainMenuView，暂停“保存旅程/读取存档”维护于 JinxCasinoPauseView，三槽与二次确认维护于 JinxCasinoSaveView；保留四按钮布局、有效控件导航及独立确认引用。游戏内保存后按选定槽自动保存；未主动选择槽的新局仍不写入任意旧槽。

键鼠、触屏、Xbox通用路由使用`Core.Runtime.Inputs`的GameplayInputRouter/Contracts、MenuInputScope、LocalPauseState、TouchInputPad，维护原则见[玩法输入模块](../../../modules/gameplay-input.md)。Demo仍保存`Data/JinxCasinoImmersion.inputactions`及玩法命令映射；编辑输入资产时保留Map/动作ID和人工键位，不另建EventSystem或菜单提交链。

## 构建约束

Windows / Android 分别生成对应 HybridCLR 元数据、Hotfix DLL 和 YooAsset 内置包，平台输出隔离。Android 不得使用 StandaloneWindows64 的元数据快照，也不得依赖本机 Mock Server。

Build Settings 继续只保留 AppEntrance。项目已打开时不另起 BatchMode，不用 dotnet build / msbuild 构建 Unity 工程。

## 当前S1单机构建

使用当前Editor内的`Tools/SleepyDemos/整蛊赌场/S1离线Player构建`窗口，或直接选择：

|菜单（同一路径下）|用途|
|---|---|
|构建Windows S1独立样板|Windows x64 IL2CPP Player及ZIP|
|构建Android S1独立样板|Android 8.0 / API 26起、ARM64 APK|
|重打包最近Windows S1分享包|为已完成S1 Player重新生成过滤调试目录的ZIP|

P0/P4入口、阶段参数及旧场景/HUD选择已删除。Start(BuildTarget)与RepackageWindowsPlayer(string)只服务当前S1。输出独立保存于Builds/JinxCasino/S1/<平台>/<构建版本>/，版本0.5.0、StartupScene=JinxCasino。独立包直达赌场主菜单，游戏内返回回到此菜单，提供退出；Editor开发入口仍从Hub进入。

必须先完成脚本编译，所有打开场景已保存；工具不代为保存或丢弃修改。当前Editor内串行执行，SessionState跨平台Domain Reload继续；两端不能同时构建。联网SDK/App ID不作为单机构建条件。

Windows分享使用Playable ZIP，DoNotShip开发排障目录不分发。Android只构建ARM64、横屏左右旋转，沿用com.sleepystudio.jinxcasino；安装可能替换历史包。Windows Player排除Streamline原生插件。平台设置、签名和原生插件配置由事务恢复。

当前仍复用DefaultPackage的资源范围，可能携带其他Demo资源；包体裁剪和双端实玩仍待验。构建成功不代表S1视觉/体验通过，实际记录见immersion-progress。历史P0/P4包只用于追溯，旧流程恢复须使用对应Git基线。

当前可分享包的运行源码基线为`c622d8552c1b3f0cc5c880de26476362e9fdf839`：Windows位于`Builds/JinxCasino/S1/StandaloneWindows64/20261002215303-6094bc1d/JinxCasinoS1-Offline-Windows-Playable.zip`，Android位于`Builds/JinxCasino/S1/Android/20261002220516-a33ce586/Player/JinxCasinoS1-Offline.apk`。二者包含最新输入及窄横屏修复，仍使用旧样板美术；Windows实际窗口已验证启动和教学入口，但暂停/完整局未通过本次操作验收，Android仅完成构建与签名检查。大小、哈希和证据见[实施记录](../immersion-progress.md)，不能作为正式S1交付。

平台 DLL、首包、构建场景配置隔离；不修改默认 HotfixConfig 或 Build Settings。构建中处理器只改 Player 的 AppEntrance 内存场景配置。任务结束恢复被覆盖的生成文件、资源首包、Collector 内存与 Editor/Player 设置，备份保留于 `Library/JinxCasinoBuild`。 Windows缓存句柄可能占用刚构建的资源；恢复覆盖前及重新导入后调用[AssetDatabase.ReleaseCachedFileHandles](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.ReleaseCachedFileHandles.html)。恢复失败时使用`Tools/SleepyDemos/整蛊赌场/重试构建配置恢复`，只重试原事务，不重新构建或丢弃产物。已成功构建的事务不再误报为用户取消；原构建失败的错误仍保留。若恢复失败，停止后续步骤并保留恢复信息；不要继续另起构建或删除备份。

URP 构建预处理会保存目标平台的着色器预过滤配置，并可能裁剪 GlobalSettings 的运行时设置列表。事务只备份目标平台启用质量级别所使用的 URP 资产与注册的 URP GlobalSettings，保留其内存 JSON、dirty 状态和原始 `.asset` / `.meta` 字节；恢复时重载对象，并在原平台恢复后再次校验原字节。备份仅在本机 `Library` 中，不进入 APK 或分享 ZIP。

Windows构建会校验本机System32的chcp.com签名和哈希，再临时提供给Bee的受限PATH编译进程，事务结束只清理自己创建且哈希一致的副本。校验使用系统Windows PowerShell及其安全模块，PSModulePath仅在校验子进程内限定为系统模块目录；从PowerShell7启动Editor也不依赖其模块自动加载。不修改用户cmd AutoRun、全局PATH或模块路径。

三台独立规则夹板、正文引用与聚焦挂点维护于保存场景及对应机台组件，按S1Layout同步源与运行合同。调整后实际进入三台机台，检查不同屏幕比例下规则文字、操作物件和结果是否完整可读、有无遮挡；不重新添加已清理的固定布局快照测试。


入口两侧验票口、离场口的文字/操作件高度及前后关系维护于保存场景。标准结局卡及保存/返回控件维护于 JinxCasinoEndingView，保持与三槽确认卡独立的引用和取消路径。

标准样板目标达成后按提示前往验票口，再到离场口领取票券；未达标可在离场口两次交互确认撤离。若超时仍有已投入牌局，先回原桌完成；暂停不会耗尽撤离确认窗口。结局可保存在三槽中，再明确返回Hub。此流程的Editor真实输入证据记录于immersion-progress，不替代Player或真机实玩。

当前旅程在Application.persistentDataPath/JinxCasino/save-1.json至save-3.json，成长在JinxCasino/Profile/profile.json。只接受冒险版本4，旧目录不读取、旧快照不迁移；不要搬旧档进新目录或手改版本号/机台ID。当前有效数据也须匹配样板实际开放内容，拒绝恢复时保留当前旅程和文件；四区/无尽等恢复等待对应场景接入。不读取或创建.bak；损坏槽按空槽、损坏成长档案按新用户处理。

JinxCasinoSettingsView 是独立设置页，可从主菜单或暂停菜单打开，分为键鼠/触控、手柄和声音。修改立即预览，保存才持久化；B/Esc/Menu或返回撤销未保存改动并回原菜单。恢复默认也是预览，仍须保存。手柄上下选控件、左右调滑条；真实设备的震动与Android后台恢复仍需单独验证。

当前偏好键由LocalDataKeys.CasinoPreferences集中定义，值为JinxCasino.LocalPreferences。只读取完整当前记录；缺失或损坏回默认，预览与取消不写盘，明确保存才替换记录。公共输入设置仍由Core消费参数和处理设备操作，赌场只保存本机偏好。


## 页面生命周期维护

页面资源统一位于 `Prefabs/UI/`。HUD 仅承担场地信息、输入提示与触控；JinxCasinoHandler 串行切换一个当前窗口。存档及教学的跨窗口待确认状态由各自 WindowState 保存，Presenter 不持有其它页面控件。

数据经 `ShowAsync<View, JinxCasinoController>` 在显示前交付，View 在 OnShow 绑定、OnHide/OnDestroy 解除。所有可导航控件配置公共 UICancelRelay，将 Cancel 转交给页面根；不新增 EventSystem 或重复输入作用域。关闭设置撤销未保存预览，关闭窗口恢复来源控件焦点，返回 Hub 先关闭当前窗口再释放 HUD 和场景。

修改静态页面直接编辑其 Prefab，维持已有字体与配色；按钮使用公共 UIStateInteraction 的独立反馈层，设置分页使用 UITab。正式界面不依赖一次性拆分工具。
