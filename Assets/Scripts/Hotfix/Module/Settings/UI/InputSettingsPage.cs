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
    public sealed class InputSettingsPage : SettingsPage
    {
        [SerializeField] private TMP_Text bindingFeedback;
        [SerializeField] private LoopScrollView bindingsList;
        private readonly Dictionary<string, InputRebindSession> sessions = new();
        private readonly List<BindingEntry> bindings = new();
        private int hostIndex;
        private int deviceIndex;
        private InputRebindSession session;
        private bool releasing;
        private bool attached;

        private sealed class BindingEntry
        {
            public InputAction Action;
            public int Index;
            public string CaptionKey;
            public string Key;
        }

        /// <summary>由独立输入分页 View 注册一次列表桥接，订阅随该 View 销毁。</summary>
        /// <param name="owner">拥有本分页生命周期的 View。</param>
        public void AttachList(View owner)
        {
            if (attached) throw new InvalidOperationException("输入列表已注册。");
            attached = true;
            owner.RegisterLoopScrollRect<SettingsBindingItemView>(bindingsList, BindItem);
            owner.RegisterLoopScrollItemHide<SettingsBindingItemView>(bindingsList, item => item.Clear());
        }

        public bool IsListening => session?.IsListening == true;

        public override void Refresh()
        {
            var value = Candidate.Input;
            var ranges = GameSettingsService.Options;
            Slider("MouseLookMultiplier", ranges.LookMultiplier, value.MouseLookMultiplier, next => value.MouseLookMultiplier = next);
            Slider("TouchLookMultiplier", ranges.LookMultiplier, value.TouchLookMultiplier, next => value.TouchLookMultiplier = next);
            Slider("GamepadLookMultiplier", ranges.LookMultiplier, value.GamepadLookMultiplier, next => value.GamepadLookMultiplier = next);
            Slider("GamepadLookDegreesPerSecond", ranges.GamepadAngularSpeed, value.GamepadLookDegreesPerSecond, next => value.GamepadLookDegreesPerSecond = next);
            Slider("GamepadDeadzone", ranges.GamepadDeadzone, value.GamepadDeadzone, next => value.GamepadDeadzone = next);
            Slider("GamepadMaximum", ranges.GamepadMaximum, value.GamepadMaximum, next => value.GamepadMaximum = next);
            Slider("RumbleStrength", ranges.RumbleStrength, value.RumbleStrength, next => value.RumbleStrength = next);
            Toggle("GamepadInvertY", value.GamepadInvertY, next => value.GamepadInvertY = next);
            Toggle("RumbleEnabled", value.RumbleEnabled, next => value.RumbleEnabled = next);

            var hosts = GameSettingsService.InputHosts;
            hostIndex = Mathf.Clamp(hostIndex, 0, hosts.Count - 1);
            var names = new List<string>();
            foreach (var host in hosts) names.Add(LanguageService.Get(host.DisplayKey));
            Row("Host").SetLabel("Settings.InputHost");
            Row("Host").SetOptions(names, hostIndex, index =>
            {
                CancelRebind();
                hostIndex = index;
                RefreshBindings();
            });
            Row("Device").SetLabel("Settings.InputDevice");
            Row("Device").SetOptions(new[] { LanguageService.Get("Settings.KeyboardMouse"), LanguageService.Get("Settings.Gamepad") },
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
            ConfigureAction("CancelRebind", "Common.Cancel", CancelRebind);
            bindingsList.SetTotalCount(bindings, getItemKey: value => ((BindingEntry)value).Key);
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
        }

        private void RefreshAvailability()
        {
            Row("Host").SetAvailable(!IsListening);
            Row("Device").SetAvailable(!IsListening);
            Row("CancelRebind").gameObject.SetActive(IsListening);
            bindingsList.RefreshCells();
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

        private void ConfigureAction(string field, string caption, Action callback)
        {
            Row(field).SetLabel(caption);
            Row(field).SetValue(LanguageService.Get(caption));
            Row(field).SetAction(callback);
        }

        private void Feedback(string message)
        {
            LanguageService.SetFont(bindingFeedback);
            bindingFeedback.text = message ?? string.Empty;
        }

        private void Slider(string field, Vector2 range, float value, Action<float> update)
        {
            var row = Row(field);
            row.SetLabel("Settings." + field);
            row.SetValue(value.ToString("0.##", LanguageService.CurrentCulture));
            row.SetSlider(range, value, next =>
            {
                row.SetValue(next.ToString("0.##", LanguageService.CurrentCulture));
                Change(() => update(next));
            });
        }

        private void Toggle(string field, bool value, Action<bool> update)
        {
            Row(field).SetLabel("Settings." + field);
            Row(field).SetToggle(value, next => Change(() => update(next)));
        }

        public override void Release()
        {
            releasing = true;
            try
            {
                foreach (var value in sessions.Values) value.Dispose();
                sessions.Clear();
                session = null;
                bindings.Clear();
                bindingsList.SetTotalCount(null);
            }
            finally { releasing = false; }
        }

        private void OnDisable() => CancelRebind();
        private void OnDestroy() => Release();
    }
}
