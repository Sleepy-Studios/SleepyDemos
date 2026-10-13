# 通用网络会话

## 职责与边界

`Core.Runtime.Networking` 提供主线程会话协议；`Core.Networking.FishNet` 是独立 AOT 适配，引用 `Core.Runtime`、`FishNet.Runtime` 与 UniTask，不引用 Hotfix。具体玩法解释命令、输入和完整状态，网络层只负责连接身份、大小限制、序号、传输与官方预测调用。目前只有纸艺卡丁车接入。

官方 FishNet 固定使用 release `4.7.3` 的提交 `73f30cf2425dc808a4f463f0a233d386010810b3`，通过 `Packages/manifest.json` 与 `packages-lock.json` 的 Git UPM 地址锁定。该 release 的上游 `package.json` 仍显示 `4.7.2`，以锁定提交为准，不修改 SDK 元数据。传输使用 SDK 自带 Tugboat，不需要账号、App ID、公共服务器或随机房间码目录服务。

`NetworkSessionServices.CreateInternet(settings)` 创建独立 service，正常启动时由 `FishNetSessionRuntime` 注册工厂。`NetworkSessionSettings` 只保存公开的协议和内容版本。创建 Host 同时启动服务器和本地客户端，传输与通用服务预留八个成员，业务房间容量由即时准入规则控制；加入方使用 Host 的 IP/域名与端口，默认 UDP `7777`；端口映射和地址可达性由部署环境负责。协议与内容版本不匹配、满员、断线与超时均明确失败，不自动切回离线。

## 生命周期与消息

宿主订阅 service → `SetAdmissionRule` → `CreateAsync` / `JoinAsync` → `RegisterSimulation` → 规则运行。服务器等待官方初始场景握手完成，再在协议核对后、成员登记和预测载体创建前调用准入判断；满房或已开赛返回可靠失败消息，不进入 Joined。服务器即时更新成员事件，避免同帧握手争抢最后名额。退出清理该回调。退出先解绑业务与 `RegisterSimulation(null)`，再等待 `LeaveAsync`，最后 `Dispose`。退出请求先在主线程帧末执行，保证 Joined/失败回调所在的 FishNet 收包与 Spawn 缓存解析栈已经退出，再改变 Host 的客户端/服务器状态。Host 先停止本地客户端并等待传输真正 Stopped，再停止服务器并等待 Stopped，最后释放宿主，避免晚到 Spawn 引用已经回收的服务器对象。停止超时保留宿主并明确失败，允许再次退出，不能在 Stopping 时提前释放。开始退出后取消不会中断底层清理；接入方不能在连接/退出尚未完成时 Dispose，也不能用 Dispose 代替异步退出。

`LocalMemberId` 与 `Members` 来自 FishNet Connection 的客户端 ID，显示名及业务 payload 都不是身份凭据。服务器只接收已通过协议、内容版本与容量检查的连接；所有可靠命令、姿态和预测载体拥有者均使用真实连接身份。每成员每通道命令序号递增，重复与旧序号忽略；快照序号在同通道递增，旧值不能覆盖新值。Host 退出即结束房间，本版没有权威迁移。

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

资源地址为 `LoadResources/Prefabs/Networking/SessionHost` 与 `LoadResources/Prefabs/Networking/PredictedSimulation`，由公共资源 loader 管理宿主生命周期。正式 Prefab 装配规则：

- SessionHost 使用官方 NetworkManager/Tugboat，Persistence 使用 AllowMultiple，允许独立服务以及同进程 Host/Client 各持有一个宿主；挂 `FishNetSessionRuntime`；字段 `predictionPrefab` 引用正式预测载体。NetworkManager 的 SpawnablePrefabs 使用 SinglePrefabObjects，并包含该载体。
- PredictedSimulation 挂 NetworkObject 与 `FishNetPredictionBridge`；启用 Prediction、类型 Other、关闭 StateForwarding。本载体没有 Camera、AudioListener 或 Demo 业务组件。
- TimeManager 保持 PhysicsMode.Unity，不接管项目 PhysX 模拟；服务设置 TickRate=120。TransportManager 的 MaximumClientPacketSize 至少 73,728 B，容纳 64 KiB 命令及包头/分片边界。

Prefab 保存与检查通过 Unity Editor 完成，不手写网络对象序列化结构。SDK 与预测载体代码属于 AOT，Hotfix 只引用公共接口。

## 离线验证与验收边界

已有 `OfflineLocalNetworkSessionService` 与 `NetworkRoomCodeCodec` 保留现有测试契约，供显式单人本机验证；Offline 拒绝 Join 和网络预测注册，不能作为联网成功证据。直连 FishNet 不生成或消费旧随机房间码；已移除 Fusion 专属配置。

直接相关已有测试为 `Tests.Module.NetworkRoomCodeTests`、`Tests.Module.OfflineLocalNetworkSessionTests`。新增长期 PlayMode 契约测试 `Tests.Module.FishNetNetworkSessionPlayModeTests.HostClientHandshakeReliableMessagesPredictionAdmissionAndExit` 使用正式资源与真实 UDP，保护握手、可信身份、可靠消息去重、分片快照、官方预测链、Host 单次固定步、准入失败与退出端口释放；不依赖临时探针。联机验证需检查实际 Host/Client、成员变更、身份伪造、版本不兼容、大小限制、可靠命令去重、快照序号、Host 单次 tick、官方重放/校正、断线与退出。Editor 单进程 Host 不证明远端连接或 IL2CPP；Windows/Android Player、弱网与真实双机以各自实测为准。
