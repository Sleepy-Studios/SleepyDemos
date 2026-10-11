using System;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// Hub 的独立公共体验入口，世界资源和两个 UI View 继续使用现有加载与导航事务。
    public static class PresentationLabFlow
    {
        public const string WorldAddress = "LoadResources/Art/Presentation/PresentationWorld";
        private static bool opening;
        private static bool closing;

        /// <summary>从稳定 Hub 打开体验页；重复打开不会再申请世界资源。</summary>
        /// <param name="token">资源准备和 UI 导航的取消令牌；失败时归还已申请的租约。</param>
        /// <returns>页面与表现根均显示后的 UI 导航结果；错误保留真实异常。</returns>
        public static async UniTask<UIOperationResult> OpenAsync(CancellationToken token = default)
        {
            var existing = UIManager.Instance.Get<PresentationLabView>();
            if (existing != null && existing.IsEnable)
            {
                return UIOperationResult.Ignored(0, UINavigationAction.Push, existing);
            }
            if (opening || closing)
            {
                return UIOperationResult.Canceled(0, UINavigationAction.Push, existing);
            }
            opening = true;
            IResourceLoader loader = null;
            PresentationWorldLease world = null;
            PresentationLabView lab = null;
            CinematicOverlayView overlay = null;
            try
            {
                if (GameSceneNavigator.Instance == null || GameSceneNavigator.Instance.CurrentScene != GameSceneId.Hub
                    || GameSceneNavigator.Instance.IsTransitioning)
                {
                    throw new InvalidOperationException("公共效果体验只能从稳定 Hub 打开。");
                }
                token.ThrowIfCancellationRequested();
                loader = ResourceServices.CreateLoader();
                var instance = await loader.InstantiateAsync(WorldAddress, null);
                token.ThrowIfCancellationRequested();
                world = new PresentationWorldLease(loader, instance);
                loader = null;
                var result = await UIManager.Instance.ShowAsync<PresentationLabView>(view => view.SetData(world),
                    new UIShowOptions(animated: false), token);
                lab = result.View as PresentationLabView;
                if (result.Status != UIOperationStatus.Succeeded && result.Status != UIOperationStatus.Ignored)
                {
                    return result;
                }
                using var overlayLifetime = CancellationTokenSource.CreateLinkedTokenSource(token, lab.LifetimeToken);
                result = await UIManager.Instance.ShowAsync<CinematicOverlayView>(view => view.SetData(lab.Preview),
                    new UIShowOptions(animated: false, hidePrevious: false), overlayLifetime.Token);
                overlay = result.View as CinematicOverlayView;
                if (result.Status != UIOperationStatus.Succeeded && result.Status != UIOperationStatus.Ignored)
                {
                    await UIManager.Instance.CloseAsync(lab, false);
                    await UIManager.Instance.ShowAsync<MainMenuView>(new UIShowOptions(animated: false));
                    return result;
                }
                lab.SetOverlay(overlay);
                world = null;
                return result;
            }
            catch (Exception exception)
            {
                if (overlay != null)
                {
                    await UIManager.Instance.CloseAsync(overlay, false);
                }
                if (lab != null)
                {
                    await UIManager.Instance.CloseAsync(lab, false);
                    await UIManager.Instance.ShowAsync<MainMenuView>(new UIShowOptions(animated: false));
                }
                return exception is OperationCanceledException
                    ? UIOperationResult.Canceled(0, UINavigationAction.Push, lab)
                    : UIOperationResult.Failed(0, UINavigationAction.Push, lab, exception);
            }
            finally
            {
                opening = false;
                world?.Dispose();
                loader?.Dispose();
            }
        }

        /// <summary>停止本次体验，再关闭其具体表现根并通过原 UIManager 返回 Hub。</summary>
        /// <param name="expected">触发关闭的具体体验页，过期实例不会关闭随后打开的新页面。</param>
        /// <returns>返回 Hub 的事务结果；失败时恢复仍可见的体验页表现根。</returns>
        public static async UniTask<UIOperationResult> CloseAsync(PresentationLabView expected)
        {
            if (closing || expected == null || !ReferenceEquals(UIManager.Instance.Get<PresentationLabView>(), expected))
            {
                return UIOperationResult.Canceled(0, UINavigationAction.Close, expected);
            }
            closing = true;
            try
            {
                expected.SetOverlay(null);
                await expected.StopAndRestoreAsync();
                var overlay = UIManager.Instance.Get<CinematicOverlayView>();
                if (overlay != null)
                {
                    await UIManager.Instance.CloseAsync(overlay, false);
                }
                var result = await UIManager.Instance.ReplaceAsync<MainMenuView>(new UIShowOptions(animated: false));
                if (result.Status is UIOperationStatus.Failed or UIOperationStatus.Canceled && expected.IsEnable)
                {
                    var restored = await UIManager.Instance.ShowAsync<CinematicOverlayView>(view => view.SetData(expected.Preview),
                        new UIShowOptions(animated: false, hidePrevious: false), expected.LifetimeToken);
                    expected.SetOverlay(restored.View as CinematicOverlayView);
                }
                return result;
            }
            finally
            {
                closing = false;
            }
        }
    }
}
