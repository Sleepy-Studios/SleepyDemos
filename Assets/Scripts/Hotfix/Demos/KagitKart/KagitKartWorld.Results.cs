using System;
using System.Linq;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartWorld
    {
        private string[] cupResultOrder;
        private string[] previousStandingOrder;

        /// <summary>读取源积分榜的开赛前名次，用于显示升降箭头；首场为0。</summary>
        /// <param name="characterId">当前结果中的稳定角色标识。</param>
        public int PreviousStandingPlace(string characterId) => previousStandingOrder == null ? 0 : Array.IndexOf(previousStandingOrder, characterId) + 1;

        private void RememberCupResultOrder()
        {
            if (GrandPrix == null || GrandPrix.Index != 0) return;
            var results = Session.Race.Results;
            cupResultOrder = new string[results.Count];
            for (int i = 0; i < results.Count; i++) cupResultOrder[i] = results[i].CharacterId;
        }

        private void CapturePreviousStandings()
        {
            if (GrandPrix == null || GrandPrix.Index == 0)
            {
                cupResultOrder = previousStandingOrder = null;
                return;
            }
            // ui.js 在 raceSetup 保存积分；源 prevOrder 只按分数稳定排序，同分保留首场字典插入顺序。
            var standings = GrandPrix.SortedStandings();
            previousStandingOrder = cupResultOrder.OrderByDescending(id => standings.First(row => row.CharacterId == id).Points).ToArray();
        }

        private async UniTaskVoid ShowStandingsAsync()
        {
            if (GrandPrix?.RoundComplete != true || Session?.Race.State != KagitKartRaceState.Done || !(modal is KagitKartResultsView)) return;
            busy = true;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                while (uiBusy) await UniTask.Yield();
                var token = lifetimeCancellation.Token;
                var closed = await UIManager.Instance.CloseAsync(modal, animated: false, cancellationToken: token);
                if (closed.Status == UIOperationStatus.Failed) throw closed.Exception;
                token.ThrowIfCancellationRequested();
                modal = null;
                var shown = await UIManager.Instance.ShowAsync<KagitKartStandingsView>(view => view.SetData(this),
                    new UIShowOptions(animated: false), token);
                if (shown.Status == UIOperationStatus.Failed) throw shown.Exception;
                token.ThrowIfCancellationRequested();
                modal = shown.View;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Feedback = "积分榜加载失败，请重试。";
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
                SynchronizeModalAsync().Forget();
            }
        }
    }
}
