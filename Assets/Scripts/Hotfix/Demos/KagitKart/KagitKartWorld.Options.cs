using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Runtime.Settings;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartWorld
    {
        private View optionsView;
        private bool systemReducedMotion;
        private float fpsElapsed;
        private int fpsFrames;
        private AccessibilityPreferences publicAccessibility = new();

        /// 当前本机赛车偏好；只有保存成功才替换。
        public KagitKartSettings Settings { get; private set; } = new();
        /// 设置保存反馈，不覆盖比赛结果反馈。
        public string SettingsFeedback { get; private set; }
        /// 当前低动态策略；自动使用启动或选项变更时读取的系统偏好。
        public bool ReducedMotion => publicAccessibility.IsReducedMotion || Settings.ResolveReducedMotion(systemReducedMotion);
        /// 触控显示策略独立于当前输入设备。
        public bool ShowTouchControls => Settings.ResolveTouch(Core.Runtime.Inputs.InputDeviceState.ActiveKind);
        /// 公共辅助偏好只控制说明性提示，操作按钮继续可用。
        public bool ShowInputPrompts => publicAccessibility.ShowInputPrompts;
        /// 按真实渲染帧间隔采样，独立于比赛规则固定步。
        public int FramesPerSecond { get; private set; } = -1;
        /// 专属设置或操作说明正在使用菜单输入。
        private bool OptionsOpen => optionsView != null;

        private static void ValidateSettings(KagitKartSettings value)
        {
            if (value == null || !value.IsValid())
            {
                throw new ArgumentException("赛车偏好无效。");
            }
        }

        private void LoadSettings()
        {
            publicAccessibility = GameSettingsService.Current.Accessibility;
            GameSettingsService.Changed += OnPublicSettingsChanged;
            Settings = LocalDataManager.LoadData(LocalDataKeys.KagitKartPreferences, new KagitKartSettings(), ValidateSettings);
            systemReducedMotion = Settings.ReducedMotion == "auto" && SystemMotionPreference.ReadReducedMotion();
            SettingsFeedback = null;
        }

        private void OnPublicSettingsChanged(GameSettingsSnapshot value)
        {
            publicAccessibility = value.Accessibility.Copy();
            ApplySettings();
            Dispatch(KagitKartCommand.Refresh);
        }

        private void SaveSetting(KagitKartAction action)
        {
            try
            {
                var next = Settings.WithValue(action.Id, action.Number);
                LocalDataManager.SaveData(LocalDataKeys.KagitKartPreferences, next, ValidateSettings);
                Settings = next;
                if (action.Id == "ReducedMotion")
                {
                    systemReducedMotion = Settings.ReducedMotion == "auto" && SystemMotionPreference.ReadReducedMotion();
                }
                SettingsFeedback = null;
                ApplySettings();
            }
            catch (Exception exception)
            {
                SettingsFeedback = "设置保存失败，已保留原设置。";
                Debug.LogWarning(exception.Message, this);
            }
        }

        private void ApplySettings()
        {
            audio?.SetVolumes(Settings.MusicVolume, Settings.SfxVolume, Settings.Muted);
            if (Input != null)
            {
                Input.AutoAccelerate = Settings.AutoAccelerate;
            }
            if (cameraRig != null)
            {
                cameraRig.ShakeEnabled = publicAccessibility.CameraShake && Settings.CameraShake && !ReducedMotion;
            }
            if (bookStage != null)
            {
                bookStage.ReducedMotion = ReducedMotion;
            }
            cinematics?.SetReducedMotion(ReducedMotion);
            if (podium != null)
            {
                podium.ReducedMotion = ReducedMotion;
            }
        }

        private void SampleFrameRate()
        {
            if (!Settings.ShowFps)
            {
                fpsElapsed = 0;
                fpsFrames = 0;
                FramesPerSecond = -1;
                return;
            }
            fpsElapsed += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsElapsed >= 0.5f)
            {
                FramesPerSecond = Mathf.RoundToInt(fpsFrames / fpsElapsed);
                fpsElapsed = 0;
                fpsFrames = 0;
            }
        }

        private async UniTaskVoid OpenOptionsAsync(bool controls)
        {
            if (OptionsOpen || Session != null && !Session.Pause.IsPaused)
            {
                return;
            }
            busy = true;
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                var token = lifetimeCancellation.Token;
                var shown = controls
                    ? await UIManager.Instance.ShowAsync<KagitKartControlsView>(view => view.SetData(this), new UIShowOptions(animated: false), token)
                    : await UIManager.Instance.ShowAsync<KagitKartSettingsView>(view => view.SetData(this), new UIShowOptions(animated: false), token);
                if (shown.Status == UIOperationStatus.Failed)
                {
                    throw shown.Exception;
                }
                token.ThrowIfCancellationRequested();
                optionsView = shown.View;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SettingsFeedback = "设置页面加载失败，请重试。";
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }

        private async UniTask CloseOptionsAsync()
        {
            if (optionsView != null)
            {
                var closed = await UIManager.Instance.CloseAsync(optionsView);
                if (closed.Status == UIOperationStatus.Failed)
                {
                    throw closed.Exception;
                }
                optionsView = null;
            }
        }

        private async UniTaskVoid CloseOptionsCommandAsync()
        {
            busy = true;
            Input.ResetInput();
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                await CloseOptionsAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SettingsFeedback = "页面关闭失败，请重试。";
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
                Dispatch(KagitKartCommand.Refresh);
            }
        }
    }
}
