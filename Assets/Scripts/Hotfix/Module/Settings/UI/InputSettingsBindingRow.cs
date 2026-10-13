using System;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hotfix
{
    /// 一个可复用的绑定条目，按钮回调总是替换成当前数据身份。
    public sealed class InputSettingsBindingRow : MonoBehaviour
    {
        [SerializeField] private InputBindingPrompt prompt;
        [SerializeField] private TMP_Text context;
        [SerializeField] private Button rebind;
        [SerializeField] private Button reset;
        [SerializeField] private TMP_Text rebindLabel;
        [SerializeField] private TMP_Text resetLabel;
        private Action rebindAction;
        private Action resetAction;

        private void Awake()
        {
            rebind.onClick.AddListener(OnRebind);
            reset.onClick.AddListener(OnReset);
        }

        /// <summary>绑定准确的单个按键，保留同一动作中的别名及独立绑定身份。</summary>
        public void Bind(InputAction action, int index, string captionKey, string details, InputDeviceKind device,
            bool editable, Action onRebind, Action onReset)
        {
            prompt.SetDisplayDevice(device);
            prompt.SetCaptionKey(captionKey);
            prompt.BindBinding(action, index);
            LanguageService.SetFont(context);
            context.text = details;
            LanguageService.SetText(rebindLabel, editable ? "Settings.Rebind" : "Settings.FixedBindingShort");
            LanguageService.SetText(resetLabel, "Common.Default");
            rebind.interactable = reset.interactable = editable;
            rebindAction = onRebind;
            resetAction = onReset;
        }

        /// 回收后不保留对候选会话和旧行的引用。
        public void Clear()
        {
            rebindAction = resetAction = null;
            prompt.Bind(null);
            prompt.SetDisplayGamepadStyle(null);
        }

        /// <summary>当前候选的图标预览只作用于这一物理条目。</summary>
        public void SetGamepadStyle(GamepadStyle style) => prompt.SetDisplayGamepadStyle(style);

        private void OnRebind() => rebindAction?.Invoke();
        private void OnReset() => resetAction?.Invoke();

        private void OnDestroy()
        {
            rebind.onClick.RemoveListener(OnRebind);
            reset.onClick.RemoveListener(OnReset);
            Clear();
        }
    }
}
