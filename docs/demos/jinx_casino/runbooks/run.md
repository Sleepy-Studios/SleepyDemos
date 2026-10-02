# 双平台运行与依赖

## 前置依赖

1. Unity 6000.3.15f1 与当前项目子模块。
2. Windows IL2CPP 支持和构建工具。
3. 同一 Editor 的 Android Build Support、SDK / NDK Tools 与 OpenJDK。
4. 当前单机验收需要 Windows Player、Android 真机与 Xbox 手柄；设备证据单独记录。

联网不属于当前单机验收，选型待定；旧 Photon 配置不作为开发前置条件。

## S1保存资源维护（开发中）

当前工作分支的Hub赌场入口使用已保存的`Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity`和`Prefabs/UI/JinxCasinoImmersionHudView.prefab`。直接维护场景与Prefab中的布局、引用、按钮导航和交互组件；保留GUID、MVC绑定及人工调整，不重新运行临时生成/装配Builder。旧Main场景保留用于恢复原型；新入口与完整流程仍需分别记录实际验证。

玩法范围维护独立`Data/ImmersionSettings.asset`，当前为一区、三机台且暂时关闭事件；不修改旧AdventureSettings。后续接入事件设施时同步本资产与保存资源测试。本地胶囊射线层、操作目标和铭牌行高直接维护保存场景组件，核查序列化引用及`JinxCasinoImmersionSceneTests`结果。

样板初始开放Slots、Blackjack、CooperativeLevers；`InitiallyAvailableGames`仍受`AllowedGames`限制，旧存档缺失该字段时保持原区域解锁。现有自动化覆盖保存引用，不代替三输入实玩、画面或S1用户验收。

实体补给柜台已保存在Immersion场景中，维护三件商品、报价牌、购买与库存目标、商品缩放及聚焦挂点。新局商品范围由ImmersionSettings限制为双人扳手、重抽牌、止损券，旧存档继续使用自身配置。柜台复用唯一游戏相机，不创建资金或库存服务。

互动教学入口、非模态提示、暂停中的跳过/重来/结果入口及共用确认卡均维护于JinxCasinoImmersionHudView。保留原MVC绑定和Prefab GUID，编辑后核查引用与真实输入回归。

开发样板从主菜单选择“互动教学”，按实际动作依次体验水果机、二十一点、柜台扳手和协作拉杆；普通练习入口保持自由游玩。教学最后离开桌面再选择完成，稍后也可从暂停菜单回到教学结果。此练习不推进标准冒险区域，标准局阶段推进和结局仍需单独实玩验证。

主菜单“继续存档”、暂停“保存旅程/读取存档”及三槽确认界面也维护于保存HUD；保留四按钮布局、有效控件导航及独立确认引用。游戏内保存后按选定槽自动保存；未主动选择槽的新局仍不写入任意旧槽。

键鼠、触屏、Xbox通用路由使用`Core.Runtime.Inputs`的GameplayInputRouter/Contracts、MenuInputScope、LocalPauseState、TouchInputPad，维护原则见[玩法输入模块](../../../modules/gameplay-input.md)。Demo仍保存`Data/JinxCasinoImmersion.inputactions`及玩法命令映射；编辑输入资产时保留Map/动作ID和人工键位，不另建EventSystem或菜单提交链。

## 构建约束

Windows / Android 分别生成对应 HybridCLR 元数据、Hotfix DLL 和 YooAsset 内置包，平台输出隔离。Android 不得使用 StandaloneWindows64 的元数据快照，也不得依赖本机 Mock Server。

Build Settings 继续只保留 AppEntrance。项目已打开时不另起 BatchMode，不用 dotnet build / msbuild 构建 Unity 工程。

## 保存原型资源与离线构建

历史原型恢复使用保留的Main场景、HUD与对应源码/资源基线；维护已保存的四区、机台和正式模型引用，不使用临时P0/P2/P4生成器覆盖现有内容。

正式资源变更必须核对Console/Editor日志和场景中实际`FormalAvatar`、各机台`FormalVisual`与音源。P4构建入口为`构建Windows P4离线试玩包`和`构建Android P4离线试玩包`，拒绝尚未装配正式角色的源场景。P4输出在`Builds/JinxCasino/P4/{target}/{version}`，版本0.4.0、标识`com.sleepystudio.jinxcasino`，保留原P0目录及应用标识。构建事务及平台恢复沿用下述经过验证的流程；是否构建成功以最新实际验证记录为准。

P0场景、HUD与Hub按钮使用已保存资源，网络设置资产单独保留。正式Player构建工具继续维护，资源布局变更不通过临时原型Builder重新生成。

`Tools/SleepyDemos/整蛊赌场/P0离线Player构建` 提供 Windows / Android 构建入口。构建在当前 Editor 串行执行，利用 SessionState 跨目标平台 Domain Reload 继续。必须先完成脚本编译，所有打开场景必须已保存；工具不代为保存或丢弃。

也可直接选择以下菜单启动相应平台事务，无需先打开窗口：

| 菜单 | 产物 |
|------|------|
| `Tools/SleepyDemos/整蛊赌场/构建Windows P0离线验证包` | Windows x64 IL2CPP Player 与 ZIP |
| `Tools/SleepyDemos/整蛊赌场/构建Android P0离线验证包` | Android 8.0 / API 26 及以上的 ARM64 APK |
| `Tools/SleepyDemos/整蛊赌场/重打包最近Windows P0分享包` | 不重新编译，过滤Unity调试目录并生成新的Playable ZIP |

两种入口共用同一任务与恢复流程，不能同时启动。离线构建不要求 Fusion SDK 或 App ID；后续互联网版本另行选型。Windows P0 不包含 Streamline 原生 DLL，Android 不生成 ARMv7 / x86 包。

Android P0仅允许横屏左右旋转；原项目方向设置由构建事务的PlayerSettings快照恢复。

构建为单人离线验证包，验收范围是已实现的 P0 场景/规则/UI能力，不构成互联网联机或完整游戏证据。Windows ZIP / Android APK 输出到 `Builds/JinxCasino/P0/{target}/{version}`；从 Hub 点击“倒霉蛋俱乐部 · P0”，再选择“单人离线验证”。

P0资源采集暂复用现有DefaultPackage的公共资源与Demo资产，并替换为本平台专用代码组；因此产物仍带有Hub和其他既有Demo资源。最终赌场交付需收敛采集范围并重新验证体积，不以P0包体积作为最终结果。

平台 DLL、首包、构建场景配置隔离；不修改默认 HotfixConfig 或 Build Settings。构建中处理器只改 Player 的 AppEntrance 内存场景配置。任务结束恢复被覆盖的生成文件、资源首包、Collector 内存与 Editor/Player 设置，备份保留于 `Library/JinxCasinoBuild`。若恢复失败，停止后续步骤并保留恢复信息；不要继续另起构建或删除备份。

URP 构建预处理会保存目标平台的着色器预过滤配置，并可能裁剪 GlobalSettings 的运行时设置列表。事务只备份目标平台启用质量级别所使用的 URP 资产与注册的 URP GlobalSettings，保留其内存 JSON、dirty 状态和原始 `.asset` / `.meta` 字节；恢复时重载对象，并在原平台恢复后再次校验原字节。备份仅在本机 `Library` 中，不进入 APK 或分享 ZIP。

Windows构建会校验本机System32的chcp.com签名和哈希，再临时提供给Bee的受限PATH编译进程，事务结束只清理自己创建且哈希一致的副本。校验使用系统Windows PowerShell及其安全模块，PSModulePath仅在校验子进程内限定为系统模块目录；从PowerShell7启动Editor也不依赖其模块自动加载。不修改用户cmd AutoRun、全局PATH或模块路径。

Windows P0已有实际构建与正式入口启动记录；Android、Photon与真机进度以[进度与验证](../progress.md)中实际证据为准。分享Windows版本使用Playable ZIP，开发机保留的DoNotShip目录和初始ZIP不需要分发。
三台独立规则夹板、正文引用与聚焦挂点维护于保存场景及对应机台组件，按S1Layout同步源与运行合同。调整后运行JinxCasinoImmersionSceneTests.AllRulesBoardsFitInsideTheirSavedTableView并检查真实聚焦截图。


入口两侧验票口、离场口的文字/操作件高度及前后关系维护于保存场景。标准结局卡及保存/返回控件维护于保存HUD，保持与三槽确认卡独立的引用和取消路径。

标准样板目标达成后按提示前往验票口，再到离场口领取票券；未达标可在离场口两次交互确认撤离。若超时仍有已投入牌局，先回原桌完成；暂停不会耗尽撤离确认窗口。结局可保存在三槽中，再明确返回Hub。此流程的Editor真实输入证据记录于immersion-progress，不替代Player或真机实玩。

读取旧原型存档时，格式有效不代表当前样板能继续所有内容。若提示需要尚未开放内容，取消确认返回即可，原档及当前旅程不变；不要手改机台ID、删掉已投入牌局或覆盖原档。四区/无尽等恢复需等待对应场景内容接入，旧Main仍保留原型恢复入口。

沉浸入口的设置可从主菜单或暂停菜单打开，分为键鼠/触控、手柄和声音。修改立即预览，保存才持久化；B/Esc/Menu或返回撤销未保存改动并回原菜单。恢复默认也是预览，仍须保存。手柄上下选控件、左右调滑条；真实设备的震动与Android后台恢复仍需单独验证。

## S1独立试玩包

使用`Tools/SleepyDemos/整蛊赌场/构建Windows S1独立样板`或`构建Android S1独立样板`，产物独立保存于`Builds/JinxCasino/S1/<平台>/<构建版本>/`。平台配置StartupScene为JinxCasino，版本0.5.0；不再经过Hub菜单。游戏中返回会回到赌场主菜单，主菜单提供退出游戏。Editor默认入口保持Hub。Windows已有成品可用`重打包最近Windows S1分享包`，ZIP包含S1说明。

S1仅是当前一区三机台样板，构建成功不等于体验验收。仍复用DefaultPackage采集范围，包内可能包含历史Demo资源，尚未做最终体积裁剪。Android沿用com.sleepystudio.jinxcasino，安装可能替换既有P4包；应用数据仍由存档兼容检查保护。构建采用原备份恢复事务，不修改默认配置、AppEntrance或Build Settings。实际构建及启动证据以immersion-progress为准。
