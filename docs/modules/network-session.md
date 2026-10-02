# 通用网络会话边界

## 职责

`Core.Runtime.Networking` 提供主线程会话协议，隔离第三方联机 SDK 与 Hotfix 业务。创建/加入/离开、成员和权威、可靠命令、完整共享快照、角色姿态归属网络层；下注、奖励、道具及场景目标仍由业务解释。

运行时入口位于 `Assets/Scripts/Core/Runtime/Networking/`。`INetworkSessionService` 定义协议；`NetworkSessionSettings` 保存公开 App ID 与平台共享的区域/协议/内容版本；`NetworkSessionServices` 仅创建已注册的互联网适配。

## 生命周期

业务宿主创建独立 service → 订阅事件 → CreateAsync / JoinAsync → 绑定完整快照 → 提交命令。退出先移除业务订阅和待提交工作，再等待 LeaveAsync 收口，最后 Dispose。SDK 失败、取消和断线不能遗留上一局回调。

收到命令的成员标识必须来自传输层；业务载荷中的身份不可信。快照在事件发布前保留，各通道序号单调递增，权威交接继续原最新序号。角色姿态允许丢帧，不参与资金结算；Avatar 不携带相机或 AudioListener。

## 房间码

`NetworkRoomCodeCodec` 规范化 `V1-hk-ABCDEFGH` 形式。编码含协议、区域和随机 token，不含 PC / Android 标记。内容版本在加入房间时额外检查，不能用代码相同代替规则兼容。

## 当前实现与限制

目前完成协议与显式 `OfflineLocalNetworkSessionService`。离线实现只允许一人，拒绝 Join，不能作为互联网联机或 SDK 兼容证据。缺少 App ID 或已注册 SDK 适配时 `CreateInternet` 明确失败，不自动切换为离线。

当前赌场目标已调整为完整单机，联网选型待定；保留这些已有协议与离线适配作为原型依赖，不继续扩建网络框架。旧P0显式离线链路曾通过Windows/Android IL2CPP构建，Windows解压启动到Hub；不构成任何SDK兼容或互联网联机证据。未来如采用Weaver，相关网络类型只进入AOT；Core不反向引用Hotfix。

## 验证

直接相关测试：`Tests.Module.NetworkRoomCodeTests`、`Tests.Module.OfflineLocalNetworkSessionTests`。覆盖规范码、序号、消息所有权、取消和清理。互联网适配必须另验权威交接、快照恢复、错误码、弱网和跨平台 Player。
