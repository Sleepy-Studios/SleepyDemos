using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private Button button;
        private List<string> options = new List<string>();
        private Action<int> onSelected;
        private bool isExpanded;

        public static event Action<Transform> TabViewShown;
        public int CurrentIndex => tabView != null ? tabView.Index : -1;

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

        private void Update()
        {
            if (!isExpanded || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current == null || !EventSystem.current.enabled)
            {
                return;
            }

            raycastResults.Clear();
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            EventSystem.current.RaycastAll(eventData, raycastResults);
            if (!IsPointerInsideDropdown(raycastResults))
            {
                Collapse(true);
            }
        }

        public async UniTask SetDataAsync(IList<string> values, Action<int> action, int selectedIndex = 0, string selectedText = null)
        {
            if (values == null || tabView == null)
            {
                return;
            }

            options = new List<string>(values);
            onSelected = action;
            await tabView.InitAsync(options, selectedIndex, false);
            SetSelectedText(string.IsNullOrEmpty(selectedText) && options.Count > selectedIndex ? options[selectedIndex] : selectedText);
            tabView.SetIndex(selectedIndex, false);
            Collapse(false);
        }

        public void SetData(IList<string> values, Action<int> action, int selectedIndex = 0, string selectedText = null)
        {
            SetDataAsync(values, action, selectedIndex, selectedText).Forget();
        }

        public void Register(Action<int> action)
        {
            onSelected += action;
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
            isExpanded = true;
            if (tabView != null)
            {
                tabView.gameObject.SetActive(true);
            }

            RefreshArrow();
            TabViewShown?.Invoke(transform);
        }

        private void Collapse(bool refreshArrow)
        {
            isExpanded = false;
            if (tabView != null)
            {
                tabView.gameObject.SetActive(false);
            }

            if (refreshArrow)
            {
                RefreshArrow();
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

        private bool IsPointerInsideDropdown(List<RaycastResult> results)
        {
            if (results.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < results.Count; i++)
            {
                var hit = results[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    return true;
                }

                if (tabView != null && (hit.transform == tabView.transform || hit.transform.IsChildOf(tabView.transform)))
                {
                    return true;
                }
            }

            return false;
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
