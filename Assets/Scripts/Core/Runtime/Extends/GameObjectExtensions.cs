using UnityEngine;

namespace Core.Runtime
{
    /// GameObject 的通用显示与组件访问扩展。
    public static class GameObjectExtensions
    {
        /// 激活对象；对象为空或已经激活时不执行额外操作。
        public static void Show(this GameObject target)
        {
            if (target != null && !target.activeSelf)
            {
                target.SetActive(true);
            }
        }

        /// 隐藏对象；对象为空或已经隐藏时不执行额外操作。
        public static void Hide(this GameObject target)
        {
            if (target != null && target.activeSelf)
            {
                target.SetActive(false);
            }
        }

        /// <summary>
        /// 获取已有组件；不存在时在当前对象上添加。
        /// </summary>
        /// <typeparam name="T">要获取或添加的组件类型。</typeparam>
        /// <param name="target">目标对象。</param>
        /// <returns>已有或新添加的组件；目标为空时返回 null。</returns>
        public static T GetOrAddComponent<T>(this GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
        }
    }
}
