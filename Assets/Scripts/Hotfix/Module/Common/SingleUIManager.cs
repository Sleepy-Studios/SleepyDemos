using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix
{
    public readonly struct SimpleTipsOptions
    {
        private readonly bool configured;
        private readonly TooltipDirection direction;
        private readonly float gap;
        private readonly float maxWidth;
        private readonly bool closeOnOutside;
        /// 首选主体方向，默认上方。
        public TooltipDirection Direction => configured ? direction : TooltipDirection.Up;
        /// 主体到目标边缘的距离，默认 12。
        public float Gap => configured ? gap : 12;
        /// 主体最大宽度，默认 600。
        public float MaxWidth => configured ? maxWidth : 600;
        /// 是否拦截外部点击并占有返回输入，默认开启。
        public bool CloseOnOutside => !configured || closeOnOutside;

        /// <summary>配置 SimpleTips，尺寸使用 Canvas 单位。</summary>
        /// <param name="direction">首选主体方向，默认上方。</param>
        /// <param name="gap">非负间距，默认 12。</param>
        /// <param name="maxWidth">有限且大于零，默认 600。</param>
        /// <param name="closeOnOutside">点击模式开启；悬停模式关闭。</param>
        public SimpleTipsOptions(TooltipDirection direction = TooltipDirection.Up, float gap = 12, float maxWidth = 600, bool closeOnOutside = true)
        {
            if (!Enum.IsDefined(typeof(TooltipDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            if (float.IsNaN(gap) || float.IsInfinity(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
            if (float.IsNaN(maxWidth) || float.IsInfinity(maxWidth) || maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            configured = true; this.direction = direction; this.gap = gap; this.maxWidth = maxWidth; this.closeOnOutside = closeOnOutside;
        }
    }

    internal readonly struct SimpleTipsRequest
    {
        internal SimpleTipsRequest(RectTransform target, Vector2 point, bool followsTarget, string content, string title,
            SimpleTipsOptions options, int version, CancellationToken token)
        {
            Target = target; Point = point; FollowsTarget = followsTarget; Content = content;
            Title = title; Options = options; Version = version; Token = token;
        }
        internal RectTransform Target { get; }
        internal Vector2 Point { get; }
        internal bool FollowsTarget { get; }
        internal string Content { get; }
        internal string Title { get; }
        internal SimpleTipsOptions Options { get; }
        internal int Version { get; }
        internal CancellationToken Token { get; }
    }

    /// 公共独立界面的业务入口；实例加载和显隐仍由 UIManager 导航管理。
    public sealed class SingleUIManager : Singleton<SingleUIManager>
    {
        private CancellationTokenSource messageRequests = new CancellationTokenSource();
        private CancellationTokenSource simpleCancellation;
        private int simpleVersion;

        /// <summary>向顶部堆叠添加一条消息，悬停展开并暂停阅读计时；新请求不覆盖旧消息。</summary>
        /// <param name="content">正文，null 视为空。</param>
        /// <param name="type">三种状态的明确枚举。</param>
        /// <param name="duration">正文滚动完成后的停留秒数，有限且大于零，默认 2。</param>
        /// <param name="cancellationToken">预先取消不添加消息；显示后取消仅移除本条消息。</param>
        /// <returns>UI 导航结果；加载错误通过 Failed.Exception 返回。</returns>
        public async UniTask<UIOperationResult> ShowTipsMessageBarAsync(string content, CommonTipsType type, float duration = 2,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(typeof(CommonTipsType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (cancellationToken.IsCancellationRequested) return UIOperationResult.Canceled(0, UINavigationAction.Push, null);
            using var pending = CancellationTokenSource.CreateLinkedTokenSource(messageRequests.Token, cancellationToken);
            CommonTipsView owner = null;
            int messageId = 0;
            var result = await UIManager.Instance.ShowAsync<CommonTipsView>(view =>
            {
                owner = view;
                messageId = view.AddMessage(content, type, duration, cancellationToken);
            }, new UIShowOptions(animated: false, hidePrevious: false), pending.Token);
            // 导航被取消或失败时，只回收本次已交付的消息，不清空其他通知。
            if (result.Status == UIOperationStatus.Canceled || result.Status == UIOperationStatus.Failed)
                owner?.RemoveMessage(messageId);
            return result;
        }

        /// <summary>显示跟随目标矩形的 SimpleTips，默认外部点击和返回关闭。</summary>
        /// <param name="target">UI 目标；销毁、禁用或移出可见范围会关闭 Tips。</param>
        /// <param name="content">正文。</param>
        /// <param name="title">可选标题。</param>
        /// <param name="options">方向、间距、宽度和交互配置。</param>
        /// <param name="cancellationToken">本次导航取消令牌。</param>
        public UniTask<UIOperationResult> ShowSimpleTipsAsync(RectTransform target, string content, string title = null,
            SimpleTipsOptions options = default, CancellationToken cancellationToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return ShowSimpleCoreAsync(target, default, true, content, title, options, cancellationToken);
        }

        /// <summary>在固定屏幕坐标旁显示 SimpleTips。</summary>
        /// <param name="screenPosition">屏幕像素坐标；不接受世界坐标。</param>
        /// <param name="content">正文。</param>
        /// <param name="title">可选标题。</param>
        /// <param name="options">方向、间距、宽度和交互配置。</param>
        /// <param name="cancellationToken">本次导航取消令牌。</param>
        public UniTask<UIOperationResult> ShowSimpleTipsAsync(Vector2 screenPosition, string content, string title = null,
            SimpleTipsOptions options = default, CancellationToken cancellationToken = default)
            => ShowSimpleCoreAsync(null, screenPosition, false, content, title, options, cancellationToken);

        /// 关闭当前 SimpleTips，并取消尚未完成的显示请求。
        public UniTask<UIOperationResult> HideSimpleTipsAsync()
        {
            simpleVersion++;
            Cancel(ref simpleCancellation);
            return UIManager.Instance.CloseAsync<SimpleTipsView>(false);
        }

        /// 关闭全部顶部消息，并取消已经排队的显示请求；不影响后来提交的新请求。
        public UniTask<UIOperationResult> HideTipsMessageBarsAsync()
        {
            var previous = messageRequests;
            messageRequests = new CancellationTokenSource();
            previous.Cancel();
            previous.Dispose();
            return UIManager.Instance.CloseAsync<CommonTipsView>(false);
        }

        internal bool IsCurrentSimple(int version) => version == simpleVersion;
        internal int CurrentSimpleVersion => simpleVersion;

        internal async UniTask CloseSimpleAsync(int version, SimpleTipsView view, CancellationToken token)
        {
            if (!IsCurrentSimple(version)) return;
            // 显示等待被业务取消后，已可见的交互 Tips 仍应允许用户关闭。
            await ObserveAsync(UIManager.Instance.CloseAsync(view, false, token.IsCancellationRequested ? default : token));
        }

        internal async UniTask ObserveAsync(UniTask<UIOperationResult> operation)
        {
            var result = await operation;
            if (result.Status == UIOperationStatus.Failed) Debug.LogException(result.Exception);
        }

        private UniTask<UIOperationResult> ShowSimpleCoreAsync(RectTransform target, Vector2 point, bool followsTarget,
            string content, string title, SimpleTipsOptions options, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return UniTask.FromResult(UIOperationResult.Canceled(0, UINavigationAction.Push, null));
            int version = ++simpleVersion;
            var token = ReplaceCancellation(ref simpleCancellation, cancellationToken);
            var request = new SimpleTipsRequest(target, point, followsTarget, content, title, options, version, token);
            return CompleteSimpleShowAsync(UIManager.Instance.ShowAsync<SimpleTipsView>(view => view.SetData(request),
                new UIShowOptions(animated: false, hidePrevious: false), token), version);
        }

        private async UniTask<UIOperationResult> CompleteSimpleShowAsync(UniTask<UIOperationResult> operation, int version)
        {
            var result = await operation;
            if (result.Status != UIOperationStatus.Canceled && result.Status != UIOperationStatus.Failed) return result;
            // 替换请求已取消旧轮任务；如果自身也没能显示，不能留下失去所有权的旧遮挡或永久消息。
            // 后来的请求已经接管时，旧完成不再清理它。
            if (IsCurrentSimple(version)) await ObserveAsync(HideSimpleTipsAsync());
            return result;
        }

        private static CancellationToken ReplaceCancellation(ref CancellationTokenSource source, CancellationToken external)
        {
            Cancel(ref source);
            source = CancellationTokenSource.CreateLinkedTokenSource(external);
            return source.Token;
        }
        private static void Cancel(ref CancellationTokenSource source)
        {
            var old = source; source = null;
            old?.Cancel(); old?.Dispose();
        }
    }
}
