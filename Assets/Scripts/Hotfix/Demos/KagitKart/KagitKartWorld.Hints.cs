using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartWorld
    {
        private KagitKartHintsView hintsView;

        /// 非交互教程与一次性驾驶提示状态，跨本场重赛保留已看历史。
        public KagitKartHints Hints { get; private set; }

        private async UniTask InitializeHintsAsync(CancellationToken token)
        {
            var history = LocalDataManager.LoadData(LocalDataKeys.KagitKartHintsPreferences,
                Array.Empty<KagitKartHintKind>(), ValidateHintHistory);
            Hints = new KagitKartHints(history);
            Hints.Changed += OnHintsChanged;
            Hints.TutorialCompleted += OnTutorialCompleted;
            Hints.HistoryChanged += SaveHintHistory;
            await ShowHintsViewAsync(token);
        }

        private static void ValidateHintHistory(KagitKartHintKind[] values)
        {
            foreach (var value in values)
            {
                if (!Enum.IsDefined(typeof(KagitKartHintKind), value))
                {
                    throw new ArgumentException("驾驶提示历史无效。");
                }
            }
        }

        private async UniTask ShowHintsViewAsync(CancellationToken token = default)
        {
            if (hintsView?.State == ViewState.Visible)
            {
                return;
            }
            var result = await UIManager.Instance.OpenAsync<KagitKartHintsView>(view => view.SetData(this),
                new UIOpenOptions(animated: false), token);
            token.ThrowIfCancellationRequested();
            hintsView = result;
        }

        private async UniTask CloseHintsViewAsync()
        {
            if (hintsView == null)
            {
                return;
            }
            await UIManager.Instance.CloseAsync(hintsView);
            hintsView = null;
        }

        private void UpdateHints()
        {
            if (Hints == null || Busy)
            {
                return;
            }
            bool raceView = Session != null && !IntroPlaying && Session.Race.State != KagitKartRaceState.Done;
            bool paused = Session?.Pause.IsPaused == true;
            Hints.Advance(paused ? 0 : Core.Runtime.TimeControls.GameTimeService.DeltaTime, raceView, ready,
                Session?.Race.State == KagitKartRaceState.Running, paused,
                ShowTouchControls && Screen.height > Screen.width * 1.1f,
                Session?.Player.TrackInfo ?? default, Session?.Player.Speed ?? 0);
        }

        private void OnHintsChanged() => Dispatch(KagitKartCommand.Refresh);

        private void OnTutorialCompleted() => Dispatch(KagitKartCommand.MarkTutorialSeen);

        private void SaveHintHistory()
        {
            try
            {
                LocalDataManager.SaveData(LocalDataKeys.KagitKartHintsPreferences, Hints.CopyHistory(), ValidateHintHistory);
            }
            catch (Exception exception)
            {
                // 原作情境提示为尽力保存；失败时本场仍只显示一次，下次启动可能重显。
                Debug.LogWarning("驾驶提示历史保存失败：" + exception.Message, this);
            }
        }

        private void OnPlayerHintSignal(KagitKartVehicle player, KagitKartVehicleSignal signal)
        {
            if (signal == KagitKartVehicleSignal.DriftTier)
            {
                Hints.OnDriftTier(player.DriftTier);
            }
        }

        private void OnItemHintSignal(KagitKartItemEvent signal)
        {
            if (signal.Kind == "itemGet" && signal.Kart == Session?.Player)
            {
                Hints.OnItemGet();
            }
        }

        private void DisposeHints()
        {
            if (Hints != null)
            {
                Hints.Changed -= OnHintsChanged;
                Hints.TutorialCompleted -= OnTutorialCompleted;
                Hints.HistoryChanged -= SaveHintHistory;
                Hints.Unbind();
                Hints = null;
            }
            CloseDestroyedHintsViewAsync().Forget();
        }

        private async UniTaskVoid CloseDestroyedHintsViewAsync()
        {
            try
            {
                await CloseHintsViewAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
