using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// HUD 铅笔路线与八车位置，只绘制本场规则数据。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KagitKartMinimap : MaskableGraphic
    {
        private KagitKartSession session;
        private Vector2 min;
        private Vector2 size;

        /// <summary>切换比赛数据，空值清空地图。</summary>
        /// <param name="value">当前场景唯一会话，不持有资源句柄。</param>
        public void SetData(KagitKartSession value)
        {
            session = value;
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (value != null)
            {
                var samples = value.Track.Track.data.samples;
                for (int i = 0; i < samples.px.Length; i++)
                {
                    var p = new Vector2(samples.px[i], samples.pz[i]);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            size = max - min;
            SetVerticesDirty();
        }

        public void Refresh() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (session == null)
            {
                return;
            }
            var samples = session.Track.Track.data.samples;
            Vector2 previous = Point(samples.px[0], samples.pz[0]);
            for (int i = 4; i <= samples.px.Length; i += 4)
            {
                int index = i % samples.px.Length;
                Vector2 next = Point(samples.px[index], samples.pz[index]);
                Vector2 normal = new Vector2(-(next - previous).y, (next - previous).x).normalized * 1.1f;
                Quad(vh, previous - normal, previous + normal, next + normal, next - normal, color);
                previous = next;
            }
            Vector2 first = Point(samples.px[0], samples.pz[0]);
            Vector2 closing = new Vector2(-(first - previous).y, (first - previous).x).normalized * 1.1f;
            Quad(vh, previous - closing, previous + closing, first + closing, first - closing, color);
            foreach (var kart in session.Karts)
            {
                Vector2 p = Point(kart.Position.x, kart.Position.z);
                float r = kart.IsPlayer ? 5.5f : 3;
                Color ink = kart.IsPlayer ? new Color(0.78f, 0.2f, 0.15f) : new Color(0.2f, 0.25f, 0.3f);
                Quad(vh, p + new Vector2(-r, -r), p + new Vector2(-r, r),
                    p + new Vector2(r, r), p + new Vector2(r, -r), ink);
            }
        }

        private Vector2 Point(float x, float z)
        {
            Rect rect = rectTransform.rect;
            float scale = Mathf.Min((rect.width - 20) / Mathf.Max(1, size.x), (rect.height - 20) / Mathf.Max(1, size.y));
            return rect.center + new Vector2(x - min.x - size.x / 2, z - min.y - size.y / 2) * scale;
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ink)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, ink, Vector2.zero);
            vh.AddVert(b, ink, Vector2.zero);
            vh.AddVert(c, ink, Vector2.zero);
            vh.AddVert(d, ink, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
