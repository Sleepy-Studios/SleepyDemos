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
        private Action<bool> dataShowStateChanged;
        private bool isExpanded;

        /// 任意 UIDropdown 展开选项列表时触发，参数为当前 Dropdown 的 Transform。
        public static event Action<Transform> TabViewShown;

        /// 展开状态变化事件。true 表示展开，false 表示收起。
        public event Action<bool> ShowStateChanged;

        /// 当前选中索引；未配置 Tab 时为 -1。
        public int CurrentIndex => tabView != null ? tabView.Index : -1;

        /// 当前下拉列表是否展开。
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

        /// <summary>
        /// 设置下拉数据。选择回调使用覆盖语义，展开状态回调仅覆盖本次 SetData 传入的回调。
        /// </summary>
        /// <param name="values">选项文本列表。</param>
        /// <param name="action">选中回调，参数为选中索引；会覆盖上一次 SetData 传入的选择回调。</param>
        /// <param name="selectedIndex">默认选中索引；非法索引不会触发选择回调。</param>
        /// <param name="selectedText">默认显示文本；为空时使用 selectedIndex 对应选项文本。</param>
        /// <param name="itemImages">选项图片 Sprite 资源路径；为空时清空图片。</param>
        /// <param name="showStateChanged">展开状态变化回调；为空时不覆盖已有事件订阅。</param>
        /// <param name="isAsync">是否异步初始化选项图片。</param>
        public void SetData(
            IList<string> values,
            Action<int> action,
            int selectedIndex = 0,
            string selectedText = null,
            IReadOnlyList<string> itemImages = null,
            Action<bool> showStateChanged = null,
            bool isAsync = false)
        {
            if (values == null || tabView == null)
            {
                return;
            }

            options = new List<string>(values);
            onSelected = action;
            dataShowStateChanged = showStateChanged;
            tabView.Init(options, itemImages, selectedIndex, false, null, isAsync);
            SetSelectedText(GetDefaultText(selectedIndex, selectedText));
            Collapse(false);
        }

        /// <summary>
        /// 追加注册选中回调。
        /// </summary>
        /// <param name="action">回调参数为选中索引。</param>
        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        /// <summary>
        /// 移除已注册的选中回调。
        /// </summary>
        /// <param name="action">需要移除的回调。</param>
        public void Unregister(Action<int> action)
        {
            onSelected -= action;
        }

        /// <summary>
        /// 静默切换当前选中项，只更新 Tab 选中态和默认文本，不触发选择回调。
        /// </summary>
        /// <param name="index">目标选项索引；非法索引会被忽略。</param>
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
                dataShowStateChanged?.Invoke(true);
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
                dataShowStateChanged?.Invoke(false);
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

        private string GetDefaultText(int selectedIndex, string selectedText)
        {
            if (!string.IsNullOrEmpty(selectedText))
            {
                return selectedText;
            }

            return selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : null;
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
