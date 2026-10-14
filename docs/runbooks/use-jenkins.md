# 使用本机 Jenkins 构建和发布

维护边界见[Jenkins 构建模块](../modules/jenkins-build.md)。默认安装根 `E:\Jenkins`，只监听 `http://127.0.0.1:18082`；Java 与 Jenkins 均不自启动。原计划端口 18081 已被本机 Docker 占用，用户确认改为 18082。

## 前置环境

Unity 6000.3.15f1 及当前用户授权、Windows IL2CPP/Android 模块、VS C++ 与 Windows SDK、.NET 8、Git、GitHub CLI。PC Streamline 使用现有锁文件和准备脚本；Android 使用 Unity 配套 SDK/NDK/JDK，不把 Jenkins Java 25 设为 Android JDK。

`scripts/jenkins/machine.example.json` 为非秘密配置样例，复制到仓库外的 `E:\Jenkins\machine.json` 后填写实际工具路径。Root 必须为独立无空格绝对目录；Pages.Path 也须是 Root 内独立克隆，不指向日常 Pages 工程。

## 手动启停

```powershell
./scripts/jenkins/Manage-Jenkins.ps1 -Action Start
./scripts/jenkins/Manage-Jenkins.ps1 -Action Status
./scripts/jenkins/Manage-Jenkins.ps1 -Action Stop
```

可用 `-ConfigPath` 指定新电脑的配置。启动检查端口和进程归属；停止会查询排队/运行任务，有任务时拒绝停止。不要手动结束正在构建的 Unity。启动 Java 时会提供 Windows PowerShell 5.1 的模块目录，避免从 PowerShell 7 启动后子脚本找不到 Get-FileHash；调用者自身环境会恢复。账号、API 和 agent 连接材料仅放仓库外受保护目录，不复制到文档或公开日志。

## 初次创建任务

1. 按 [WAR 官方步骤](https://www.jenkins.io/doc/book/installing/war-file/)准备 Java 25/Jenkins LTS，并记录版本、来源与校验值。
2. 安装 Pipeline、Git、凭据绑定及必要依赖，配置登录与禁止匿名访问。
3. 管理端执行器为 0；用 `scripts/jenkins/bootstrap/agent.xml` 创建 `sleepydemos-local`，1 执行器、WebSocket inbound。
4. 配置本机 agent.jar 和受保护的 agent-secret.txt，重复 Start 启动节点。
5. 按 `scripts/jenkins/bootstrap/job.xml` 创建 `SleepyDemos-Build`，从源码仓 main 读取 Jenkinsfile；填写正确源码凭据。
6. Jenkinsfile 必须已存在于配置的仓库提交中。本地提交未推送时，远端 main 不会自动得到新流水线。

本机实际版本、账号文件位置、安装结果和验收记录在个人 Obsidian“Jenkins 自动构建与发布”，不将一台机器的历史结果当通用步骤的通过证明。

## 凭据

| ID | Jenkins 类型/用途 |
| --- | --- |
| sleepydemos-source | 用户名/密码，Git HTTPS SCM，密码为 Token |
| sleepydemos-github | Secret text，GH_TOKEN，CI checkout及Pages |
| sleepydemos-ssh | SSH Username with private key，上传服务器 |
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
| AndroidVersionCode | 空/0 使用 Jenkins 构建编号，可覆盖为合法正整数 |
| ResourceVersion | 空值自动生成唯一版本，不复用版本覆盖不同内容 |
| Publish / Provider | 默认不发布；Pages 或 Ssh |

Offline 内置全部资源、不联网热更；OnlineFull 全量内置并在线更新；OnlineDownload 不内置 Bundle，首次下载资源。离线模式选择发布仅上传同批资源，不改变安装包运行模式。

从任务产物下载 Player、资源、日志和摘要。30 次记录与 10 次普通产物自动保留；current 基线独立保存。失败任务也保留已生成产物和阶段日志。

## 发布

Pages 使用机器配置的仓库、网站根和项目名，平台路径自动生成。推送后必须等待 Pages 部署并核对正式 HTTP 内容，不能仅看 Git push 退出码。当前规则见[Pages 发布](deploy-github-pages-hotupdate.md)。

SSH 配置 Host/User/Port/RootPath/BaseUrl/HostKeySha256；BaseUrl 为平台 HTTP 地址，RootPath 自动追加平台。主机指纹须可信核实。没有真实 SSH/HTTP 服务器时只验证实现和失败保护，不声称实发通过。

## 热更与故障

先成功构建并发布在线 Full，建立对应平台 current。Hotfix 只更新该基线支持的代码与资源；基线缺失、主包版本/Development/架构不匹配或兼容指纹变化时重新 Full。旧安装包跨新基线不受支持。

| 错误 | 操作 |
| --- | --- |
| 工作区已被打开 | 关闭手动打开的 CI 工程；不要关闭日常工程掩盖路径错误 |
| 缺工具或授权 | 修正机器工具路径及该用户 Unity 环境 |
| 缺凭据 | 在 Jenkins 受保护入口配置对应 ID |
| Pages 分歧 | 人工检查专用克隆和远端，不强推或自动 reset |
| SSH 指纹不匹配 | 向服务器管理员核实，不自动接受新 key |
| 下载失败停止启动 | 修复网络/资源后重启；不得绕过失败进入 Hotfix |

## 备份与迁移

停机后备份 home（含凭据加密材料，master.key 分离到单独受保护备份，恢复时补回 home/secrets/master.key）、非秘密 machine.json、工具/插件版本记录、current 基线和必要受保护连接材料；签名 keystore 单独备份。新电脑安装同版 Unity/模块/工具并授权，更新工具路径和节点配置，先做仅构建验收再发布。Library 默认重建。

保留管理端换构建节点时，仅在新电脑准备工具链、授权和 agent，配置节点标签及本机配置路径。跨电脑连接须单独配置网络和认证；loopback 本机设置不会自动变成远程部署。

按[官方备份说明](https://www.jenkins.io/doc/book/system-administration/backing-up/)保护敏感备份，不把 credentials.xml 与其解密材料分离后宣称可恢复。隔离恢复演练不连接真实发布目标；第二台电脑实际验收另行记录。
