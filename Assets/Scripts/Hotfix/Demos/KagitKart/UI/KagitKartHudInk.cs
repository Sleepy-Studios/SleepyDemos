using Core.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作屏幕墨水：独立表现随机、三团曲线墨斑和六条 CSS 时钟滴落。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KagitKartHudInk : MaskableGraphic
    {
        private static readonly Color Ink = KagitKartUIColors.ScreenInk;
        private static readonly Color Highlight = KagitKartUIColors.ScreenInkHighlight;
        private readonly List<Part> parts = new();
        private readonly Drip[] drips = new Drip[6];
        private float duration;
        private double elapsed;
        private bool visible;
        private bool reducedMotion;
        private RenderTexture target;
        private Mesh inkMesh;
        private Material fillMaterial;
        private readonly List<Color> fillColors = new();

        /// 本次动态合成纹理；没有墨水时不分配。
        public override Texture mainTexture => target != null ? target : s_WhiteTexture;

        private sealed class Part
        {
            public Vector2[] vertices;
            public int[] triangles;
            public Color ink;
            public int drip = -1;
        }

        private struct Drip
        {
            public float originY;
            public float delay;
        }

        /// 当前整层渐隐值；最后 35% 规则时长由 1 衰减至 0。
        public float Opacity { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>按新的独立种子重建本次墨水，替换上次遮挡，零时长沿用原作默认 3.5 秒。</summary>
        /// <param name="seconds">本次 Inked 事件的原始时长，不是规则延长后的累计剩余时长。</param>
        /// <param name="seed">本次表现 Mulberry32 种子，不得从比赛随机流取数。</param>
        public void Splat(float seconds, uint seed)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            if (material.shader.name != "KagitKart/HudInk")
            {
                throw new InvalidOperationException("屏幕墨水必须绑定原作 HudInk 合成材质。");
            }
            parts.Clear();
            raycastTarget = false;
            duration = seconds == 0 ? 3.5f : seconds;
            elapsed = 0;
            visible = true;
            Opacity = 1;
            var random = new KagitKartRandom(seed);
            BuildBlob(random, 0.38, 0.4, 210, 0);
            BuildBlob(random, 0.63, 0.34, 160, 2);
            BuildBlob(random, 0.53, 0.64, 135, 4);
            fillColors.Clear();
            foreach (var part in parts)
            {
                for (int i = 0; i < part.vertices.Length; i++)
                {
                    fillColors.Add(part.ink);
                }
            }
            RenderInk();
            SetVerticesDirty();
        }

        /// <summary>滴落采用未缩放墙钟时间；暂停时 playerInkLeft 冻结，渐隐仍以该规则状态为准。</summary>
        /// <param name="dt">未缩放表现帧时长，暂停菜单期间仍传真实 dt；不使用规则固定步。</param>
        /// <param name="playerInkLeft">玩家实际剩余墨水秒数；小于等于零清除遮挡。</param>
        /// <param name="reduceMotion">低动态取消滴落动画，立即使用完整长度。</param>
        public void Advance(float dt, float playerInkLeft, bool reduceMotion)
        {
            if (!visible)
            {
                return;
            }
            if (playerInkLeft <= 0)
            {
                Clear();
                return;
            }
            bool motionChanged = reducedMotion != reduceMotion;
            double previousElapsed = elapsed;
            if (motionChanged)
            {
                elapsed = 0;
                reducedMotion = reduceMotion;
            }
            elapsed += Math.Max(0, dt);
            Opacity = (float)Math.Round(Mathf.Clamp01(playerInkLeft / (duration * 0.35f)), 3, MidpointRounding.AwayFromZero);
            if (motionChanged || elapsed < 3.6 || previousElapsed < 3.6 || TargetSizeChanged())
            {
                RenderInk();
            }
            SetVerticesDirty();
        }

        /// 清除本场遮挡；HUD 换场或隐藏时调用。
        public void Clear()
        {
            visible = false;
            parts.Clear();
            fillColors.Clear();
            Opacity = 0;
            ReleaseTarget();
            SetVerticesDirty();
        }

        private Vector2Int TargetSize()
        {
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1;
            Rect rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width * scale)), Mathf.Max(1, Mathf.CeilToInt(rect.height * scale)));
        }

        private bool TargetSizeChanged()
        {
            Vector2Int size = TargetSize();
            return target == null || !target.IsCreated() || target.width != size.x || target.height != size.y;
        }

        private void RenderInk()
        {
            if (TargetSizeChanged())
            {
                ReleaseTarget();
                Vector2Int size = TargetSize();
                // SVG默认在sRGB数值空间合成；禁止RT写入或采样时自动执行色彩转换。
                target = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                target.name = "KagitKart HUD Ink";
                target.filterMode = FilterMode.Bilinear;
                target.wrapMode = TextureWrapMode.Clamp;
                SetMaterialDirty();
            }
            if (inkMesh == null)
            {
                inkMesh = new Mesh { name = "KagitKart HUD Ink Geometry", hideFlags = HideFlags.HideAndDontSave };
                fillMaterial = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
                fillMaterial.SetShaderPassEnabled("InkFill", true);
            }
            using (var helper = new VertexHelper())
            {
                PopulateInkMesh(helper);
                helper.FillMesh(inkMesh);
            }
            // 高光是源SVG的精确.3透明度，绕开UIVertex的8位颜色量化。
            inkMesh.SetColors(fillColors);
            Rect rect = rectTransform.rect;
            var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(rect.xMin, rect.xMax, rect.yMin, rect.yMax, -1, 1), true);
            fillMaterial.SetMatrix("_InkClipMatrix", projection);
            bool previousSrgbWrite = GL.sRGBWrite;
            using (var command = new CommandBuffer())
            {
                command.SetRenderTarget(target);
                command.SetViewport(new Rect(0, 0, target.width, target.height));
                command.ClearRenderTarget(false, true, Color.clear);
                command.DrawMesh(inkMesh, Matrix4x4.identity, fillMaterial, 0, 1);
                try
                {
                    GL.sRGBWrite = false;
                    Graphics.ExecuteCommandBuffer(command);
                }
                finally
                {
                    GL.sRGBWrite = previousSrgbWrite;
                }
            }
        }

        private void ReleaseTarget()
        {
            if (target == null)
            {
                return;
            }
            RenderTexture.ReleaseTemporary(target);
            target = null;
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseTarget();
            CoreUtils.Destroy(inkMesh);
            CoreUtils.Destroy(fillMaterial);
            base.OnDestroy();
        }

        private void BuildBlob(KagitKartRandom random, double bx, double by, double radius, int dripIndex)
        {
            double cx = bx * 1600 + (random.Next() - 0.5) * 100;
            double cy = by * 900 + (random.Next() - 0.5) * 70;
            double r = radius * (0.92 + random.Next() * 0.16);
            double phase1 = random.Next() * 6.28;
            double phase2 = random.Next() * 6.28;
            var points = new (double x, double y)[16];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = i / 16.0 * Math.PI * 2 + (random.Next() - 0.5) * 0.18;
                double length = r * (1 + 0.16 * Math.Sin(3 * angle + phase1) + 0.09 * Math.Sin(5 * angle + phase2) + (random.Next() - 0.5) * 0.1);
                points[i] = (cx + Math.Cos(angle) * length, cy + Math.Sin(angle) * length);
            }
            var contour = new Vector2[128];
            Vector2 start = Rounded((points[15].x + points[0].x) / 2, (points[15].y + points[0].y) / 2);
            for (int i = 0; i < 16; i++)
            {
                var next = points[(i + 1) % 16];
                Vector2 control = Rounded(points[i].x, points[i].y);
                Vector2 end = Rounded((points[i].x + next.x) / 2, (points[i].y + next.y) / 2);
                for (int step = 0; step < 8; step++)
                {
                    contour[i * 8 + step] = Quadratic(start, control, end, step / 8f);
                }
                start = end;
            }
            Fan(contour, new Vector2((float)cx, (float)cy), Ink);
            int arms = 3 + (int)Math.Floor(random.Next() * 3);
            for (int k = 0; k < arms; k++)
            {
                double angle = random.Next() * Math.PI * 2;
                double length = r * (0.2 + random.Next() * 0.28);
                double width = 20 + random.Next() * 12;
                double ca = Math.Cos(angle), sa = Math.Sin(angle), nx = -sa, ny = ca;
                double x0 = cx + ca * r * 0.85, y0 = cy + sa * r * 0.85;
                double x1 = x0 + ca * length, y1 = y0 + sa * length;
                var arm = new[] { Rounded(x0 + nx * width, y0 + ny * width), Rounded(x1 + nx * 2, y1 + ny * 2),
                    Rounded(x1 - nx * 2, y1 - ny * 2), Rounded(x0 - nx * width, y0 - ny * width) };
                Fan(arm, (arm[0] + arm[1] + arm[2] + arm[3]) / 4, Ink);
                Circle(Rounded(x1 + ca * 8, y1 + sa * 8), Round(5 + random.Next() * 7), Ink);
            }
            for (int k = 0; k < 4; k++)
            {
                double angle = random.Next() * Math.PI * 2;
                double distance = r * (1.55 + random.Next() * 0.6);
                Circle(Rounded(cx + Math.Cos(angle) * distance, cy + Math.Sin(angle) * distance), Round(4 + random.Next() * 10), Ink);
            }
            for (int k = 0; k < 2; k++)
            {
                double x = cx + (random.Next() - 0.5) * r * 1.1;
                double top = cy + r * 0.55;
                double length = 70 + random.Next() * 160;
                double width = 12 + random.Next() * 10;
                int index = dripIndex + k;
                drips[index] = new Drip { originY = Round(top, 0), delay = Round(random.Next() * 0.4, 2) };
                Capsule(Round(x - width / 2), Round(top), Round(width), Round(length), index);
                Circle(Rounded(x, top + length), Round(width * 0.8), Ink, index);
            }
            Vector2 highlightStart = Rounded(cx - r * 0.5, cy - r * 0.15);
            Stroke(highlightStart, highlightStart + Rounded(r * 0.1, -r * 0.32), highlightStart + Rounded(r * 0.42, -r * 0.38));
        }

        private static float Round(double value, int digits = 1) => (float)Math.Round(value, digits, MidpointRounding.AwayFromZero);

        private static Vector2 Rounded(double x, double y) => new Vector2(Round(x), Round(y));

        private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t) => (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;

        private void Fan(Vector2[] contour, Vector2 center, Color ink, int drip = -1)
        {
            var vertices = new Vector2[contour.Length + 1];
            vertices[0] = center;
            Array.Copy(contour, 0, vertices, 1, contour.Length);
            var triangles = new int[contour.Length * 3];
            for (int i = 0; i < contour.Length; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % contour.Length + 1;
            }
            parts.Add(new Part { vertices = vertices, triangles = triangles, ink = ink, drip = drip });
        }

        private void Circle(Vector2 center, float radius, Color ink, int drip = -1, double from = 0, double sweep = Math.PI * 2)
        {
            var contour = new Vector2[33];
            for (int i = 0; i < contour.Length; i++)
            {
                double angle = from + sweep * i / 32;
                contour[i] = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            }
            Fan(contour, center, ink, drip);
        }

        private void Capsule(float left, float top, float width, float length, int drip)
        {
            float radius = width / 2;
            var contour = new Vector2[34];
            for (int i = 0; i <= 16; i++)
            {
                double angle = Math.PI + Math.PI * i / 16;
                contour[i] = new Vector2(left + radius + (float)Math.Cos(angle) * radius, top + radius + (float)Math.Sin(angle) * radius);
                angle = Math.PI * i / 16;
                contour[i + 17] = new Vector2(left + radius + (float)Math.Cos(angle) * radius, top + length - radius + (float)Math.Sin(angle) * radius);
            }
            Fan(contour, new Vector2(left + radius, top + length / 2), Ink, drip);
        }

        private void Stroke(Vector2 start, Vector2 control, Vector2 end)
        {
            var vertices = new Vector2[50];
            var triangles = new int[24 * 6];
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                Vector2 tangent = ((1 - t) * (control - start) + t * (end - control)).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x) * 6;
                Vector2 p = Quadratic(start, control, end, t);
                vertices[i * 2] = p + normal;
                vertices[i * 2 + 1] = p - normal;
                if (i == 24)
                {
                    continue;
                }
                int v = i * 2, n = i * 6;
                triangles[n] = v;
                triangles[n + 1] = v + 1;
                triangles[n + 2] = v + 2;
                triangles[n + 3] = v + 1;
                triangles[n + 4] = v + 3;
                triangles[n + 5] = v + 2;
            }
            parts.Add(new Part { vertices = vertices, triangles = triangles, ink = Highlight });
            Circle(start, 6, Highlight, from: Math.Atan2(control.y - start.y, control.x - start.x) + Math.PI / 2, sweep: Math.PI);
            Circle(end, 6, Highlight, from: Math.Atan2(end.y - control.y, end.x - control.x) - Math.PI / 2, sweep: Math.PI);
        }

        private static float DripProgress(double value)
        {
            double x = Math.Max(0, Math.Min(1, value));
            double low = 0, high = 1;
            for (int i = 0; i < 18; i++)
            {
                double t = (low + high) / 2;
                double curveX = 0.9 * (1 - t) * (1 - t) * t + 1.8 * (1 - t) * t * t + t * t * t;
                if (curveX < x)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }
            }
            double u = (low + high) / 2;
            return (float)(3 * (1 - u) * u * u + u * u * u);
        }

        private void PopulateInkMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!visible)
            {
                return;
            }
            Rect rect = rectTransform.rect;
            float scale = Mathf.Max(rect.width / 1600, rect.height / 900);
            foreach (var part in parts)
            {
                float stretch = 1;
                float origin = 0;
                if (part.drip >= 0 && !reducedMotion)
                {
                    var drip = drips[part.drip];
                    origin = drip.originY;
                    stretch = 0.15f + 0.85f * DripProgress((elapsed - drip.delay) / 3.2);
                }
                int first = helper.currentVertCount;
                foreach (var vertex in part.vertices)
                {
                    Vector2 point = vertex;
                    if (part.drip >= 0 && !reducedMotion)
                    {
                        point.y = origin + (point.y - origin) * stretch;
                    }
                    Vector2 local = rect.center + new Vector2(point.x - 800, 450 - point.y) * scale;
                    helper.AddVert(local, part.ink, Vector2.zero);
                }
                for (int i = 0; i < part.triangles.Length; i += 3)
                {
                    helper.AddTriangle(first + part.triangles[i], first + part.triangles[i + 1], first + part.triangles[i + 2]);
                }
            }
        }

        /// <summary>把已合成墨水作为一个全屏Quad显示，整层只应用一次渐隐。</summary>
        /// <param name="helper">由UGUI管理的显示Quad，UV保持RenderTexture的Unity采样约定。</param>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!visible || target == null)
            {
                return;
            }
            Rect rect = rectTransform.rect;
            var tint = ColorUtil.WithAlpha(Color.white, Opacity);
            helper.AddVert(new Vector2(rect.xMin, rect.yMin), tint, new Vector2(0, 0));
            helper.AddVert(new Vector2(rect.xMin, rect.yMax), tint, new Vector2(0, 1));
            helper.AddVert(new Vector2(rect.xMax, rect.yMax), tint, new Vector2(1, 1));
            helper.AddVert(new Vector2(rect.xMax, rect.yMin), tint, new Vector2(1, 0));
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(0, 2, 3);
        }
    }
}
