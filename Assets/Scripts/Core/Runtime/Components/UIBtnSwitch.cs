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
        [SerializeField] private UIState uiState;
        [SerializeField] private bool defaultState;

        private Button button;
        private UnityAction clickHandler;
        private System.Action<bool> onValueChanged;
        private bool initialized;

        /// 当前开关状态。
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

        /// <summary>
        /// 覆盖当前状态变化回调。
        /// </summary>
        /// <param name="action">新的状态变化回调，参数为当前状态。</param>
        public void SetAction(System.Action<bool> action)
        {
            onValueChanged = action;
        }

        /// <summary>
        /// 追加注册状态变化回调。
        /// </summary>
        /// <param name="action">状态变化回调，参数为当前状态。</param>
        public void Register(System.Action<bool> action)
        {
            onValueChanged += action;
        }

        /// <summary>
        /// 移除已注册的状态变化回调。
        /// </summary>
        /// <param name="action">需要移除的回调。</param>
        public void Unregister(System.Action<bool> action)
        {
            onValueChanged -= action;
        }

        /// 获取当前开关状态。
        public bool GetStatus()
        {
            return IsOn;
        }

        /// <summary>
        /// 设置当前开关状态。
        /// </summary>
        /// <param name="value">目标状态。</param>
        /// <param name="notify">是否触发状态变化回调。</param>
        public void SetStatus(bool value, bool notify = false)
        {
            Set(value, notify);
        }

        /// <summary>
        /// 设置当前开关状态。
        /// </summary>
        /// <param name="value">目标状态。</param>
        /// <param name="notify">是否触发状态变化回调。</param>
        public void Set(bool value, bool notify = false)
        {
            SetInternal(value, notify);
        }

        private void Toggle()
        {
            SetInternal(!IsOn, true);
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
            uiState = uiState != null ? uiState : GetComponentInChildren<UIState>(true);

            clickHandler = Toggle;
            if (button != null)
            {
                button.onClick.AddListener(clickHandler);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (uiState != null)
            {
                var targetState = IsOn ? "On" : "Off";
                if (uiState.GetState(targetState) != null)
                {
                    uiState.SetState(targetState);
                    return;
                }
            }

            if (onState != null)
            {
                onState.SetActive(IsOn);
            }

            if (offState != null)
            {
                offState.SetActive(!IsOn);
            }
        }

        private void SetInternal(bool value, bool notify)
        {
            Initialize();
            IsOn = value;
            Refresh();
            if (notify)
            {
                onValueChanged?.Invoke(IsOn);
            }
        }
    }
}
