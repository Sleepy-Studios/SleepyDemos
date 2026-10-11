using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    [RequireComponent(typeof(Button))]
    public sealed class UIDropdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text defaultText;
        [SerializeField] private Transform arrow;
        [SerializeField] private UITab tabView;
        [SerializeField, Tooltip("同一 View 内、位于滚动裁剪外的选项挂载容器；不改变 Canvas 排序。")]
        private Transform popupRoot;
        [SerializeField] private bool rotateArrow = true;
        [SerializeField] private float expandedArrowZ = 180f;

        private Button button;
        private List<string> options = new List<string>();
        private Action<int> onSelected;
        private Action<bool> dataShowStateChanged;
        private bool isExpanded;
        private bool popupDetached;
        private bool popupRestoreQueued;
        private Transform originalParent;
        private int originalSibling;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPivot;
        private Vector2 originalSize;
        private Vector3 originalAnchoredPosition;

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
            // 父级仍可能位于原生激活遍历中，此时不能改变子节点的父级或顺序。
            Collapse(false, deferRestore: true);
        }

        private void OnDisable()
        {
            // 仅禁用本组件时宿主仍活动，可以立即归还；整树停用须等原生遍历结束。
            Collapse(false, deferRestore: enabled || !gameObject.activeInHierarchy);
        }

        private void OnDestroy()
        {
            // 下一轮再判断原父级是否存活，不能将借出选项重新放入待销毁的宿主树。
            Collapse(false, deferRestore: true);
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

        /// <summary>指定同一 View 的弹出容器；更换容器先收起列表，原列表仍由 Dropdown 负责归还。</summary>
        /// <param name="value">滚动裁剪外且受本页 CanvasGroup 约束的容器；null 恢复原父级展开。</param>
        public void SetPopupRoot(Transform value)
        {
            if (tabView != null && value != null && value.IsChildOf(tabView.transform))
            {
                throw new ArgumentException("下拉选项不能挂载到自身或其子节点。", nameof(value));
            }
            if (popupRoot == value)
            {
                return;
            }
            Collapse(true);
            popupRoot = value;
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
        /// <param name="isAsync">默认 true；是否异步初始化选项图片。</param>
        public void SetData(
            IList<string> values,
            Action<int> action,
            int selectedIndex = 0,
            string selectedText = null,
            IReadOnlyList<string> itemImages = null,
            Action<bool> showStateChanged = null,
            bool isAsync = true)
        {
            if (values == null || tabView == null)
            {
                return;
            }

            options = new List<string>(values);
            onSelected = action;
            dataShowStateChanged = showStateChanged;
            // 先隐藏旧内容，再启动新初始化，避免 OnDisable 取消刚创建的任务。
            Collapse(false);
            tabView.Init(options, itemImages, selectedIndex, false, null, isAsync);
            SetSelectedText(GetDefaultText(selectedIndex, selectedText));
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
            popupRestoreQueued = false;
            var changed = !isExpanded;
            if (tabView != null)
            {
                AttachPopup();
                tabView.gameObject.SetActive(true);
                ClampPopup();
            }
            isExpanded = true;

            RefreshArrow();
            TabViewShown?.Invoke(transform);
            if (changed)
            {
                ShowStateChanged?.Invoke(true);
                dataShowStateChanged?.Invoke(true);
            }
        }

        private void Collapse(bool refreshArrow, bool deferRestore = false)
        {
            var changed = isExpanded;
            isExpanded = false;
            if (tabView != null)
            {
                tabView.gameObject.SetActive(false);
            }
            if (deferRestore)
            {
                QueuePopupRestore();
            }
            else
            {
                RestorePopup();
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

        private void AttachPopup()
        {
            if (popupDetached || popupRoot == null || tabView == null)
            {
                return;
            }
            var popup = tabView.transform;
            if (popupRoot.IsChildOf(popup))
            {
                throw new InvalidOperationException("下拉选项的制作挂载容器不能属于选项自身。");
            }
            originalParent = popup.parent;
            originalSibling = popup.GetSiblingIndex();
            originalPosition = popup.localPosition;
            originalRotation = popup.localRotation;
            originalScale = popup.localScale;
            Vector3 position = popup.position;
            var rect = popup as RectTransform;
            Vector2 size = rect != null ? rect.rect.size : Vector2.zero;
            if (rect != null)
            {
                originalAnchorMin = rect.anchorMin;
                originalAnchorMax = rect.anchorMax;
                originalPivot = rect.pivot;
                originalSize = rect.sizeDelta;
                originalAnchoredPosition = rect.anchoredPosition3D;
            }
            popupDetached = true;
            popup.SetParent(popupRoot, true);
            // Stretch 型选项也保留展开前的世界尺寸，不能按新父容器拉伸。
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
                rect.sizeDelta = size;
                rect.position = position;
            }
            popup.SetAsLastSibling();
        }

        private void QueuePopupRestore()
        {
            if (!popupDetached || popupRestoreQueued)
            {
                return;
            }
            popupRestoreQueued = true;
            UniTask.Post(() =>
            {
                if (!popupRestoreQueued)
                {
                    return;
                }
                popupRestoreQueued = false;
                if (!isExpanded)
                {
                    RestorePopup();
                }
            });
        }

        private void RestorePopup()
        {
            popupRestoreQueued = false;
            if (!popupDetached)
            {
                return;
            }
            if (tabView == null)
            {
                popupDetached = false;
                originalParent = null;
                return;
            }
            var popup = tabView.transform;
            if (originalParent == null)
            {
                // 原控件已经卸载时，不让借出的选项留在仍存活的宿主页。
                Destroy(popup.gameObject);
                popupDetached = false;
                return;
            }
            popup.SetParent(originalParent, false);
            popup.SetSiblingIndex(originalSibling);
            popup.localRotation = originalRotation;
            popup.localScale = originalScale;
            if (popup is RectTransform rect)
            {
                rect.anchorMin = originalAnchorMin;
                rect.anchorMax = originalAnchorMax;
                rect.pivot = originalPivot;
                rect.sizeDelta = originalSize;
                rect.anchoredPosition3D = originalAnchoredPosition;
            }
            else
            {
                popup.localPosition = originalPosition;
            }
            popupDetached = false;
            originalParent = null;
        }

        private void ClampPopup()
        {
            if (!popupDetached || popupRoot is not RectTransform root || tabView.transform is not RectTransform popup)
            {
                return;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, popup);
            var area = root.rect;
            // 超过制作容器的长列表由制作侧决定尺寸与滚动方式，不强行缩小或创建另一套列表。
            if (bounds.size.x > area.width || bounds.size.y > area.height)
            {
                return;
            }
            float x = bounds.min.x < area.xMin ? area.xMin - bounds.min.x
                : bounds.max.x > area.xMax ? area.xMax - bounds.max.x : 0;
            float y = bounds.min.y < area.yMin ? area.yMin - bounds.min.y
                : bounds.max.y > area.yMax ? area.yMax - bounds.max.y : 0;
            popup.position += root.TransformVector(new Vector3(x, y, 0));
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
