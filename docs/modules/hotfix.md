# Hotfix 热更新模块

## 负责什么

热更新模块负责启动期 AOT 元数据补充、热更程序集加载，以及编辑器侧热更构建和本地资源调试。它属于 Core 底座能力，不承载具体 Hotfix 业务逻辑。

## 代码位置

运行时：

- `Assets/Scripts/Core/Runtime/Hotfix/HotfixConfig.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HybridAotAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Hotfix/HotfixAssemblyLoader.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/HybridMetadataSystem.cs`
- `Assets/Scripts/Core/Runtime/Startup/Systems/HotfixAssemblySystem.cs`

编辑器：

- `Assets/Scripts/Core/Editor/Hotfix/HotfixBuildWindow.cs`
- `Assets/Scripts/Core/Editor/Hotfix/HotfixAssemblyDefinitionFilter.cs`
- `Assets/Scripts/Core/Editor/Hotfix/HotfixAssemblyBuild.cs`
- `Assets/Scripts/Core/Editor/Hotfix/ReleaseProfiles.cs`
- `Assets/Scripts/Core/Editor/Hotfix/HotfixBuildAutomation.cs`
- `Assets/Scripts/Core/Editor/Hotfix/TapTapBuildAdapter.cs`
- `Assets/Scripts/Core/Editor/Hotfix/LocalBundleHttpServer.cs`
- `Assets/Scripts/Core/Editor/Hotfix/GitHubPagesDeployment.cs`

配置与资源：

- 默认配置资产：`Assets/LoadResources/Config/HotfixConfig.asset`
- 发布白名单：`Assets/Settings/ReleaseProfiles.json`
- 热更 DLL 构建产物：`Assets/LoadResources/Codes/**`
- SSH 私钥默认位置：`Assets/Settings/Hotfix/key`

## 启动链路

热更新运行在 `BeforeHotfixStartupState` 阶段：

1. `HybridMetadataSystem` 读取 `HotfixConfig.AotAssemblies`，通过 `HybridAotAssemblyLoader` 补充 HybridCLR 泛型元数据。
2. `HotfixAssemblySystem` 读取 `HotfixConfig.HotfixAssemblies`，通过 `HotfixAssemblyLoader` 加载热更程序集。
3. `UITypeReflection` 初始化已加载 Hotfix 程序集的页面索引，并扫描 Core 与启动宿主程序集；HotfixEntry 补充扫描自身程序集，重复调用不重复枚举类型。
4. `HotfixEntrySystem` 才会进入 Hotfix 业务入口。

## 编辑器入口

| 菜单路径 | 说明 |
|----------|------|
| `Tools/UI Framework/Hotfix Build` | 热更构建主窗口：HybridCLR 生成、YooAsset 构建、本地 Mock Server、远端部署 |
| `Tools/一键打包工具` | 同一窗口的中文入口 |

本地 Bundle HTTP 服务通过 `HotfixBuildWindow` 的 Mock Server 区启停，底层实现为 `LocalBundleHttpServer`。

“一键编译并同步”依次执行 HybridCLR 全量生成与编译、刷新 AOT/热更程序集列表、AOT 剥离，再把剥离后的 AOT DLL 与热更 DLL 同步为资源目录中的 `.dll.bytes`。单独的“同步当前 DLL 到资源目录”仍直接使用未剥离的 AOT 源目录。

窗口全量生成和 CI Full Prepare 共用锁定版本的 HybridCLR 工具链准备。当前 Unity 6000.3.15f1 / HybridCLR 8.14.1 的数组方法会复用 AOT 定义，但热更元素数组的 `image` 属于热更程序集；原 `GlobalMetadata.GetParameterInfo` 按数组 image 查形参，会在 JSON 反射等路径读取错误参数表。准备入口仅在本机生成的 `GlobalMetadata.cpp` 中，将该查询改为已有的 methodDefinition 重载，按方法实际归属选表；重复执行不重写，未知实现直接阻断并要求复核。不修改上游仓库或业务 JSON 格式。兼容修正进入构建指纹，受影响游戏必须重建 Full，旧 Player 不能通过 DLL 热更获得此原生修复。普通 EditorSimulateMode 使用编辑器 Mono，不能替代该问题的 IL2CPP Player 验证。

构建平台以 `EditorUserBuildSettings.activeBuildTarget` 为唯一来源，窗口保留 PC/Android/WebGL 按钮并显示当前平台的选中状态。点击另一平台只提示先到 `File > Build Profiles` 切换，不直接切换 Unity 平台或修改路径。打开窗口、切换配置或检测到 Unity 平台变化时，自动同步 AOT、热更 DLL 源目录和当前平台最新的 Bundle 目录；手动选择的路径可在本轮操作中继续使用，再次同步平台时会恢复该平台的默认路径。新建配置也使用当前构建平台。HybridCLR 编译与 YooAsset 资源打包应使用同一平台；SSH 部署保留 Windows64 的 `/PC` 目录，Android 使用 `/Android`。

### 发布目标与 TapTap

窗口“发布游戏”使用 `ReleaseProfiles.json` 的白名单，GUI 与 CI 均通过 `ReleaseProfiles.Apply` 设置应用标识、启动场景、运行时 DLL 列表、编译宏和 HybridCLR 热更 asmdef。DLL 按配置顺序加载，公共 `Hotfix.dll` 在 Demo DLL 之前；`ReadHotfixAssemblies(gameId, target)` 验证每个必需 asmdef 存在，缺失直接失败，不退化为不完整列表。`Hotfix.Diagnostics.PC.dll` 只进入 Hub 的 Windows64 列表。分组与程序集的关系见[资源发布边界](../architecture/resource-system.md#独立游戏发布边界)。

GUI 支持 PC、Android、WebGL，平台按钮仍只提示到 Build Profiles 切换。Hub 保留既有本地路径和官方 YooAsset 窗口；独立游戏使用窗口内的“构建当前游戏本地资源”，与 CI 共用 `ReleaseProfiles.BuildResources` 的收集配置、依赖校验和标准 ScriptableBuildPipeline。独立游戏不通过官方窗口打包，避免其固定输出根覆盖其它游戏。正式 Full/Hotfix 与远端发布使用 Jenkins；GUI 对非 Hub 禁用远端发布。

独立游戏 GUI 的 DLL 源与剥离输出保存在 `HybridCLRData/ReleaseTargets/<GameId>/<BuildTarget>/{Hotfix,Aot,StrippedAot}`，每次编译后只从 HybridCLR 上游固定生成目录归档选中程序集。资源输出及最新版本查找位于 `Bundles/ReleaseTargets/<GameId>/<BuildTarget>/<Package>`。`LoadResources/Codes` 是当前目标的共享构建暂存区，地址保持不变；同步会清除不在本次列表中的旧 `.dll.bytes`，不能同时构建两个目标。GUI 本地资源不复制到 StreamingAssets，也不建立正式 AOT 基线。

运行、编译、Player 构建和存在自动化请求期间，热更窗口只读，不以 EditorPrefs 覆盖构建请求的配置。独立游戏的远端地址由对应 Jenkins 请求管理，窗口保留当前值；Hub 的远端地址在用户实际编辑后同步，不能在每次绘制时重写。正常打开窗口和实际切换平台时仍同步本地 DLL/Bundle 路径。

DLL 暂存资源的 GUID 也属于发布身份。同步在任何 Refresh 前补齐 `.meta`：保留现有合法元数据，已存在的程序集资源使用固定身份表，新程序集使用规范项目路径的稳定 SHA256 标识。即使跨游戏切换时被删除再生成，同一 DLL 也不应因随机 GUID 改变 Bundle。身份表只维护构建生成的 DLL TextAsset，不修改场景、Prefab 或普通资源 GUID。

Demo 收集器使用 `ReleaseDemoPackRule`：普通 Demo 延续目录分包；卡丁车赛道 Prefab、赛道/AI 数据、World 和章节网格/美术按章节分组，音乐逐 Clip 分包。AudioBank 仅引用短效和引擎，音乐通过地址按需加载，不能用全曲硬引用绕过分包。Web 无预下载策略时按实际资源请求下载对应 Bundle；原生 Host 的启动下载流程保持原契约。

TapTap 沿用创意工坊应用 971575，`TapTapBuildAdapter` 直接通过标准 Unity BuildPipeline 构建 H5，唯一启动场景为 AppEntrance。Prepare 与 Full 共用 WebGL2、IL2CPP OptimizeSpeed、单线程、Gzip 与 decompressionFallback 设置，使用 Unity 默认加载页和全屏画布。完整网页输出到 `player/webgl/`；`player/h5.zip` 只包含 game/ 一个顶层目录，内有 index.html 与全部 Build、StreamingAssets 等网页文件，压缩后不得超过 300 MB（300,000,000 字节）。归档前校验 wasm 的游戏入口与 SendMessage，并拒绝路径链接和缺少 index.html 的包。

请求、准备记录、基线与结果使用 TapTapFormat=H5；Full 的 H5Archive 冻结 ZIP 路径、大小和 SHA256，Deploy 核对冻结文件。仓库外 TapTapAppId 仅作可选应用记录，不需要小游戏 SDK AppId 或 Player CDN。H5 基线和资源版本登记位于 `TapTap/<GameId>/WebGL/H5`，严格拒绝旧 SDK 小游戏基线，首次必须使用新的 ResourceVersion 重新 Full。旧已发布目录与旧基线保留，不由 H5 流程覆盖或删除。

Pages 只发布 `unity/<项目>/TapTap/<GameId>/hotupdate/WebGL` 下的 YooAsset 资源，沿用内容上传、版本激活、HTTP SHA 校验和基线提升；完整网页依赖随 H5 ZIP 内置。Full、Hotfix 均默认不发布，H5 平台上传与审核另行操作。WebGL 使用 WebPlayMode，平台桥保留 `UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR`，实际使用浏览器生命周期、AudioContext 暂停恢复和按 GameId 隔离的 localStorage；当前单机不消费平台身份，因此不强制执行小游戏登录。2026-10-16 Full release05 已完成开发包上传和 Pages 激活，129个HTTP文件与冻结记录一致；本轮桌面连续四次启动、首章进入及暂停通过，未再复现95%反射越界。Android iQOO用户已反馈D02可正常游玩、Full04越过47%进入游戏；Full05仍待单独真机确认。真实前后台、听音及配额失败未验收，未提审或上线。前期设置保存/重载及漂移/加速特效记录与本轮结果分别见[H5验收记录](../agent/archive/demo-release-taptap-2026-10-15.md#full05-最终交付与回归)。

独立包隐藏 Hub 返回入口已接入八个 View；卡丁车 WebGL 特效因 StructuredBuffer/procedural 不兼容而增加模板动态 Mesh 与 shader target 3.5 路径，保留原生绘制。该修复新增旧基线未保留的 AOT 成员，Hotfix Prepare 正确拒绝后，已重建 Full release03 并实测网页出图；不能向旧 Full 包直接发布这份补丁。玩法维护见[卡丁车模块](../demos/kagit_kart/module.md)。

### GitHub Pages 资源环境

`HotfixBuildWindow` 提供 `SSH` / `GitHub Pages` 上传方式。Pages 的本地仓库路径、网站根 URL、Unity 项目目录名由 Core.Editor 使用 `EditorPrefs` 按 Unity 项目和配置保存；`HotfixConfig` 不增加部署字段，只保留运行时资源地址等现有配置。窗口负责设置、当前平台展示、URL 同步、按钮与清空确认；`GitHubPagesDeployment` 负责目标路径和发布白名单、仓库与登录校验、增量复制、当前平台清理，以及异步 Git 提交和推送，不参与运行时资源加载。

固定目标为 `unity/<项目>/hotupdate/<当前平台>/`：`StandaloneWindows64` 映射 `PC`，`Android` 映射 `Android`，其他平台沿用 `BuildTarget` 名称。Pages 模式从网站根 URL、项目和平台自动生成 `RemoteBaseServerURL`；关闭本地模拟后 `BaseServerURL` 使用该地址，SSH 模式仍保留手工 URL。当前 PC 地址为 `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/PC`，日常 `EditorSimulateMode` 不变。

Pages 默认只复制变更的下载文件并保留旧包；独立的“清空当前平台目录”操作经确认后删除目标文件并保留 `.gitkeep`。两种操作均执行提交、推送流程，只作用于当前项目和平台；含子目录或链接的目标拒绝操作。本地仓库须为干净的 `main`，远端不能领先或分歧；仅允许当前平台资源与站点骨架存在未推送提交，其他本地待发布文件会被拒绝，工具不自动合并。

公开仓库 `H-Bole/H-Bole.io` 已开启 `main` 根目录 Pages。2026-10-14 首次发布 Android 版本 `2026-10-14-86`，Pages 部署成功，全部 129 个下载文件的 SHA256 与源文件一致；同包再次增量上传未产生新提交。PC 仍只有 `.gitkeep`，此次未清空目录。首次发布核对时 Android 源配置为 HostPlayMode、两个远端 URL 为空；随后已修正源工作树 Pages 设置，两个 URL 均指向 `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/Android`，保留 HostPlayMode 与已有构建。APK 未重建、设备实际热更未验证，尚未核查 APK 内配置；源配置修正和上传成功不代表已安装包连接 Pages。发布记录、窗口步骤及验证边界见 [用 GitHub Pages 发布热更资源](../runbooks/deploy-github-pages-hotupdate.md)。其他 Unity 项目并列放在 `unity/` 下，网站根目录可容纳其他用途，不预建未知用途目录。

## 边界规则

### Jenkins 非交互构建

自动入口 `HotfixBuildAutomation.Prepare / Build / Deploy` 与窗口共用程序集和发布后端。请求由仓库外 JSON 及 Jenkins 凭据环境提供，CI 只修改独立平台工程；不会把日常窗口偏好当构建参数。在线 Full 发布成功后保存当前平台 AOT 基线，Hotfix 使用原基线检查缺失元数据；Offline 不替换在线基线。维护契约见 [Jenkins 构建模块](jenkins-build.md)，操作见 [使用本机 Jenkins](../runbooks/use-jenkins.md)。

本轮程序集拆分和 `LanguageService.RegisterCatalog` 等公共 AOT API 变更需要各游戏重新 Full 构建，不能直接沿用改造前基线发 Hotfix。此后仅改某个 Demo 的私有 DLL、词表或资源时，在其对应游戏/平台基线内更新；公共 AOT/ABI 再变化时重新为受影响游戏出整包。

SSH 上传现采用主机 SHA256 指纹校验、内容先上传及版本最后切换，不再先清空平台目录。指纹存编辑器偏好或 CI 非秘密配置，不保存私钥内容。

- Hotfix 业务代码不要直接操作 HybridCLR 或 YooAsset 的底层句柄。
- 热更程序集列表、AOT 元数据列表优先通过 `HotfixConfig` 管理。
- HybridCLR 上游的 `hotUpdate*` 配置键与 `HybridCLRData/HotUpdateDlls` 生成目录是插件固定约定，不属于项目自有命名，禁止改写。
- 自动填写热更程序集时统一过滤 Test Assembly、`.Tests` 后缀和 Editor-only asmdef。
- 测试 asmdef 禁止放在 Hotfix 目录；框架相关测试统一放到对应 TestMode 的 `Assets/Scripts/Tests/.../Module/<模块>`，Demo 专属测试放到 `.../Demo/<Demo>`。
- 热更 DLL 等构建产物位于 `LoadResources/Codes/**`，资源命名校验会跳过该目录。
- 修改热更构建窗口、热更配置字段、启动期热更装配顺序时，必须同步更新本文和 [启动与热更流程](../architecture/startup-flow.md)。

## 验证重点

### Editor 运行验证

默认模式遵循 [根验证规则](../../AGENTS.md#验证与-unity-editor)。普通功能验证使用 `HotfixConfig.PlayMode = EditorSimulateMode`，通过正式 `AppEntrance` 启动，检查 Hub 与受影响 Demo 的资源加载和交互。复用 Unity Editor 已编译的代码及开发资源，无需重编译 DLL 或构建首包；临时改变配置时记录原值并在结束或失败后恢复。

### 按明确要求执行 Offline 首包验收

只有用户明确要求 Offline 验收时，才使用既有 `Hotfix Build` 和 YooAsset 打包入口执行以下流程，不新增启动或验证框架。

- 验收前记录正式 `Assets/LoadResources/Config/HotfixConfig.asset` 的原状态，包括资源模式、包名、启动场景与路径；仅临时切换到 `OfflinePlayMode`，保留原配置以供结束或失败后完整恢复。
- 使用本轮最新源码与资源生成的首包，核对实际 `StreamingAssets` 中的版本、清单与 Bundle / DLL 资源；旧版本或 EditorSimulate 的开发目录不能代替本轮首包。通过正式 `Assets/Scenes/AppEntrance.unity` 启动，核对 Offline 包初始化、Hub 与目标 Demo 的实际资源加载和交互，结束后恢复原配置。
- `YooAssetInitializeSystem` 将配置模式交给资源服务；`YooAssetResourceSystem` 的 Offline 分支使用内置文件系统。`HotfixAssemblyLoader` 在 `UNITY_EDITOR` 下优先复用已加载的 Hotfix 程序集，因此此验收覆盖正式首包资源与 Editor 启动流程，不能证明首包 DLL 的独立加载、IL2CPP 编译或裁剪结果。
- 报告分别记录首包构建与文件核对、Editor Offline 实际运行、配置恢复及未完成项。IL2CPP 编译 / 裁剪、独立 Player 和原生发布验证后置，真实手柄与触屏仍待实际设备验收；不将这些后置项目记录为本轮已通过。

已记录的公共设置首包结果与验证限制见 [公共设置与表现交付状态](settings-presentation.md#交付状态与验证边界)。旧生成基线、Player 构建失败及机器修复顺序见 [历史实施记录](../agent/archive/hotfix-build-history.md)，本模块不把历史环境状态作为实时结论。

### 接入检查

- Editor 中能打开 `Tools/UI Framework/Hotfix Build`。
- Windows64、Android 与 WebGL 下，窗口平台显示和 AOT、热更 DLL、Bundle 路径均与 Unity 当前构建平台一致；重新打开窗口不会回到固定 PC 状态。
- 点击非当前平台按钮只出现切换提示，选中状态和路径保持当前平台；在 Build Profiles 完成切换后自动更新。
- 启动时 `HybridMetadataSystem` 和 `HotfixAssemblySystem` 不报错。
- 热更程序集加载后，具体 View 自动按类名与 UIBind 别名进入索引，新增页面无需手写清单；未知名称只查缓存，不扫描其它程序集兜底。
- `Tests.Module.HotfixAssemblyDefinitionFilterTests` 和 `Tests.Module.TestAssemblyBoundaryTests` 全部通过。
- `HotfixConfig.HotfixAssemblies` 与选中 GameId、实际 BuildTarget 的发布配置一致；热更代码目录不存在 `.Tests.dll`。
- 如果使用本地 Mock Server，资源包 URL 能访问并被 YooAsset 正常下载。
