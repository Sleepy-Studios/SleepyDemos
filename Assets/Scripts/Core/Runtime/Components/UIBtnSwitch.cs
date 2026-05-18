using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    [RequireComponent(typeof(Button))]
    public sealed class UIBtnSwitch : MonoBehaviour
    {
        [SerializeField] private GameObject onState;
        [SerializeField] private GameObject offState;
        [SerializeField] private bool defaultState;

        private Button button;
        private UnityAction clickHandler;
        private System.Action<bool> onValueChanged;
        private bool initialized;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            Initialize();
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null && clickHandler != null)
            {
                button.onClick.RemoveListener(clickHandler);
            }

            onValueChanged = null;
            initialized = false;
        }

        public void SetAction(System.Action<bool> action)
        {
            onValueChanged = action;
        }

        public void Register(System.Action<bool> action)
        {
            onValueChanged += action;
        }

        public bool GetStatus()
        {
            return IsOn;
        }

        public void SetStatus(bool value, bool notify = false)
        {
            Set(value, notify);
        }

        public void Set(bool value, bool notify = false)
        {
            Initialize();
            IsOn = value;
            Refresh();
            if (notify)
            {
                onValueChanged?.Invoke(IsOn);
            }
        }

        private void Toggle()
        {
            Set(!IsOn, true);
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            IsOn = defaultState;
            button = GetComponent<Button>();
            clickHandler = Toggle;
            if (button != null)
            {
                button.onClick.AddListener(clickHandler);
            }
        }

        private void Refresh()
        {
            if (onState != null)
            {
                onState.SetActive(IsOn);
            }

            if (offState != null)
            {
                offState.SetActive(!IsOn);
            }
        }
    }
}
