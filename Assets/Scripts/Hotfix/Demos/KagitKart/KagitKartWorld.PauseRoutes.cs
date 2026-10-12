using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartWorld
    {
        private bool TryHandleRaceShortcuts()
        {
            if (Session == null)
            {
                return false;
            }
            bool racePhase = !IntroPlaying && (Session.Race.State == KagitKartRaceState.Countdown
                || Session.Race.State == KagitKartRaceState.Running || Session.Race.State == KagitKartRaceState.Finishing);
            if (Mode == KagitKartRaceMode.TimeTrial && racePhase && Input.Pressed("Restart"))
            {
                Dispatch(KagitKartCommand.Restart, playClick: false);
                return true;
            }
            if (!Input.Pressed("Pause"))
            {
                return false;
            }
            if (Session.Pause.IsPaused)
            {
                // Escape既是Pause又是Core菜单Cancel；Esc/B由当前UIMenuScope只退一层。
                var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
                if (module?.cancel?.action?.WasPressedThisFrame() == true)
                {
                    return false;
                }
                Dispatch(KagitKartCommand.Resume);
                return true;
            }
            if (racePhase)
            {
                Dispatch(KagitKartCommand.Pause, playClick: false);
                return true;
            }
            return false;
        }

        private async UniTaskVoid ResumeFromOptionsAsync()
        {
            busy = true;
            Input.ResetInput();
            Dispatch(KagitKartCommand.Refresh);
            try
            {
                await CloseOptionsAsync();
                Input.ResetInput();
                Session?.Pause.TryResume();
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
                // TryResume的Changed发生时busy为true；结束后补跑已有Modal同步。
                SynchronizeModalAsync().Forget();
                Dispatch(KagitKartCommand.Refresh);
            }
        }
    }
}
