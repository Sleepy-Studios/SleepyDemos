using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    [RequireComponent(typeof(Button))]
    public sealed class UIDropdown : MonoBehaviour
    {
        [SerializeField] private Text defaultText;
        [SerializeField] private Transform arrow;
        [SerializeField] private UITab tabView;
        [SerializeField] private bool rotateArrow = true;
        [SerializeField] private float expandedArrowZ = 180f;

        private Button button;
        private List<string> options = new List<string>();
        private Action<int> onSelected;
        private bool isExpanded;

        public static event Action<Transform> TabViewShown;
        public event Action<bool> ShowStateChanged;
        public int CurrentIndex => tabView != null ? tabView.Index : -1;
        public bool IsExpanded => isExpanded;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Toggle);
            if (tabView != null)
            {
                tabView.Register(OnTabSelected);
            }

            Collapse(false);
        }

        private void OnEnable()
        {
            Collapse(false);
        }

        private void OnDisable()
        {
            Collapse(false);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
            }

            if (tabView != null)
            {
                tabView.Unregister(OnTabSelected);
            }

            onSelected = null;
        }

        public async UniTask SetDataAsync(
            IList<string> values,
            Action<int> action,
            int selectedIndex = 0,
            string selectedText = null,
            IReadOnlyList<string> itemImages = null,
            IReadOnlyList<float> itemImageScales = null)
        {
            if (values == null || tabView == null)
            {
                return;
            }

            options = new List<string>(values);
            onSelected = action;
            await tabView.InitAsync(options, selectedIndex, false, false, null, null, itemImages, itemImageScales);
            SetSelectedText(string.IsNullOrEmpty(selectedText) && options.Count > selectedIndex ? options[selectedIndex] : selectedText);
            tabView.SetIndex(selectedIndex, false);
            Collapse(false);
        }

        public void SetData(
            IList<string> values,
            Action<int> action,
            int selectedIndex = 0,
            string selectedText = null,
            IReadOnlyList<string> itemImages = null,
            IReadOnlyList<float> itemImageScales = null)
        {
            SetDataAsync(values, action, selectedIndex, selectedText, itemImages, itemImageScales).Forget();
        }

        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        public void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= options.Count || tabView == null)
            {
                return;
            }

            tabView.SetIndex(index, false);
            SetSelectedText(options[index]);
        }

        private void Toggle()
        {
            if (isExpanded)
            {
                Collapse(true);
            }
            else
            {
                Expand();
            }
        }

        private void Expand()
        {
            var changed = !isExpanded;
            isExpanded = true;
            if (tabView != null)
            {
                tabView.gameObject.SetActive(true);
            }

            RefreshArrow();
            TabViewShown?.Invoke(transform);
            if (changed)
            {
                ShowStateChanged?.Invoke(true);
            }
        }

        private void Collapse(bool refreshArrow)
        {
            var changed = isExpanded;
            isExpanded = false;
            if (tabView != null)
            {
                tabView.gameObject.SetActive(false);
            }

            if (refreshArrow)
            {
                RefreshArrow();
            }

            if (changed)
            {
                ShowStateChanged?.Invoke(false);
            }
        }

        private void OnTabSelected(int index)
        {
            if (index >= 0 && index < options.Count)
            {
                SetSelectedText(options[index]);
            }

            onSelected?.Invoke(index);
            Collapse(true);
        }

        private void SetSelectedText(string value)
        {
            if (defaultText != null && !string.IsNullOrEmpty(value))
            {
                defaultText.text = value;
            }
        }

        private void RefreshArrow()
        {
            if (!rotateArrow || arrow == null)
            {
                return;
            }

            var euler = arrow.localEulerAngles;
            euler.z = isExpanded ? expandedArrowZ : 0f;
            arrow.localEulerAngles = euler;
        }
    }
}
