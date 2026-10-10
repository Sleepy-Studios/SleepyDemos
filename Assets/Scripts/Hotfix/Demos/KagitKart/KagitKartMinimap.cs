using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作静态地图底图与每帧赛车标记；源坐标为 100 × 100，Y 轴向下。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KagitKartMinimap : MaskableGraphic
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] routeSprites = new Sprite[4];

        private static readonly Color32 Outline = new(45, 42, 50, 255);
        private static readonly Color32 PlayerColor = new(217, 72, 59, 255);
        private static readonly Vector2[] Arrow = { new(4.6f, 0), new(-3, 3.6f), new(-1.6f, 0), new(-3, -3.6f) };
        private readonly Vector2[] arrowPoints = new Vector2[4];
        private KagitKartSession session;
        private Color[] kartColors;
        private double minX, minZ, scale, offsetX, offsetZ;

        public override Texture mainTexture => Texture2D.whiteTexture;

        /// <summary>切换当前比赛及对应章的静态底图，空值清空。</summary>
        /// <param name="value">当前比赛会话，不持有资源句柄。</param>
        public void SetData(KagitKartSession value)
        {
            session = value;
            if (backgroundImage != null)
            {
                int chapter = value == null ? -1 : value.Track.Track.definition.chapter - 1;
                backgroundImage.sprite = chapter >= 0 && routeSprites != null && chapter < routeSprites.Length ? routeSprites[chapter] : null;
                backgroundImage.enabled = backgroundImage.sprite != null;
            }
            kartColors = null;
            if (value != null)
            {
                var samples = value.Track.Track.data.samples;
                minX = minZ = double.PositiveInfinity;
                double maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
                for (int i = 0; i < samples.px.Length; i++)
                {
                    minX = Math.Min(minX, samples.px[i]);
                    minZ = Math.Min(minZ, samples.pz[i]);
                    maxX = Math.Max(maxX, samples.px[i]);
                    maxZ = Math.Max(maxZ, samples.pz[i]);
                }
                scale = 92 / Math.Max(Math.Max(maxX - minX, maxZ - minZ), 1);
                offsetX = (100 - (maxX - minX) * scale) / 2;
                offsetZ = (100 - (maxZ - minZ) * scale) / 2;
                kartColors = new Color[value.Karts.Count];
                for (int i = 0; i < kartColors.Length; i++)
                {
                    var colors = value.Karts[i].Character.colors;
                    string html = !string.IsNullOrEmpty(colors.kart) ? colors.kart : !string.IsNullOrEmpty(colors.body) ? colors.body : "#8a8490";
                    ColorUtility.TryParseHtmlString(html, out kartColors[i]);
                }
            }
            SetVerticesDirty();
        }

        /// <summary>供 HUD 刷新调用；标记位置另按原作逐帧更新。</summary>
        public void Refresh() => SetVerticesDirty();

        private void LateUpdate()
        {
            if (session != null)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (session == null)
            {
                return;
            }
            for (int i = 0; i < session.Karts.Count; i++)
            {
                var kart = session.Karts[i];
                if (!kart.IsPlayer)
                {
                    Vector2 point = Project(kart.Position.x, kart.Position.z);
                    Circle(vh, point, 2.7f, Outline);
                    Circle(vh, point, 1.9f, kartColors[i]);
                }
            }
            // 原作排序将玩家置顶；凹箭头填充由两片三角形组成，描边使用圆连接。
            var player = session.Player;
            Vector2 origin = Project(player.Position.x, player.Position.z);
            for (int i = 0; i < Arrow.Length; i++)
            {
                arrowPoints[i] = origin + RotateArrow(Arrow[i], player.Heading);
            }
            Quad(vh, arrowPoints[0], arrowPoints[1], arrowPoints[2], arrowPoints[3], PlayerColor);
            for (int i = 0; i < Arrow.Length; i++)
            {
                Vector2 a = arrowPoints[i], b = arrowPoints[(i + 1) % Arrow.Length];
                Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 0.5f;
                Quad(vh, a - normal, a + normal, b + normal, b - normal, Outline);
                Circle(vh, a, 0.5f, Outline);
            }
        }

        internal Vector2 Project(float x, float z) => new(
            (float)SourceFixed((x - minX) * scale + offsetX, 2),
            (float)SourceFixed((z - minZ) * scale + offsetZ, 2));

        internal static Vector2 RotateArrow(Vector2 point, float heading)
        {
            double degrees = SourceFixed(Math.Atan2(Math.Cos(heading), Math.Sin(heading)) * 180 / Math.PI, 1);
            double radians = degrees * Math.PI / 180;
            double cosine = Math.Cos(radians), sine = Math.Sin(radians);
            return new Vector2((float)(point.x * cosine - point.y * sine), (float)(point.x * sine + point.y * cosine));
        }

        // JS toFixed 按实际二进制值取最近十进制；普通 Math.Round 会在 2.675 等边界先乘法舍入。
        internal static double SourceFixed(double value, int digits)
        {
            ulong factor = digits == 2 ? 100UL : 10UL;
            double absolute = Math.Abs(value);
            if (absolute < 0.5 / factor)
            {
                return 0;
            }
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(absolute);
            int exponent = (int)((bits >> 52) & 0x7ff) - 1023 - 52;
            if (exponent >= 0)
            {
                return value;
            }
            ulong numerator = ((bits & ((1UL << 52) - 1)) | (1UL << 52)) * factor;
            int shift = -exponent;
            ulong whole = numerator >> shift;
            ulong remainder = numerator & ((1UL << shift) - 1);
            if (remainder >= (1UL << (shift - 1)))
            {
                whole++;
            }
            double result = (double)whole / factor;
            return value < 0 ? -result : result;
        }

        private Vector2 ToRect(Vector2 point)
        {
            Rect rect = rectTransform.rect;
            return rect.center + new Vector2((point.x - 50) * rect.width / 100, (50 - point.y) * rect.height / 100);
        }

        private void Circle(VertexHelper vh, Vector2 center, float radius, Color ink)
        {
            const int segments = 24;
            int start = vh.currentVertCount;
            vh.AddVert(ToRect(center), ink, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(ToRect(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), ink, new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
            }
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ink)
        {
            int start = vh.currentVertCount;
            vh.AddVert(ToRect(a), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(ToRect(b), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(ToRect(c), ink, new Vector2(0.5f, 0.5f));
            vh.AddVert(ToRect(d), ink, new Vector2(0.5f, 0.5f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
