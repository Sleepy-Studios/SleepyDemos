using Core.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原积分条的斜切边框、玩家行的六边印刷底色；只有这两种源形状。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KagitKartResultShape : MaskableGraphic
    {
        [SerializeField] private bool scoreBar;
        private float fillAmount;
        private Color fillColor;

        public override Texture mainTexture => Texture2D.whiteTexture;

        /// <summary>绘制源积分条，边框随当前积分比例一起缩短。</summary>
        /// <param name="amount">相对当前最高积分的比例。</param>
        /// <param name="ink">该角色原 body 印刷色。</param>
        public void SetFill(float amount, Color ink)
        {
            fillAmount = Mathf.Clamp01(amount);
            fillColor = ink;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (scoreBar)
            {
                float width = Mathf.Max(4.8f, rect.width * fillAmount);
                // CSS skewX(-14deg) 的 Y 向下，转换到 UGUI 后取反。
                Bar(vh, rect.xMin, rect.yMin, width, rect.height, KagitKartUIColors.Ink);
                if (width > 5.76f && rect.height > 5.76f)
                {
                    Bar(vh, rect.xMin + 2.88f, rect.yMin + 2.88f, width - 5.76f, rect.height - 5.76f, fillColor);
                }
                return;
            }
            Add(vh, rect, 0, 0.12f);
            Add(vh, rect, 0.03f, 0);
            Add(vh, rect, 0.99f, 0.06f);
            Add(vh, rect, 1, 0.88f);
            Add(vh, rect, 0.97f, 1);
            Add(vh, rect, 0.01f, 0.92f);
            for (int i = 1; i < 5; i++) vh.AddTriangle(0, i, i + 1);
        }

        private void Add(VertexHelper vh, Rect rect, float x, float y)
        {
            float py = rect.yMax - y * rect.height;
            vh.AddVert(new Vector2(rect.xMin + x * rect.width + Mathf.Tan(8 * Mathf.Deg2Rad) * (py - rect.center.y), py),
                color, new Vector2(0.5f, 0.5f));
        }

        private static void Bar(VertexHelper vh, float left, float bottom, float width, float height, Color ink)
        {
            int start = vh.currentVertCount;
            float skew = Mathf.Tan(14 * Mathf.Deg2Rad) * height * 0.5f;
            vh.AddVert(new Vector2(left - skew, bottom), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(left + width - skew, bottom), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(left + width + skew, bottom + height), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(new Vector2(left + skew, bottom + height), ink, new Vector2(0.5f, 0.5f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
