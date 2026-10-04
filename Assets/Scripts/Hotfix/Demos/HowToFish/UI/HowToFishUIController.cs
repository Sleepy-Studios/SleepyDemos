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
        private enum Page { None, MainMenu, Pause, Journal, Ending, Settings, Outfits }
        private readonly HowToFishWorld world;
        private readonly CancellationToken token;
        private View main;
        private View overlay;
        private Page mainPage;
        private Page overlayPage;
        private Page requestedOverlay;
        private GameObject returnFocus;
        private bool changing;
        private bool stopping;
        internal HowToFishUIController(HowToFishWorld owner, CancellationToken lifetime)
        { world = owner; token = lifetime; world.Changed += Refresh; }
        internal void OpenSettings() { if (MainWanted is Page.None or Page.Journal) return; returnFocus = EventSystem.current?.currentSelectedGameObject; requestedOverlay = Page.Settings; Refresh(); }
        internal void CloseSettings() { if (requestedOverlay == Page.Settings) requestedOverlay = Page.None; Refresh(); }
        internal void OpenOutfits() { if (!world.HasSession || MainWanted is Page.None or Page.Journal) return; returnFocus = EventSystem.current?.currentSelectedGameObject; requestedOverlay = Page.Outfits; Refresh(); }
        internal void CloseOutfits() { if (requestedOverlay == Page.Outfits) requestedOverlay = Page.None; Refresh(); }
        private Page MainWanted => stopping ? Page.None : !world.HasSession ? Page.MainMenu :
            world.ShowEnding ? Page.Ending : world.ShowJournal ? Page.Journal : world.IsPaused ? Page.Pause : Page.None;
        private Page OverlayWanted => MainWanted is Page.None or Page.Journal ? Page.None : requestedOverlay;
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
                        await CloseViewAsync(overlay); overlay = null; overlayPage = Page.None;
                        if (mainPage == MainWanted && returnFocus != null && returnFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnFocus);
                        returnFocus = null;
                    }
                    if (mainPage != MainWanted)
                    {
                        if (main != null) { await CloseViewAsync(main); main = null; mainPage = Page.None; }
                        var target = MainWanted;
                        if (target != Page.None) { main = await OpenAsync(target); mainPage = target; }
                        if (target == Page.None) requestedOverlay = Page.None;
                        continue;
                    }
                    var top = OverlayWanted;
                    if (top != Page.None && overlay == null) { overlay = await OpenAsync(top); overlayPage = top; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { stopping = true; Debug.LogException(exception, world); }
            finally { changing = false; }
        }
        private async UniTask<View> OpenAsync(Page page)
        {
            var options = new UIShowOptions(animated: false, hidePrevious: page is Page.Settings or Page.Outfits);
            UIOperationResult result = page switch
            {
                Page.MainMenu => await UIManager.Instance.ShowAsync<HowToFishMainMenuView, HowToFishWorld>(world, options, token),
                Page.Pause => await UIManager.Instance.ShowAsync<HowToFishPauseView, HowToFishWorld>(world, options, token),
                Page.Journal => await UIManager.Instance.ShowAsync<HowToFishJournalView, HowToFishWorld>(world, options, token),
                Page.Ending => await UIManager.Instance.ShowAsync<HowToFishEndingView, HowToFishWorld>(world, options, token),
                Page.Settings => await UIManager.Instance.ShowAsync<HowToFishSettingsView, HowToFishWorld>(world, options, token),
                Page.Outfits => await UIManager.Instance.ShowAsync<HowToFishOutfitsView, HowToFishWorld>(world, options, token),
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
            stopping = true; requestedOverlay = Page.None;
            await UniTask.WaitUntil(() => !changing);
            if (overlay != null) { await CloseViewAsync(overlay); overlay = null; overlayPage = Page.None; }
            if (main != null) { await CloseViewAsync(main); main = null; mainPage = Page.None; }
        }
        internal void Restore() { stopping = false; Refresh(); }
        public void Dispose() { stopping = true; world.Changed -= Refresh; }
    }
}
