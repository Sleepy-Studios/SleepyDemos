using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Hotfix.BlockPorters
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
        internal BlockPortersUIController(BlockPortersController controller, CancellationToken lifetime)
        { owner = controller; token = lifetime; GlobalData.Subscribe<BlockPortersData>(OnData); }
        internal void OpenSettings() => GlobalData.Dispatch(new BlockPortersOpenSettingsAction());
        internal void CloseSettings() => GlobalData.Dispatch(new BlockPortersCloseSettingsAction());
        private void OnData(BlockPortersData value) => Refresh();
        private void Refresh()
        {
            wanted = stopping || owner.IsExiting ? Page.None : owner.Data.SettingsRequested ? Page.Settings :
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
                        ? await UIManager.Instance.ShowAsync<BlockPortersSettingsView>( options, token)
                        : await UIManager.Instance.ShowAsync<BlockPortersResultView>( options, token);
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
        public void Dispose() { stopping = true; GlobalData.UnSubscribe<BlockPortersData>(OnData); }
    }
}
