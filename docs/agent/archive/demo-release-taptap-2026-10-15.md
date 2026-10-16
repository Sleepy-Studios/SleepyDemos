# Demo 独立发布与 TapTap 改造验收记录

实施分支：`codex/demo-release-taptap-refactor`，使用当前目录，没有创建工作树。以下发布、提交 SHA 和待验收描述按当时阶段保留；Pages 资源与开发包上传已获授权，未正式提审或上线。

主线交付前将本地 25 次实施提交整理为一条最终变更，避免把已删除的小游戏 SDK 及其 node_modules 带入远端历史。整理前 Git bundle 已校验并保存在本机 `E:\Project\SleepyDemos-Build\HistoryBackups\demo-release-taptap-before-cleanup-20261016.bundle`；文中的旧源码 SHA 对应该备份及原构建记录，不是整理后分支的祖先。清理旧 SDK 忽略规则、专属 link.xml 指纹扫描及无效参数，Jenkins 默认 SourceRef 改为 main；H5 运行桥、真实修复、完整构建校验及验收证据保留。

清理后的 Unity 编译任务 `c0e590b9` 完成、0 错误；PowerShell 构建脚本语法与 TapTap 作业 SCM/默认来源静态核对通过。未新增测试文件，未重复构建 Player 或全量回归；此项不扩大 Full05 的真机验收范围。

当前架构与操作以[热更模块](../../modules/hotfix.md)、[Jenkins](../../modules/jenkins-build.md)、[输入接入](../../runbooks/use-gameplay-input.md)、[卡丁车音频](../../demos/kagit_kart/runbooks/audio.md)为准。本记录只保存本轮实际验证证据。

## 已执行的定向验证

以下按实施阶段保留历史证据。用户随后选择沿用创意工坊，当前路线已改为标准 Unity WebGL H5；旧 SDK 的构建、分包、CDN 与基线不能替代 H5 验收，H5 结果见末节。

- Unity 6000.3.15f1 Editor 编译通过；使用 HybridCLR 8.14.1、YooAsset 3.0.6、锁定并内嵌兼容补丁的 TapTap SDK 2.0.21。
- 11 个 GameId/平台组合的资源依赖白名单检查通过；DLSS 代码、场景及预览图归 PC 诊断资源，无人机预览和 Loading 图归自身资源。
- DLL 实测：修改卡丁车私有音频代码后，卡丁车 DLL 变化，公共 Hotfix 与无人机 DLL 的 SHA256 不变；随后还原原始源码并重新编译。证据位于本机 `Library/ReleaseValidation/dll-isolation.txt`。
- 资源实测：最终资源归属整理后，修改卡丁车私有音频清单，无人机两次资源构建的 69 个 Bundle 文件名与 SHA256 全部一致。证据位于本机 `Library/ReleaseValidation/drone-bundle-isolation.txt`。清单版本与归档时间不作为内容哈希比较项。
- WebGL Player C# 编译通过：仅公共 Hotfix 与卡丁车热更程序集，不包含无人机、夹爆它、PC 诊断与 Core FishNet 适配程序集。此项不等于 IL2CPP/wasm 或小游戏容器通过。
- 复用现有 Unity Test Runner：改键会话 2 项；设置服务原 5 项与新覆盖提交契约 1 项；公共输入页真实点击及旧覆盖保留 1 项；导航 25 项（含三个独立游戏返回、跨游戏阻断、失败重试及 Hub 诊断入口）；场景准备失败回滚 1 项；Loading 上层遮挡与状态恢复 1 项；本地存储 6 项；热更程序集筛选 8 项；测试程序集边界 5 项；资源服务 5 项；UI Prefab 规范 1 项；无人机可移植边界 1 项；Pages 发布 10 项。
- 卡丁车定向测试：音频规则 6 项；真实 Listener 非零音频输出、暂停恢复及重叠音频所有者释放 1 项；键盘暂停、重启、返回 1 项；四章大奖赛、领奖返回 1 项；100 个原始音频资源完整性 1 项。真实输出测试证明捕获到声音，不能替代主观听音和 Web 容器验收。
- Jenkins 新任务 `SleepyDemos-TapTap` 已在本机创建；现有任务保留；节点单执行器用于两任务串行。尚未运行远端 SCM 流水线，本分支未推送。
- PowerShell 7 与 Windows PowerShell 5.1 语法检查通过；平台/目录映射和嵌套 Pages 发布路径校验通过。原生旧发布路径保持。

未运行全量回归，未使用性能分析器，不声明未经测量的帧率改善。

## 平台与发布配置

纸艺卡丁车 MiniApp ID：`tapmciplkfbcaae6f3`。从用户已登录的 TapTap 配置中心读取，未读取密钥。首次读取时后台没有 H5 基础配置和已上传小游戏包；后续发布与上传结果见末节。

小游戏大资源根：`https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/TapTap/kagit_kart/player`，Full 再追加不可变资源版本。YooAsset 根：`https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/TapTap/kagit_kart/hotupdate/WebGL`。非秘密本机配置放仓库外 `E:\Jenkins\machine.json`。

首次构建阶段默认仅本地归档；之后用户单独授权发布这批 Pages 资源及上传开发包，不授权提审、上线或推送源码。Release 转换、实际下载/缓存/更新、Android/iOS 容器、触屏与音频、前后台恢复、连续存档，以及受影响原生 IL2CPP Player 结果须分别记录，不能以 Editor 或 C# 编译代替。

首次实际 WebGL Full Prepare 在 Unity 发布后处理阶段报 `Build target 'WebGL' not supported`：模块安装在已运行的 Editor 之后，该进程尚未加载发布扩展。对应失败结果已留在本机 `Library/ReleaseValidation/prepare-before-module-restart.json`。切换目标和 C# 编译成功不能代替发布模块已加载；需重启该 Editor 后重新执行 Prepare。SDK 自动生成的 `Assets/TapTapMiniGame`、`Assets/TJ-WASM-SDK-V2` 不纳入版本管理，完成本地验证后清理；实际复制的 `link.xml` 内容参与构建兼容指纹。

重启并修正 SDK 两个孤立 `[Preserve]` extern 后，源码 `8947b03f` 的实际 Full Prepare 成功，完整 WebGL wasm 为 79,633,461 字节。随后 SDK Build 覆盖 Unity 6 默认导出列表，将 wasm 裁成 523 字节，缺少 `__main_argc_argv` 和 SendMessage；该轮无效转换已中止，未生成或归档有效小游戏 ZIP。失败结果保存在本机 `Library/ReleaseValidation/invalid-sdk-empty-wasm-result.json`。不能把这次 Prepare 或 SDK 中间产物当成小游戏通过。

修复采用增量导出 SDK C 函数、共用 Prepare/PerformBuild 设置，并在压缩前读取实际 wasm 导出段阻断空壳。标准 Brotli 用真实 36,783,246 字节 data 做压缩/解压往返，SHA256 一致，质量 11 压缩约 85 秒，产物 20,451,069 字节；此结果仅证明本机压缩完整性，不代表宿主解码通过。原自定义 MT 压缩在中止时仍有 CPU 活动，没有认定为死锁。

补充审查发现，删除 DLL 暂存资源同时删除 `.meta` 会使跨游戏重建的 GUID 和 Bundle 哈希漂移。现已固定生成身份，并复用现有测试文件验证删除重建后 GUID 稳定、已有合法元数据保留（`9454338a`，1/1）。此项补充了原先同一个无人机 Profile 前后构建的验证范围；真实跨 Profile Bundle 往返结果另行记录。

源码 `929ec702` 的修复后 Full Prepare 成功：实际 wasm 79,699,646 字节，导出包含 Unity 入口、SendMessage 和 SDK 三个 C 辅助函数。此项仍不表示最终 SDK ZIP 或容器通过。

随后补齐真实按需资源边界：Bank 原 GUID 保留，70 个短音频引用与 30 条音乐地址分离；章节资源按章、音乐按 Clip 打包。100 个音频增加 WebGL AAC/DecompressOnLoad/预加载覆盖；逐文件核对原 GUID、默认原生设置与其它元数据保持。迁移使用平台转换后的采样设置；直接复制 defaultSampleSettings 的采样率模式会被实际导入器拒绝，不能仅看菜单 REST 的 accepted/success。

补充验证：程序集与分包规则 `9aa0e80a` 14/14；100 音频完整性 `d7c7570e` 1/1；真实输出、音乐句柄淘汰及重叠所有者 `1865652b` 1/1；四章至领奖返回 `2eba3dbf` 1/1。首次重跑因仍使用 OnlineDownload 构建配置，在 SetUp 大厅启动阶段失败（`7ba24289`）；恢复 Hub EditorSimulateMode 后重跑通过，没有删除断言。

发现已打开的 HotfixBuildWindow 会逐帧把自动化 URL 覆盖为 Hub 偏好，现为运行/编译/构建/自动化期间只读，独立游戏保留请求 URL，Hub 仅真实编辑时同步。最终 Prepare/Build 需重新核对隔离地址，不能沿用已被窗口覆盖的准备现场。

公共词表原有的 96 条 Demo 私有输入文案已分别迁入无人机 64 条、卡丁车 30 条、夹爆它 2 条的私有词表。迁移前后全部 342 条 Key、FontId、中英文序列化内容逐条完全一致；原公共词表 GUID 保留。正式导航在加载目标场景前登记，实际卸载后释放，Editor 直启等待 Bootstrap 就绪后读取。语言租约测试 `1f1f42b9` 4/4、导航 `37bb22a5` 28/28、场景运行与失败恢复 `bd457c54` 4/4 通过。本轮新增 Core API，旧完整包基线不可直接热更，须分别重建 Full。

运行测试过滤器首次误写为不存在的命名空间（`4c398952`）未执行用例；随后正确过滤的卡丁车流程 `2e9e710f` 因 Editor 原生菜单路径与当前 WebGL 资源剔除配置不一致而失败。后续应在 Windows 目标验证原生菜单，WebGL 独立验证小游戏单机入口，不把上述失败计为通过。

切回 Windows 后，真实 Hub→卡丁车→键盘开赛→暂停→重赛→返回流程 `ef53aae6` 1/1 通过，11 组资源依赖重新检查通过。源码 `ca98e7a5` 重新执行私有音频源码变更的三轮 DLL 编译：公共 Hotfix 与无人机 SHA256 不变，卡丁车变化，源码恢复。随后实际 Drone→Kart→Drone 五轮资源构建通过：70 个无人机 Bundle 的名字、SHA256 和资源 GUID 集合不变；Meadow 赛道修改仅改变该章节 Bundle；私有 `Input.Kart.DriftSteer` 英文修改仅改变 Kart Data Bundle；30 条音乐逐 Clip 打包且章节依赖无串章。所有探针修改按原始字节恢复，证据 `Library/ReleaseValidation/profile-bundle-roundtrip.txt`。

资源往返验证首次因 SBP 卸载了临时工具缓存的 Unity 资产引用而在恢复步骤失败；修正临时工具为重新加载引用、独立恢复各文件，保留首次构建证据后完整重跑通过。临时工具随本轮结束清理，不作为生产框架保留。

首次 PC Full Prepare 进入实际 UnityLinker 后失败：SDK 预编译 `TapExtManagerHandler` 仍被原生导入，保留字段引用了已按平台排除的 `TapExtWrapper`。结果保留为 `Library/ReleaseValidation/native-before-sdk-plugin-isolation.json`。修正所有 Runtime/Plugins 托管插件的原生平台导入边界，保留 SDK 所需 Editor/WebGL 范围；可选程序集 link.xml 增加 ignoreIfMissing。asmdef 已排除不能证明预编译插件同时排除，后续以完整包重跑结果为准。

源码 `8be9b0de` PC Full Prepare 成功，原生裁剪不再依赖 SDK；随后的 Build 因原生请求缺省 TapTap 字段为 null、Prepare JSON 中为空字符串而被输入校验阻断。记录 `Library/ReleaseValidation/native-before-optional-taptap-fields.json`。Build、Hotfix 基线和 Deploy 统一仅在 TapTap 渠道核对专属 AppId/CDN；其它平台输入、源码与指纹校验保留，继续重跑正式入口。

源码 `88201a72` 的 PC Hub Full Prepare 与正式 Build 均成功（OnlineDownload、IL2CPP x64、Publish=false）。输出 `E:\Project\SleepyDemos-Build\Validation\PC\hub\local-20261015-native01`，包含实际 SleepyDemos.exe、资源集合、built.json 与候选基线；最终 result.json 为 Success=true、Stage=Build。未发布对应 CDN 版本，未将此结果声明为 Player 在线启动、联机或 DLSS 运行通过。

切换 Android 首次编译遇到 Unity API Updating Consent：旧 gradlePath 需更新为 AndroidExternalToolsSettings.Gradle.path。Unity 自动更新遗漏命名空间导致两处 CS0103，已补全 UnityEditor.Android 命名空间；SDK/NDK/JDK/Gradle 原偏好快照与 finally 恢复继续保留。本变更仅位于 UNITY_ANDROID 编辑器分支，未改变前述 PC Player 代码。

源码 `a0dc1930` Android Hub Full Prepare 与正式 Build 成功（OnlineDownload、ARM64 IL2CPP、非 Development、Debug 签名、Publish=false）。输出 `E:\Project\SleepyDemos-Build\Validation\Android\hub\local-20261015-native01`；实际 APK 67,712,309 字节，apksigner 验证 v2 签名有效，aapt 确认仅 arm64-v8a、minSdk 26、targetSdk 36、版本 0.1.0/1。未连接真机，未执行安装、触控、音频或原生联机验收。

PC 91 份、Android 85 份裁剪后 AOT DLL 均不含 TapTap SDK 程序集；Android 完成后逐项核对 8 个工具偏好键的存在性和值，全部恢复。WebGL 回切发现 TapExtManagerHandler 原始 Editor 标记在关闭 Any Platform 后不足以支持 TapExt.cs 的 UNITY_WEBGL 编辑器分支，已显式打开其 Editor 导入；原生 Player 排除不变。编译 `7d8225f7` 通过，并由导入器 API 重新验证 Editor/WebGL/原生范围。失败记录 `Library/ReleaseValidation/taptap-before-editor-handler-import.json` 保留。

源码 `7306c693` 最终 WebGL Full Prepare 成功：实际 wasm 79,773,797 字节，SHA256 `9e8ecb2847ae8cdd1f6bf7cc23670bf006be6c92da3e671b16e6f8eff6a53219`，含 Unity 入口、SendMessage 与三个 SDK 辅助导出。HTTP 客户端 900 秒超时后，Unity 继续完成并写出成功结果；未重复提交同一准备过程。

该源码的 SDK 实际转换生成 game.zip 12,808,205 字节、game_wasm_split.zip 19,110,246 字节，wasm.br 12,606,221 字节，data.br 20,466,571 字节；最终 Build 被 CDN 白名单校验阻断，因请求用 `/` 而 Windows 文件枚举目录用 `\`，直接字符串比较误判无 data。文件真实存在，失败记录 `Library/ReleaseValidation/taptap-before-cdn-path-normalization.json`。公共 GetPlayerFiles 统一使用现有 NormalizeDirectory，并扩展原 Pages 用例覆盖混合与尾部分隔符；最终成功结果另记，不以 ZIP 存在替代整个 Build 通过。

当前实测 wasm.br 超过 10M，按 [TapTap 官方分包流程](https://developer.taptap.cn/minigameapidoc/dev/tutorial/performance/launch/wasm-subpackage/)必须开启 Wasm 分包。仅生成或上传 split ZIP 不等于完成；仍需开发包上传、iOS/Android 函数采集、生成并提交分包和未采集设备自测。本轮未发布 CDN、未上传开发包，设备验收未完成。

CDN 路径归一化后，原 Pages 定向测试 `6d33da64` 13/13 通过，覆盖本次失败的正反斜线混用、目录尾分隔符以及平台 ZIP 拒绝规则；未新增测试文件。正式 Build 继续按新提交重跑，不跳过 data/白名单检查。

源码 `e6847fe9` 的最终 TapTap Full Prepare 与正式 Build 均成功，最终 `result.json` 为 Success=true、Stage=Build，`built.json` 已冻结。输出 `E:\Project\SleepyDemos-Build\TapTap\Artifacts\kagit_kart\local-20261015-release01`。实际 938 个资源不含无人机、夹爆它及诊断资源，热更仅 Hotfix.dll 与 Hotfix.KagitKart.dll；129 个 YooAsset 文件共 107,261,640 字节，7 个 Player CDN 文件共 20,574,199 字节，均与冻结 SHA256 一致。

最终 game.zip 为 12,808,205 字节（SHA256 `b62fe74fe5a1f2eb55132bfede9b1ad5594e93a9351e403cacff3be1719e201f`），game_wasm_split.zip 为 19,110,246 字节（SHA256 `6d727b5f0d7de89eb6d03101ae45640744546b02a2a966767b8311514a8cd560`），ZIP CRC、包内 MiniApp ID 与隔离 CDN 地址检查通过。最终 wasm 为 79,704,166 字节，压缩为 12,606,221 字节，包含 Unity 入口、SendMessage 及 SDK 三个导出；data 为 36,782,847 字节，压缩为 20,450,730 字节。两份 Brotli 解压 SHA256 均与原始文件一致，临时解压文件已删除。此项仍不代表分包、宿主解码、启动或真实音频通过。

收尾已恢复原 Windows、Hub、EditorSimulateMode、后台运行选项、原有项目选项与构建 DLL/StreamingAssets 暂存；正式 Collector 仅保留新的 ReleaseDemoPackRule。删除一次性 ReleaseMigrationTemporary.cs/.meta、已应用候选副本和 SDK 自动生成目录；保留 Library/ReleaseValidation 中恢复快照、证据及 RecoveredScenes 用户场景备份。用户原有未跟踪 Assets/Plugins/Streamline/x86_64/ 未改动。清理后 Editor 编译 `aaa89f03` 通过；未主动检查 Hot Reload，未推送源码。

最终恢复刷新编译 `e3d065d2` 通过。实际构建目标已为 StandaloneWindows64，但 SDK 留下的 selectedBuildTargetGroup 仍是 WebGL；在现有 TapTapBuildAdapter 的 finally 中补充保存/恢复该面板选择，不更改构建流程或运行时代码。此编辑器收尾补偿未重新执行耗时的完整 SDK 转换，前述 Full 产物与源码 `e6847fe9` 的对应关系保持。

构建面板选择已恢复 Standalone，场景列表仅正式 AppEntrance；状态补偿代码编译 `0050cb7a` 通过，一次性恢复脚本删除后的编译 `84778692` 通过。项目设置仅保留安装 WebGL 模块后 Odin 自动补齐的平台宏，这一行在 Editor 重载后会自动写回；没有留下 TapTap 发布宏、独立游戏配置或签名数据。未运行全量回归，所有平台上传、CDN 发布与真机阶段仍待明确授权及设备。

## 后续授权发布：Pages 完成，平台开发包受阻

用户随后明确授权“发布 Pages 并上传开发包”，并说明暂时没有设备；授权不包含提审、上线或推送项目源码。复用 `local-20261015-release01` 冻结产物只执行正式 Deploy，没有重建、改写 SourceCommit / ConfigCommit（仍为 `e6847fe908514a3bf500be85ad6b3d60cb94c0e8`）或替换配套 ZIP。

首次 CDN 发布提交 `a2d206b` 后，HTTP 校验阻断于 `StreamingAssets/ServerConnection.json`：原文件为 43 字节，Git 的 `core.autocrlf=true` 将提交内容转成 42 字节，远端 SHA256 与冻结记录不同。没有跳过验证或改写冻结清单。Pages 仓库提交 `7a1f178` 增加 `unity/SleepyDemos/TapTap/** -text`，重新暂存该文件以保留原字节，未改变原生目录。重试正式 Deploy 后，资源内容提交 `a16f0cc`、版本激活提交 `033c934` 均完成 Pages 部署。

最终 `result.json` 为 Stage=Deploy、Success=true、PlayerCdnPublished=true、Published=true、Activated=true，Failure 为空，未回滚。7 个 CDN 文件与 129 个 YooAsset 文件全部通过真实 HTTP SHA256 校验；再次逐项对照 built.json 的相对路径、大小与 SHA256，136 条均一致。提升后的平台基线 baseline.json 与本批归档候选的 SHA256 相同。

- Player CDN：`https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/TapTap/kagit_kart/player/local-20261015-release01`
- YooAsset：`https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/TapTap/kagit_kart/hotupdate/WebGL`
- 线上 `DefaultPackage.version` 实际返回 `local-20261015-release01`。
- 产物与最终结果：`E:\Project\SleepyDemos-Build\TapTap\Artifacts\kagit_kart\local-20261015-release01`。

TapTap 上传尚未成功：应用 `971575`（纸艺卡丁车）标记为“创意工坊”；新版 H5 管理页两次选择 game_wasm_split.zip 后均停在 100% / 包体解析中，基础库和函数数未解析出来，提交按钮不可用，退出时没有生成开发版本。补齐最低支持基础库 `2.15.0` 后重试仍未完成；旧版基础配置也已读回该值，最低可用游戏版本保持未设置。

旧版同应用仅显示 H5 包体，上传要求为解压后一个含 index.html 的文件夹，不适用于本批 Unity SDK Tap 小游戏 ZIP；没有在该入口提交、伪装 H5 包或另建应用。按[官方形态说明](https://developer.taptap.cn/agents/)，H5 与 Tap 小游戏属于不同形态。新版解析停滞的具体服务端原因未确认，不能只凭现象断言是权限错误。已询问用户是否有普通 Tap 小游戏应用入口或该应用的包型开通情况；若最终需要更换 AppId，须使用新 Full 版本，不能修改本次已发布的不可变版本。

截图证据保存在 `Library/ReleaseValidation/taptap-upload-entry.jpg`，基础库设置不表示容器兼容已验证。暂时无设备，未进行扫码运行、Android/iOS 函数采集、实际生成 Wasm 分包、触屏或听音验收；仅上传 split ZIP 也不能替代这些步骤。未提审、未上线、未推送源码分支。

发布收尾恢复 Windows / Standalone、Hub EditorSimulateMode 与原项目设置；删除临时 ReleasePublishTemporary.cs 及 .meta。正式刷新编译 `06f23655` 完成且无错误，场景仍仅 AppEntrance。没有新增永久测试文件或运行全量回归，用户原有 Streamline/x86_64 未跟踪目录保留。

## 沿用创意工坊：改为标准 H5

用户明确选择继续使用应用 `971575`，本轮因此改为标准 Unity WebGL H5。`620d16e8` 完成运行时、编辑器构建和 Jenkins 接线；`b334bb1c` 补充冻结归档大小与输出边界检查。移除嵌入的小游戏 SDK，保留原有 Adapter、平台桥和资源 GUID。当前不再要求 MiniApp 初始化、Wasm 函数采集或独立 Player CDN；旧 SDK 产物、线上目录和基线作为历史保留。

浏览器存储通过 `Application.identifier` 隔离 localStorage 键，完整记录使用单次 setItem 替换，写入失败保留旧记录；沿用 LocalDataManager 业务入口。浏览器前后台事件接入原暂停/焦点路径，释放输入，并暂停/恢复 WebAudio Context；手势解锁仍由锁定 Unity 版本的正式实现负责。没有新增广告、支付、账号后端或存档存储框架。

Unity 编译 `f12a2c28`、`b32cc9ff`、`7cc5b88e` 完成且无错误。复用既有 Test Runner：Pages/H5 归档契约 `6c052ce8` 12/12、本地存储 `19fe3506` 7/7，补充输出边界后的归档用例 `98df894a` 1/1。只扩展现有测试文件，保护唯一网页根、入口、嵌套二进制完整性和 ZIP 不覆盖待打包目录的稳定交付契约；未新增永久测试文件或执行全量回归。浏览器桥的 Node VM 定向检查覆盖隔离、损坏/缺失记录、原子写失败、路径拒绝、生命周期注销及异步音频恢复竞态；这不替代实际网页或移动容器验收。

PowerShell 7.6.6 与 Windows PowerShell 5.1 检查了语法、平台/游戏状态目录、H5 基线识别和旧 SDK 基线拒绝、请求继承、PC/Android/TapTap Package 及归档 SHA。TapTap 状态目录为 `TapTap/<GameId>/WebGL/H5`，原生旧路径保持。未运行远端 SCM Jenkins 任务，源码分支未推送；不能把本地正式构建入口或脚本检查当作真实 Jenkins 流水线通过。

本轮 H5 Full 使用源码/配置 `b334bb1cc6b409598be6648498e3590447cbb635`，版本 `local-20261015-h5-release01`。正式 Prepare 和 Build 的 result.json 均记录 Success=true；Prepare 工具等待900秒超时后，Unity继续完成，未重复触发。输出为 `E:\Project\SleepyDemos-Build\TapTap\Artifacts\kagit_kart\local-20261015-h5-release01`，使用独立 H5 基线目录 `E:\Project\SleepyDemos-Build\State\Baselines\TapTap\kagit_kart\WebGL\H5`。

`player/h5.zip` 为40,096,269字节，SHA256 `3998d21daeae118dabffc5c132ccbc9178ddeff2a78391d874af8ab8cbaa0bfa`。23个ZIP条目均在唯一 `game/` 根下，入口为 `game/index.html`，CRC检查通过。wasm Gzip解压为78,506,480字节，SHA256 `b62396f1048f87f416621e7d2a6f7e836fe852dc7ba8bb69ae7c6da8488afb25`，正式构建验证包含Unity入口及SendMessage；data、framework与wasm的Gzip解压完整性均通过。built.json的H5Archive及129个资源文件逐项大小/SHA256与本地产物一致，热更程序集仅Hotfix.dll和Hotfix.KagitKart.dll。

应用971575的旧版H5入口已实际接收并保存此ZIP。上传显示成功后点击确认，包体列表为1条：`h5.zip`（38.239MB），更新时间2026-10-15 22:03:14，最低支持库2.4.0，并提供“自测”按钮。点击后显示“使用最新版TapTap扫码自测”，不是旧SDK的解析中状态。界面未提供可读包体ID，没有编造ID。成功列表截图为本机 `Library/ReleaseValidation/taptap-h5-upload-success.jpg`。没有扫码设备，未提审或上线。

首轮桌面H5冷启动实际显示54%的AOT元数据阶段、95%的主菜单阶段，并进入单机封面/菜单和赛道演出；UnityCache日志确认Pages Bundle下载后写入浏览器缓存。随后实际设置写入报“The game application identifier is required.”，发现WebGL的Application.identifier为空。首轮上传及资源发布成功不等于持久化验收通过，release01不作为最终可交付版本。

修复提交 `977f32480d04868b603230d89c8dc50980fe7a6e`：PrepareRuntimeSystem在玩家设置初始化之前，直接使用统一配置的GameId初始化TapTapStorage；不读取平台未提供的identifier、不从网址或显示名推断身份。桥仍拒绝空GameId，单次原子替换及配额失败保护保留。Unity编译 `bedd7b4a` 完成且无错误。新Full版本为 `local-20261015-h5-release02`，不改写release01冻结记录或旧ZIP；实际重载保存验收另记。

release01的正式Deploy已完成：Success=true、Stage=Deploy、Published=true、Activated=true；129条真实HTTP大小/SHA256记录逐项与built.json一致。资源共107,261,474字节，版本入口返回local-20261015-h5-release01，提升后H5基线SHA256为`e3908f85d34e650cdf432624a272d7182f0e5f3cb73a1c5d2e710878cec6e545`。Pages内容提交`c3b120e`、版本提交`0effed9`；未推送项目源码。

release02 的正式 Deploy 与平台上传均完成：129条HTTP核对与built.json一致，Pages内容提交 `73c3cb7a99e256e890e83aeccf6c8d9025a2fe41`、版本提交 `38c958b1cd84c283a573b86f03c8f82525cc6ca3`。平台最新条目 `h5.zip` 38.245MB、2026-10-15 22:40:48、最低支持库2.4.0。ZIP为40,102,742字节，SHA256 `c683d3efaaaf9dd96b3579dc97da5201b8f0ce806acc1bdd77aab586a24f2eb2`。在独立本地验收origin8767，实际关闭镜头抖动后reload，经封面重新打开设置仍保持关闭；新启动无存储警告。UnityCache实际出现revalidated and served from the browser cache，验证热缓存复用。没有清理用户试玩origin8766的设置。

独立包Hub入口修正提交 `2f4a9dcea071202ec4b0a26aae22d3bae9415f22`，隐藏六处卡丁车菜单/暂停页及无人机、夹爆它的Hub退出入口，保留游戏内返回。Unity编译 `8004f6af` 通过，Hotfix01正式Prepare与Build通过，但尚未发布。随后用户反馈漂移火花和加速尾流消失，暂不发布这份仅菜单修复候选。

## WebGL 比赛特效修复

三个FX Shader原先仅有target4.5，Bits/Cards使用StructuredBuffer，RaceFx统一使用DrawMeshInstancedProcedural；普通WebGL2不支持此路径。构建报告确认Shader、材质与模板资源已经入包，业务事件和Advance并未删除，根因是绘制能力边界。`9b5b21b26bddcc8160e5ab2fb272dbe20aeeea11`保留原生GPU快路径，为WebGL/低Shader等级增加模板扩展的动态Mesh和target3.5 SubShader，复用原运动/着色公式，覆盖火花、加速火焰、速度线与纸卡。按脏区间上传状态，重绑清池、销毁释放；不改规则、音效、Prefab或GUID。Unity正式编译 `ffb16fcc` 完成、0错误。

`local-20261015-h5-hotfix02` 的正式Prepare被基线缺失AOT成员（含SubMeshDescriptor.set_vertexCount）拦截，result.Success=false，未Build/发布。未绕过检查；改为新的Full `local-20261015-h5-release03`，source/config均固定9b5b21b26bddcc8160e5ab2fb272dbe20aeeea11，旧候选与已发布版本保持不可变。实际出图与新Full发布结果随后记录。

release03 Full Prepare/Build/Deploy均成功，Published=true、Activated=true；129条HTTP文件逐项大小/SHA256与built.json一致，线上版本入口实际返回local-20261015-h5-release03。Pages内容提交 `552c41bd63ca2e15452229d78d78ce9a4b550616`、版本提交 `d8560dce6bd14b1e14f9e73065904eba7a24537d`。提升后的H5基线与候选逐字节一致，SHA256 `1250a1584652e52292a7d480f219f5f626209d0454102aa91f9f27de4f624fe6`。Prepare的REST等待900秒超时后Unity继续完成，未重启或重复触发。

ZIP `player/h5.zip` 40,099,380字节，SHA256 `8f0b8a873bafae8e8b0db59991082ee85897386b8571a9e6df7feae7cdf27da7`；23条ZIP项的唯一网页根、CRC、3份Gzip解压完整性通过。wasm解压78,507,706字节，SHA256 `58c337036f9fb17f5e259991ccba6cb94836002aa9e7bf86bd1981e41fcad0d1`。构建报告仍为938资源、126 Bundle，三份FX Shader均收录，无其它Demo或DLSS诊断资源。最终构建的AOT元数据复查通过。

应用971575已实际保存新ZIP，最新列表38.242MB、2026-10-15 23:33:05、最低支持库2.4.0，共三条，原包保留历史。截图 `Library/ReleaseValidation/taptap-h5-release03-success.jpg`。新Full网页已穿过95%菜单加载进入封面/模式页，Hub入口确实隐藏，未出现新的warn/error；同origin8767的新包仍读到上一包保存的镜头抖动关闭。实际特效驾驶结果随后记录。

release03桌面网页实际验收：比赛Loading显示“完成比赛首帧”95%后进入比赛；单场首章120g，重赛后受控按住油门、转向+漂移，蓄力提示出现，松键后蓝色火花/尾焰、屏幕速度线真实出图。截图 `Library/ReleaseValidation/kagit-kart-webgl-fx-boost.jpg` 保存了暂停恢复后的6.95秒画面。纸屑、尘迹及纸鹤救援也在运行中显示。手动暂停/继续、重赛、返回封面、新包reload启动通过；新包热缓存日志同时包含重新下载和revalidated命中，未声称全部命中。两次启动及驾驶期间无新增warn/error。已恢复验收origin8767的减少动态=系统、镜头抖动=开、自动油门=关；没有清理或覆盖用户试玩origin8766的偏好。

内嵌浏览器标签切换后，未观察到可确认的后台暂停结果；不能把它记为生命周期或后台音频验收通过。JS桥的注册/注销、音频恢复竞态仍仅有先前定向VM检查，真实前后台、移动容器、主观听音和配额失败保留未验收。无手机设备，未扫码、未提审、未上线；项目源码未推送，未运行真实远端SCM Jenkins。最终原生Player没有因本次FX调整重建，既有原生构建通过记录不冒充最终源码Player验证。

发布后切回StandaloneWindows64，恢复任务前292份项目设置、Hub配置、Codes和StreamingAssets快照及7份生成/导入改动，删除临时ReleaseH5Temporary.cs及.meta与8份新增构建代码/资源文件。正式编译f4b67764完成、0错误，项目读回SleepyDemos/WindowsEditor/PC_RPAsset。两个临时HTTP服务8766/8767及临时验收标签已关闭；平台包体页保留。用户原有Streamline/x86_64未跟踪目录保留。未新增永久测试文件或使用Profiler，最终相关Test Runner结果随后记录。

恢复后复用现有Test Runner：`Tests.Demo.KagitKartScenePlayModeTests.HubRaceKeyboardPauseRestartAndReturn`（a810fca2）1/1通过；`NativeEightItemsRenderAndReachUnityAudioOutput`（b795ea3a）1/1通过，保护原生道具表现及非零音频输出，不代替Web听感或移动设备。最终Console Error=0，Editor读回isPlaying=false/isCompiling=false。292份快照逐字节哈希复核全部一致，没有PerformanceTestRun临时资源残留；12份本次文档的70个相对链接均存在，diff检查通过。没有执行全量回归。

## 2026-10-16：Android 真机启动停滞排查

用户在 Android iQOO 的 TapTap 自测容器中，每次冷进入均停在“进入热更 / 加载公共设置”47%。当前移动容器启动失败；此前桌面运行记录仍只代表桌面。该提示覆盖 `GameSettingsService.InitializeAsync` 全过程，不能仅凭截图确定卡在存储、字体或显示应用。

源码核对：首次公共设置加载依赖五个 Bundle，合计约6.29 MiB；普通 Web 的默认文件系统没有预下载策略，前面的下载阶段并不代表这些 Bundle 已真正下载。YooAsset 3.0.6 的 Web AssetBundle 请求没有超时上限且使用无限重试，网络或缓存失败可一直保留此画面；这是候选原因，尚无该手机请求日志确认。存储读取是同步调用，异常回默认；制作默认关闭 DLSS。

为取得真机证据，从冻结 release03 派生独立诊断包 `E:\Project\SleepyDemos-Build\TapTap\Artifacts\kagit_kart\local-20261016-h5-diagnostic01\h5-diagnostic01.zip`：只修改 `game/index.html` 并新增屏幕日志脚本，原有另外22条归档内容逐字节不变，包括 wasm/data/资源。记录 GET/HEAD/XHR、响应头状态、未返回响应头的等待时间、Console 错误与未处理异常；HTTP 200 不表示响应正文或资源加载完成。不读取玩家存档、不上传诊断日志，不改 Pages、资源版本入口或正式基线。日志有条数与长度上限，可折叠。

诊断 ZIP 为40,130,610字节，SHA256 `86c042a23d89a33f07ec621169d8606085d9f0562ca1e37081e3c38a8918d4f7`。ZIP CRC、原始文件一致性、JS语法与定向VM检查通过；VM覆盖响应对象保持、拒绝传播、请求方法记录、错误显示、折叠和日志上限，没有新增永久测试文件。应用971575已保存为最新开发包：`h5-diagnostic01.zip`，38.272MB，2026-10-16 00:15:03，列表共四条；未提审或上线。

桌面本地首次运行诊断副本时，另观察到95%“加载游戏主菜单”的 `RuntimeError: memory access out of bounds`；随后原始HTML和诊断HTML热缓存启动均进入封面，无新增错误。匹配的Wasm没有函数名、DWARF或符号映射，仅能确认栈顶非法读取，尚无证据直接归因FX，也不能与真机47%认定为同一问题。当时未更改运行时源码、未重建Unity或执行全量测试，等待真机诊断反馈后再确认修复范围。后续复查确认95%覆盖整段场景导航，封面前已并行启动比赛预热，可能执行RaceFx绑定和Mesh上传；不能据“尚未进比赛”排除该路径。

### 真机反馈与 Full04 候选修复

用户已提供 D01 真机截图：约20.7秒时配置、字体材料及中文字体 Bundle 返回 HTTP 200，画面仍停在47%。D01只记录响应头，不能据此认定正文、Unity解包或公共设置已完成；尚未取得真机异常栈。ADB当前没有连接设备。

D02 对照包只对 Bundle 关闭 Unity 自定义 IndexedDB/Cache API 缓存，保留普通浏览器 HTTP 缓存；其余22条原有归档内容逐字节不变。ZIP为40,130,626字节，SHA256 `2a14db94ab1ef00c5a8bc3d521e509a7f8510c81837cdb98764b245c13dc5490`。应用971575已保存 `h5-diagnostic02.zip`，38.272MB、2026-10-16 00:43:56，列表共五条。该包不改变Pages版本、资源或正式基线，真机结果尚待反馈。

候选源码 `421488ef6e6ca3002a2285b5fc47a5b726aca07e`：H5 Bundle跳过Unity自定义缓存层；YooAsset Web请求设置120秒超时并关闭无限重试；公共设置报告真实初始化步骤，启动异常更新为失败状态；TapTap流水线默认OnlineFull，将完整首包资源与BuiltinCatalog随H5交付，仍保留版本查询及后续更新。120秒约束针对网络请求，不承诺资源解码等完整流程的硬期限；原生资源路径和玩家存档未改。

补充提交 `a0eab3ae45e66a877ab5764bf0308640f6989348` 在H5构建时显式启用 `runInBackground`。原设置关闭时，宿主焦点回调可暂停Unity PlayerLoop，而JS网络仍可返回；这是一条有源码依据的停滞风险，但没有该手机当时焦点状态的证据，尚不能认定为已确认根因。真正后台冻结仍由浏览器决定，玩法、输入和音频继续通过既有生命周期暂停。

Unity正式编译 `c78e9cf9`、`29d24412`、`4c302b71`、`64b0d3f0` 均完成且无错误。复用既有Test Runner：`Tests.Module.ResourceServiceTests`（588274cd）5/5；`GameSettingsServiceTests.SharedInitializationAndSavedCandidateDoNotModifyConfig`（7b54cb42）1/1。内联Node检查确认data为must-revalidate、内置及远端Bundle为no-store。没有新增永久测试文件、运行全量回归或使用Profiler。

Full04使用独立普通CI clone `E:\Project\SleepyDemos-Build\TapTap\Project`，并非工作树；PackageCache和HybridCLR工具链均为独立实复制，Library不共享。源码固定a0eab3ae，SleepyConfigs子模块固定 `1549e3662dce158a08ef5ac6f8691a5e02603606`，版本为 `local-20261016-h5-release04`、OnlineFull。主工作区保持原生Hub EditorSimulate配置，306份设置、代码与资源快照逐字节核对一致。构建、发布及实际运行结果另记，不以候选代码或Prepare代替成功交付。

Full04最终Prepare、Build、Deploy全部记录Success=true；Published=true、Activated=true，129条真实HTTP大小/SHA256与built.json全部一致。Pages内容提交 `2da78902a46d21c6fa74cde5571b92563da2888c`、版本提交 `f1460ab97286a1e1e1e039188db0a70f7be06a46`；H5基线SHA256 `4ea357c770bd9405ed2816dc8a1a5c34033913ce7818171de5bfc2ba68c38263`。资源938项、126个Bundle，仅kagit_kart Demo，无其它Demo或DLSS诊断资源；发布资源合107,267,777字节。

H5 ZIP共149条，包含完整126个Bundle及BuiltinCatalog.bytes/json；目录条目逐个与发布Bundle字节匹配，CRC、唯一game根及三份Gzip解压完整性通过。ZIP为139,859,840字节，SHA256 `6a2d2b4755e66a2c0b188f0967e618bb090ea73bdf16a025f33e6355c648dd32`；Wasm解压78,511,051字节，SHA256 `b3bc925e81478daa1350c167558a8e88d259b11adadddc429c08710709ab698c`。上传别名h5-release04.zip与冻结player/h5.zip逐字节一致，未改冻结包。应用971575已保存为最新开发包，133.381MB、2026-10-16 01:27:32，列表共六条；上传凭据截图 `Library/ReleaseValidation/taptap-h5-release04-success.jpg`。未提审、未上线，项目源码未推送。

独立本地origin8768的Full04首次冷启动完成公共设置四步骤，设置配置及字体读取阶段约0.15秒，进入封面、模式/角色/章节选择及首章比赛，无新增warn/error。HTTP服务记录确认公共设置及其五个Bundle依赖由StreamingAssets提供，未把HTTP 200本身当加载成功。随后reload在95%出现一次Wasm越界：function[192430]、offset0x2e58fbc；再reload成功回到封面。截图 `Library/ReleaseValidation/h5-release04-reload-error.jpg`。因此Full04发布成功不等于运行稳定通过，95%偶发崩溃继续调查，手机47%仍待最新包实测，不能把两者认定为同一故障。

最终核对主工作区306份快照仍完全一致，已按精确路径清理本次临时备份；独立CI缓存及正式产物保留，用户原有Streamline/x86_64保留。未执行最终原生Player、真实Jenkins、iOS、后台/听音及全量回归。

### 95% 原生越界：数组形参元数据归属

从独立CI的原始Bee响应文件重放链接，仅将输出定向至Full04的symbolization目录并增加最终符号表。重放Wasm整体哈希与原包不同：CODE区227个函数体改变，其余12个section完全相同；14个相关栈函数体逐字节一致，类型表、函数表、元素表和数据区不变。独立反汇编又核对连续五层直接调用的原包地址与目标，因此可可靠映射这些栈函数，不使用重放产物的绝对地址。证据保留于 `symbolization/build.js.symbols`、`verification.json`、`link.log`；临时重放JS/Wasm已删除，冻结原包未改。

崩溃链为Newtonsoft `DefaultContractResolver.GetCallbackMethodsForType` → `RuntimeMethodInfo.GetParameters` → `Method.GetParamName` → `GlobalMetadata.GetParameterInfo`，末端读取形参typeIndex对应的类型指针时越界。实际AOT磁盘参数表143,059条均合法，不能认定资源或元数据文件损坏。

源码与实际AOT元数据共同确认一条完整错误路径：`ConstructGenericArrayMethod` 对非泛型方法复制原System.Array方法，再将klass改为具体数组；数组image来自元素类型，但复制的方法仍持有AOT metadata handle。例 `InternalArray__RemoveAt(int)` 非泛型、参数起点6330，Newtonsoft的实例非公有方法扫描会枚举它，且在检查回调属性之前先GetParameters。热更元素数组由此按Hotfix image查AOT参数起点，可能读错表；未取得当次崩溃运行时方法名，不能断言具体一定是RemoveAt。

修复提交 `651b36d30901bc1531c518de8181313a8ae5893d`：构建准备定向修正本机HybridCLR生成工具链的单处查询，改用已存在的methodDefinition重载按方法真实归属选表。GUI和CI共用，重复执行幂等，未知实现阻断；兼容标记纳入构建指纹，旧基线必须Full。Unity编译20a4c94e完成、0错误，独立源码复核无阻断项；未用Editor Mono测试冒充原生修复验证。

### Full05 最终交付与回归

新完整包 `local-20261016-h5-release05` 的source/config均固定651b36d3，SleepyConfigs子模块仍为1549e366；使用同一独立CI缓存。Prepare、Build、Deploy全部Success=true，Published=true、Activated=true；129条真实HTTP大小/SHA256逐项与built.json一致，线上版本入口实际返回Full05。Pages内容提交 `d4666757708c4ea7996f821bd861aecd22a29a80`、版本提交 `73eb967602e06483c54f689d1885d260be198e24`，提升后的H5基线与候选字节一致，SHA256 `ecb3df66debb8ad1289e8de0d3f90289d7220de8a148d401e61f5bf017198aab`。新旧指纹确实不同，新指纹包含参数元数据兼容标记。

冻结ZIP为139,848,968字节，SHA256 `8739a33fe87de89c67a49d9818532aaa74f1e7287512b95de1675dc8098891a9`；149个条目、唯一game根、CRC、三份Gzip解压及完整126个BuiltinCatalog条目通过。Wasm解压78,511,096字节，SHA256 `8fe3187f85c9d2c4e59b1decb74af373f9a8218eb3e44a3874cd5153a72d3f9a`。资源仍为938项/126 Bundle，仅kagit_kart，热更DLL仅Hotfix.dll和Hotfix.KagitKart.dll；所有126个Bundle与Full04完全一致。上传别名与冻结ZIP逐字节一致。

origin8769实际完成四次独立Wasm启动：UTC 18:04:05.253、18:04:43.642、18:05:09.190、18:10:04.178，每次均进入封面，无新增warn/error，未再复现95%越界。前三次使用当时线上Full04清单，其126个Bundle与Full05相同；最后一次在Full05线上激活后，从比赛暂停页reload回到封面。中间还通过单场/120g/肉桂狐/首章进入比赛、油门移动及手动暂停。HTTP日志保留于产物desktop-http.log，公共设置及字体从StreamingAssets读取。没有复跑四章、整场结算、听音或所有特效，既有验收记录不冒充本次重测。

应用971575已保存最终最新开发包 `h5-release05.zip`：133.37MB、2026-10-16 02:10:36，最低支持库2.4.0，列表共七条；截图 `Library/ReleaseValidation/taptap-h5-release05-success.jpg`。Full04及D01/D02保留历史，后续应扫码Full05。该手机47%的具体原因尚无完整运行栈，新完整包综合处理首包网络、缓存、焦点和原生反射路径；Android iQOO的新包结果仍待用户实测，不能将桌面回归作为手机通过。

临时网页标签、8768/8769 HTTP服务及符号重放JS/Wasm已清理，诊断符号表、校验JSON、正式产物和独立CI缓存保留。日常编辑器读回WindowsEditor、isPlaying=false、isCompiling=false，Hub/EditorSimulate配置保持；原有Streamline/x86_64保留。未推送项目源码、未提审或上线，未新增永久测试文件、全量回归、Profiler、最终PC/Android原生Player或真实Jenkins验收。

### 后续 Android iQOO 真机反馈

用户随后分别回复D02对照包“好了，可以正常游玩了”，以及Full04“已越过47%／进入游戏”。两项均记为用户真机反馈，原47%阻塞在这两版测试中未再出现；不将其登记为Full05真机通过，也不扩大为四章、前后台或听音验收。

D02包体相比D01仅调整Unity额外Bundle缓存策略及诊断标签，这一结果进一步支持缓存链与原47%停滞有关；但用户未提供具体测试时刻、实际加载的远端资源版本或完整运行栈，仍不能认定唯一根因或细分为IndexedDB、流克隆、跨域HEAD中的某一环节。Full05保留该处理，并包含95%原生反射修复；后续验收使用最新Full05，当前无需因这次成功反馈再改代码或重新构建。
