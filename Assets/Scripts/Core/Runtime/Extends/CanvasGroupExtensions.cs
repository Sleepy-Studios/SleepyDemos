using UnityEngine;

namespace Core.Runtime
{
    /// CanvasGroup 的可见与交互状态扩展。
    public static class CanvasGroupExtensions
    {
        /// <summary>统一设置透明度、交互和射线拦截状态，不改变 GameObject 激活状态。</summary>
        /// <param name="target">目标 CanvasGroup。</param>
        /// <param name="visible">是否可见且允许交互。</param>
        public static void SetCanvasGroupVisible(this CanvasGroup target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            target.alpha = visible ? 1f : 0f;
            target.interactable = visible;
            target.blocksRaycasts = visible;
        }
    }
}
