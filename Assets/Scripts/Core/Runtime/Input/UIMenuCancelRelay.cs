using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime.Inputs
{
    /// 普通菜单控件没有取消处理时，将 UI Module 的直接事件转交最近菜单。
    [DisallowMultipleComponent]
    public sealed class UIMenuCancelRelay : MonoBehaviour, ICancelHandler
    {
        /// <summary>只转发当前控件的取消事件，不改变确认或方向导航。</summary>
        /// <param name="value">公共 EventSystem 发给当前选中控件的取消事件。</param>
        public void OnCancel(BaseEventData value) => GetComponentInParent<UIMenuScope>()?.OnCancel(value);
    }
}
