using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Hotfix.BlockPorters.Adapters
{
    /// 当前会话只拥有一个独立弹窗，所有开关按现有导航队列顺序执行。
    internal sealed class BlockPortersUIController : IDisposable
    {
        private enum Page { None, Settings, Result }
        private readonly BlockPortersController owner;
        private readonly CancellationToken token;
        private View current;
        private Page shown;
        private Page wanted;
        private bool changing;
        private bool stopping;
        private bool settingsRequested;
        private bool wasPaused;
        private BlockPortersSession settingsSession;
        internal BlockPortersUIController(BlockPortersController controller, CancellationToken lifetime)
        { owner = controller; token = lifetime; owner.Changed += Refresh; }
        internal void OpenSettings()
        {
            if (stopping || owner.IsExiting || settingsRequested || owner.Session.Status != BlockPortersStatus.Playing) return;
            wasPaused = owner.IsPaused; settingsSession = owner.Session; settingsRequested = true;
            if (!wasPaused) owner.TogglePause();
            Refresh();
        }
        internal void CloseSettings()
        {
            if (!settingsRequested) return;
            settingsRequested = false;
            if (!wasPaused && owner.IsPaused && owner.Session == settingsSession && !owner.IsExiting) owner.TogglePause();
            Refresh();
        }
        private void Refresh()
        {
            if (!owner.IsPaused || owner.Session != settingsSession) settingsRequested = false;
            wanted = stopping || owner.IsExiting ? Page.None : settingsRequested ? Page.Settings :
                owner.Session.Status != BlockPortersStatus.Playing ? Page.Result : Page.None;
            if (!changing && shown != wanted) SynchronizeAsync().Forget();
        }
        private async UniTaskVoid SynchronizeAsync()
        {
            changing = true;
            try
            {
                while (shown != wanted)
                {
                    if (current != null)
                    {
                        var closed = await UIManager.Instance.CloseAsync(current, false);
                        if (closed.Status == UIOperationStatus.Failed) throw closed.Exception;
                        current = null; shown = Page.None;
                    }
                    var target = wanted;
                    if (target == Page.None) continue;
                    var options = new UIShowOptions(animated: false, hidePrevious: false);
                    var opened = target == Page.Settings
                        ? await UIManager.Instance.ShowAsync<BlockPortersSettingsView, BlockPortersController>(owner, options, token)
                        : await UIManager.Instance.ShowAsync<BlockPortersResultView, BlockPortersController>(owner, options, token);
                    if (opened.Status == UIOperationStatus.Canceled) return;
                    if (opened.Status == UIOperationStatus.Failed) throw opened.Exception;
                    current = opened.View; shown = target;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, owner); CloseSettings(); }
            finally { changing = false; }
        }
        internal async UniTask CloseAsync()
        {
            stopping = true; Refresh();
            await UniTask.WaitUntil(() => !changing);
            if (current != null) throw new InvalidOperationException("搬豆工弹窗关闭失败。");
        }
        internal void Restore() { stopping = false; Refresh(); }
        public void Dispose() { stopping = true; owner.Changed -= Refresh; }
    }
}
