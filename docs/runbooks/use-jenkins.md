# 使用本机 Jenkins 构建和发布

维护边界见[Jenkins 构建模块](../modules/jenkins-build.md)。默认安装根 `E:\Jenkins`，只监听 `http://127.0.0.1:18082`；Java 与 Jenkins 均不自启动。原计划端口 18081 已被本机 Docker 占用，用户确认改为 18082。

## 前置环境

Unity 6000.3.15f1 及当前用户授权、Windows IL2CPP/Android 模块、Visual Studio 2022 17.x 的 C++ x64 工具和 Windows SDK、.NET 8、Git、GitHub CLI。原生桥接脚本使用 `Visual Studio 17 2022`，不能只安装其他代 VS。现有本机记录为 VS Community 2022 `17.14.37628.2`、MSVC `19.44.35228.0`（VC Tools `14.44.35207`）、Windows SDK `10.0.26100.0`；迁移按对应基线的实际工具记录恢复。PC Streamline 使用现有锁文件和准备脚本；Android 使用 Unity 配套 SDK/NDK/JDK，不把 Jenkins Java 25 设为 Android JDK。

`scripts/jenkins/machine.example.json` 为非秘密配置样例，复制到仓库外的 `E:\Jenkins\machine.json` 后填写实际工具路径。Root 必须为独立无空格绝对目录；Pages.Path 也须是 Root 内独立克隆，不指向日常 Pages 工程。必填 `WorkspaceRoot` 当前为 `E:\Project\SleepyDemos-Build`，其下 `PC`、`Android` 分别保存 CI 工程和 Library；Jenkins home、runs、基线、版本登记与 Pages 仍留在 `E:\Jenkins`。

`WorkspaceRoot` 须为无空格的本地盘符绝对路径，不能是盘根、日常工程或其祖先/后代，路径不得经过 Junction/符号链接。不要手动打开 CI 工程；日常开发工程可保持打开。两者同属当前 Windows 用户，Android Full 期间不要手动切换 External Tools 或同时手工打 Android，独立工程不代表用户偏好隔离。

## 手动启停

```powershell
./scripts/jenkins/Manage-Jenkins.ps1 -Action Start
./scripts/jenkins/Manage-Jenkins.ps1 -Action Status
./scripts/jenkins/Manage-Jenkins.ps1 -Action Stop
```

可用 `-ConfigPath` 指定新电脑的配置，该参数只影响启停脚本。Jenkins 节点的 `Configure > Node Properties > Environment variables` 同时设置 `SLEEPY_MACHINE_CONFIG=新机器配置文件的绝对路径`；否则流水线仍读取默认 `E:/Jenkins/machine.json`。启动检查端口和进程归属；停止会查询排队/运行任务，有任务时拒绝停止。不要手动结束正在构建的 Unity。启动 Java 时会提供 Windows PowerShell 5.1 的模块目录，避免从 PowerShell 7 启动后子脚本找不到 Get-FileHash；调用者自身环境会恢复。账号、API 和 agent 连接材料仅放仓库外受保护目录，不复制到文档或公开日志。

## 初次创建任务

1. 按 [WAR 官方步骤](https://www.jenkins.io/doc/book/installing/war-file/)准备 Java 25/Jenkins LTS，并记录版本、来源与校验值。
2. 填好机器配置，先运行 Manage-Jenkins Start 启动管理端，再登录安装 Pipeline、Git、凭据绑定及必要依赖，配置登录与禁止匿名访问。此时 agent secret 尚未放到配置路径，Start 只启动管理端。
3. 管理端执行器为 0；用 `scripts/jenkins/bootstrap/agent.xml` 创建 `sleepydemos-local`，1 执行器、WebSocket inbound。按实际 Root 修改 Remote root directory，并在 Node Properties 设置上述 `SLEEPY_MACHINE_CONFIG`。
4. 配置本机 agent.jar 和受保护的 agent-secret.txt，重复 Start 启动节点。
5. 按 `scripts/jenkins/bootstrap/job.xml` 创建 `SleepyDemos-Build`，从源码仓 main 读取 Jenkinsfile；填写正确源码凭据。
6. Jenkinsfile 必须已存在于配置的仓库提交中。本地提交未推送时，远端 main 不会自动得到新流水线。

TapTap 另按 `scripts/jenkins/bootstrap/job-taptap.xml` 创建 `SleepyDemos-TapTap`，SCM 固定分支仍为 main，脚本路径为 `Jenkinsfile.TapTap`；该文件必须先按授权进入远端对应提交。SourceRef 独立指定要构建的同仓游戏源码，默认 `main`；指定其它分支、标签或提交时必须已推送，才能被 CI fetch。两任务使用同一个 `sleepydemos-local` 单执行器，不修改原任务为多平台混合任务。

本机实际版本、账号文件位置、安装结果和验收记录在个人 Obsidian“Jenkins 自动构建与发布”，不将一台机器的历史结果当通用步骤的通过证明。

## 凭据

| ID | Jenkins 类型/用途 |
| --- | --- |
| sleepydemos-source | 用户名/密码，Git HTTPS SCM，密码为 Token |
| sleepydemos-github | Secret text，GH_TOKEN，CI checkout及Pages |
| sleepydemos-ssh | SSH Username with private key，上传服务器；有口令的私钥在同一凭据填写 Passphrase |
| sleepydemos-keystore | Secret file，自定义 Android keystore |
| sleepydemos-store-password | Secret text，keystore 密码 |
| sleepydemos-key-alias | Secret text，签名别名 |
| sleepydemos-key-password | Secret text，别名密码 |

ID 是 Jenkinsfile 的当前接口，不通过 machine.json 自动改写。缺少所选凭据会失败，不能静默改用其他签名。私钥、密码和 Token 不写进仓库、请求、输出摘要或命令行。

## 每次构建

打开任务“Build with Parameters”：

| 参数 | 选择 |
| --- | --- |
| SourceRef | 分支/Tag/完整40位SHA，默认 main；不要填写短SHA |
| GameId | 原任务默认 hub；独立游戏填写固定源码 ReleaseProfiles 中的正式 GameId |
| Platform / BuildKind | PC 或 Android；Full 或 Hotfix |
| ResourceMode | Full 选择 Offline / OnlineFull / OnlineDownload；Hotfix 继承当前基线 |
| AndroidFormat / AndroidSigning | Apk 或 Aab；Debug 或 Custom |
| Development | 独立控制开发构建，默认 false |
| PlayerVersion | Full 空值读取项目；Hotfix 空值继承基线，填写值必须匹配 |
| AndroidVersionCode | 空值使用 Jenkins 构建编号，可覆盖为合法正整数 |
| ResourceVersion | 空值自动生成唯一版本，已分配版本（含失败任务）不可重用 |
| Publish / Provider | 默认不发布；Pages 或 Ssh |

Offline 内置全部资源、不联网热更；OnlineFull 全量内置并在线更新；OnlineDownload 不内置 Bundle，保留空 BuiltinCatalog 与必要清单，首次下载资源。离线模式选择发布仅上传同批资源，不改变安装包运行模式。

TapTap 任务默认选择 OnlineFull：首包 Bundle 与官方完整 BuiltinCatalog 随 H5 ZIP 内置，避免冷启动逐个跨域下载；后续仍通过 Pages 清单按内容 Hash 更新。OnlineDownload 仍可显式选择，但 Web 默认没有预下载策略，资源使用时才实际请求，不能将下载阶段结束当作所有资源已缓存。H5 的 Bundle 使用宿主包缓存与普通 HTTP 缓存，通过 cacheControl=no-store 绕过 Unity 额外的 IndexedDB、响应流克隆和跨域 HEAD 链；这里的 no-store 只控制 Unity 缓存层，并未关闭浏览器 HTTP 缓存。Player data 文件仍使用原有 Unity 版本校验策略。

从任务产物下载 Player、资源、日志和摘要。30 次记录与 10 次普通产物自动保留；current 基线独立保存。失败任务也保留已生成产物和阶段日志。

TapTap 任务固定 BuildChannel=TapTap、Platform=WebGL、TapTapFormat=H5、Provider=Pages，默认 GameId=kagit_kart；不提供 Android 签名或格式参数。安装匹配 Unity 的 WebGL 模块，采用标准 Unity H5 构建，不需要小游戏 SDK。工程和缓存位于 `E:\Project\SleepyDemos-Build\TapTap\Project`，持久交付包位于 `TapTap\Artifacts\<GameId>\<任务名-编号>`；失败也归档已生成部分，结果以 summary.json/result.json 为准。其基线、资源版本、资源发布目录均与原生和其它 GameId 隔离。SCM 必须能读取本轮 Jenkinsfile.TapTap 与固定源提交；本地未推送改动不能被远端检出。

本轮沿用创意工坊应用 971575。仓库外 machine.json 的 `TapTap.Apps.<GameId>.AppId` 可记录该应用编号，示例为 971575；这是可选记录，H5 构建不需要原生小游戏 MiniAppId 或 CdnUrl。首次 H5 使用新的 ResourceVersion 构建 Full，在线发布成功后才建立 `baselines/TapTap/<GameId>/WebGL/H5/current`；Hotfix 只继承这个带 H5 格式标记的在线基线，拒绝旧 SDK 小游戏基线。

TapTap 首次验证按顺序进行：

1. 先执行 Publish=false Full，核对 `player/webgl/index.html`、Build/StreamingAssets 等完整网页，以及 `player/h5.zip`。ZIP 解压后仅有 game/ 一个顶层目录，入口为 game/index.html，不要重新套目录或改后缀。压缩包不得超过 300 MB（300,000,000 字节）；核对 built.json 的 H5Archive 大小/SHA、TapTapFormat=H5 和固定源码/配置 SHA。
2. OnlineFull/OnlineDownload 包在平台启动前，需要发布本次配套 YooAsset 资源。确认产物后显式选择 Publish=true 构建/发布正式 Full，使用该次版本配套的 H5 ZIP；已成功归档的固定产物也可按既有恢复流程只执行 Deploy，不改变 SourceCommit、ConfigCommit、版本或冻结文件。Offline 包不消费在线更新。
3. 在应用 971575 的 H5 上传入口上传 h5.zip，以解析完成并出现在开发版本列表为准；上传进度 100% 不能代替解析成功。该包使用 Unity 默认加载页与全屏画布，不执行小游戏 SDK 代码分包或函数采集。
4. 分别在 Android/iOS 容器验证首次启动、触屏、首个交互后出声、切后台静音与回前台、玩家确认继续、存档重启和存储失败反馈；在线包再验证同一 H5 包消费自身基线的 Hotfix。当前单机不使用平台身份，不强制调用小游戏登录；后续若接入身份功能，再按实际 H5 接口和审核要求处理，不将当前实现解释为平台对所有 H5 免登录的保证。

Pages 仅发布 `unity/SleepyDemos/TapTap/kagit_kart/hotupdate/WebGL/` 下的 YooAsset 下载白名单，不上传 H5 ZIP 或额外 Player CDN。GitHub 单文件 100 MiB、站点总量 1 GB 限制仍生效，检查 result.json 的 Published/Downloads 与 HTTP SHA 校验。旧 SDK 已发布目录保留历史状态，不清空、不覆盖，也不与新 H5 资源版本混用。

当前发布状态与验证边界见[热更模块](../modules/hotfix.md#发布目标与-taptap)，各阶段产物、Pages 提交、桌面验证及 iQOO 用户反馈见[H5 验收记录](../agent/archive/demo-release-taptap-2026-10-15.md#full05-最终交付与回归)。平台上传确认、资源发布和实际运行各自记录；不以旧阶段包或桌面结果代替最新包的真机验收。

取消当前任务使用 Jenkins 的 Stop；不要结束管理端或手动杀 Unity。收尾记录 `unity-Prepare|Build|Deploy.process.json`，严格核对本次进程并确认工程解锁后才压缩和清理；收尾失败会保留 `runs/<任务名>-<编号>`，摘要明确失败原因。此时先检查归档进程记录、Windows CIM 进程和 UnityLockfile，不能仅凭任务显示 ABORTED 或 Get-Process 查不到就删除目录。身份无法确认的进程须人工核实，不能按 Unity.exe 名称批量结束。

## 发布

Pages 使用机器配置的仓库、网站根和项目名，平台路径自动生成。新构建用户须在专用 Pages 克隆中配置 Git 提交作者（`git -C <Pages克隆路径> config --local user.name <CI作者名>` 与 `user.email <CI作者邮箱>`）；Jenkins 备份不会自动包含当前用户的全局 Git 配置，不复制个人凭据配置来替代此步骤。推送后必须等待 Pages 部署并核对正式 HTTP 内容，不能仅看 Git push 退出码。当前规则见[Pages 发布](deploy-github-pages-hotupdate.md)。

SSH 配置 Host/User/Port/RootPath/BaseUrl/HostKeySha256；机器配置中的 RootPath 与 BaseUrl 均填写共用资源根，流水线为上传目录和 HTTP 地址同步追加 `PC` / `Android`。例如 `/var/www/hotupdate` 与 `https://cdn.example.com/hotupdate` 对应 PC 的 `/var/www/hotupdate/PC` 与 `https://cdn.example.com/hotupdate/PC`，不要在根字段重复填平台。主机指纹须可信核实。没有真实 SSH/HTTP 服务器时只验证实现和失败保护，不声称实发通过。

## 热更与故障

先成功构建并发布在线 Full，建立对应平台 current。Hotfix 只更新该基线支持的代码与资源；基线缺失、主包版本/Development/架构不匹配或兼容指纹变化时重新 Full。旧安装包跨新基线不受支持。

| 错误 | 操作 |
| --- | --- |
| 工作区已被打开 | 关闭手动打开的 CI 工程；不要关闭日常工程掩盖路径错误 |
| 取消后收尾失败 | 按 unity-*.process.json 核对本次用户、路径、创建时间和任务参数；确认进程退出与工程解锁后再清理归属明确的本次目录 |
| 缺工具或授权 | 修正机器工具路径及该用户 Unity 环境 |
| Android 阶段被强制终止 | 用归档 android-tools-before.json 核对原外部工具路径/embedded 开关；正常退出会自动恢复，强制终止后先恢复再构建 |
| 缺凭据 | 在 Jenkins 受保护入口配置对应 ID |
| Pages 分歧 | 人工检查专用克隆和远端，不强推或自动 reset |
| Pages 未提交修改 | 对照失败归档的资源文件 SHA/大小确认来源；只清理确认本次产生且未发布的文件，不执行全目录 clean。已冻结成功产物可通过 Jenkins Replay 仅恢复校验并 Deploy；源提交、全部归档 SHA及 APK/基线必须与原任务一致 |
| SSH 指纹不匹配 | 向服务器管理员核实，不自动接受新 key |
| 下载失败停止启动 | 修复网络/资源后重启；不得绕过失败进入 Hotfix |

## 备份与迁移

停机后备份 home（含凭据加密材料，master.key 分离到单独受保护备份，恢复时补回 home/secrets/master.key）、非秘密 machine.json、工具/插件版本记录、current 基线、resource-versions 登记目录和必要受保护连接材料；签名 keystore 单独备份。目录快照恢复时直接复制 home，按备份清单核对相对路径、大小与 SHA256；历史普通产物可排除，但须明确恢复后对应下载链接不可用。各渠道、游戏和平台的 current 与 resource-versions 独立于 30 次记录/10 次普通产物保留，不随过期归档删除。

继续原 PC 主包热更还需单独备份原独立 CI 工程 `<WorkspaceRoot>/PC/Assets/Plugins/Streamline/x86_64/` **整个目录**，包括 DLL、`Streamline~` 运行库、许可证及现有 `.meta`/GUID。记录全部文件相对路径、大小与 SHA256；迁移后逐文件核对，不只核对 ZIP。Library 默认重建，不用新生成的 DLL 或 `.meta` 冒充原基线依赖。

本机搬迁 CI 目录时，先确认队列空闲、两个 CI 工程关闭并记录原生目录校验值，再整体移动 `WorkspaceRoot` 内容并修改机器配置。移动后逐文件复核原生依赖；将旧 `PC/Library/Streamline/build` 缓存移开，新 Full 重建 CMake，原基线 Hotfix 保留原生文件且不重编译。历史备份仍按原清单校验，不修改旧 manifest；从旧备份恢复后，另行给恢复的 machine.json 补上 `WorkspaceRoot` 并调整实际落点。

新电脑按以下顺序恢复：

1. 安装同版 Java/Jenkins、Unity/模块及记录的 VS2022/MSVC/Windows SDK 等工具，完成构建用户 Unity 授权；恢复 home、master.key、配置和受保护 API 材料，调整所有本机路径，显式填写独立的 `WorkspaceRoot`。agent secret 先保留在受保护备份位置。
2. 使用 `-ConfigPath` 指定新配置，先 Start 管理端，再登录检查凭据和插件。修改节点 Remote root directory、名称和标签，设置 `SLEEPY_MACHINE_CONFIG`；放好对应节点 secret 后重复 Start，确认 1 执行器在线。
3. 恢复并校验 current 与 resource-versions。先做一次 `Publish=false` Full，准备专用克隆、工具和缓存；该任务不提升 current。然后确认任务结束、CI Unity 已关闭，再将新克隆的 `x86_64/` 目录替换为已校验的原目录，保留运行库、许可证和 `.meta`，复核全部 SHA256。
4. 用原主包版本和 Development 执行 Hotfix 兼容检查，仍由正式指纹拒绝真实不兼容；检查包内配置后再校验发布目标。若无法恢复原二进制/元数据，或真实指纹仍不兼容，必须重新构建并发布 Full，通知旧安装包重装后使用新 current。

保留管理端换构建节点时，仅在新电脑准备工具链、授权和 agent，配置节点标签及本机配置路径；current、resource-versions 和上述原 PC 原生目录一并迁移，仍先准备新克隆再恢复原生目录。跨电脑连接须单独配置网络和认证；loopback 本机设置不会自动变成远程部署。

按[官方备份说明](https://www.jenkins.io/doc/book/system-administration/backing-up/)保护敏感备份，不把 credentials.xml 与其解密材料分离后宣称可恢复。隔离恢复演练不连接真实发布目标；第二台电脑实际验收另行记录。

## 本机已验收范围（2026-10-15）

- PC：三种资源模式实际构建，离线仅 EXE 出站阻断后到 Hub；在线全量及原包代码/资源热更到 Hub；在线下载空缓存首次下载到 Hub，原包文件 SHA 不变。
- Android：离线 APK、在线全量 AAB、在线下载 APK实际产出；ARM64/IL2CPP、调试签名和版本通过，在线下载包原生 HostPlayMode/Android URL、空 Catalog 与零 Bundle 已核对。无连接设备，真机三种模式及原包热更待验收。
- Pages：两平台正式 HTTP 逐文件校验、平台隔离、相同内容重复发布无新提交通过。APK/AAB不进热更目录；发布失败的冻结产物已实际只重发而不重建。
- 保护：缺基线、Development不匹配、Core源码变化及缺自定义签名凭据正确拒绝；真实初始化/下载失败阻断 AOT、Hotfix 与业务入口。已有资源测试5/5、发布保护9/9通过，未执行全量回归。
- 待环境：自定义正式 keystore、Android真机、真实SSH服务器、第二台电脑构建；不以调试签名或本机隔离恢复替代这些验收。清理被策略阻止的少量验证目录记录在个人实施日志，临时包体与代码/资源探针已清理。
- 最终停机备份与18083隔离恢复通过：3609文件SHA一致、两个凭据真实解密原值验证一致，current/版本登记/原生文件一致；主密钥和现有调试keystore独立保护，普通历史archive未备份。正式18082已恢复controller0/node1，无自启动；第二台实际构建仍待环境。
- CI 工程已从 `E:\Jenkins\workspaces` 整体迁至 `E:\Project\SleepyDemos-Build`，16 个原生文件逐项 SHA 一致，PC/Android 工程关闭、日常工程继续运行。旧 PC CMake 缓存保留为 `build.before-relocation-20261015`；搬迁后 Jenkins #41 PC Hotfix（Publish=false）实际 SUCCESS，15 项归档大小/SHA一致，资源ZIP 109811676字节；没有 Player 或发布，两个 current 共432文件及16个原生文件保持一致。新路径 PC Full、Android 构建尚未复跑。
- WorkspaceRoot 配置与3份脚本已在 PS7.6.6、Windows PowerShell5.1解析及9种非法路径检查通过；增量配置备份 `E:\Jenkins\backups\20261015-workspace-relocation-config` 与原 `20261015-final` 全量备份配用，不改旧manifest。迁移后 #40 因填写短SHA在检出阶段失败，未启动Unity；#41 使用完整SHA执行不发布的 PC Hotfix，已最终 SUCCESS。

- 旧手工安卓工作树 `E:\Project\SleepyDemos-Andriod_Bulid` 已按授权完成非缓存备份及17542文件逐项SHA校验后移除，原分支保留；备份位于 `E:\Jenkins\backups\20261015-android-manual-before-removal`，仅当前用户和SYSTEM可访问。当时 WebGL 仅完成浏览器接入评估；本轮 TapTap 流水线入口与待验收项见前文。
