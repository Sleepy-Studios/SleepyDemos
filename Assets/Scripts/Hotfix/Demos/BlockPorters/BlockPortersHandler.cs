using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// 规则命令和奖励生命周期；Unity对象的装配与演出由场景入口执行。
    internal sealed class BlockPortersHandler : HandlerBase<BlockPortersAction, BlockPortersData>
    {
        private readonly BlockPortersController scene;

        private CancellationTokenSource rewardLifetime;

        private IBlockPortersReward reward = new SimulatedBlockPortersReward();

        internal BlockPortersHandler(BlockPortersController scene)
        {
            this.scene = scene;
        }

        internal void SetReward(IBlockPortersReward provider) => reward = provider ?? throw new ArgumentNullException(nameof(provider));

        internal void CancelReward()
        {
            rewardLifetime?.Cancel();
            rewardLifetime?.Dispose();
            rewardLifetime = null;
        }

        private void RenewReward()
        {
            CancelReward();
            rewardLifetime = CancellationTokenSource.CreateLinkedTokenSource(scene.Lifetime);
        }

        internal void Publish() => ApplyState();

        internal void Advance(double time)
        {
            var previous = State.Session.Status;
            State.Scheduler.AdvanceTo(time);
            if (State.Session.Status != previous)
                Publish();
        }

        /// <summary>
        /// 同步处理规则命令，奖励请求在完成后通过结果 Action 发布。
        /// </summary>
        /// <param name="action">本场待处理的业务命令。</param>
        protected override void Reduce(BlockPortersAction action)
        {
            switch (action)
            {
                case BlockPortersReadyAction ready when ReferenceEquals(ready.Source, State):
                    State.Ready = ready.Ready;
                    break;
                case BlockPortersLoadLevelAction load:
                    LoadLevel(load.Index, load.ChooseTheme);
                    return;
                case BlockPortersDispatchAction dispatch:
                    if (State.Ready && !State.IsPaused && !State.IsExiting && !State.IsRewardPending && State.Scheduler.Dispatch(dispatch.Column))
                        Publish();
                    return;
                case BlockPortersTogglePauseAction:
                    if (State.IsExiting)
                        return;
                    State.IsPaused = !State.IsPaused;
                    break;
                case BlockPortersToggleSoundAction:
                    State.IsMuted = !State.IsMuted;
                    scene.ApplySound(State.IsMuted);
                    break;
                case BlockPortersRestartAction:
                    if (!State.IsExiting)
                        LoadLevel(State.LevelIndex, false);
                    return;
                case BlockPortersNextLevelAction:
                    if (!State.IsExiting && State.Session.Status == BlockPortersStatus.Won)
                        LoadLevel((State.LevelIndex + 1) % State.LevelCount, true);
                    return;
                case BlockPortersUnlockSlotAction unlock:
                    UnlockAsync(unlock.Side).Forget();
                    return;
                case BlockPortersRewardResultAction completed:
                    if (completed.Version != State.Version || State.IsExiting)
                        return;
                    if (completed.Result == PorterRewardResult.Completed)
                        State.Session.TryUnlockExtraSlot(completed.Side);
                    State.IsRewardPending = false;
                    break;
                case BlockPortersOpenSettingsAction:
                    if (State.IsExiting || State.SettingsRequested || State.Session.Status != BlockPortersStatus.Playing)
                        return;
                    State.WasPaused = State.IsPaused;
                    State.SettingsSession = State.Session;
                    State.SettingsRequested = true;
                    State.IsPaused = true;
                    break;
                case BlockPortersCloseSettingsAction:
                    if (!State.SettingsRequested)
                        return;
                    State.SettingsRequested = false;
                    if (!State.WasPaused && State.Session == State.SettingsSession && !State.IsExiting)
                        State.IsPaused = false;
                    break;
                case BlockPortersExitAction:
                    if (State.IsExiting)
                        return;
                    State.IsExiting = true;
                    State.Version++;
                    CancelReward();
                    Publish();
                    scene.ExitScene();
                    return;
                case BlockPortersRestoreAction restore when ReferenceEquals(restore.Source, State):
                    State.IsExiting = State.IsRewardPending = false;
                    RenewReward();
                    break;
                default:
                    return;
            }

            if (!State.IsPaused || State.Session != State.SettingsSession)
                State.SettingsRequested = false;
            Publish();
        }

        private void LoadLevel(int index, bool chooseTheme)
        {
            State.Levels = scene.Definitions;
            if ((uint)index >= State.LevelCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            State.Version++;
            State.IsRewardPending = State.IsPaused = State.SettingsRequested = false;
            State.LevelIndex = index;
            State.SettingsSession = null;
            RenewReward();
            State.Session = new BlockPortersSession(State.CurrentLevel.CreateData());
            State.Scheduler = new BlockPortersScheduler(State.Session);
            scene.BuildLevel(chooseTheme);
            Publish();
        }

        private async UniTask UnlockAsync(int side)
        {
            if (!State.Ready || State.IsExiting || State.IsRewardPending || (uint)side > 1 || State.Session.Status == BlockPortersStatus.Won || State.Session.IsSlotAvailable(side + 5))
                return;
            int version = State.Version;
            State.IsRewardPending = true;
            Publish();
            PorterRewardResult result = PorterRewardResult.Canceled;
            try
            {
                result = await reward.RequestExtraSlotAsync(side, rewardLifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error, scene);
            }

            // 旧关卡或旧场景的奖励完成不能影响重新注册的会话。
            if (version == State.Version && ReferenceEquals(GlobalData.Get<BlockPortersData>(), State))
                GlobalData.Dispatch(new BlockPortersRewardResultAction(version, side, result));
        }
    }
}
