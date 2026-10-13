using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using TMPro;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix
{
    /// 控制参数与正式动作候选；监听只修改克隆资产，应用前不影响玩法输入。
    [Module("Settings")]
    [UIBind("InputSettingsPanelView")]
    public partial class InputSettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows(
                UISettingRow_GamepadStyle,
                UISettingRow_MouseLookMultiplier,
                UISettingRow_TouchLookMultiplier,
                UISettingRow_GamepadLookMultiplier,
                UISettingRow_GamepadLookDegreesPerSecond,
                UISettingRow_GamepadDeadzone,
                UISettingRow_GamepadMaximum,
                UISettingRow_RumbleStrength,
                UISettingRow_GamepadInvertY,
                UISettingRow_RumbleEnabled,
                UISettingRow_Host,
                UISettingRow_Device,
                UISettingRow_CancelRebind);
            sessions ??= new Dictionary<string, InputRebindSession>();
            sessions.Clear();
            bindings ??= new List<BindingEntry>();
            bindings.Clear();
            session = null;
            releasing = false;
            this.RegisterLoopScrollRect<SettingsBindingItemView>(LoopScrollView_Bindings, BindItem);
            this.RegisterLoopScrollItemHide<SettingsBindingItemView>(LoopScrollView_Bindings, item => item.Clear());
            base.OnGameObjectInitialize();
        }

        private Dictionary<string, InputRebindSession> sessions;
        private List<BindingEntry> bindings;
        private int hostIndex;
        private int deviceIndex;
        private InputRebindSession session;
        private bool releasing;

        private sealed class BindingEntry
        {
            public InputAction Action;
            public int Index;
            public string CaptionKey;
            public string Key;
        }

        public bool IsListening => session?.IsListening == true;

        public override void Refresh()
        {
            if (!IsLoaded) return;
            var value = Candidate.Input;
            var ranges = GameSettingsService.Options;
            Slider(UISettingRow_MouseLookMultiplier, ranges.LookMultiplier, value.MouseLookMultiplier, next => value.MouseLookMultiplier = next);
            Slider(UISettingRow_TouchLookMultiplier, ranges.LookMultiplier, value.TouchLookMultiplier, next => value.TouchLookMultiplier = next);
            Slider(UISettingRow_GamepadLookMultiplier, ranges.LookMultiplier, value.GamepadLookMultiplier, next => value.GamepadLookMultiplier = next);
            Slider(UISettingRow_GamepadLookDegreesPerSecond, ranges.GamepadAngularSpeed, value.GamepadLookDegreesPerSecond, next => value.GamepadLookDegreesPerSecond = next);
            Slider(UISettingRow_GamepadDeadzone, ranges.GamepadDeadzone, value.GamepadDeadzone, next => value.GamepadDeadzone = next);
            Slider(UISettingRow_GamepadMaximum, ranges.GamepadMaximum, value.GamepadMaximum, next => value.GamepadMaximum = next);
            Slider(UISettingRow_RumbleStrength, ranges.RumbleStrength, value.RumbleStrength, next => value.RumbleStrength = next);
            Toggle(UISettingRow_GamepadInvertY, value.GamepadInvertY, next => value.GamepadInvertY = next);
            Toggle(UISettingRow_RumbleEnabled, value.RumbleEnabled, next => value.RumbleEnabled = next);
            UISettingRow_GamepadStyle.SetLabel("Settings.GamepadStyle");
            UISettingRow_GamepadStyle.SetOptions(new[] { "Xbox", "PS5", "NS" }, (int)value.GamepadStyle - 1, index =>
            {
                Change(() => value.GamepadStyle = (GamepadStyle)(index + 1));
                LoopScrollView_Bindings.RefreshCells();
            });

            var hosts = GameSettingsService.InputHosts;
            hostIndex = Mathf.Clamp(hostIndex, 0, hosts.Count - 1);
            var names = new List<string>();
            foreach (var host in hosts) names.Add(LanguageService.Get(host.DisplayKey));
            UISettingRow_Host.SetLabel("Settings.InputHost");
            UISettingRow_Host.SetOptions(names, hostIndex, index =>
            {
                CancelRebind();
                hostIndex = index;
                RefreshBindings();
            });
            UISettingRow_Device.SetLabel("Settings.InputDevice");
            UISettingRow_Device.SetOptions(new[] { LanguageService.Get("Settings.KeyboardMouse"), LanguageService.Get("Settings.Gamepad") },
                deviceIndex, index => { CancelRebind(); deviceIndex = index; RefreshBindings(); });
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            var host = GameSettingsService.InputHosts[hostIndex];
            if (!sessions.TryGetValue(host.SourceId, out session))
            {
                Candidate.BindingOverrides.TryGetValue(host.SourceId, out string saved);
                session = new InputRebindSession(host.Asset, saved);
                sessions.Add(host.SourceId, session);
            }
            bindings.Clear();
            foreach (var item in host.Actions)
            {
                var candidateAction = session.Asset.FindAction(item.ActionId, true);
                for (int index = 0; index < candidateAction.bindings.Count; index++)
                {
                    var binding = candidateAction.bindings[index];
                    if (binding.isComposite) continue;
                    var device = deviceIndex == 1 ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse;
                    if (InputBindingDisplay.GetParts(candidateAction, out _, device, bindingIndex: index).Count == 0) continue;
                    bindings.Add(new BindingEntry { Action = candidateAction, Index = index, CaptionKey = item.DisplayKey,
                        Key = host.SourceId + "/" + candidateAction.id + "/" + binding.id });
                }
            }
            ConfigureAction(UISettingRow_CancelRebind, "Common.Cancel", CancelRebind);
            LoopScrollView_Bindings.SetTotalCount(bindings, getItemKey: value => ((BindingEntry)value).Key);
            RefreshAvailability();
        }

        private void BindItem(SettingsBindingItemView item, int index)
        {
            var entry = bindings[index];
            bool editable = session.CanRebind(entry.Action.id.ToString(), entry.Index, out _);
            var binding = entry.Action.bindings[entry.Index];
            string part = binding.isPartOfComposite ? " · " + LanguageService.Get("Settings.Binding." + binding.name.ToLowerInvariant()) : string.Empty;
            item.SetData(entry.Action, entry.Index, entry.CaptionKey, entry.Action.actionMap.name + part,
                deviceIndex == 1 ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse, editable && !IsListening,
                () => BeginRebind(entry), () => ResetAction(entry));
            item.SetGamepadStyle(Candidate.Input.GamepadStyle);
        }

        private void RefreshAvailability()
        {
            gameObject.GetComponentInParent<UIMenuScope>(true)?.SetNavigationEnabled(!IsListening);
            UISettingRow_Host.SetAvailable(!IsListening);
            UISettingRow_Device.SetAvailable(!IsListening);
            UISettingRow_GamepadStyle.SetAvailable(!IsListening);
            UISettingRow_CancelRebind.gameObject.SetActive(IsListening);
            LoopScrollView_Bindings.RefreshCells();
        }

        private void BeginRebind(BindingEntry entry)
        {
            Feedback(LanguageService.Get("Settings.RebindListening"));
            session.Begin(entry.Action.id.ToString(), entry.Index, (succeeded, message) =>
            {
                if (releasing) return;
                if (succeeded) StoreOverrides();
                string feedback = LanguageService.Get(succeeded ? "Settings.RebindDone" : "Settings.RebindCancelled");
                if (!succeeded && session.ConflictingActionId != null)
                {
                    feedback = LanguageService.Format("Settings.RebindConflict", LanguageService.Get("Settings.OtherAction"));
                    foreach (var item in GameSettingsService.InputHosts[hostIndex].Actions)
                    {
                        if (item.ActionId != session.ConflictingActionId) continue;
                        feedback = LanguageService.Format("Settings.RebindConflict", LanguageService.Get(item.DisplayKey));
                        break;
                    }
                }
                Feedback(feedback);
                RefreshBindings();
                Edited?.Invoke();
            });
            RefreshAvailability();
            Edited?.Invoke();
        }

        public void CancelRebind() => session?.Cancel();

        private void ResetAction(BindingEntry entry)
        {
            session.ResetAction(entry.Action.id.ToString());
            StoreOverrides();
            Feedback(LanguageService.Get("Settings.RebindDone"));
            RefreshBindings();
        }

        private void StoreOverrides() => Change(() => Candidate.BindingOverrides[session.SourceId] = session.ExportOverrides());

        private void ConfigureAction(UISettingRow row, string caption, Action callback)
        {
            row.SetLabel(caption);
            row.SetValue(LanguageService.Get(caption));
            row.SetAction(callback);
        }

        private void Feedback(string message)
        {
            LanguageService.SetFont(TextMeshProUGUI_Binding_feedback);
            TextMeshProUGUI_Binding_feedback.text = message ?? string.Empty;
        }

        private void Slider(UISettingRow row, Vector2 range, float value, Action<float> update)
        {
            row.SetLabel("Settings." + row.Key);
            row.SetValue(value.ToString("0.##", LanguageService.CurrentCulture));
            row.SetSlider(range, value, next =>
            {
                row.SetValue(next.ToString("0.##", LanguageService.CurrentCulture));
                Change(() => update(next));
            });
        }

        private void Toggle(UISettingRow row, bool value, Action<bool> update)
        {
            row.SetLabel("Settings." + row.Key);
            row.SetToggle(value, next => Change(() => update(next)));
        }

        public override void Release()
        {
            if (Rows == null) return;
            releasing = true;
            try
            {
                foreach (var value in sessions.Values) value.Dispose();
                sessions.Clear();
                session = null;
                bindings.Clear();
                LoopScrollView_Bindings.SetTotalCount(null);
            }
            finally
            {
                releasing = false;
                gameObject.GetComponentInParent<UIMenuScope>(true)?.SetNavigationEnabled(true);
            }
        }

    }
}
