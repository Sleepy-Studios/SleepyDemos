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
    /// 协调本场页面导航与焦点，业务状态由 Data 和 Handler 管理。
    internal sealed class JinxCasinoUIController : IDisposable
    {
        private readonly JinxCasinoController owner;

        private readonly CancellationToken token;

        private View current;

        private readonly Dictionary<JinxCasinoPage, string> focusNames = new();

        private JinxCasinoPage shownKind = JinxCasinoPage.None;

        private bool changing;

        private bool started;

        private bool stopping;

        private JinxCasinoPage menuState = JinxCasinoPage.None;

        internal JinxCasinoPage State => owner.Data.Page;

        internal JinxCasinoUIController(JinxCasinoController controller, CancellationToken lifetime)
        {
            owner = controller;
            token = lifetime;
            GlobalData.Subscribe<JinxCasinoData>(OnData);
        }

        private void Dispatch(JinxCasinoUiCommand command) => GlobalData.Dispatch(new JinxCasinoUiAction(owner, command));

        internal void OnSettingsClosed() => Dispatch(JinxCasinoUiCommand.CloseSettings);

        internal void CancelImmersionHudWindow() => Dispatch(JinxCasinoUiCommand.CancelWindow);

        private void OnData(JinxCasinoData value)
        {
            if (ReferenceEquals(value, owner.Data))
                Refresh();
        }

        internal void Begin()
        {
            started = true;
            Refresh();
        }

        internal void SetFirstSelection(GameObject first) => owner.Player.SetMenuState(menuState != JinxCasinoPage.Field, menuState == JinxCasinoPage.Settings, first, CancelImmersionHudWindow);

        internal void OpenSettings() => Dispatch(JinxCasinoUiCommand.OpenSettings);

        internal void OpenSaveLoad() => Dispatch(JinxCasinoUiCommand.OpenSaveLoad);

        internal void OpenSaveWrite() => Dispatch(JinxCasinoUiCommand.OpenSaveWrite);

        internal void Refresh()
        {
            if (!started || stopping)
                return;
            JinxCasinoPage state = owner.Data.Page;
            if (menuState != state)
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (current != null && selected != null && selected.transform.IsChildOf(current.transform))
                    focusNames[shownKind] = selected.name;
                menuState = state;
                owner.Player.SetMenuState(state != JinxCasinoPage.Field, state == JinxCasinoPage.Settings, null, CancelImmersionHudWindow);
            }

            if (!changing && shownKind != WantedKind)
                SynchronizeAsync().Forget();
        }

        private JinxCasinoPage WantedKind => stopping || owner.IsBusy ? JinxCasinoPage.None : menuState is JinxCasinoPage.TutorialReady or JinxCasinoPage.TutorialChoice or JinxCasinoPage.TutorialConfirm ? JinxCasinoPage.TutorialReady : menuState is JinxCasinoPage.SaveSlots or JinxCasinoPage.SaveConfirm ? JinxCasinoPage.SaveSlots : menuState == JinxCasinoPage.Field ? JinxCasinoPage.None : menuState;

        private async UniTaskVoid SynchronizeAsync()
        {
            changing = true;
            try
            {
                while (shownKind != WantedKind)
                {
                    if (current != null)
                    {
                        await CloseViewAsync(current);
                        current = null;
                        shownKind = JinxCasinoPage.None;
                    }

                    JinxCasinoPage kind = WantedKind;
                    if (kind == JinxCasinoPage.None)
                        continue;
                    var options = new UIShowOptions(animated: false, hidePrevious: false);
                    UIOperationResult result = kind switch
                    {
                        JinxCasinoPage.MainMenu => await UIManager.Instance.ShowAsync<JinxCasinoMainMenuView>(view => view.SetData(owner), options, token),
                        JinxCasinoPage.Pause => await UIManager.Instance.ShowAsync<JinxCasinoPauseView>(view => view.SetData(owner), options, token),
                        JinxCasinoPage.TutorialReady => await UIManager.Instance.ShowAsync<JinxCasinoTutorialView>(view => view.SetData(owner), options, token),
                        JinxCasinoPage.SaveSlots => await UIManager.Instance.ShowAsync<JinxCasinoSaveView>(view => view.SetData(owner), options, token),
                        JinxCasinoPage.Ending => await UIManager.Instance.ShowAsync<JinxCasinoEndingView>(view => view.SetData(owner), options, token),
                        JinxCasinoPage.Settings => await UIManager.Instance.ShowAsync<JinxCasinoSettingsView>(view => view.SetData(owner), options, token),
                        _ => throw new InvalidOperationException("未知赌场窗口状态。")
                    };
                    if (result.Status == UIOperationStatus.Canceled)
                        return;
                    if (result.Status == UIOperationStatus.Failed)
                        throw result.Exception;
                    current = result.View ?? throw new InvalidOperationException("赌场窗口没有返回实例。");
                    shownKind = kind;
                    if (focusNames.TryGetValue(kind, out var name))
                        foreach (var selectable in current.gameObject.GetComponentsInChildren<Selectable>(true))
                            if (selectable.name == name && selectable.IsActive() && selectable.IsInteractable())
                            {
                                SetFirstSelection(selectable.gameObject);
                                break;
                            }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, owner);
            }
            finally
            {
                changing = false;
            }
        }

        private static async UniTask CloseViewAsync(View view)
        {
            var result = await UIManager.Instance.CloseAsync(view, false);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
        }

        internal async UniTask CloseAsync()
        {
            stopping = true;
            await UniTask.WaitUntil(() => !changing);
            if (current != null)
            {
                await CloseViewAsync(current);
                current = null;
                shownKind = JinxCasinoPage.None;
            }
        }

        internal void Restore()
        {
            stopping = false;
            Refresh();
        }

        public void Dispose()
        {
            stopping = true;
            GlobalData.UnSubscribe<JinxCasinoData>(OnData);
        }
    }
}
