# 六位联机码与独立服务器房间

## 适用场景

纸艺卡丁车使用独立 .NET 10 房间服务和 Unity Dedicated Server。玩家创建房间后分享六位数字码，来宾填写显示名与码；玩家页面不输入IP或端口。一个房间对应一个只执行规则的Unity进程，没有本地玩家、不占玩家容量；每名真人都是客户端。支持单场和四章大奖赛，容量2/4/8人，至少两名在线真人开赛，其余赛车由AI补齐八车网格。计时赛仍走单人入口。

HTTP目录负责创建、按码加入、票据、重连、主动离开与进程调度；FishNet/Tugboat UDP负责输入、快照、预测与比赛规则。代码边界见[公共网络会话](../../../modules/network-session.md)和[Demo模块](../module.md#联机房间与状态同步)。Core保留既有Host API与公共测试，卡丁车产品只维护本页的新链。

## 开发环境与构建

- 独立服务仓库为同级 `SleepyDemos-Server`，运行要求.NET 10。部署与HTTP JSON以该仓库[README](../../../../../SleepyDemos-Server/README.md)和[契约](../../../../../SleepyDemos-Server/docs/contract.md)为准；默认最多4房、Unity内部UDP7777–7780。
- 客户端读取 `Assets/StreamingAssets/ServerConnection.json` 的 `directoryUrl`，当前Windows本机为 `http://127.0.0.1:18080`。8080已有无关服务，不停止或复用它；环境地址由开发/部署配置，不放入玩家表单。当前协议为整数2，内容版本为 `kagit_server_v1`，客户端、目录与规则构建必须匹配。
- Unity菜单 `Tools/KagitKart/Server/Build Windows` / `Build Linux` 生成Mono Dedicated产物，入口为 `Assets/Scenes/KagitKartServer.unity`，输出到独立仓库 `artifacts/game-server/win-x64/` / `linux-x64/`。可执行文件为 `KagitKartServer.exe` / `KagitKartServer`；完整目录包含规则数据，以及记录版本、commit、时间和SHA256的 `server-manifest.json`。
- Server直接编入Hotfix规则，使用纯规则StreamingAssets，不启动客户端AppEntrance、资源包、音频、UI或个人存档。构建临时选择Server Mono并禁用HybridCLR过滤器，finally恢复活动平台、完整PlayerSettings、HybridCLR设置、子目标和HotfixConfig文件/内存状态；跨平台重载后单次复核，避免已打开的热更窗口重新覆盖客户端路径。客户端继续既有HybridCLR流程；服务器Mono通过不证明客户端IL2CPP通过。
- 当前客户端多实例使用Multiplayer Play Mode 2.0.2（项目Unity6000.3），走同项目原生PlayModeScenario：主Editor与MPM虚拟Player是两个独立客户端进程，不另建临时验证工程。独立Unity规则服务器仍由目录管理器启动，MPM不承担服务器权威。测试Linux时使用18081，结束恢复开发配置的18080；两端仍从正式AppEntrance进入。

在独立服务仓库双击 `Start-Local.cmd` / `Stop-Local.cmd`，或使用PowerShell；默认读取本仓Windows构建并监听18080，可用参数覆盖：

```powershell
.\scripts\Start-Local.ps1
Invoke-RestMethod http://127.0.0.1:18080/health
# 联调完成后正常停服，回收管理器创建的所有房间进程。
.\scripts\Stop-Local.ps1
```

Linux本地Docker使用隔离HTTP18081，宿主UDP7781–7784映射容器7777–7780，`Rooms__PublicFirstPort=7781`确保JoinGrant返回宿主端口。管理器与真实规则进程ready、UDP监听和取消回收已通过；完整比赛状态见下方验收表。不要同时启动第二个管理器抢同组端口。

本机双客户端使用 `Window/Multiplayer/Play Mode Scenarios`，选择 `Assets/Settings/PlayMode/KagitKartDedicatedVerification.asset` 后使用原生Scenario播放。主Editor与Player 2均为Client，并从正式AppEntrance进入；另一侧虚拟Player共享主工程导入数据。自动驾驶验收只通过真实InputSystem提交操作量，不改变服务器圈数、位置或时间。MPM后台Player须启用后台运行后再等待启动完成，否则失焦可能停在资源初始化；验收驱动和装配菜单是一次性工具，交付时清理，原生Scenario保留供手动联调。

## 创建、加入与开始

1. 先启动目录服务与对应平台的规则构建，再从正式 `AppEntrance` 进入Hub“纸艺卡丁车”，封面开始后选择联机。
2. 房主填写房间名，选择2/4/8人、80g/120g/200g及单场或大奖赛；单场选四条赛道，大奖赛从雏菊草原开始。管理器分配独立进程和端口，Unity成功启动UDP并回报ready后，创建接口才返回授权。
3. 房主复制六位码，例如 `001234`；来宾输入完整码和显示名。码保留前导零，只用于找房；短期Ticket和私密resumeToken绑定稳定memberId，显示名不是身份凭据。
4. 大厅选择未被占用的角色并准备。成员行按房间 `LeaderMemberId` 显示房主，与物理服务器 `AuthorityMemberId` 分开。房主可改大厅配置，设置变化重置全员准备，改角色重置本人准备；容量不能低于现有业务成员，并随心跳同步目录。
5. 至少两名在线真人且全员Ready后，房主请求开始。服务器准备完整规则Session，客户端加载/激活比赛资源；ServerLoaded与全体在线成员Loaded完成后才统一倒计时。旧轮次Loaded不能激活新场。
6. 键鼠、手柄与触屏继续使用公共输入链。联机暂停、失焦和UI过渡只提交本机中性输入，服务器和其他玩家继续。

新成员只在Lobby接纳。Preparing、Racing、Results拒绝新join，但原成员保留资格时可以resume；不能用联机码认领正在比赛的角色。满员、版本不兼容、票据过期或进程不可用均明确反馈，不回退单人模式。

## 掉线、房主权限与主动退出

掉线后原身份最多保留60秒，赛车AI代驾，客户端显示重连并用resumeToken申请新Ticket。每张Ticket只有30秒有效期和一次性nonce，Unity校验每房HMAC签名、房间、时间及撤销状态后消费nonce；重复票据或已离开身份不能入场。恢复沿用原memberId/角色，加载当前完整baseline、圈数、道具、赛果和杯赛进度，再交还本机输入。Results也必须恢复唯一最终快照，不能重复结算积分。

房主断线或退出会把大厅配置/开赛权限转交给仍在线成员；权威模拟一直留在独立服务器，不迁移物理权威。原房主恢复后沿用玩家身份，不自动夺回权限。

主动离开或取消重连会调用HTTP `/v1/sessions/leave`，立即撤销resumeToken。管理器在下一心跳响应通知Unity清Ready、Loaded与保留资格、赛车交AI并转交权限，下一status确认清席位。目录暂时不可达时保留失败反馈，不能声称远端已即时撤销。客户端取消重连/准备、解绑Simulation与事件，再等待公共 `LeaveAsync` 后Dispose，不用固定等待100ms代替退出确认。

Unity每2秒报告阶段、容量、在线/保留/离开身份，管理器每5秒检查。连续15秒无有效心跳、进程崩溃或报错会回收；新进程60秒从未连接玩家清理，曾有玩家后无在线、有效保留或入场预约即清理。正常停服回收自己启动的全部进程。Windows强杀/断电后的残留需核对，不自动收养旧进程。

## 结算与下一场

单场结束后从结算返回原房间，重新准备后由当前房主开下一场；重新开放Lobby后可改设置、接纳新玩家。进行中不能由单个玩家重赛。

大奖赛按雏菊草原、博斯普鲁斯夜景、冰川通道、书写桌面推进。每章显示本场成绩与总积分，章间保留原房间重新准备，服务器推进下一章并按积分倒序排网格。未完成杯赛保持Results，不能加入新人或修改选赛；四章后可展示颁奖、再开大厅和新杯。准备60秒超时或在线真人不足两名时回大厅并取消旧任务；后续章重试保留当前章与已提交积分，不退回首章或默认网格。

## LAN、公网与传输限制

HTTP地址必须能访问目录，`Rooms__PublicAddress`必须是玩家可达的UDP地址。`127.0.0.1`只用于同机，Docker示例回环绑定也不供另一台机器连接。LAN应使用服务器LAN地址、开放对应HTTP/UDP防火墙并核对网络隔离；玩家仍只操作联机码。

公网使用域名、Caddy HTTPS和可达UDP端口。HTTPS保护目录请求，不加密或转发UDP比赛数据；Caddy不公开`/internal/*`。路由/NAT或云防火墙需映射整组UDP；没有Relay、自动NAT穿透、公网地址发现或持久化账号。公网DNS、证书、跨机和设备须各自验收，容器health不能代替它们。

比赛使用120Hz权威步与约30Hz快照，客户端通过官方Replicate/Reconcile预测校正自己的赛车。关键房间/准备/结果使用可靠快照，八车完整状态通常4～8KiB，超过MTU后FishNet转可靠分片，丢片仍会重传等待。约400B拥有者校正常可低于MTU；不能用目标延迟替代实测RTT，也不能由“大包请求不可靠”推断不会等待。

## 排障

| 现象 | 核对顺序 |
|---|---|
| 目录health失败 | directoryUrl是18080还是18081、管理器是否启动；不要停止8080无关服务。失败state按本次PID/时间安全清理。 |
| 创建失败/不ready | Unity可执行路径、完整构建/manifest版本、规则数据、剩余槽和UDP端口、每房日志与完整controlUrl；不输出密钥或票据。 |
| 目录成功但UDP超时 | JoinGrant.address/port可达性、PublicFirstPort与Docker映射、防火墙/LAN隔离/NAT；公共连接等待上限15秒。 |
| 满员或已开赛 | 在线、离线保留和30秒首次预约都占位；新人等Lobby，原成员用resumeToken。 |
| 开始不可用/准备超时 | 至少两名在线真人、全员Ready，服务器Session与客户端资源Loaded；不要绕过加载门闩。 |
| 重连失败/超过60秒 | HTTP与UDP均需可达且版本一致；主动离开撤销token，不能靠码抢回旧身份。 |
| 本机不动、其他车继续 | 暂停/失焦/后台/UI门闩或仍在恢复baseline；比赛不随本机冻结。 |
| 弱网滞后/突跳 | 分开记录原生RTT、丢包、拥有者校正和共享分片等待；先排状态缺失，不提高延迟掩盖。 |

## 2026-10-14 联机验收

| 范围 | 实际结果与边界 |
|---|---|
| Core独立服务器契约 | job `22540a29`，1/1；零本地玩家、稳定身份、认证替换与断线恢复。使用固定字符串测试认证回调，未验证卡丁车HMAC/nonce，也不是完整玩法验收。 |
| Windows Mono Dedicated | 已构建，PlayerSettings/HybridCLR设置/子目标完整恢复；HTTP18080创建真实Unity进程并等待UDP ready smoke成功。构建和ready只证明启动，不证明开赛或游戏状态同步。 |
| Windows真实客户端短流程 | 此前clone Editor经正式AppEntrance，`600be6bf`、`28eeb268` 各1/1：真实KagitKart HMAC nonce重放拒绝、World断线自动resume/新载体、权限转交及原房主回来不夺权限、主动leave后HTTP401/session_expired；不是两个独立World客户端或MPM完整GP证明。AI规则 `77ebd9f8` 为34/34，单独记录。 |
| 当前主工程局部复验 | `76e07873` 1/1：真实创建请求在授权返回前取消，晚到授权撤销、Unity子进程回收、表单恢复并可再次创建；`77c5153f` 1/1：再次验证票据重放拒绝、原身份自动恢复、新预测载体、房主转交及主动离开撤销。均为Unity Test Runner单方法运行，未跑全量。 |
| GP结算恢复与准备超时 | `f6ae5809` 1/1：自然首章八车结果，Results原身份恢复且积分不重复；第二章故意缺一份Loaded，实际60秒超时后保持章节、非零积分/总时间/逆序网格，再以新轮次版本正常起跑。修复复用救援纸鹤仍引用旧赛车的异常；已有Fx用例扩展跨Session救援绑定，`40eac740` 18/18。 |
| 独立.NET服务 | Release build与无框架契约检查通过，含签名、幂等/取消、动态容量、预约/重连撤销、主动leave、端口映射、崩溃和停服回收；见[服务验证记录](../../../../../SleepyDemos-Server/docs/verification-2026-10-14.md)。 |
| Linux真实规则进程 | 只读挂载linux-x64，真实create201等待Unity ready，容器内UDP7777监听、grant宿主7781映射和私密文件600 app已核对；入场前leave204/resume401，真实status确认后3.8秒清配置与Unity子进程。未跑客户端驾驶或比赛。 |
| Linux完整四章大奖赛 | 原生MPM主Editor PID20528与Player 2 PID50408，连接18081目录及Linux规则进程，证据目录 `SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Dedicated_20261014/mpm-gp-1245/`。双方4章自然完赛，逐章8车成绩/积分/累计时间相等、每章积分只累计一次、逆序网格一致，Results及正式Podium实际显示，两端报告passed。之后双方主动离房，容器游戏进程已回收。该项是MPM场景运行证据，与Unity Test Runner计数分开。 |
| Linux比赛资源采样 | `/proc/1756/stat`两次差分，CLK_TCK=100，401.17秒比赛采样占用约7.45%单核CPU；后段VmRSS/VmHWM=159596KiB、61线程。15个3600步日志窗口平均规则耗时0.059–0.075ms，窗口最大单步7.759ms，仅包规则Advance、不含网络发送和整个进程开销。四章主端实际RTT范围3–23ms、虚拟端8–27ms，均收到官方校正和重放；这不是公网延迟或弱网保证。 |
| 尚待单独验收 | 真实双机/设备、公网HTTPS、60秒期限、弱网、单场重赛/新杯与客户端IL2CPP按各自实际结果记录；四章局域本机MPM通过不能替代这些项目。 |

Editor与构建模式边界引用[测试架构](../../../architecture/testing.md)、[正式测试流程](../../../runbooks/run-unity-tests.md)及[热更新模块](../../../modules/hotfix.md)。EditorSimulate复用已加载Hotfix，服务器Mono直接编入规则；二者均不证明客户端独立DLL加载、IL2CPP裁剪、Android或真实设备通过。本页不把旧Host结果计入新产品链，也没有自动重跑全量测试。

## 历史Host验证（已替换）

2026-10-14曾对玩家Host/IP直连完成公共契约、双Editor单场/四章GP、UDP抖动/丢包、准备超时和退出验证。追溯证据见外部[Host报告](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/PeerVerification_20261014/peer-run-20261014-03/host.json)、[Client报告](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/PeerVerification_20261014/peer-run-20261014-03/client.json)、[GP准备超时a2afea3b](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/a2afea3b.json)及[最终局部复验c8c50ec6](../../../../../SleepyDemos-ArtSource/ArtSource/kagit_kart/Previews/Verification_20261014/c8c50ec6.json)。这些报告针对已替换的Host流程和当时构建，不证明新目录/独立服务器/重连链当前通过；旧丢包配置也不等于稳定RTT或固定驾驶响应保证。
