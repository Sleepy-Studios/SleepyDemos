using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作立体书与镜头的数学规则，输入输出均保留源坐标，由表现层统一反射 X。
    public static class KagitKartBookGeometry
    {
        /// <summary>读取书页在书脊与外缘之间的原作弧面高度。</summary>
        /// <param name="x">源书页横坐标，左右半页均为 12 米宽。</param>
        public static float PageTop(float x)
        {
            float u = Mathf.Min(1, Mathf.Abs(x) / 12);
            return 0.16f + 1.18f + 0.32f * (1 - u) * (1 - u) - 0.95f * Mathf.Exp(-u * u / (0.07f * 0.07f));
        }

        /// <summary>将左半书页绕书脊合到右页，保留封面硬板与书页厚度的区别。</summary>
        /// <param name="point">源坐标下的打开姿态顶点。</param>
        /// <param name="closed">0 为打开，1 为闭合。</param>
        public static Vector3 CoverPoint(Vector3 point, float closed)
        {
            float w = Smooth(0.08f, 0.92f, closed);
            float y = point.y;
            if (w > 0 && y > 0.16f)
            {
                float top = PageTop(point.x);
                float delta = 2 * 1.42f - 2 * top - 0.02f;
                y += w * delta * Mathf.Min(1, (y - 0.16f) / Mathf.Max(0.001f, top - 0.16f));
            }
            float angle = -Mathf.PI * closed;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle), relative = y - 1.42f;
            return new Vector3(point.x * c - relative * s, 1.42f + point.x * s + relative * c, point.z);
        }

        /// <summary>原作卷页的自由边领先与落页曲线，不使用刚性矩形翻转代替。</summary>
        /// <param name="point">源卷页基础网格顶点。</param>
        /// <param name="progress">0 在右页，1 在左页。</param>
        /// <param name="forward">是否向后续章节翻页。</param>
        public static Vector3 SheetPoint(Vector3 point, float progress, bool forward)
        {
            float w = Smooth(0.1f, 0.9f, progress);
            float lead = Mathf.Sin(Mathf.PI * progress) * ((forward ? 0.62f : 0.38f) - progress) * 1.9f;
            float y = point.y + w * (1.5f - 2 * point.y);
            float angle = Mathf.PI * progress + lead * Mathf.Pow(point.x / 12, 1.3f);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle), relative = y - 0.75f;
            return new Vector3(point.x * c - relative * s, 0.75f + point.x * s + relative * c, point.z);
        }

        /// <summary>将观察目标偏到屏幕指定位置，保留原作菜单为卡片留出的空间。</summary>
        /// <param name="eye">源摄像机位置。</param>
        /// <param name="subject">源观察主体。</param>
        /// <param name="screen">主体的屏幕 NDC 位置。</param>
        /// <param name="fov">垂直视角，单位度。</param>
        /// <param name="aspect">横纵比。</param>
        public static Vector3 FrameAt(Vector3 eye, Vector3 subject, Vector2 screen, float fov, float aspect)
        {
            Vector3 forward = subject - eye;
            float distance = Mathf.Max(forward.magnitude, 0.000001f);
            forward /= distance;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            right = right.sqrMagnitude < 1e-8f ? Vector3.right : right.normalized;
            Vector3 up = Vector3.Cross(right, forward);
            float tangent = Mathf.Tan(fov * Mathf.Deg2Rad / 2);
            return subject - right * (screen.x * tangent * aspect * distance) - up * (screen.y * tangent * distance);
        }

        /// <summary>在窄横屏上保留原作最小水平视野，不改变项目画面设置。</summary>
        /// <param name="aspect">横纵比。</param>
        /// <param name="vertical">默认垂直角度。</param>
        /// <param name="horizontal">最小水平角度。</param>
        public static float FitFov(float aspect, float vertical, float horizontal) =>
            Mathf.Min(100, Mathf.Max(vertical, 2 * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad / 2) / Mathf.Max(0.2f, aspect)) * Mathf.Rad2Deg));

        private static float Smooth(float a, float b, float value)
        {
            float t = Mathf.Clamp01((value - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
