using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime.Inputs
{
    /// EventSystem只向选中控件发Cancel；转给页面作用域或宿主页面的取消处理。
    public sealed class UICancelRelay : MonoBehaviour, ICancelHandler
    {
        /// 页面可直接接收取消；菜单导航继续由当前公共作用域处理。
        public event Action Canceled;

        /// <summary>将选中控件的取消事件交给最近页面；不创建或接管输入作用域。</summary>
        /// <param name="value">原生UI模块的取消事件，由页面处理并决定是否消费。</param>
        public void OnCancel(BaseEventData value)
        {
            if (Canceled != null) { Canceled.Invoke(); value.Use(); return; }
            var scope = GetComponentInParent<UIMenuScope>();
            if (scope != null) scope.OnCancel(value);
            else if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, value, ExecuteEvents.cancelHandler);
        }
    }
}
