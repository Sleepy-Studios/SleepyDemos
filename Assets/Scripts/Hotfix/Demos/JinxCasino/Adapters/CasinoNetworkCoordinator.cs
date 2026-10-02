using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    // 业务状态在通道 1 上始终发送完整快照；服务负责传输身份、可靠性和权威选举。
    public sealed class CasinoNetworkCoordinator : IDisposable
    {
        public const ushort StateChannel = 1;

        private static readonly UTF8Encoding PayloadEncoding = new UTF8Encoding(false, true);
        private static readonly ConditionalWeakTable<INetworkSessionService, CommandCursor> CommandCursors = new ConditionalWeakTable<INetworkSessionService, CommandCursor>();
        private readonly INetworkSessionService network;
        private readonly CommandCursor commandCursor;
        private readonly Queue<PendingWork> queue = new Queue<PendingWork>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource generationCancellation = new CancellationTokenSource();
        private CasinoBetReceipt lastReceipt;
        private ulong snapshotSequence;
        private ulong publishingSequence;
        private ulong publishingEpoch;
        private int generation;
        private bool isDraining;
        private bool disposed;

        /// 最近已提交的规则局；只有协调者应执行结算。
        public CasinoSession Session { get; private set; }
        /// 本地玩家在最新完整快照中的最后一条回执；返回独立副本。
        public CasinoBetReceipt LastReceipt => lastReceipt == null ? null : JsonUtility.FromJson<CasinoBetReceipt>(JsonUtility.ToJson(lastReceipt));
        /// 最近一次协议或快照发布错误；成功提交后清空。
        public string LastError { get; private set; }
        /// 主线程派发已提交的业务状态或错误变化。
        public event Action Changed;

        /// <summary>绑定现有网络服务；不会连接、创建房间或取得该服务的释放所有权。</summary>
        /// <param name="network">必须在 Unity 主线程调用并从主线程派发事件的会话服务。</param>
        public CasinoNetworkCoordinator(INetworkSessionService network)
        {
            EnsureMainThread();
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            // 同一服务内重建 UI/协调者不能重置传输命令序号，否则重发过滤会吞掉下一次下注。
            commandCursor = CommandCursors.GetValue(network, service => new CommandCursor());
            network.StateChanged += OnStateChanged;
            network.SessionChanged += OnSessionChanged;
            network.AuthorityChanged += OnAuthorityChanged;
            network.CommandReceived += OnCommandReceived;
            network.SnapshotReceived += OnSnapshotReceived;
            RestoreRetainedSnapshot();
        }

        /// <summary>仅已入房的当前权威初始化；已有完整快照时恢复该局，不覆盖为新局。</summary>
        /// <param name="seed">首次创建局时使用的随机种子；恢复已有局时忽略。</param>
        /// <param name="cancellationToken">取消队列等待或发布；失败不会提交局状态。</param>
        public async UniTask InitializeRunAsync(uint seed, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            EnsureAvailable();
            if (!IsLocalAuthority()) throw new InvalidOperationException("仅当前房间权威可以初始化赌场局。");
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new UniTaskCompletionSource();
            queue.Enqueue(new PendingWork
            {
                IsInitialize = true, Seed = seed, Generation = generation, Epoch = network.AuthorityEpoch,
                CancellationToken = cancellationToken, Completion = completion
            });
            DrainAsync().Forget();
            await completion.Task.AttachExternalCancellation(cancellationToken);
        }

        /// <summary>向当前权威提交一条新下注；完成仅表示发送被接纳，结果通过 Changed 和 LastReceipt 更新。</summary>
        /// <param name="game">P0 的水果机、单号轮盘或抛硬币。</param>
        /// <param name="stake">期望投入的整数筹码；合法性在权威规则层验证。</param>
        /// <param name="choice">水果机为 0；轮盘为 0..36；硬币为 0/1。</param>
        /// <param name="cancellationToken">取消提交前等待；已提交的业务请求不能因此撤销。</param>
        public async UniTask BetAsync(CasinoGameKind game, long stake, int choice, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            EnsureAvailable();
            if (Session == null) throw new InvalidOperationException("尚未收到赌场局完整快照。");
            if (commandCursor.MemberId != network.LocalMemberId)
            {
                commandCursor.MemberId = network.LocalMemberId;
                commandCursor.Sequence = 0;
            }
            if (commandCursor.Sequence == ulong.MaxValue) throw new InvalidOperationException("命令序号已耗尽，请重新入房。");
            var request = new CasinoBetRequest
            {
                RunId = Session.RunId, PlayerId = network.LocalMemberId, RequestId = Guid.NewGuid().ToString("N"),
                Game = game, Stake = stake, Choice = choice
            };
            var command = new NetworkCommand(StateChannel, ++commandCursor.Sequence, PayloadEncoding.GetBytes(JsonUtility.ToJson(request)));
            await network.SendCommandAsync(command, cancellationToken);
        }

        /// 取消尚未提交的工作并解除事件；网络服务仍由宿主负责离房和释放。
        public void Dispose()
        {
            EnsureMainThread();
            if (disposed) return;
            disposed = true;
            generation++;
            lifetime.Cancel();
            generationCancellation.Cancel();
            network.StateChanged -= OnStateChanged;
            network.SessionChanged -= OnSessionChanged;
            network.AuthorityChanged -= OnAuthorityChanged;
            network.CommandReceived -= OnCommandReceived;
            network.SnapshotReceived -= OnSnapshotReceived;
            CancelQueuedWork();
            Session = null;
            lastReceipt = null;
            Changed = null;
            lifetime.Dispose();
            generationCancellation.Dispose();
        }

        private void OnCommandReceived(NetworkCommandEvent command)
        {
            EnsureMainThread();
            if (disposed || command.Command.Channel != StateChannel || !IsLocalAuthority() ||
                command.AuthorityEpoch != network.AuthorityEpoch || Session == null) return;
            queue.Enqueue(new PendingWork { Command = command, Epoch = command.AuthorityEpoch, Generation = generation });
            DrainAsync().Forget();
        }

        private async UniTask DrainAsync()
        {
            if (isDraining || disposed) return;
            isDraining = true;
            try
            {
                while (queue.Count > 0 && !disposed)
                {
                    PendingWork work = queue.Dequeue();
                    try
                    {
                        ValidateWork(work);
                        await ExecuteAsync(work);
                        work.Completion?.TrySetResult();
                    }
                    catch (OperationCanceledException exception)
                    {
                        await UniTask.SwitchToMainThread();
                        work.Completion?.TrySetCanceled(exception.CancellationToken);
                    }
                    catch (Exception exception)
                    {
                        await UniTask.SwitchToMainThread();
                        if (!disposed && work.Generation == generation)
                        {
                            LastError = exception.Message;
                            NotifyChanged();
                        }
                        work.Completion?.TrySetException(exception);
                    }
                }
            }
            finally { isDraining = false; }
        }

        private async UniTask ExecuteAsync(PendingWork work)
        {
            CasinoSession candidate;
            if (work.IsInitialize)
            {
                if (network.Snapshots.ContainsKey(StateChannel))
                {
                    RestoreRetainedSnapshot();
                    if (Session == null) throw new InvalidOperationException("已有赌场快照无法恢复，不能覆盖为新局。");
                    return;
                }
                if (Session != null) return;
                candidate = new CasinoSession(Guid.NewGuid().ToString("N"), work.Seed);
            }
            else
            {
                if (Session == null) return;
                byte[] payload = work.Command.Command.Payload;
                if (payload.Length > 4096) throw new ArgumentException("赌场请求载荷超过 P0 协议大小。");
                var request = JsonUtility.FromJson<CasinoBetRequest>(PayloadEncoding.GetString(payload));
                if (request == null) throw new ArgumentException("赌场请求不能为空。");
                // 客户端载荷中的 PlayerId 不具备授权意义，永远以传输层来源为准。
                request.PlayerId = work.Command.SenderMemberId;
                string committed = Session.ToSnapshotJson();
                candidate = CasinoSession.Restore(committed);
                candidate.Apply(request);
                if (candidate.ToSnapshotJson() == committed) return;
            }

            ulong latest = snapshotSequence;
            if (network.Snapshots.TryGetValue(StateChannel, out var retained)) latest = Math.Max(latest, retained.Snapshot.Sequence);
            if (latest == ulong.MaxValue) throw new InvalidOperationException("赌场快照序号已耗尽。");
            ulong next = latest + 1;
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, work.CancellationToken, generationCancellation.Token))
            {
                publishingSequence = next;
                publishingEpoch = work.Epoch;
                try
                {
                    // 保持 Session 为提交前对象；发布失败只丢弃 candidate，钱包和幂等账本均不会泄漏。
                    await network.PublishSnapshotAsync(new NetworkSnapshot(StateChannel, next,
                        PayloadEncoding.GetBytes(candidate.ToSnapshotJson())), cancellation.Token);
                    await UniTask.SwitchToMainThread();
                    ValidateWork(work, false);
                    if (next > snapshotSequence) Commit(candidate, next);
                }
                finally { publishingSequence = 0; publishingEpoch = 0; }
            }
        }

        private void OnSnapshotReceived(NetworkSnapshotEvent snapshot)
        {
            EnsureMainThread();
            if (disposed || snapshot.Snapshot.Channel != StateChannel || network.State != NetworkSessionState.Joined ||
                snapshot.AuthorityEpoch != network.AuthorityEpoch || snapshot.AuthorityMemberId != network.AuthorityMemberId) return;
            // 本权威发布会同步回调；等发布方法成功返回后才向 UI 暴露候选钱包。
            if (snapshot.Snapshot.Sequence == publishingSequence && snapshot.AuthorityEpoch == publishingEpoch) return;
            RestoreSnapshot(snapshot);
        }

        private void OnAuthorityChanged(NetworkAuthorityInfo authority)
        {
            EnsureMainThread();
            if (disposed) return;
            generation++;
            CancelCurrentGeneration();
            CancelQueuedWork();
            // 保留快照可能来自上一代权威；服务已验证来源，先恢复才能处理新代命令。
            RestoreRetainedSnapshot();
        }

        private void OnSessionChanged(NetworkSessionInfo info)
        {
            EnsureMainThread();
            if (disposed) return;
            if (info == null) ClearRoom();
            else RestoreRetainedSnapshot();
        }

        private void OnStateChanged(NetworkSessionState value)
        {
            EnsureMainThread();
            if (!disposed && value != NetworkSessionState.Joined) ClearRoom();
        }

        private void ClearRoom()
        {
            generation++;
            CancelCurrentGeneration();
            CancelQueuedWork();
            Session = null;
            lastReceipt = null;
            LastError = null;
            snapshotSequence = 0;
            NotifyChanged();
        }

        private void RestoreRetainedSnapshot()
        {
            if (network.State == NetworkSessionState.Joined && network.Snapshots.TryGetValue(StateChannel, out var retained))
                RestoreSnapshot(retained, true);
        }

        private void RestoreSnapshot(NetworkSnapshotEvent snapshot, bool force = false)
        {
            if (!force && snapshot.Snapshot.Sequence <= snapshotSequence) return;
            try { Commit(CasinoSession.Restore(PayloadEncoding.GetString(snapshot.Snapshot.Payload)), snapshot.Snapshot.Sequence); }
            catch (Exception exception)
            {
                Session = null;
                lastReceipt = null;
                LastError = "赌场快照恢复失败：" + exception.Message;
                NotifyChanged();
            }
        }

        private void Commit(CasinoSession committed, ulong sequence)
        {
            Session = committed;
            snapshotSequence = sequence;
            lastReceipt = null;
            var ledger = committed.CaptureState().Ledger;
            for (int index = ledger.Count - 1; index >= 0; index--)
            {
                if (ledger[index].Request.PlayerId != network.LocalMemberId) continue;
                lastReceipt = ledger[index];
                break;
            }
            LastError = null;
            NotifyChanged();
        }

        private void ValidateWork(PendingWork work, bool checkCancellation = true)
        {
            if (checkCancellation) work.CancellationToken.ThrowIfCancellationRequested();
            if (disposed || work.Generation != generation || !IsLocalAuthority() || work.Epoch != network.AuthorityEpoch)
                throw new OperationCanceledException("房间或权威已改变，未提交工作取消。");
        }

        private bool IsLocalAuthority()
        {
            return network.State == NetworkSessionState.Joined && network.AuthorityEpoch != 0 &&
                !string.IsNullOrWhiteSpace(network.LocalMemberId) && network.LocalMemberId == network.AuthorityMemberId;
        }

        private void EnsureAvailable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CasinoNetworkCoordinator));
            if (network.State != NetworkSessionState.Joined || string.IsNullOrWhiteSpace(network.LocalMemberId))
                throw new InvalidOperationException("尚未加入网络房间。");
        }

        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread) throw new InvalidOperationException("赌场协调者必须在 Unity 主线程使用。");
        }

        private void CancelQueuedWork()
        {
            while (queue.Count > 0) queue.Dequeue().Completion?.TrySetCanceled();
        }

        private void CancelCurrentGeneration()
        {
            // 交接必须同时取消正在等待传输确认的发布；只丢队列会让旧候选状态以新权威代次提交。
            var previous = generationCancellation;
            generationCancellation = new CancellationTokenSource();
            previous.Cancel();
            previous.Dispose();
        }

        private void NotifyChanged()
        {
            EnsureMainThread();
            if (disposed || Changed == null) return;
            foreach (Action handler in Changed.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private sealed class PendingWork
        {
            public bool IsInitialize;
            public uint Seed;
            public int Generation;
            public ulong Epoch;
            public NetworkCommandEvent Command;
            public CancellationToken CancellationToken;
            public UniTaskCompletionSource Completion;
        }

        private sealed class CommandCursor
        {
            public string MemberId;
            public ulong Sequence;
        }
    }
}
