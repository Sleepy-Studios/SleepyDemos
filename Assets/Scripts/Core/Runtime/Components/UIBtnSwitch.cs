using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    [RequireComponent(typeof(Button))]
    public sealed class UIBtnSwitch : MonoBehaviour
    {
        [SerializeField] private GameObject onState;
        [SerializeField] private GameObject offState;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
            Refresh();
        }

        public void Set(bool value)
        {
            IsOn = value;
            Refresh();
        }

        private void Toggle()
        {
            Set(!IsOn);
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
