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
                JoinOnlineRoomAsync(join.Address, join.Port, join.DisplayName).Forget();
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
                case KagitKartCommand.CopyAddress:
                    GUIUtility.systemCopyBuffer = Network?.Address ?? string.Empty;
                    Feedback = "连接地址已复制。";
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

        /// <summary>创建玩家 Host 房间；错误保留在当前表单。</summary>
        /// <param name="name">房间名称。</param>
        /// <param name="capacity">2、4或8。</param>
        /// <param name="mode">单场或大奖赛。</param>
        /// <param name="speedClass">80、120或200。</param>
        /// <param name="trackId">原作赛道。</param>
        public async UniTask CreateOnlineRoomAsync(string name, int capacity, KagitKartRaceMode mode, int speedClass, string trackId)
        {
            busy = true;
            Feedback = null;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                Network ??= new KagitKartNetworkRoom(this, lifetimeCancellation.Token);
                await Network.CreateAsync(new KagitKartRoomState { Name = name, Capacity = capacity, Mode = mode, SpeedClass = speedClass, TrackId = trackId });
                await OpenMenuAsync(KagitKartCommand.Room);
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Feedback = "创建房间失败：" + exception.Message;
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        /// <summary>按地址连接房主；失败不自动退回离线。</summary>
        /// <param name="address">房主IP或域名。</param>
        /// <param name="port">监听端口。</param>
        /// <param name="displayName">玩家显示名。</param>
        public async UniTask JoinOnlineRoomAsync(string address, ushort port, string displayName)
        {
            busy = true;
            Feedback = null;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                Network ??= new KagitKartNetworkRoom(this, lifetimeCancellation.Token);
                await Network.JoinAsync(address, port, displayName);
                await OpenMenuAsync(KagitKartCommand.Room);
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Feedback = "加入房间失败：" + exception.Message;
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
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
                if (GrandPrix == null || GrandPrix.Completed || TrackId == "meadow") GrandPrix = new KagitKartGrandPrix(source.roster);
                else if (GrandPrix.TrackId != TrackId && !GrandPrix.NextRound()) throw new InvalidOperationException("上一场大奖赛尚未完成。");
            }
            else
            {
                GrandPrix = null;
            }
            await StartRoundAsync(preparationToken);
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

        private async UniTask LeaveOnlineRoomAsync()
        {
            busy = true;
            try
            {
                if (Network != null) await Network.LeaveAsync();
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
            await OpenMenuAsync(KagitKartCommand.Online);
        }

        internal async UniTask OnOnlineDisconnectedAsync()
        {
            if (exiting) return;
            Feedback = "房主已离线或连接中断，房间已结束。";
            await LeaveOnlineRoomAsync();
        }
    }
}
