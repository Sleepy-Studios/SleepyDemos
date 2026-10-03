using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime.Inputs
{
    /// EventSystem 只向选中控件发 Cancel；转给最近的页面作用域。
    public sealed class UICancelRelay : MonoBehaviour, ICancelHandler
    {
        public void OnCancel(BaseEventData value) => GetComponentInParent<UIMenuScope>()?.OnCancel(value);
    }
}
