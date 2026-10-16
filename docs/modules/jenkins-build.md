# Jenkins 构建与发布

## 职责

`Jenkinsfile.Full` 和 `Jenkinsfile.Hotfix` 分别调度整包和热更，均可选择 PC、Android 或创意工坊 TapTap H5。两个薄流水线共用 PowerShell 的进程、工具、独立工作区和归档流程，Core.Editor 复用 HybridCLR、YooAsset 及发布后端。Core.Runtime 仅负责资源失败阻断，不依赖 Jenkins。

本机操作、参数和迁移见[使用 Jenkins](../runbooks/use-jenkins.md)。个人知识库保存完整搭建过程；本文维护当前工程契约。

## 入口与阶段

- `Jenkinsfile.Full` / `Jenkinsfile.Hotfix`：`SleepyDemos-Full` / `SleepyDemos-Hotfix` 两个参数化任务，固定 Full / Hotfix，页面不提供 BuildKind。共用单节点单执行器，跨任务串行排队，6 小时总超时、30 分钟发布超时。
- `scripts/jenkins/bootstrap/job-full.xml` / `job-hotfix.xml`：首次创建即包含参数和 SCM 配置；旧任务迁移验证后停用，保留历史记录。
- `scripts/jenkins/Manage-Jenkins.ps1`：手动 Start / Status / Stop，无服务或自启动。
- `scripts/jenkins/Invoke-Build.ps1`：固定源与子模块提交、工具准备、分阶段 Unity、PC 目录 ZIP / Android APK或AAB / 资源 ZIP 及失败归档。
- `Core.Editor.Hotfix.HotfixBuildAutomation.Prepare / Build / Deploy`：由 `SLEEPY_BUILD_REQUEST` 指向绝对 JSON 请求，非交互 `-executeMethod` 入口。
- `HotfixAssemblyBuild`：GUI 与 CI 共用程序集识别、AOT 剥离及同步；缺少文件失败，不静默复用旧 DLL。
- `ReleaseProfiles`：固定发布白名单、实际平台 DLL 列表、Collector 配置、跨游戏依赖校验及 GUI/CI 共用资源构建入口。
- `TapTapBuildAdapter`：标准 Unity WebGL 构建，固定 Player 设置、唯一 AppEntrance，校验 wasm 游戏入口并生成 H5 上传 ZIP。
- `SshBundleDeployment` / `HotfixDeploymentVerification`：共享 SSH 上传及正式 HTTP 校验。

正常流程：固定源码 → Luban/原生依赖 → Prepare 生成程序集 → 新 Unity 进程重新导入 → Build 资源与 Player → 可选 Deploy → 归档。生成物只落在已登记的 CI 克隆和本次产物目录，日常工程不参与 reset 或清理。

SourceRef 使用官方 Git Parameter 插件列出远端分支和标签，支持快速过滤，默认 main；可选 SourceCommit 仅接受完整 40 位 SHA，填写后优先。任务 SCM 提供固定的流水线版本，游戏源码可以选择另一个提交：记录 RunnerCommit、SourceCommit 和 ConfigCommit；SCM 只检出一次，后续阶段核对 runner、游戏源码与递归子模块，禁止中途漂移。Finalize/Cleanup 即使源码漂移仍执行进程身份核对。选择分支不会创建新的线上基线或 CDN 目录。

机器配置的 `Root` 负责 Jenkins 数据与产物，当前为 `E:\Jenkins`；必填的 `WorkspaceRoot` 单独负责 Unity 克隆与平台缓存，当前为 `E:\Project\SleepyDemos-Build`，其中 `PC`、`Android` 各有自己的源码与 Library。日常开发工程可继续运行，不会争用 CI 工程锁；同 Windows 用户的 Android External Tools 偏好仍共享，构建期间不能手动切换工具或同时手工构建 Android。

`WorkspaceRoot` 必须是无空格的本地盘符绝对路径，不能为盘根、日常工程或其祖先/后代，路径不能经过 Junction/符号链接。迁移目录不改变 `Root` 下的 home、runs、baselines、resource-versions 与 Pages。PC Full 的 CMake 缓存含绝对路径，搬迁后须重建；继续原基线的 Hotfix 不重新编译原生库，应保留原 DLL、运行库、许可证和 `.meta`。

TapTap 使用 `WorkspaceRoot/TapTap/Project` 的独立源码和 Library，最终产物持久保存到 `WorkspaceRoot/TapTap/Artifacts/<GameId>/<任务名-编号>/` 并逐文件核对大小和 SHA256；临时请求、日志和构建现场继续进入 Root/runs，正常清理不删除持久交付目录。`Common.ps1/Get-BuildIdentity` 统一启动、路径和失败收尾的身份映射，TapTap 明确映射 WebGL，不走 Android 默认分支。单执行器保持不变，不共享 PC/Android 的 Library，也不另建 Jenkins 管理端。

每次 Unity 启动先原子记录本次 run、项目、方法、日志、用户和启动意图，再记录 PID/创建时间。失败或取消时，Finalize 核对 Win32_Process 身份并收尾本次 Unity 及能确认归属的子进程，排除授权服务；确认退出和工程解锁后才归档、清理。身份失配或退出未确认则保留现场，仅归档收尾摘要与进程记录。不能以 Get-Process 查不到 PID 代替退出证明，也不按名称终止日常 Editor。

Finalize 和 Jenkins archiveArtifacts 分别成功后，才允许 Cleanup 删除本次 runs 与临时 ci-archive；任一步失败仍执行进程退出核对，但保留原始产物和日志，避免只归档半份内容后删除唯一现场。SCM 尚未成功检出本轮 runner 时不执行旧工作区中的收尾脚本。

## 请求与结果

请求包含 BuildChannel、GameId、平台、构建类型、资源模式、Development、签名类型、版本、源与配置 SHA、独立输出/基线目录及非秘密发布参数。ReleaseTarget 的 PC / Android 映射 Native 渠道，TapTapH5 映射 TapTap + WebGL、TapTapFormat=H5，拒绝 hub 且只允许 Pages。GameId 页面列出正式 ReleaseProfiles，固定游戏源码后再次以 `Assets/Settings/ReleaseProfiles.json` 校验；新增正式游戏需同步两个流水线与 bootstrap 的 choices。TapTapAppId 仅为可选应用记录，不参与原生小游戏 SDK 初始化。签名密码、私钥和 GH_TOKEN 从 Jenkins 凭据作用域的环境读取，不写入 JSON。

输出 `prepared.json` 记录准备输入，`built.json` 冻结资源 SHA，`result.json` 记录阶段成败、版本和发布下载证据。`resources/` 是平面的 YooAsset 下载白名单，`player/` 保存 Player；TapTap Full 的完整网页位于 `player/webgl/`，上传包为 `player/h5.zip`，直接复制到最终归档根，不再套一层 Player ZIP。`baseline/` 保存整包候选基线；TapTap 的准备、构建和基线记录包含 TapTapFormat，Full 构建/基线中的 H5Archive 冻结 H5 ZIP 的相对路径、大小和 SHA256。失败尚未生成非空 H5 ZIP 时，Finalize 仍将已生成的 Player 内容归档为 player.zip，结果以失败摘要为准。resource-versions 持久登记每个平台已分配的资源版本，失败任务的版本也不释放，不随普通归档过期删除。构建成功不等于发布成功；资源发布成功而基线提升失败须如实记录。

## 平台和资源模式

PC 为 Windows x64 IL2CPP，Android 为 ARM64 IL2CPP，APK/AAB 可选；Development 与调试签名独立。Android Full 明确使用当前 Unity 配套 SDK/NDK/OpenJDK/Gradle，关闭 Gradle 工程导出并检查实际 APK/AAB 文件存在，阶段退出恢复当前用户的原工具偏好。同用户日常 Editor 在构建期间会短暂看到这些偏好，不能与 CI 同时手动切换外部工具或启动 Android 构建；强制结束进程可能跳过恢复。

Full 才提供资源模式、Development、PlayerVersion 及 Android 格式、签名、版本号。Android versionCode 使用 `Root/android-version-codes/<StateKey>/current.json` 持久登记，与任务 BUILD_NUMBER 无关；空值分配上次值加一，显式值必须更大。Checkout 原子消费，失败不释放。已有 Android current 而缺少版本登记时拒绝构建，先核对旧 APK/AAB 或归档请求迁移下限，不能默认从 1 开始。备份和迁移必须保留此目录。

CI 主包与热更 DLL 统一使用 `.NET Framework`（Unity `NET_Unity_4_8`），摘要记录实际 API profile，热更必须与基线一致。Unity 会在 Player 构建中剥除 netstandard 依赖，热更 DLL 若仍直接引用它会失败；遵循 [HybridCLR 官方说明](https://www.hybridclr.cn/docs/help/commonerrors)。Prepare 同时从 Unity 真实宏定义和平台 URP 集合同步批处理配置，并显式固定 Editor 程序集为同一 Framework profile，避免默认值在 Full 编译后具体化而造成热更指纹误差；指纹和原 AOT 基线仍严格校验。此设置只写入独立 CI 工程；继续手工构建 Player 时也须核对 API profile，日常工程设置不会由 Jenkins 改写。

原生 Offline 使用 OfflinePlayMode 并内置全量资源；OnlineFull 使用 HostPlayMode 并内置全量；OnlineDownload 使用 HostPlayMode 不内置 Bundle，但用锁定 YooAsset 包的官方生成器保留空 BuiltinCatalog，供默认内置文件系统初始化；缺表或混入 Bundle 即构建失败。日常 EditorSimulateMode 和已有配置资产不会被 CI 改写。Offline 可发布同批资源，但其安装包不消费在线更新。

## 基线与发布边界

每个渠道、游戏和平台只维护一个当前在线主包。Native hub 的 `baselines/<PC|Android>/current` 与 `resource-versions/<平台>/` 保留；原生独立游戏使用 `baselines/<平台>/<GameId>/current` 与 `resource-versions/<平台>/<GameId>/`，TapTap H5 使用 `baselines/TapTap/<GameId>/WebGL/H5/current` 与 `resource-versions/TapTap/<GameId>/WebGL/H5/`。格式标记和独立路径共同阻止误用旧 SDK 小游戏基线；首次 H5 必须重新 Full 并使用新的资源版本。候选基线保存实际配套 AOT、补充元数据、程序集、渠道/游戏/平台/架构/Unity/Development/版本、兼容指纹和 SHA。只有在线 Full 发布及 HTTP 校验成功才提升 current；Offline 和失败任务不替换它。

Hotfix 使用 current 的 AOT 与补充元数据，运行 HybridCLR MissingMetadataChecker；Core.Runtime、Packages、原生插件和主包构建配置采用保守指纹检查。URP GlobalSettings 的派生运行时列表按锁定包的官方平台剥离器重算后纳入哈希，源设置、引用和 GUID 仍完整校验；不写回资产或基线。剥离接口或序列化格式变化时直接失败。缺基线或不匹配要求 Full，不为旧主包维持并行兼容链。

Hotfix 页面不提供资源模式、Development、PlayerVersion 或 Android 主包参数。Checkout 从 current 继承三项主包设置并在控制台展示基线游戏、平台、资源版本和主包摘要；不允许页面残留参数覆盖。仍使用原有基线校验，继承参数不表示任意源码都兼容。

准备记录与基线同时保存逐文件指纹，失配时报告变化路径。JSON、程序集定义、meta 和 YAML 资产统一文本换行后校验，避免 Unity/ProBuilder 保存时仅 CRLF/LF 转换阻断构建；配置内容与二进制依赖仍完整参与校验。

Pages 沿用 `unity/SleepyDemos/hotupdate/PC|Android`，独立 CI 克隆与凭据，保留下载白名单/路径/大小保护；等待站点部署后逐文件 HTTP SHA 校验。CI shader Bundle 名称复用官方默认 pack 规则，并以官方构建报告的完整 Bundle 列表校验发布输出，防止白名单过滤后静默漏发。内容与激活共用 22 分钟预算，恢复另给 5 分钟；HTTP 正文和 Git/GH 子进程均支持取消，不能让外层 30 分钟先中止恢复。远端暂仍旧版本但本地激活已提交时，普通推送恢复后继提交作为引用 CAS 屏障；并发或不明状态保留人工处理。相同资源重复发布不创建空提交。

独立游戏按渠道和 GameId 划分目录：原生为 `unity/SleepyDemos-<GameId>/hotupdate/<平台>`，TapTap 为 `unity/SleepyDemos/TapTap/<GameId>/hotupdate/WebGL`。Pages 路径入口只接受单段项目名或严格的 `<项目>/TapTap/<GameId>`，拒绝其它嵌套形式和越界段；未推送提交也按完整目录范围检查。原生 SSH 的根目录和 HTTP 根同样追加游戏目录，再追加平台；不得与原生 Hub 或其它游戏共用版本入口。

TapTap 沿用创意工坊应用 971575，采用标准 Unity WebGL H5。Prepare 和 Player 构建统一使用 WebGL2、IL2CPP OptimizeSpeed、单线程、Gzip 与 decompressionFallback；保留 Unity 默认加载页并使画布铺满宿主，不另加游戏美术。实际 wasm 解压后检查 `main` 或 `__main_argc_argv` 以及 SendMessage，空壳直接失败。

H5 ZIP 只有 `game/` 一个顶层目录，其中包含 index.html 和完整 Build、StreamingAssets 等网页文件。源码限制压缩包不超过 300 MB（300,000,000 字节），拒绝路径链接和缺失入口。压缩包内置网页依赖，Pages 只承担 YooAsset 资源发布；不再配置独立 Player CDN，也不执行小游戏 SDK 转换或函数采集。

TapTap Full 发布沿用 YooAsset 内容上传、版本激活、HTTP 下载 SHA256 校验和基线提升；Deploy 同时核对已冻结 H5 ZIP 的大小与 SHA。`Published`、`Downloads` 表示资源发布结果，不代表 H5 已上传平台或通过审核。Publish 默认 false；只有明确选择发布才改变远端资源。Pages 的单文件 100 MiB、站点 1 GB 限额仍作用于上传的 YooAsset 资源。

旧 SDK 已发布的 Player 目录与基线保留为历史记录，当前 H5 链路不删除、不覆盖也不消费它们。当前发布状态与验证边界统一见[热更模块](hotfix.md#发布目标与-taptap)，具体构建、HTTP 校验、平台上传和真机反馈见[H5 验收记录](../agent/archive/demo-release-taptap-2026-10-15.md#full05-最终交付与回归)。

SSH 机器配置的上传根与 HTTP 根均由流水线追加平台；请求中的 SshServerUrl 为最终平台 URL，编辑器窗口的地址接口保持原义。固定主机 SHA256 指纹，内容先上传，清单随后，版本入口最后原子替换；禁止先清空目录。版本入口/恢复操作最多等待 1 分钟，口令私钥从同一 Jenkins 凭据的 Passphrase 读取。旧不可变资源不能以不同内容覆盖。APK/AAB/PC ZIP 不上传到热更目录。

## 验证

按[项目测试政策](../architecture/testing.md)复用相关资源与发布测试，不默认全量回归。真实 IL2CPP Player、包内资源、HTTP 与原安装包热更分别记录；SSH 实发、真机或新电脑未具备环境时明确为待验收。源码写入和 Editor 编译不能代替这些证据。
