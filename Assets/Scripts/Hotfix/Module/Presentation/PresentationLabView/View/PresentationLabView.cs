using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Core.Runtime.TimeControls;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix
{
    /// 公共效果体验独立页面，只拥有临时演出；玩家公共设置仍由设置服务负责。
    [Module("Presentation")]
    [UIBind("PresentationLabView")]
    public partial class PresentationLabView : View
    {
        private PresentationWorldLease world;
        private CinematicOverlayView overlay;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource playback;
        private int selectedTab = 2;
        private bool busy;
        private bool ready;
        private int operationVersion;
        private float elapsed;
        private int displayedSecond = -1;
        private float displayedScale = -1;

        /// UIManager 托管表现根要跟随的制作预览容器。
        public RectTransform Preview => PresentationLabBindings_Lab.Preview;
        /// 本次显示生命期，Flow 据此取消迟到的表现根请求。
        public CancellationToken LifetimeToken => lifetime?.Token ?? CancellationToken.None;

        protected override void OnGameObjectInitialize()
        {
            var bindings = PresentationLabBindings_Lab;
            bindings.Initialize();
            bindings.Tabs.Register(SelectTab);
            this.RegisterButton(bindings.Play, OnPlay);
            this.RegisterButton(bindings.Stop, OnStop);
            this.RegisterButton(bindings.Reset, OnReset);
            this.RegisterButton(bindings.Close, OnClose);
            UIMenuScope_Menu.Canceled += OnClose;
            BindUpdate(UpdateClock);
        }

        /// <summary>在显示前接收加载世界的所有权，失败或销毁时归还。</summary>
        /// <param name="value">只属于本次页面的独立世界租约。</param>
        public void SetData(PresentationWorldLease value)
        {
            if (world != null && !ReferenceEquals(world, value))
            {
                throw new InvalidOperationException("不能替换仍由体验页持有的世界租约。");
            }
            world = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>正式表现 Widget 完成显示后交付，之后允许电影播放。</summary>
        /// <param name="value">本次 UIManager Show 返回的具体表现 View。</param>
        public void SetOverlay(CinematicOverlayView value)
        {
            overlay = value;
            ready = value != null && value.IsEnable;
            RefreshButtons();
        }

        protected override void OnShow()
        {
            if (world == null)
            {
                throw new InvalidOperationException("体验页必须通过 PresentationLabFlow 打开制作世界。");
            }
            lifetime = new CancellationTokenSource();
            world.Activate();
            GameSettingsService.Changed += RefreshSettings;
            RefreshSettings(GameSettingsService.Current);
            elapsed = 0;
            displayedSecond = -1;
            displayedScale = -1;
            RefreshButtons();
            base.OnShow();
        }

        protected override void OnHide()
        {
            StopImmediately();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            GameSettingsService.Changed -= RefreshSettings;
            world?.Deactivate();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            StopImmediately();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            GameSettingsService.Changed -= RefreshSettings;
            if (PresentationLabBindings_Lab != null && PresentationLabBindings_Lab.Tabs != null)
            {
                PresentationLabBindings_Lab.Tabs.Unregister(SelectTab);
            }
            if (UIMenuScope_Menu != null)
            {
                UIMenuScope_Menu.Canceled -= OnClose;
            }
            world?.Dispose();
            world = null;
            base.OnDestroy();
        }

        private void SelectTab(int index)
        {
            selectedTab = index;
            PresentationLabBindings_Lab.SelectTab(index);
            displayedScale = -1;
        }

        private void RefreshSettings(GameSettingsSnapshot settings)
        {
            foreach (var row in PresentationLabBindings_Lab.Effects)
            {
                row.RefreshLimits(settings.Graphics, settings.Accessibility);
            }
        }

        private void RefreshButtons()
        {
            PresentationLabBindings_Lab.Play.interactable = ready && !busy;
            PresentationLabBindings_Lab.Reset.interactable = !busy;
        }

        private void OnPlay()
        {
            if (!ready || busy || lifetime == null)
            {
                return;
            }
            StopImmediately();
            int version = ++operationVersion;
            busy = true;
            RefreshButtons();
            try
            {
                if (selectedTab == 0)
                {
                    foreach (var row in PresentationLabBindings_Lab.Effects)
                    {
                        row.Play();
                    }
                    CompleteOperation(version);
                }
                else if (selectedTab == 1)
                {
                    PlayTimeAsync(version).Forget(Debug.LogException);
                }
                else
                {
                    PresentationLabBindings_Lab.CinematicPanel.Play(world.World,
                        PresentationLabBindings_Lab.PreviewHud, overlay,
                        () => CompleteOperation(version), exception => FailOperation(version, exception));
                }
            }
            catch (Exception exception)
            {
                FailOperation(version, exception);
            }
        }

        private void OnStop() => StopAndRestore();

        private void OnReset()
        {
            StopImmediately();
            foreach (var row in PresentationLabBindings_Lab.Effects)
            {
                row.ResetControls();
            }
            PresentationLabBindings_Lab.TimePanel.ResetControls();
            PresentationLabBindings_Lab.CinematicPanel.ResetControls();
            RefreshSettings(GameSettingsService.Current);
            world.World.ResetMotion();
            elapsed = 0;
            displayedSecond = -1;
        }

        private void OnClose() => PresentationLabFlow.Close(this);

        private async UniTask PlayTimeAsync(int version)
        {
            var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            playback = operation;
            try
            {
                await PresentationLabBindings_Lab.TimePanel.PlayAsync(operation.Token);
            }
            catch (OperationCanceledException)
            {
                // 本页停止或隐藏已经立即清理拥有的作用域。
            }
            catch (Exception exception)
            {
                FailOperation(version, exception);
            }
            finally
            {
                if (ReferenceEquals(playback, operation))
                {
                    playback = null;
                }
                operation.Dispose();
                CompleteOperation(version);
            }
        }

        private void CompleteOperation(int version)
        {
            if (version != operationVersion)
            {
                return;
            }
            busy = false;
            if (IsLoaded)
            {
                RefreshButtons();
            }
        }

        private void FailOperation(int version, Exception exception)
        {
            if (version != operationVersion)
            {
                return;
            }
            StopImmediately();
            if (IsEnable)
            {
                LanguageService.SetText(PresentationLabBindings_Lab.Status, "Presentation.Failed");
                Debug.LogException(exception);
            }
        }

        /// 播放中立即停止；已完成的演出缩回黑边并恢复原 HUD。
        public void StopAndRestore()
        {
            if (busy || overlay == null || overlay.IsTransitioning || overlay.IsTitlePlaying || lifetime == null)
            {
                StopImmediately();
                return;
            }
            int version = ++operationVersion;
            foreach (var row in PresentationLabBindings_Lab.Effects)
            {
                row.Stop();
            }
            PresentationLabBindings_Lab.TimePanel.Stop();
            busy = true;
            RefreshButtons();
            try
            {
                PresentationLabBindings_Lab.CinematicPanel.ExitLetterbox(() =>
                {
                    if (version != operationVersion)
                    {
                        return;
                    }
                    CompleteOperation(version);
                    displayedScale = -1;
                });
            }
            catch (Exception exception)
            {
                FailOperation(version, exception);
            }
        }

        /// 异常或卸载时同步释放本页所有作用域，停止动画无须等待时间流动。
        public void StopImmediately()
        {
            operationVersion++;
            playback?.Cancel();
            busy = false;
            if (PresentationLabBindings_Lab != null)
            {
                foreach (var row in PresentationLabBindings_Lab.Effects)
                {
                    row.Stop();
                }
                PresentationLabBindings_Lab.TimePanel.Stop();
                PresentationLabBindings_Lab.CinematicPanel.Stop();
            }
            if (overlay != null && overlay.IsLoaded)
            {
                overlay.ResetEffects();
            }
            if (IsEnable)
            {
                RefreshButtons();
            }
            displayedScale = -1;
        }

        private void UpdateClock()
        {
            PresentationLabBindings_Lab.RefreshLayout();
            elapsed += Time.unscaledDeltaTime;
            int seconds = Mathf.FloorToInt(elapsed);
            if (seconds != displayedSecond)
            {
                displayedSecond = seconds;
                LanguageService.SetText(PresentationLabBindings_Lab.Clock, "Presentation.Clock", null, seconds / 60, seconds % 60);
            }
            if (!Mathf.Approximately(displayedScale, GameTimeService.Scale))
            {
                displayedScale = GameTimeService.Scale;
                LanguageService.SetText(PresentationLabBindings_Lab.Status, "Presentation.Experience", null,
                    LanguageService.Get(selectedTab == 0 ? "Presentation.Post" : selectedTab == 1 ? "Presentation.Time" : "Presentation.Cinematic"), displayedScale);
            }
        }
    }
}
