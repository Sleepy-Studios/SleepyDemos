using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Hotfix.HowToFish
{
    /// 管理本世界的主页面与一个覆盖页；页面数据随正式导航操作交付。
    internal sealed class HowToFishUIController : IDisposable
    {
        private readonly HowToFishWorld world;
        private readonly CancellationToken token;
        private View main;
        private View overlay;
        private HowToFishPage mainPage;
        private HowToFishPage overlayPage;
        private HowToFishPage requestedOverlay => world.Data.RequestedOverlay;
        private GameObject returnFocus;
        private bool changing;
        private bool stopping;
        internal HowToFishUIController(HowToFishWorld owner, CancellationToken lifetime)
        { world = owner; token = lifetime; GlobalData.Subscribe<HowToFishData>(OnData); }
        internal void OpenSettings() => Dispatch(HowToFishUiCommand.OpenSettings);
        internal void CloseSettings() => Dispatch(HowToFishUiCommand.CloseSettings);
        internal void OpenOutfits() => Dispatch(HowToFishUiCommand.OpenOutfits);
        internal void CloseOutfits() => Dispatch(HowToFishUiCommand.CloseOutfits);
        private HowToFishPage MainWanted => stopping ? HowToFishPage.None : !world.HasSession ? HowToFishPage.MainMenu :
            world.ShowEnding ? HowToFishPage.Ending : world.ShowJournal ? HowToFishPage.Journal : world.IsPaused ? HowToFishPage.Pause : HowToFishPage.None;
        private HowToFishPage OverlayWanted => MainWanted is HowToFishPage.None or HowToFishPage.Journal ? HowToFishPage.None : requestedOverlay;
        internal void CaptureReturnFocus() => returnFocus = EventSystem.current?.currentSelectedGameObject;
        private void Dispatch(HowToFishUiCommand command) => GlobalData.Dispatch(new HowToFishUiAction(world, command));
        private void OnData(HowToFishData value) { if (ReferenceEquals(value, world.Data)) Refresh(); }
        internal void Refresh() { if (!changing) SynchronizeAsync().Forget(); }
        private async UniTaskVoid SynchronizeAsync()
        {
            changing = true;
            try
            {
                while (mainPage != MainWanted || overlayPage != OverlayWanted)
                {
                    if (overlay != null && (overlayPage != OverlayWanted || mainPage != MainWanted))
                    {
                        await CloseViewAsync(overlay); overlay = null; overlayPage = HowToFishPage.None;
                        if (mainPage == MainWanted && returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
                        returnFocus = null;
                    }
                    if (mainPage != MainWanted)
                    {
                        if (main != null) { await CloseViewAsync(main); main = null; mainPage = HowToFishPage.None; }
                        var target = MainWanted;
                        if (target != HowToFishPage.None) { main = await OpenAsync(target); mainPage = target; }
                        continue;
                    }
                    var top = OverlayWanted;
                    if (top != HowToFishPage.None && overlay == null) { overlay = await OpenAsync(top); overlayPage = top; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { stopping = true; Debug.LogException(exception, world); }
            finally { changing = false; }
        }
        private async UniTask<View> OpenAsync(HowToFishPage page)
        {
            var options = new UIShowOptions(animated: false, hidePrevious: page is HowToFishPage.Settings or HowToFishPage.Outfits);
            UIOperationResult result = page switch
            {
                HowToFishPage.MainMenu => await UIManager.Instance.ShowAsync<HowToFishMainMenuView>(view => view.SetData(world), options, token),
                HowToFishPage.Pause => await UIManager.Instance.ShowAsync<HowToFishPauseView>(view => view.SetData(world), options, token),
                HowToFishPage.Journal => await UIManager.Instance.ShowAsync<HowToFishJournalView>(view => view.SetData(world), options, token),
                HowToFishPage.Ending => await UIManager.Instance.ShowAsync<HowToFishEndingView>(view => view.SetData(world), options, token),
                HowToFishPage.Settings => await UIManager.Instance.ShowAsync<HowToFishSettingsView>(view => view.SetData(world), options, token),
                HowToFishPage.Outfits => await UIManager.Instance.ShowAsync<HowToFishOutfitsView>(view => view.SetData(world), options, token),
                _ => throw new ArgumentOutOfRangeException(nameof(page))
            };
            if (result.Status == UIOperationStatus.Canceled) throw new OperationCanceledException(token);
            if (result.Status == UIOperationStatus.Failed) throw result.Exception;
            return result.View ?? throw new InvalidOperationException("渔力全开页面没有返回实例。");
        }
        private static async UniTask CloseViewAsync(View view)
        { var result = await UIManager.Instance.CloseAsync(view, false); if (result.Status == UIOperationStatus.Failed) throw result.Exception; }
        internal async UniTask CloseAsync()
        {
            stopping = true;
            await UniTask.WaitUntil(() => !changing);
            if (overlay != null) { await CloseViewAsync(overlay); overlay = null; overlayPage = HowToFishPage.None; }
            if (main != null) { await CloseViewAsync(main); main = null; mainPage = HowToFishPage.None; }
        }
        internal void Restore() { stopping = false; Refresh(); }
        public void Dispose() { stopping = true; GlobalData.UnSubscribe<HowToFishData>(OnData); }
    }
}
