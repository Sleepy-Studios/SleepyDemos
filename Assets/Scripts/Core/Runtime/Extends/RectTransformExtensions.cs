using UnityEngine;

namespace Core.Runtime
{
    /// RectTransform 的尺寸与锚点坐标扩展。
    public static class RectTransformExtensions
    {
        /// <summary>按当前锚点设置宽度。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="width">目标宽度。</param>
        public static void SetWidth(this RectTransform target, float width)
        {
            if (target != null)
            {
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
        }

        /// <summary>按当前锚点设置高度。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="height">目标高度。</param>
        public static void SetHeight(this RectTransform target, float height)
        {
            if (target != null)
            {
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }

        /// <summary>按当前锚点设置宽高。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="size">目标宽高。</param>
        public static void SetSize(this RectTransform target, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            target.SetWidth(size.x);
            target.SetHeight(size.y);
        }

        /// <summary>只修改锚点坐标 X。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="x">目标 X。</param>
        public static void SetAnchoredPositionX(this RectTransform target, float x)
        {
            if (target == null)
            {
                return;
            }

            Vector2 position = target.anchoredPosition;
            position.x = x;
            target.anchoredPosition = position;
        }

        /// <summary>只修改锚点坐标 Y。</summary>
        /// <param name="target">目标 RectTransform。</param>
        /// <param name="y">目标 Y。</param>
        public static void SetAnchoredPositionY(this RectTransform target, float y)
        {
            if (target == null)
            {
                return;
            }

            Vector2 position = target.anchoredPosition;
            position.y = y;
            target.anchoredPosition = position;
        }
    }
}
