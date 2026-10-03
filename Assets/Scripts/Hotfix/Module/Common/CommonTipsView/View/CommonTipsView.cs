namespace Hotfix
{
    using Core.Runtime;
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    [Module("Common")]
    [Mvc("CommonTipsView")]
    public partial class CommonTipsView : View
    {
        private CancellationTokenSource lifetime;
        private CancellationToken requestToken;
        private int version;
        private float duration;
        private Rect previousSafe;

        internal void SetData(string content, CommonTipsType type, float staySeconds, int requestVersion, CancellationToken token)
        {
            StopTimer();
            version = requestVersion;
            requestToken = token;
            duration = staySeconds;
            UITipsPanel_CommonTips.SetMessage(content, type);
            RefreshLayout();
            if (State == ViewState.Visible) StartTimer();
        }

        protected override void OnShow() => StartTimer();
        protected override void OnHide() => StopTimer();
        protected override void OnDestroy() => StopTimer();

        private void StartTimer()
        {
            StopTimer();
            var source = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
            lifetime = source;
            RunAsync(version, duration, source).Forget();
        }

        private async UniTask RunAsync(int requestVersion, float staySeconds, CancellationTokenSource source)
        {
            CancellationToken token = source.Token;
            CancellationToken closeToken = requestToken;
            try
            {
                float elapsed = 0;
                while (!token.IsCancellationRequested && TipsUI.IsCurrentMessage(requestVersion))
                {
                    Rect safe = TooltipPlacementUtil.GetSafeRect(transform as RectTransform);
                    if (safe != previousSafe) RefreshLayout();
                    float scrollSeconds = UITipsPanel_CommonTips.ScrollDistance / 48;
                    float scrollStart = scrollSeconds > 0 ? .75f : 0;
                    if (scrollSeconds > 0) UITipsPanel_CommonTips.SetScrollProgress((elapsed - scrollStart) / scrollSeconds);
                    if (elapsed >= scrollStart + scrollSeconds + staySeconds) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    elapsed += Time.unscaledDeltaTime;
                }
                token.ThrowIfCancellationRequested();
                // OnHide 会取消组件计时，关闭事务必须使用请求令牌，避免自己取消自己的导航。
                await TipsUI.CloseMessageAsync(requestVersion, this, closeToken);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (closeToken.IsCancellationRequested && TipsUI.IsCurrentMessage(requestVersion) && State == ViewState.Visible)
                    await TipsUI.CloseMessageAsync(requestVersion, this, default);
            }
            finally
            {
                if (ReferenceEquals(lifetime, source)) lifetime = null;
                source.Dispose();
            }
        }

        private void RefreshLayout()
        {
            var boundary = transform as RectTransform;
            previousSafe = TooltipPlacementUtil.GetSafeRect(boundary);
            Vector2 size = UITipsPanel_CommonTips.RefreshLayout(previousSafe.size);
            UITipsPanel_CommonTips.Body.position = boundary.TransformPoint(new Vector2(previousSafe.center.x, previousSafe.yMax - size.y * .5f));
        }

        private void StopTimer()
        {
            var previous = lifetime;
            lifetime = null;
            previous?.Cancel();
        }
    }
}
