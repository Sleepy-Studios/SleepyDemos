using UnityEngine;

namespace Core.Runtime
{
    /// Component 的通用对象与组件访问扩展。
    public static class ComponentExtensions
    {
        /// 激活组件所在对象。
        public static void Show(this Component target)
        {
            target?.gameObject.Show();
        }

        /// 隐藏组件所在对象。
        public static void Hide(this Component target)
        {
            target?.gameObject.Hide();
        }

        /// <summary>
        /// 获取同对象已有组件；不存在时添加。
        /// </summary>
        /// <typeparam name="T">要获取或添加的组件类型。</typeparam>
        /// <param name="target">任意同对象组件。</param>
        /// <returns>已有或新添加的组件；目标为空时返回 null。</returns>
        public static T GetOrAddComponent<T>(this Component target) where T : Component
        {
            return target == null ? null : target.gameObject.GetOrAddComponent<T>();
        }
    }
}
