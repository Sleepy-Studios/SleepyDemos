using System;
using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

namespace Core.Runtime.Networking
{
    /// 官方预测载体；输入历史、校正与重放全部由 FishNet Weaver 管理。
    public sealed class FishNetPredictionBridge : NetworkBehaviour
    {
        /// <summary>官方预测的数组比较器；null 与空数组不同，内容相同才相等。</summary>
        /// <param name="left">第一个完整载荷。</param>
        /// <param name="right">第二个完整载荷。</param>
        /// <returns>两个 null、同一个数组或逐字节内容相同时为 true。</returns>
        [CustomComparer]
        public static bool PayloadEquals(byte[] left, byte[] right) => ReferenceEquals(left, right) ||
            left != null && right != null && left.AsSpan().SequenceEqual(right);

        public struct InputData : IReplicateData
        {
            public byte[] Payload;
            private uint tick;
            public uint GetTick() => tick;
            public void SetTick(uint value) => tick = value;
            public void Dispose() { Payload = null; }
        }

        public struct StateData : IReconcileData
        {
            public byte[] Payload;
            private uint tick;
            public uint GetTick() => tick;
            public void SetTick(uint value) => tick = value;
            public void Dispose() { Payload = null; }
        }

        private FishNetNetworkSessionService service;

        /// 注册官方固定步回调，不创建第二套 Update 时钟。
        public override void OnStartNetwork()
        {
            service = NetworkManager.GetComponent<FishNetSessionRuntime>().Service;
            service.RegisterBridge(this);
            TimeManager.OnTick += OnTick;
            TimeManager.OnPostTick += OnPostTick;
        }

        /// 解除固定步订阅和借用的业务状态。
        public override void OnStopNetwork()
        {
            TimeManager.OnTick -= OnTick;
            TimeManager.OnPostTick -= OnPostTick;
            service?.UnregisterBridge(this);
            service = null;
        }

        private void OnTick()
        {
            if (service?.Simulation == null || service.State != NetworkSessionState.Joined)
            {
                return;
            }
            try
            {
                var data = default(InputData);
                if (IsOwner)
                {
                    byte[] input = service.Simulation.ReadInput();
                    FishNetNetworkSessionService.ValidatePayload(input, NetworkPayloadLimits.Input, allowNull: true);
                    // FishNet 保留输入直到确认；调用方下一帧重用缓冲不能改变历史。
                    data.Payload = input == null ? null : (byte[])input.Clone();
                }
                RunInput(data);
            }
            catch (Exception exception)
            {
                service.Fail(exception);
            }
        }

        private void OnPostTick()
        {
            if (service?.Simulation == null || service.State != NetworkSessionState.Joined)
            {
                return;
            }
            if (IsServerStarted && TimeManager.LocalTick % 4 != 0)
            {
                return;
            }
            try
            {
                CreateReconcile();
            }
            catch (Exception exception)
            {
                service.Fail(exception);
            }
        }

        [Replicate]
        private void RunInput(InputData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (service?.Simulation == null || !service.TryGetMemberId(Owner.ClientId, out string memberId))
            {
                return;
            }
            if (!IsServerStarted && !IsOwner)
            {
                return;
            }
            if (data.Payload?.Length > NetworkPayloadLimits.Input)
            {
                if (IsServerStarted)
                {
                    service.Reject(Owner, "预测输入超过大小上限。");
                }
                return;
            }
            // 远端输入原 tick 属于客户端时钟；服务器业务超时判断统一使用实际应用 tick。
            uint appliedTick = IsServerStarted ? TimeManager.LocalTick : data.GetTick();
            service.Simulation.Replicate(memberId, appliedTick, (float)TimeManager.TickDelta,
                data.Payload, state.ContainsReplayed(), IsServerStarted);
        }

        /// <summary>服务器每四步发送校正；客户端每步保存官方本地回退历史，Host 只捕获权威状态。</summary>
        public override void CreateReconcile()
        {
            if (service?.Simulation == null || !service.TryGetMemberId(Owner.ClientId, out string memberId))
            {
                return;
            }
            if (!IsServerStarted && !IsOwner)
            {
                return;
            }
            byte[] payload = service.Simulation.CaptureState(memberId);
            FishNetNetworkSessionService.ValidatePayload(payload, NetworkPayloadLimits.State, allowNull: true);
            if (payload == null && IsServerStarted)
            {
                return;
            }
            // 菜单/换轮的空状态也占一个本地 tick，避免官方历史按 tick 差索引时出现空洞。
            ApplyState(new StateData { Payload = payload == null ? null : (byte[])payload.Clone() });
        }

        [Reconcile]
        private void ApplyState(StateData data, Channel channel = Channel.Unreliable)
        {
            if (IsServerStarted || !IsOwner || service?.Simulation == null || data.Payload == null ||
                !service.TryGetMemberId(Owner.ClientId, out string memberId))
            {
                return;
            }
            if (data.Payload.Length > NetworkPayloadLimits.State)
            {
                service.Fail(new InvalidOperationException("权威校正超过大小上限。"));
                return;
            }
            service.Simulation.Reconcile(memberId, data.GetTick(), data.Payload);
        }
    }
}
