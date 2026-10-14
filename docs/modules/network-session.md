# 通用网络会话

## 职责与边界

`Core.Runtime.Networking` 提供主线程会话协议；`Core.Networking.FishNet` 是独立 AOT 适配，引用 `Core.Runtime`、`FishNet.Runtime` 与 UniTask，不引用 Hotfix。具体玩法解释命令、输入和完整状态，网络层只负责连接身份、大小限制、序号、传输与官方预测调用。目前只有纸艺卡丁车接入。

官方 FishNet 固定使用 release `4.7.3` 的提交 `73f30cf2425dc808a4f463f0a233d386010810b3`，通过 `Packages/manifest.json` 与 `packages-lock.json` 的 Git UPM 地址锁定。该 release 的上游 `package.json` 仍显示 `4.7.2`，以锁定提交为准，不修改 SDK 元数据。UDP 传输使用 SDK 自带 Tugboat；房间目录、进程调度和票据签发由独立服务承担，不进入 FishNet SDK。

`NetworkSessionServices.CreateInternet(settings)` 创建独立 service，正常启动时由 `FishNetSessionRuntime` 注册工厂。`NetworkSessionSettings` 只保存公开的协议和内容版本。`StartServerAsync(NetworkServerOptions)` 只启动权威服务器：没有本地客户端，`LocalMemberId` 为空，服务器不在 `Members` 中，也不占玩家容量；启动必须预先设置认证规则。`JoinAsync(NetworkJoinOptions)` 使用目录返回的地址、UDP 端口和短期 Ticket 连接。协议与内容版本不匹配、满员、断线与超时均明确失败，不自动切回离线。

通用 `CreateAsync` 仍保留创建玩家 Host 的既有契约，同时启动服务器和本地客户端，供已有接入与公共测试使用；纸艺卡丁车产品只走独立服务器、六位联机码和重连票据链，不提供 Host/IP 输入入口。HTTP 目录客户端为 `RoomDirectoryClient`，负责 create/join/resume/leave；它不传输赛车输入、快照或预测数据。产品操作及独立 .NET 10 服务入口见[卡丁车联机手册](../demos/kagit_kart/runbooks/multiplayer.md)。

## 生命周期与消息

宿主订阅 service → 设置 `SetAuthenticationRule` / `SetAdmissionRule` → 注册 `INetworkSimulation` → `StartServerAsync` / `JoinAsync` → 规则运行。服务器等待官方初始场景握手完成，再在协议核对后、成员登记和预测载体创建前认证 Ticket 与判断准入；满房或已开赛的新成员返回可靠失败消息，不进入 Joined。认证明确授权的旧身份替换、断线恢复可以绕过新成员的业务阶段门闩，不能只根据旧连接是否仍在线判断重连。服务器即时更新成员事件，避免同帧握手争抢最后名额。SDK 宿主自动启动关闭，连接由当前 service 单次启动并收口。

退出先结束业务准备与重连任务、解绑业务及 `RegisterSimulation(null)`，再等待公共 `LeaveAsync`，最后 `Dispose`。卡丁车业务主动退出还会通过 HTTP 撤销 resumeToken，防止断线期间退出被误当成继续保留；这一动作属于业务目录链，不由通用 UDP 退出代办。退出请求先在主线程帧末执行，保证 Joined/失败回调所在的 FishNet 收包与 Spawn 缓存解析栈已经退出，再改变传输状态。通用 Host 先停止本地客户端并等待真正 Stopped，再停止服务器并等待 Stopped；独立服务器没有本地客户端步骤。停止超时保留宿主并明确失败，允许再次退出，不能在 Stopping 时提前释放。开始退出后取消不会中断底层清理；接入方不能在连接/退出尚未完成时 Dispose，也不能用 Dispose 代替异步退出。

认证成功的稳定 `MemberId` 绑定真实 FishNet Connection，断线恢复沿用业务身份；未配置认证的既有 Host 契约仍使用连接 ID。显示名及业务 payload 都不是身份凭据。独立服务器的 `AuthorityMemberId` 是服务器身份，卡丁车房主操作权限使用房间快照中的 `LeaderMemberId`；房主断线/退出可以转交操作权限，物理权威始终留在服务器进程，不做玩家权威迁移。所有可靠命令、姿态和预测载体拥有者均从已认证连接映射身份，旧连接被替换后不能继续提交该身份的命令。每成员每通道命令序号递增，重复与旧序号忽略；快照序号在同通道递增，旧值不能覆盖新值。

Ticket 的签名、房间号、有效期、一次性 nonce 及已撤销成员由服务器认证规则验证；Core 只传递认证输入和可信结果，不持有房间签名密钥，不用公开联机码认领成员。恢复完整比赛状态、60 秒业务保留、AI 代驾和杯赛状态仍归 Demo。

可靠命令最多 64 KiB；预测输入最多 1 KiB；完整校正/共享快照最多 256 KiB。非法或超限客户端消息拒绝并断开对应连接；本地非法提交抛异常。公开消息构造会复制 payload，预测桥也复制交给 FishNet 保留的输入与状态，业务不能修改历史数据。FishNet Reader 自带无效分配长度检查，公共适配再检查业务边界。

共享快照每四个网络 tick 发送一次，约 30 Hz；同通道发送前只保留最新完整状态。服务器本地可立即读取已发布状态，迟加入方收到保留快照。`NetworkSnapshot` 的可选参数 `reliable=true` 用于房间、资源准备、开始和最终结果等关键状态；这些状态不能只发布一次不可靠快照。常规比赛快照与姿态使用不可靠通道；超过 MTU 的快照和校正由官方分片机制改用可靠通道，4～8 KiB 的普通比赛快照也会进入此路径；一次丢片可能造成可靠队头等待。当前优先保证完整状态正确，后续弱网实测确认明显等待时再精简/压缩共享远端呈现快照，不提高延迟参数掩盖问题；约 400 B 的 owner Reconcile 本身仍可低于 MTU。不能按“小包丢帧”理解大包快照。

## 固定步与预测

FishNet TimeManager 使用 120 Hz，是联机模式唯一规则时钟。每个拥有者 `FishNetPredictionBridge` 在 OnTick 读取本地输入，调用织入的 `[Replicate]`。服务器的 Replicate 只将可信成员输入交给规则；主服务在 OnPostTick 收完输入后执行一次 `AuthorityTick`，随后服务器载体每四步调用 `[Reconcile]`；客户端拥有者每步创建官方本地回退状态，保持 LocalReconcile 历史的连续 tick 索引。Host 只走权威路径，不同时推进另一份客户端预测规则。

`INetworkSimulation` 的接口为：

```csharp
byte[] ReadInput();
void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority);
void AuthorityTick(uint tick, float dt);
byte[] CaptureState(string memberId);
void Reconcile(string memberId, uint tick, byte[] state);
```

客户端只有自己拥有的载体读取输入和执行预测，其他车手/实体通过共享快照表现。完整校正必须包含会影响后续步的隐藏字段、随机状态及交互上下文，不能只保存 Transform。输入历史、重放和校正调度由 FishNet 官方机制管理，不另建预测缓存或时钟。服务器 Replicate 回调的 tick 是实际应用的服务器 tick，客户端 tick 则属于官方输入/重放时钟；二者不能直接相减。byte[] 使用官方 `[CustomComparer]` 入口并通过标准库 SequenceEqual 按完整内容比较，null 与空数组不同，不用比较器跳过校正。`replay` 来自官方 ReplicateState，业务禁止在重放时重复音效、UI、奖励或存档副作用。更换注册对象清空官方 Replicate 缓存；业务 payload 仍须核对比赛/阶段标识，拒绝网络中已在途的旧场数据。

## 正式资源

资源地址为 `LoadResources/Prefabs/Networking/SessionHost` 与 `LoadResources/Prefabs/Networking/PredictedSimulation`。客户端通过公共资源 loader 管理宿主生命周期；独立服务器通过服务器场景中的序列化正式 Prefab 引用启动，不依赖客户端资源/热更入口。正式 Prefab 装配规则：

- SessionHost 使用官方 NetworkManager/Tugboat，Persistence 使用 AllowMultiple，允许独立服务以及同进程 Host/Client 各持有一个宿主；挂 `FishNetSessionRuntime`；字段 `predictionPrefab` 引用正式预测载体。NetworkManager 的 SpawnablePrefabs 使用 SinglePrefabObjects，并包含该载体。
- PredictedSimulation 挂 NetworkObject 与 `FishNetPredictionBridge`；启用 Prediction、类型 Other、关闭 StateForwarding。本载体没有 Camera、AudioListener 或 Demo 业务组件。
- TimeManager 保持 PhysicsMode.Unity，不接管项目 PhysX 模拟；服务设置 TickRate=120。TransportManager 的 MaximumClientPacketSize 至少 73,728 B，容纳 64 KiB 命令及包头/分片边界。

Prefab 保存与检查通过 Unity Editor 完成，不手写网络对象序列化结构。SDK 与预测载体代码属于 AOT，Hotfix 只引用公共接口。

## 离线验证与验收边界

已有 `OfflineLocalNetworkSessionService` 与 `NetworkRoomCodeCodec` 保留现有测试契约，供显式单人本机验证；Offline 拒绝 Join 和网络预测注册，不能作为联网成功证据。旧离线码编解码与卡丁车目录服务生成的六位数字码无关；已移除 Fusion 专属配置。

直接相关已有测试为 `Tests.Module.NetworkRoomCodeTests`、`Tests.Module.OfflineLocalNetworkSessionTests`。长期 PlayMode 测试 `Tests.Module.FishNetNetworkSessionPlayModeTests.HostClientHandshakeReliableMessagesPredictionAdmissionAndExit` 保护既有 Host 契约；`DedicatedServerAuthenticatesStableMembersAndReplacesOnlyAuthorizedReconnects` 使用测试认证回调保护仅服务器启动、零玩家成员、稳定身份、受权替换与断线恢复。2026-10-14 job `22540a29` 为1/1；该回调使用固定测试字符串，不验证卡丁车HMAC或nonce消费，也不证明完整Loaded、驾驶、赛果或四章GP通过。

卡丁车Windows/Linux Mono Dedicated已构建；真实进程、短流程重连/权限转交/主动撤销、HMAC nonce重放拒绝、并行双房及65秒资格过期已有定向记录。原生MPM两个独立客户端已连接Linux服完成四章自然GP，两端八车赛果、积分和逆序网格一致；分层证据与本次弱网范围见[卡丁车联机验收](../demos/kagit_kart/runbooks/multiplayer.md#2026-10-14-联机验收)。服务器直接编入规则程序集，客户端继续既有HybridCLR流程，两种构建不能互相证明。Editor、公共Host测试或服务器Mono构建均不证明客户端独立DLL加载、IL2CPP、裁剪、Android或真实双机通过；运行流程引用[测试架构](../architecture/testing.md)、[正式测试流程](../runbooks/run-unity-tests.md)与[热更新模块](hotfix.md)。
