using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using UnityEngine;
namespace Hotfix.JinxCasino.UI
{
    /// 保存本场景的窗口请求与待确认状态，不持有页面控件。
    internal sealed class JinxCasinoUIController : IDisposable
    {
        private readonly JinxCasinoController owner;
        private readonly CancellationToken token;
        private View current;
        private readonly Dictionary<int, string> focusNames = new();
        private int shownKind = -1;
        private bool changing;
        private bool started;
        private bool stopping;
        private int menuState = -1;
        private bool settingsOpen;
        private int lastSettingsCancelFrame = -1;
        internal JinxCasinoSaveWindowState Save { get; }
        internal JinxCasinoTutorialWindowState Tutorial { get; }
        internal int State => menuState;
        internal JinxCasinoUIController(JinxCasinoController controller, CancellationToken lifetime)
        { owner = controller; token = lifetime; Save = new(owner); Tutorial = new(owner); owner.Changed += Refresh; owner.Player.Changed += Refresh; }
        internal void Begin() { started = true; Refresh(); }
        internal void SetFirstSelection(GameObject first) => owner.Player.SetMenuState(menuState != 2, menuState == 9, first, CancelImmersionHudWindow);
        internal void OpenSettings()
        { if (owner.IsBusy || settingsOpen || menuState != 0 && menuState != 1) return; settingsOpen = true; Refresh(); }
        internal void OnSettingsClosed()
        { lastSettingsCancelFrame = Time.frameCount; settingsOpen = false; Refresh(); }
        internal void CloseSettingsWindow()
        {
            if (!settingsOpen || lastSettingsCancelFrame == Time.frameCount) return;
            lastSettingsCancelFrame = Time.frameCount;
            var view = current as JinxCasinoSettingsView;
            if (view != null) view.CancelPreview(); else OnSettingsClosed();
        }
        internal void OpenSaveLoad() => Save.Open(false);
        internal void OpenSaveWrite() => Save.Open(true);
        private int ResolveEndingHudState(int normal) => owner.Player.Exit.HasEnding && !owner.Game.HasActiveRound && !owner.Player.HasFocus ? 8 : normal;
        internal void Refresh()
        {
            if (!started || stopping) return;
            int state = settingsOpen ? 9 : Save.IsOpen ? Save.PendingSlot > 0 ? 7 : 6 : ResolveEndingHudState(Tutorial.ResolveState());
            if (menuState != state)
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (current != null && selected != null && selected.transform.IsChildOf(current.transform)) focusNames[shownKind] = selected.name;
                menuState = state;
                owner.Player.SetMenuState(state != 2, state == 9, null, CancelImmersionHudWindow);
            }
            if (!changing && shownKind != WantedKind) SynchronizeAsync().Forget();
        }
        private int WantedKind => stopping || owner.IsBusy ? -1 : menuState is 3 or 4 or 5 ? 3 : menuState is 6 or 7 ? 6 : menuState == 2 ? -1 : menuState;
        private async UniTaskVoid SynchronizeAsync()
        {
            changing = true;
            try
            {
                while (shownKind != WantedKind)
                {
                    if (current != null)
                    {
                        await CloseViewAsync(current); current = null; shownKind = -1;
                    }
                    int kind = WantedKind;
                    if (kind < 0) continue;
                    var options = new UIShowOptions(animated: false, hidePrevious: false);
                    UIOperationResult result = kind switch
                    {
                        0 => await UIManager.Instance.ShowAsync<JinxCasinoMainMenuView, JinxCasinoController>(owner, options, token),
                        1 => await UIManager.Instance.ShowAsync<JinxCasinoPauseView, JinxCasinoController>(owner, options, token),
                        3 => await UIManager.Instance.ShowAsync<JinxCasinoTutorialView, JinxCasinoController>(owner, options, token),
                        6 => await UIManager.Instance.ShowAsync<JinxCasinoSaveView, JinxCasinoController>(owner, options, token),
                        8 => await UIManager.Instance.ShowAsync<JinxCasinoEndingView, JinxCasinoController>(owner, options, token),
                        9 => await UIManager.Instance.ShowAsync<JinxCasinoSettingsView, JinxCasinoController>(owner, options, token),
                        _ => throw new InvalidOperationException("未知赌场窗口状态。")
                    };
                    if (result.Status == UIOperationStatus.Canceled) return;
                    if (result.Status == UIOperationStatus.Failed) throw result.Exception;
                    current = result.View ?? throw new InvalidOperationException("赌场窗口没有返回实例。"); shownKind = kind;
                    if (focusNames.TryGetValue(kind, out var name))
                        foreach (var selectable in current.gameObject.GetComponentsInChildren<Selectable>(true))
                            if (selectable.name == name && selectable.IsActive() && selectable.IsInteractable())
                            { SetFirstSelection(selectable.gameObject); break; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, owner); }
            finally { changing = false; }
        }
        private static async UniTask CloseViewAsync(View view)
        { var result = await UIManager.Instance.CloseAsync(view, false); if (result.Status == UIOperationStatus.Failed) throw result.Exception; }
        internal async UniTask CloseAsync()
        {
            stopping = true; await UniTask.WaitUntil(() => !changing);
            if (current != null)
                    {
                        await CloseViewAsync(current); current = null; shownKind = -1;
                    }
        }
        internal void Restore() { stopping = false; Refresh(); }
        public void Dispose() { stopping = true; owner.Changed -= Refresh; owner.Player.Changed -= Refresh; }
        internal void CancelImmersionHudWindow()
        {
            if (lastSettingsCancelFrame == Time.frameCount) return;
            if (settingsOpen) CloseSettingsWindow();
            else if (menuState == 8) Refresh();
            else if (Save.IsOpen) Save.CancelSaveWindow();
            else Tutorial.CancelTutorialWindow();
        }
    }
}
