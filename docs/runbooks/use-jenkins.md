# 使用本机 Jenkins 构建和发布

维护边界见[Jenkins 构建模块](../modules/jenkins-build.md)。默认安装根 `E:\Jenkins`，只监听 `http://127.0.0.1:18082`；Java 与 Jenkins 均不自启动。原计划端口 18081 已被本机 Docker 占用，用户确认改为 18082。

## 前置环境

Unity 6000.3.15f1 及当前用户授权、Windows IL2CPP/Android 模块、Visual Studio 2022 17.x 的 C++ x64 工具和 Windows SDK、.NET 8、Git、GitHub CLI。原生桥接脚本使用 `Visual Studio 17 2022`，不能只安装其他代 VS。现有本机记录为 VS Community 2022 `17.14.37628.2`、MSVC `19.44.35228.0`（VC Tools `14.44.35207`）、Windows SDK `10.0.26100.0`；迁移按对应基线的实际工具记录恢复。PC Streamline 使用现有锁文件和准备脚本；Android 使用 Unity 配套 SDK/NDK/JDK，不把 Jenkins Java 25 设为 Android JDK。

`scripts/jenkins/machine.example.json` 为非秘密配置样例，复制到仓库外的 `E:\Jenkins\machine.json` 后填写实际工具路径。Root 必须为独立无空格绝对目录；Pages.Path 也须是 Root 内独立克隆，不指向日常 Pages 工程。

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
| SourceRef | 分支/Tag/SHA，默认 main |
| Platform / BuildKind | PC 或 Android；Full 或 Hotfix |
| ResourceMode | Full 选择 Offline / OnlineFull / OnlineDownload；Hotfix 继承当前基线 |
| AndroidFormat / AndroidSigning | Apk 或 Aab；Debug 或 Custom |
| Development | 独立控制开发构建，默认 false |
| PlayerVersion | Full 空值读取项目；Hotfix 空值继承基线，填写值必须匹配 |
| AndroidVersionCode | 空值使用 Jenkins 构建编号，可覆盖为合法正整数 |
| ResourceVersion | 空值自动生成唯一版本，已分配版本（含失败任务）不可重用 |
| Publish / Provider | 默认不发布；Pages 或 Ssh |

Offline 内置全部资源、不联网热更；OnlineFull 全量内置并在线更新；OnlineDownload 不内置 Bundle，保留空 BuiltinCatalog 与必要清单，首次下载资源。离线模式选择发布仅上传同批资源，不改变安装包运行模式。

从任务产物下载 Player、资源、日志和摘要。30 次记录与 10 次普通产物自动保留；current 基线独立保存。失败任务也保留已生成产物和阶段日志。

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
| SSH 指纹不匹配 | 向服务器管理员核实，不自动接受新 key |
| 下载失败停止启动 | 修复网络/资源后重启；不得绕过失败进入 Hotfix |

## 备份与迁移

停机后备份 home（含凭据加密材料，master.key 分离到单独受保护备份，恢复时补回 home/secrets/master.key）、非秘密 machine.json、工具/插件版本记录、current 基线、resource-versions 登记目录和必要受保护连接材料；签名 keystore 单独备份。每平台 current 和 resource-versions 独立于 30 次记录/10 次普通产物保留，不随过期归档删除。

继续原 PC 主包热更还需单独备份原独立 CI 工程 `<Root>/workspaces/PC/Assets/Plugins/Streamline/x86_64/` **整个目录**，包括 DLL、`Streamline~` 运行库、许可证及现有 `.meta`/GUID。记录全部文件相对路径、大小与 SHA256；迁移后逐文件核对，不只核对 ZIP。Library 默认重建，不用新生成的 DLL 或 `.meta` 冒充原基线依赖。

新电脑按以下顺序恢复：

1. 安装同版 Java/Jenkins、Unity/模块及记录的 VS2022/MSVC/Windows SDK 等工具，完成构建用户 Unity 授权；恢复 home、master.key、配置和受保护 API 材料，调整所有本机路径。agent secret 先保留在受保护备份位置。
2. 使用 `-ConfigPath` 指定新配置，先 Start 管理端，再登录检查凭据和插件。修改节点 Remote root directory、名称和标签，设置 `SLEEPY_MACHINE_CONFIG`；放好对应节点 secret 后重复 Start，确认 1 执行器在线。
3. 恢复并校验 current 与 resource-versions。先做一次 `Publish=false` Full，准备专用克隆、工具和缓存；该任务不提升 current。然后确认任务结束、CI Unity 已关闭，再将新克隆的 `x86_64/` 目录替换为已校验的原目录，保留运行库、许可证和 `.meta`，复核全部 SHA256。
4. 用原主包版本和 Development 执行 Hotfix 兼容检查，仍由正式指纹拒绝真实不兼容；检查包内配置后再校验发布目标。若无法恢复原二进制/元数据，或真实指纹仍不兼容，必须重新构建并发布 Full，通知旧安装包重装后使用新 current。

保留管理端换构建节点时，仅在新电脑准备工具链、授权和 agent，配置节点标签及本机配置路径；current、resource-versions 和上述原 PC 原生目录一并迁移，仍先准备新克隆再恢复原生目录。跨电脑连接须单独配置网络和认证；loopback 本机设置不会自动变成远程部署。

按[官方备份说明](https://www.jenkins.io/doc/book/system-administration/backing-up/)保护敏感备份，不把 credentials.xml 与其解密材料分离后宣称可恢复。隔离恢复演练不连接真实发布目标；第二台电脑实际验收另行记录。
