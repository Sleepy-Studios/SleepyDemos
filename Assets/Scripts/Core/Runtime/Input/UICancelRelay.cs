using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime.Inputs
{
    /// EventSystem只向选中控件发Cancel；转给页面作用域或宿主页面的取消处理。
    public sealed class UICancelRelay : MonoBehaviour, ICancelHandler
    {
        public void OnCancel(BaseEventData value)
        {
            var scope = GetComponentInParent<UIMenuScope>();
            if (scope != null) scope.OnCancel(value);
            else if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, value, ExecuteEvents.cancelHandler);
        }
    }
}
