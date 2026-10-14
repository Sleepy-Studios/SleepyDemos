# Jenkins 构建与发布

## 职责

仓库中的 Jenkinsfile 调度本机手动 PC/Android 整包、热更及可选发布。PowerShell 管理进程、工具、独立工作区和归档，Core.Editor 复用 HybridCLR、YooAsset 及发布后端。Core.Runtime 仅负责资源失败阻断，不依赖 Jenkins。

本机操作、参数和迁移见[使用 Jenkins](../runbooks/use-jenkins.md)。个人知识库保存完整搭建过程；本文维护当前工程契约。

## 入口与阶段

- `Jenkinsfile`：一个参数化任务，单节点单执行器，串行排队，6 小时总超时、30 分钟发布超时。
- `scripts/jenkins/Manage-Jenkins.ps1`：手动 Start / Status / Stop，无服务或自启动。
- `scripts/jenkins/Invoke-Build.ps1`：固定源与子模块提交、工具准备、分阶段 Unity、PC 目录 ZIP / Android APK或AAB / 资源 ZIP 及失败归档。
- `Core.Editor.Hotfix.HotfixBuildAutomation.Prepare / Build / Deploy`：由 `SLEEPY_BUILD_REQUEST` 指向绝对 JSON 请求，非交互 `-executeMethod` 入口。
- `HotfixAssemblyBuild`：GUI 与 CI 共用程序集识别、AOT 剥离及同步；缺少文件失败，不静默复用旧 DLL。
- `SshBundleDeployment` / `HotfixDeploymentVerification`：共享 SSH 上传及正式 HTTP 校验。

正常流程：固定源码 → Luban/原生依赖 → Prepare 生成程序集 → 新 Unity 进程重新导入 → Build 资源与 Player → 可选 Deploy → 归档。生成物只落在已登记的 CI 克隆和本次产物目录，日常工程不参与 reset 或清理。

每次 Unity 启动先原子记录本次 run、项目、方法、日志、用户和启动意图，再记录 PID/创建时间。失败或取消时，Finalize 核对 Win32_Process 身份并收尾本次 Unity 及能确认归属的子进程，排除授权服务；确认退出和工程解锁后才归档、清理。身份失配或退出未确认则保留现场，仅归档收尾摘要与进程记录。不能以 Get-Process 查不到 PID 代替退出证明，也不按名称终止日常 Editor。

## 请求与结果

请求包含平台、构建类型、资源模式、Development、签名类型、版本、源与配置 SHA、独立输出/基线目录及非秘密发布参数。签名密码、私钥和 GH_TOKEN 从 Jenkins 凭据作用域的环境读取，不写入 JSON。

输出 `prepared.json` 记录准备输入，`built.json` 冻结资源 SHA，`result.json` 记录阶段成败、版本和发布下载证据。`resources/` 是平面的下载白名单，`player/` 保存 Player，`baseline/` 保存整包候选基线。resource-versions 持久登记每个平台已分配的资源版本，失败任务的版本也不释放，不随普通归档过期删除。构建成功不等于发布成功；资源发布成功而基线提升失败须如实记录。

## 平台和资源模式

PC 为 Windows x64 IL2CPP，Android 为 ARM64 IL2CPP，APK/AAB 可选；Development 与调试签名独立。Android Full 明确使用当前 Unity 配套 SDK/NDK/OpenJDK/Gradle，阶段退出恢复当前用户的原工具偏好。同用户日常 Editor 在构建期间会短暂看到这些偏好，不能与 CI 同时手动切换外部工具或启动 Android 构建；强制结束进程可能跳过恢复。

CI 主包与热更 DLL 统一使用 `.NET Framework`（Unity `NET_Unity_4_8`），摘要记录实际 API profile，热更必须与基线一致。Unity 会在 Player 构建中剥除 netstandard 依赖，热更 DLL 若仍直接引用它会失败；遵循 [HybridCLR 官方说明](https://www.hybridclr.cn/docs/help/commonerrors)。此设置只写入独立 CI 工程；继续手工构建 Player 时也须核对 API profile，日常工程设置不会由 Jenkins 改写。

Offline 使用 OfflinePlayMode 并内置全量资源；OnlineFull 使用 HostPlayMode 并内置全量；OnlineDownload 使用 HostPlayMode 不内置 Bundle。日常 EditorSimulateMode 和已有配置资产不会被 CI 改写。Offline 可发布同批资源，但其安装包不消费在线更新。

## 基线与发布边界

每平台只维护一个当前在线主包。候选基线保存实际配套 AOT、补充元数据、程序集、平台/架构/Unity/Development/版本、兼容指纹和 SHA。只有在线 Full 发布及 HTTP 校验成功才提升 current；Offline 和失败任务不替换它。

Hotfix 使用 current 的 AOT 与补充元数据，运行 HybridCLR MissingMetadataChecker；Core.Runtime、Packages、原生插件和主包构建配置采用保守指纹检查。缺基线或不匹配要求 Full，不为旧主包维持并行兼容链。

准备记录与基线同时保存逐文件指纹，失配时报告变化路径。JSON、程序集定义、meta 和 YAML 资产统一文本换行后校验，避免 Unity/ProBuilder 保存时仅 CRLF/LF 转换阻断构建；配置内容与二进制依赖仍完整参与校验。

Pages 沿用 `unity/SleepyDemos/hotupdate/PC|Android`，独立 CI 克隆与凭据，保留下载白名单/路径/大小保护；等待站点部署后逐文件 HTTP SHA 校验。内容与激活共用 22 分钟预算，恢复另给 5 分钟；HTTP 正文和 Git/GH 子进程均支持取消，不能让外层 30 分钟先中止恢复。远端暂仍旧版本但本地激活已提交时，普通推送恢复后继提交作为引用 CAS 屏障；并发或不明状态保留人工处理。相同资源重复发布不创建空提交。

SSH 机器配置的上传根与 HTTP 根均由流水线追加平台；请求中的 SshServerUrl 为最终平台 URL，编辑器窗口的地址接口保持原义。固定主机 SHA256 指纹，内容先上传，清单随后，版本入口最后原子替换；禁止先清空目录。版本入口/恢复操作最多等待 1 分钟，口令私钥从同一 Jenkins 凭据的 Passphrase 读取。旧不可变资源不能以不同内容覆盖。APK/AAB/PC ZIP 不上传到热更目录。

## 验证

按[项目测试政策](../architecture/testing.md)复用相关资源与发布测试，不默认全量回归。真实 IL2CPP Player、包内资源、HTTP 与原安装包热更分别记录；SSH 实发、真机或新电脑未具备环境时明确为待验收。源码写入和 Editor 编译不能代替这些证据。
