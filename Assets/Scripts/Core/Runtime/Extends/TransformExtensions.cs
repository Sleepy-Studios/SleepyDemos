using UnityEngine;

namespace Core.Runtime
{
    /// Transform 的通用层级操作扩展。
    public static class TransformExtensions
    {
        /// <summary>递归设置当前节点及全部子节点的 Layer。</summary>
        /// <param name="target">层级根节点。</param>
        /// <param name="layer">目标 Layer。</param>
        public static void SetLayerRecursively(this Transform target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.gameObject.layer = layer;
            for (int i = 0; i < target.childCount; i++)
            {
                target.GetChild(i).SetLayerRecursively(layer);
            }
        }

        /// 将局部位置、旋转与缩放恢复为默认值。
        public static void ResetLocalTransform(this Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
