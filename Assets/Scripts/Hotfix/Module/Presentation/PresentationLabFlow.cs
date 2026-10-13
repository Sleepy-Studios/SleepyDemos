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

        /// <summary>按钮调用入口；成功后通过回调取得完整可用页面。</summary>
        /// <param name="onOpened">页面及其必要资源就绪后调用；可省略。</param>
        /// <param name="onFailed">失败反馈；省略时记录 Console，正常取消不反馈错误。</param>
        /// <param name="cancellationToken">取消本次准备或打开。</param>
        public static void Show(Action<PresentationLabView> onOpened = null,
            Action<Exception> onFailed = null, CancellationToken cancellationToken = default)
        {
            ShowRequestAsync(onOpened, onFailed, cancellationToken).Forget(UnityEngine.Debug.LogException);
        }

        private static async UniTask ShowRequestAsync(Action<PresentationLabView> onOpened,
            Action<Exception> onFailed, CancellationToken cancellationToken)
        {
            PresentationLabView view;
            try
            {
                view = await OpenAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (onFailed == null)
                {
                    UnityEngine.Debug.LogException(exception);
                }
                else
                {
                    onFailed(exception);
                }

                return;
            }

            onOpened?.Invoke(view);
        }

        /// <summary>从稳定 Hub 打开体验页；重复打开不会再申请世界资源。</summary>
        /// <param name="token">资源准备和 UI 导航的取消令牌；失败时归还已申请的租约。</param>
        /// <returns>页面与表现根均显示后的体验页实例；错误保留真实异常。</returns>
        public static async UniTask<PresentationLabView> OpenAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (opening || closing)
            {
                throw new InvalidOperationException("公共效果体验正在打开或关闭，请等待当前操作完成。");
            }
            var existing = UIManager.Instance.Get<PresentationLabView>();
            if (existing?.State == ViewState.Visible)
            {
                return existing;
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
                lab = await UIManager.Instance.OpenAsync<PresentationLabView>(view => view.SetData(world),
                    new UIOpenOptions(animated: false), token);
                using var overlayLifetime = CancellationTokenSource.CreateLinkedTokenSource(token, lab.LifetimeToken);
                overlay = await UIManager.Instance.OpenAsync<CinematicOverlayView>(view => view.SetData(lab.Preview),
                    new UIOpenOptions(animated: false, hidePrevious: false), overlayLifetime.Token);
                lab.SetOverlay(overlay);
                world = null;
                return lab;
            }
            catch
            {
                if (overlay != null)
                {
                    await TryRecoverAsync(() => UIManager.Instance.CloseAsync(overlay, false));
                }

                if (lab != null)
                {
                    await TryRecoverAsync(() => UIManager.Instance.CloseAsync(lab, false));
                    await TryRecoverAsync(() => UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(false)).AsUniTask());
                }

                throw;
            }
            finally
            {
                opening = false;
                TryRelease(world);
                TryRelease(loader);
            }
        }

        private static async UniTask TryRecoverAsync(Func<UniTask> recovery)
        {
            try
            {
                await recovery();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void TryRelease(IDisposable resource)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        /// <summary>按钮关闭入口；停止、恢复和资源释放由既有流程完成。</summary>
        /// <param name="expected">本次体验页的具体实例。</param>
        public static void Close(PresentationLabView expected)
        {
            CloseAsync(expected).Forget(UnityEngine.Debug.LogException);
        }

        /// <summary>停止本次体验，再关闭其具体表现根并通过原 UIManager 返回 Hub。</summary>
        /// <param name="expected">触发关闭的具体体验页，过期实例不会关闭随后打开的新页面。</param>
        /// <returns>返回 Hub；失败时恢复仍可见的体验页表现根并抛原异常。</returns>
        public static async UniTask CloseAsync(PresentationLabView expected)
        {
            if (closing || expected == null || !ReferenceEquals(UIManager.Instance.Get<PresentationLabView>(), expected))
            {
                return;
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
                await UIManager.Instance.ReplaceAsync<MainMenuView>(animated: false);
            }
            catch
            {
                if (expected.IsEnable)
                {
                    await TryRecoverAsync(async () =>
                    {
                        var restored = await UIManager.Instance.OpenAsync<CinematicOverlayView>(view => view.SetData(expected.Preview),
                            new UIOpenOptions(animated: false, hidePrevious: false), expected.LifetimeToken);
                        expected.SetOverlay(restored);
                    });
                }

                throw;
            }
            finally
            {
                closing = false;
            }
        }
    }
}
