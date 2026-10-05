namespace Hotfix
{
    using Core.Runtime;
    using System.Threading;
    using Cysharp.Threading.Tasks;

    [Module("Common")]
    [UIBind("CommonTipsView")]
    public partial class CommonTipsView : View
    {
        private CancellationTokenSource emptyClose;

        protected override void OnGameObjectInitialize() => UITipsStack_CommonTips.Emptied += CloseWhenEmpty;

        internal int AddMessage(string content, CommonTipsType type, float duration, CancellationToken token)
        {
            // 新交付取消已排队的空列表关闭，不能让旧关闭事务清掉新消息。
            emptyClose?.Cancel();
            return UITipsStack_CommonTips.Add(content, type, duration, token);
        }

        internal void RemoveMessage(int id)
        {
            if (gameObject != null) UITipsStack_CommonTips.Dismiss(id);
        }

        protected override void OnHide() => UITipsStack_CommonTips.Clear();

        protected override void OnDestroy()
        {
            emptyClose?.Cancel();
            if (UITipsStack_CommonTips != null) UITipsStack_CommonTips.Emptied -= CloseWhenEmpty;
        }

        private void CloseWhenEmpty()
        {
            emptyClose?.Cancel();
            var source = new CancellationTokenSource();
            emptyClose = source;
            CloseEmptyAsync(source).Forget();
        }

        private async UniTask CloseEmptyAsync(CancellationTokenSource source)
        {
            try
            {
                // OnHide 只清理卡片，不取消正在完成的关闭事务自身。
                await SingleUIManager.Instance.ObserveAsync(UIManager.Instance.CloseAsync(this, false, source.Token));
            }
            finally
            {
                if (ReferenceEquals(emptyClose, source)) emptyClose = null;
                source.Dispose();
            }
        }
    }
}
