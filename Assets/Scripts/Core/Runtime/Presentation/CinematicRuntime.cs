using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime.Presentation
{
    /// 注册正式表现根后提供电影演出 API；不创建 Canvas 或资源替代模板。
    public static class CinematicRuntime
    {
        private static Registration current;

        /// 正式公共表现组件是否已注册并完成初始化。
        public static bool IsRegistered => current != null && current.Presenter != null
            && current.Presenter.IsInitialized && current.Presenter.isActiveAndEnabled;

        /// <summary>登记 UIManager 已加载的正式表现根；替换旧根会取消其全部演出。</summary>
        /// <param name="presenter">已经显式初始化的公共表现组件。</param>
        public static void Register(CinematicPresenter presenter)
        {
            EnsureMainThread();
            if (presenter == null || !presenter.IsInitialized)
            {
                throw new ArgumentException("电影演出需要已初始化的正式表现根。", nameof(presenter));
            }

            if (current != null && ReferenceEquals(current.Presenter, presenter))
            {
                return;
            }

            Shutdown();
            current = new Registration(presenter);
        }

        /// <summary>只释放调用者登记的表现根，不误清理下一场景的新实例。</summary>
        /// <param name="expected">退出的正式组件实例。</param>
        public static void Unregister(CinematicPresenter expected)
        {
            EnsureMainThread();
            if (current != null && ReferenceEquals(current.Presenter, expected))
            {
                Shutdown();
            }
        }

        /// <summary>按队列闭幕，等待中点业务，再开幕；失败和取消均清除本次遮幕。</summary>
        /// <param name="preset">正式遮幕 SO 预设。</param>
        /// <param name="midpoint">完全遮住画面后执行的异步业务；为空表示只演出。取消会停止视觉等待，已启动业务自行负责取消或回滚；中点不能等待同一黑幕通道的另一次转场。</param>
        /// <param name="cancellationToken">取消排队或当前演出。</param>
        /// <returns>演出和中点业务全部完成的任务；异常原样传递。</returns>
        public static async UniTask PlayTransitionAsync(CinematicPreset preset,
            Func<CancellationToken, UniTask> midpoint = null, CancellationToken cancellationToken = default)
        {
            var registration = RequireRegistration();
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            preset.Validate();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, registration.Lifetime.Token);
            await registration.CurtainGate.WaitAsync(linked.Token);
            try
            {
                await UniTask.SwitchToMainThread(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                await registration.Presenter.PlayTransitionAsync(preset, midpoint, linked.Token);
            }
            finally
            {
                registration.CurtainGate.Release();
            }
        }

        /// <summary>进入电影黑边并淡出明确传入的 HUD；正常退出可等待 ExitAsync，Dispose 用于立即清理。</summary>
        /// <param name="preset">正式内容宽高比与进入时长预设。</param>
        /// <param name="hudGroups">仅本次需要隐藏的 HUD 根；不搜索或隐藏其它页面。</param>
        /// <param name="cancellationToken">取消后在主线程下一帧恢复本作用域。</param>
        /// <returns>可等待正常退出、或重复与乱序 Dispose 的句柄；必须在 Unity 主线程调用。</returns>
        public static CinematicPresenter.LetterboxHandle PushLetterbox(CinematicPreset preset, IReadOnlyList<CanvasGroup> hudGroups = null,
            CancellationToken cancellationToken = default)
        {
            var registration = RequireRegistration();
            cancellationToken.ThrowIfCancellationRequested();
            return registration.Presenter.PushLetterbox(preset, hudGroups, cancellationToken, registration.Lifetime.Token);
        }

        /// <summary>串行显示章节或地点字幕；语言与字体在本次显示时一次性解析。</summary>
        /// <param name="titleKey">主标题的稳定语言键。</param>
        /// <param name="subtitleKey">副标题键；为空时清空副标题。</param>
        /// <param name="fadeIn">非缩放淡入时间。</param>
        /// <param name="hold">非缩放可读保持时间。</param>
        /// <param name="fadeOut">非缩放淡出时间。</param>
        /// <param name="cancellationToken">取消排队或当前字幕。</param>
        /// <returns>字幕完整退出的任务；取消保证无残留文本或射线阻挡。</returns>
        public static async UniTask ShowTitleAsync(string titleKey, string subtitleKey = null,
            float fadeIn = 0.3f, float hold = 2, float fadeOut = 0.3f, CancellationToken cancellationToken = default)
        {
            var registration = RequireRegistration();
            CinematicPreset.ValidateTitleTiming(fadeIn, hold, fadeOut);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, registration.Lifetime.Token);
            await registration.TitleGate.WaitAsync(linked.Token);
            try
            {
                await UniTask.SwitchToMainThread(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                await registration.Presenter.ShowTitleAsync(titleKey, subtitleKey, fadeIn, hold, fadeOut, linked.Token);
            }
            finally
            {
                registration.TitleGate.Release();
            }
        }

        /// 取消全部已登记演出并恢复 HUD；不销毁 UIManager 拥有的表现根。
        public static void Shutdown()
        {
            EnsureMainThread();
            var previous = current;
            current = null;
            if (previous == null)
            {
                return;
            }

            previous.Lifetime.Cancel();
            if (previous.Presenter != null)
            {
                previous.Presenter.ResetEffects();
            }

            previous.Lifetime.Dispose();
        }

        private static Registration RequireRegistration()
        {
            EnsureMainThread();
            if (!IsRegistered)
            {
                throw new InvalidOperationException("公共电影演出 View 尚未加载并注册，不能创建替代 Canvas。");
            }

            return current;
        }

        internal static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("电影演出只能在 Unity 主线程调用。");
            }
        }

        private sealed class Registration
        {
            internal readonly CinematicPresenter Presenter;
            internal readonly CancellationTokenSource Lifetime = new();
            internal readonly SemaphoreSlim CurtainGate = new(1, 1);
            internal readonly SemaphoreSlim TitleGate = new(1, 1);

            internal Registration(CinematicPresenter presenter) => Presenter = presenter;
        }
    }
}
