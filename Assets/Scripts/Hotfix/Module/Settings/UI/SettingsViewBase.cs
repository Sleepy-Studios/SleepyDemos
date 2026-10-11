using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 五个宿主共享的候选与提交生命周期，语言重建由页面外 Flow 拥有。
    public abstract class SettingsViewBase : View
    {
        protected abstract SettingsScreen Screen { get; }
        public abstract GameSceneId Host { get; }
        private CancellationTokenSource lifetime;
        private CancellationTokenSource submittingCancellation;
        private UniTaskCompletionSource<SettingsApplyResult> submitting;
        private bool closing;
        private SettingsCategory category;

        protected override void OnGameObjectInitialize()
        {
            Screen.Initialize(this, Host, OnApply, OnCancel);
            BindData<SettingsData>(RefreshState);
        }

        /// <summary>显示前选择分类；候选每次从当前已保存快照重新创建。</summary>
        /// <param name="initialCategory">默认画质，Demo 的语言分类回到画质。</param>
        public void SetData(SettingsCategory initialCategory = SettingsCategory.Graphics)
        {
            category = initialCategory;
            Screen.Bind(GameSettingsService.Current, category);
        }

        protected override void OnShow()
        {
            lifetime = new CancellationTokenSource();
            Screen.Activate();
            RefreshState(GlobalData.Get<SettingsData>());
            base.OnShow();
        }

        protected override void OnHide()
        {
            submittingCancellation?.Cancel();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            Screen.Deactivate();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            submittingCancellation?.Cancel();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            if (Screen != null)
            {
                Screen.Deactivate();
            }
            base.OnDestroy();
        }

        private void RefreshState(SettingsData data)
        {
            Screen.SetBusy(submitting != null || data?.IsApplying == true || GameSettingsService.IsApplying || HubLanguageFlow.IsChanging);
        }

        private void OnApply()
        {
            if (submitting != null || lifetime == null || Screen.IsListening || SettingsFlow.IsSubmitting)
            {
                return;
            }
            var source = new UniTaskCompletionSource<SettingsApplyResult>();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            submitting = source;
            submittingCancellation = cancellation;
            Screen.SetBusy(true);
            SubmitAsync(Screen.Capture(), cancellation, source).Forget();
        }

        private async UniTask SubmitAsync(GameSettingsSnapshot candidate, CancellationTokenSource cancellation,
            UniTaskCompletionSource<SettingsApplyResult> source)
        {
            SettingsApplyResult result;
            try
            {
                result = await SettingsFlow.ApplyAsync(Host, candidate, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                result = new SettingsApplyResult(false, canceled: true);
            }
            catch (Exception exception)
            {
                result = new SettingsApplyResult(false, exception.Message);
            }
            try
            {
                if (ReferenceEquals(submitting, source))
                {
                    submitting = null;
                    submittingCancellation = null;
                    if (IsEnable && Screen != null)
                    {
                        Screen.ShowResult(result);
                        RefreshState(GlobalData.Get<SettingsData>());
                    }
                }
            }
            finally
            {
                cancellation.Dispose();
                source.TrySetResult(result);
            }
        }

        private void OnCancel()
        {
            if (Screen.IsListening)
            {
                Screen.CancelRebind();
                return;
            }
            CancelAndCloseAsync().Forget();
        }

        private async UniTask CancelAndCloseAsync()
        {
            if (closing)
            {
                return;
            }
            closing = true;
            try
            {
                var source = submitting;
                submittingCancellation?.Cancel();
                if (source != null)
                {
                    await source.Task;
                }
                await UIManager.Instance.CloseAsync(this);
            }
            finally
            {
                closing = false;
            }
        }

        /// <summary>页面外语言事务完成后将结果交给重建出的新宿主。</summary>
        /// <param name="result">完整语言应用与 UI 重建事务的结果。</param>
        public void ShowApplyResult(SettingsApplyResult result)
        {
            Screen.ShowResult(result);
            RefreshState(GlobalData.Get<SettingsData>());
        }
    }
}
