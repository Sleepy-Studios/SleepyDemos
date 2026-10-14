using System;
using System.Linq;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartWorld
    {
        /// 本 Demo 的房间规则，SDK 连接由公共网络层持有。
        public KagitKartNetworkRoom Network { get; private set; }
        private UniTaskCompletionSource onlineConnectionCompleted;
        private UniTaskCompletionSource onlineLeaveCompleted;
        private bool cancelingOnlineConnection;

        /// 当前表单仍拥有首次连接流程，包括目录请求与传输清理。
        public bool ConnectingOnlineRoom => onlineConnectionCompleted != null;
        /// 取消请求已提交，原连接尚在独占收口。
        public bool CancelingOnlineConnection => cancelingOnlineConnection;

        private bool ApplyOnlineCommand(KagitKartAction action)
        {
            if (action is KagitKartRoomConfigurationAction configuration)
            {
                if (configuration.UpdateExistingRoom)
                {
                    ConfigureOnlineRoom(configuration.Name, configuration.Capacity, configuration.Mode, configuration.SpeedClass, configuration.TrackId);
                }
                else
                {
                    CreateOnlineRoomAsync(configuration.Name, configuration.Capacity, configuration.Mode, configuration.SpeedClass, configuration.TrackId).Forget();
                }
                return true;
            }
            if (action is KagitKartJoinRoomAction join)
            {
                JoinOnlineRoomAsync(join.Code, join.DisplayName).Forget();
                return true;
            }
            switch (action.Command)
            {
                case KagitKartCommand.PlayModes:
                case KagitKartCommand.Online:
                case KagitKartCommand.CreateRoom:
                case KagitKartCommand.JoinRoom:
                case KagitKartCommand.Room:
                    OpenMenuAsync(action.Command).Forget();
                    return true;
                case KagitKartCommand.SinglePlayer:
                    OpenMenuAsync(KagitKartCommand.Modes).Forget();
                    return true;
                case KagitKartCommand.LeaveRoom:
                    LeaveOnlineRoomAsync().Forget();
                    return true;
                case KagitKartCommand.Ready:
                    Network?.SetReady(action.Number != 0);
                    return true;
                case KagitKartCommand.StartOnlineRace:
                    Network?.StartRace();
                    return true;
                case KagitKartCommand.ChooseOnlineCharacter:
                    Network?.ChooseCharacter(action.Id);
                    return true;
                case KagitKartCommand.CopyCode:
                    GUIUtility.systemCopyBuffer = Network?.Code ?? string.Empty;
                    Feedback = "联机码已复制。";
                    Dispatch(KagitKartCommand.Refresh);
                    return true;
                case KagitKartCommand.ReturnRoom:
                    ReturnOnlineRoomAsync().Forget();
                    return true;
            }
            if (Network?.Connected != true) return false;
            if (action.Command is KagitKartCommand.Title or KagitKartCommand.Modes)
            {
                if (Network.Room?.Phase == "Results") ReturnOnlineRoomAsync().Forget();
                else LeaveOnlineRoomAsync().Forget();
                return true;
            }
            if (action.Command == KagitKartCommand.Restart)
            {
                if (Network.Room?.Phase == "Results") ReturnOnlineRoomAsync().Forget();
                else Feedback = "联机比赛不能单独重赛。";
                return true;
            }
            if (action.Command == KagitKartCommand.NextRound && GrandPrix?.Completed != true)
            {
                ReturnOnlineRoomAsync().Forget();
                return true;
            }
            if (action.Command is KagitKartCommand.Classes or KagitKartCommand.Characters or KagitKartCommand.Tracks
                or KagitKartCommand.ChooseMode or KagitKartCommand.ChooseSpeedClass or KagitKartCommand.ChooseCharacter
                or KagitKartCommand.ChooseTrack or KagitKartCommand.PreviewCharacter or KagitKartCommand.PreviewTrack)
            {
                Feedback = "请返回大厅修改联机比赛设置。";
                return true;
            }
            return false;
        }

        /// <summary>创建独立服务器房间并以普通客户端加入；错误保留在当前表单。</summary>
        /// <param name="name">房间名称。</param>
        /// <param name="capacity">2、4或8。</param>
        /// <param name="mode">单场或大奖赛。</param>
        /// <param name="speedClass">80、120或200。</param>
        /// <param name="trackId">原作赛道。</param>
        public async UniTask CreateOnlineRoomAsync(string name, int capacity, KagitKartRaceMode mode, int speedClass, string trackId)
        {
            if (Busy || onlineConnectionCompleted != null)
            {
                return;
            }
            var completion = new UniTaskCompletionSource();
            onlineConnectionCompleted = completion;
            busy = true;
            Feedback = null;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                Network ??= new KagitKartNetworkRoom(this, lifetimeCancellation.Token);
                await Network.CreateAsync(new KagitKartRoomState { Name = name, Capacity = capacity, Mode = mode, SpeedClass = speedClass, TrackId = trackId });
                if (cancelingOnlineConnection)
                {
                    throw new OperationCanceledException();
                }
                lifetimeCancellation.Token.ThrowIfCancellationRequested();
                await OpenMenuAsync(KagitKartCommand.Room);
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Feedback = "创建房间失败：" + exception.Message;
            }
            finally
            {
                busy = false;
                if (Network?.ConnectionCleanupPending != true)
                {
                    onlineConnectionCompleted = null;
                }
                completion.TrySetResult();
                if (this != null && !exiting)
                {
                    Dispatch(KagitKartCommand.Refresh);
                }
            }
        }

        /// <summary>按六位联机码连接独立服务器；失败保留表单。</summary>
        /// <param name="code">保留前导零的六位数字联机码。</param>
        /// <param name="displayName">玩家显示名。</param>
        public async UniTask JoinOnlineRoomAsync(string code, string displayName)
        {
            if (Busy || onlineConnectionCompleted != null)
            {
                return;
            }
            var completion = new UniTaskCompletionSource();
            onlineConnectionCompleted = completion;
            busy = true;
            Feedback = null;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                Network ??= new KagitKartNetworkRoom(this, lifetimeCancellation.Token);
                await Network.JoinAsync(code, displayName);
                if (cancelingOnlineConnection)
                {
                    throw new OperationCanceledException();
                }
                lifetimeCancellation.Token.ThrowIfCancellationRequested();
                await OpenMenuAsync(KagitKartCommand.Room);
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Feedback = "加入房间失败：" + exception.Message;
            }
            finally
            {
                busy = false;
                if (Network?.ConnectionCleanupPending != true)
                {
                    onlineConnectionCompleted = null;
                }
                completion.TrySetResult();
                if (this != null && !exiting)
                {
                    Dispatch(KagitKartCommand.Refresh);
                }
            }
        }

        /// 取消表单当前连接，等待网络和 World 原流程结束后再返回联机入口。
        public async UniTask CancelOnlineRoomConnectionAsync()
        {
            var completion = onlineConnectionCompleted;
            var network = Network;
            if (completion == null || network == null || cancelingOnlineConnection)
            {
                return;
            }
            cancelingOnlineConnection = true;
            Feedback = "正在取消连接，请稍候……";
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                await network.CancelConnectionAsync();
                await completion.Task;
                // 取消恰好发生在 UDP 已成功返回之后时，仍需撤销刚得到的身份。
                await network.LeaveAsync();
                onlineConnectionCompleted = null;
                if (this == null || exiting || lifetimeCancellation.IsCancellationRequested)
                {
                    return;
                }
                Feedback = "已取消连接。";
                await OpenMenuAsync(KagitKartCommand.Online);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Feedback = "取消连接失败：" + exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                cancelingOnlineConnection = false;
                if (this != null && !exiting)
                {
                    Dispatch(KagitKartCommand.Refresh);
                }
            }
        }

        /// <summary>修改已加入大厅的比赛候选，实际授权由服务器检查。</summary>
        /// <param name="name">房间名称。</param>
        /// <param name="capacity">2、4或8。</param>
        /// <param name="mode">单场或大奖赛。</param>
        /// <param name="speedClass">80、120或200。</param>
        /// <param name="trackId">原作章节。</param>
        public void ConfigureOnlineRoom(string name, int capacity, KagitKartRaceMode mode, int speedClass, string trackId)
        {
            try
            {
                Network.Configure(new KagitKartRoomState { Name = name, Capacity = capacity, Mode = mode, SpeedClass = speedClass, TrackId = trackId });
                OpenMenuAsync(KagitKartCommand.Room).Forget();
            }
            catch (ArgumentException exception)
            {
                Feedback = exception.Message;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        internal async UniTask PrepareOnlineRaceAsync(KagitKartRoomState room, CancellationToken preparationToken)
        {
            while (busy || uiBusy) await UniTask.Yield(preparationToken);
            preparationToken.ThrowIfCancellationRequested();
            var player = Array.Find(room.Players, p => p.MemberId == Network.LocalMemberId);
            if (player == null) throw new InvalidOperationException("当前玩家不在比赛网格中。");
            Mode = room.Mode;
            TrackId = room.TrackId;
            SpeedClass = room.SpeedClass;
            CharacterId = player.CharacterId;
            if (Mode == KagitKartRaceMode.GrandPrix)
            {
                GrandPrix = new KagitKartGrandPrix(source.roster);
                if (room.CupPoints != null) GrandPrix.RestoreNetworkState(room.CupIndex, false, room.CupPoints, room.CupPlaces, room.CupTimes);
            }
            else
            {
                GrandPrix = null;
            }
            await StartRoundAsync(preparationToken);
            if (GrandPrix != null && room.CupRoundComplete)
                GrandPrix.RestoreNetworkState(room.CupIndex, true, room.CupPoints, room.CupPlaces, room.CupTimes);
        }

        internal void BeginOnlineCountdown()
        {
            if (Session == null || busy || cinematics == null || !IntroPlaying) return;
            cinematics.SkipIntro();
            FinishIntro();
        }

        internal void PrepareRoomSelection(KagitKartRoomState room)
        {
            if (Session != null && Session.Race.State != KagitKartRaceState.Done) return;
            Mode = room.Mode;
            TrackId = room.TrackId;
            SpeedClass = room.SpeedClass;
            var player = Array.Find(room.Players, p => p.MemberId == Network.LocalMemberId);
            if (player != null) CharacterId = player.CharacterId;
            PrepareSelectedRaceAsync().Forget();
        }

        internal async UniTask ReturnOnlineRoomAsync()
        {
            if (Network?.Connected != true) return;
            if (Network.Room.Phase is "Racing" or "Preparing") return;
            Network.ReturnToLobby();
            while (busy || uiBusy) await UniTask.Yield(lifetimeCancellation.Token);
            if (Network?.Connected != true || Network.Room.Phase is "Racing" or "Preparing") return;
            await OpenMenuAsync(KagitKartCommand.Room);
        }

        private UniTask LeaveOnlineRoomAsync()
        {
            if (onlineLeaveCompleted != null)
            {
                return onlineLeaveCompleted.Task;
            }
            var completion = new UniTaskCompletionSource();
            onlineLeaveCompleted = completion;
            LeaveOnlineRoomCoreAsync(completion).Forget();
            return completion.Task;
        }

        private async UniTask LeaveOnlineRoomCoreAsync(UniTaskCompletionSource completion)
        {
            busy = true;
            try
            {
                if (Network != null) await Network.LeaveAsync();
                var connection = onlineConnectionCompleted;
                if (connection != null)
                {
                    await connection.Task;
                }
                if (this == null || exiting || lifetimeCancellation.IsCancellationRequested)
                {
                    return;
                }
                await CloseReconnectAsync();
                await OpenMenuAsync(KagitKartCommand.Online);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                busy = false;
                onlineLeaveCompleted = null;
                if (Network?.ConnectionCleanupPending != true)
                {
                    onlineConnectionCompleted = null;
                }
                if (this != null && !exiting)
                {
                    Dispatch(KagitKartCommand.Refresh);
                }
                completion.TrySetResult();
            }
        }

        internal async UniTask OnOnlineDisconnectedAsync()
        {
            if (exiting) return;
            Feedback = Network?.Status ?? "服务器连接已中断，请重新加入。";
            await LeaveOnlineRoomAsync();
        }

        internal void OnReconnectStarted()
        {
            Input.SetDriving(false);
            ShowReconnectAsync().Forget(Debug.LogException);
        }

        private View reconnectView;

        private async UniTask ShowReconnectAsync()
        {
            reconnectView = await UIManager.Instance.OpenAsync<KagitKartReconnectView>(view => view.SetData(this),
                new UIOpenOptions(animated: false), lifetimeCancellation.Token);
            if (Network?.Reconnecting != true) await CloseReconnectAsync();
        }

        internal void OnReconnectFinished() => CloseReconnectAsync().Forget(Debug.LogException);

        internal void OnNetworkStateRestored()
        {
            if (Session?.Race.State == KagitKartRaceState.Countdown) BeginOnlineCountdown();
            else if (Session != null)
            {
                cinematics?.SkipIntro();
                audio.RestoreNetworkPlayback();
                Input.SetDriving(Session.Race.State != KagitKartRaceState.Done);
            }
            if (Session?.Race.State == KagitKartRaceState.Done)
            {
                Input.SetDriving(false);
                SynchronizeModalAsync().Forget();
            }
            Dispatch(KagitKartCommand.Refresh);
        }

        private async UniTask CloseReconnectAsync()
        {
            if (reconnectView == null) return;
            var view = reconnectView;
            reconnectView = null;
            await UIManager.Instance.CloseAsync(view);
        }
    }
}
