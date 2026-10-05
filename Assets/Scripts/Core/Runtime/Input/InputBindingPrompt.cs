using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Core.Runtime.Inputs
{
    /// Inspector 选择动作与图标目录；设备/绑定变化时更新，业务可绑定会话动作副本。
    public sealed class InputBindingPrompt : MonoBehaviour
    {
        [SerializeField] private InputActionReference action;
        [SerializeField] private InputGlyphCatalog catalog;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string caption;
        [SerializeField] private string uiAction;
        [SerializeField, Tooltip("触控操作按钮只显示动作名，设备按键由独立提示区提供。")]
        private bool captionOnly;
        private InputAction runtimeAction;
        /// 已绑定会话动作优先于 Inspector 的源资产引用。
        public InputAction Action => runtimeAction ?? action?.action;

        /// <summary>将提示绑定到业务会话副本，而不是未生效的源资产。</summary>
        /// <param name="value">实际动作。</param>
        public void Bind(InputAction value) { runtimeAction = value; Refresh(); }

        /// <summary>按源动作GUID绑定实际会话副本，避免页面重复解析提示动作。</summary>
        /// <param name="asset">本次输入会话的动作资产；为空时恢复源引用。</param>
        public void BindAsset(InputActionAsset asset) => Bind(asset != null && action != null ? asset.FindAction(action.action.id) : null);
        private void OnEnable()
        {
            InputDeviceState.Initialize();
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (uiAction == "Submit") runtimeAction = module?.submit?.action;
            if (uiAction == "Cancel") runtimeAction = module?.cancel?.action;
            InputDeviceState.Changed += Refresh;
            InputSystem.onActionChange += OnActionChange;
            Refresh();
        }
        private void OnDisable()
        {
            InputDeviceState.Changed -= Refresh;
            InputSystem.onActionChange -= OnActionChange;
        }
        private void OnActionChange(object target, InputActionChange change)
        { if (change == InputActionChange.BoundControlsChanged) Refresh(); }
        private void Refresh()
        {
            string path = null;
            string key = captionOnly ? string.Empty : InputBindingDisplay.Get(Action, out path);
            var sprite = !captionOnly && catalog != null ? catalog.Get(InputDeviceState.PromptStyle, path) : null;
            if (icon != null) { icon.sprite = sprite; icon.gameObject.SetActive(sprite != null); }
            if (label != null) label.text = sprite != null || string.IsNullOrEmpty(key) ? caption : key + " " + caption;
        }
    }
}
