using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Hotfix.KagitKart
{
    /// 原作角色与章节卡片的预览事件；确认与返回仍由 Button 和公共菜单处理。
    public sealed class KagitKartChoiceCard : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerMoveHandler
    {
        /// 焦点或鼠标悬停时仅更新预览。
        public event Action Focused;

        /// <summary>键盘或手柄导航到本卡片时更新预览，不提交选择。</summary>
        /// <param name="value">公共 EventSystem 的选择事件。</param>
        public void OnSelect(BaseEventData value) => Focused?.Invoke();

        /// <summary>沿用原作鼠标悬停预览，触摸进入不抢占焦点。</summary>
        /// <param name="value">公共 UI Module 的指针事件。</param>
        public void OnPointerEnter(PointerEventData value)
            => PreviewPointer(value);

        /// <summary>鼠标在当前卡片内实际移动时恢复悬停预览。</summary>
        /// <param name="value">公共 UI Module 的移动事件。</param>
        public void OnPointerMove(PointerEventData value) => PreviewPointer(value);

        private void PreviewPointer(PointerEventData value)
        {
            // 页面出现也会产生 Enter，不能让静止鼠标覆盖键盘或手柄选择。
            if (value.delta.sqrMagnitude > 0.01f && value is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.MouseOrPen)
            {
                EventSystem.current?.SetSelectedGameObject(gameObject);
            }
        }
    }
}
