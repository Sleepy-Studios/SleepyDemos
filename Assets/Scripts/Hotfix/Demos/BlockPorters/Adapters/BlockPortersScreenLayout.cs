using UnityEngine;

namespace Hotfix.BlockPorters.Adapters
{
    /// Demo 的统一设计坐标，HUD 和世界相机共同使用，长屏只扩展背景边缘。
    public readonly struct BlockPortersScreenLayout
    {
        public readonly Rect ContentPixels;
        public readonly float Scale;
        private BlockPortersScreenLayout(Rect rect, float scale) { ContentPixels = rect; Scale = scale; }

        /// <summary>将 600×1080 内容等比居中于设备安全区。</summary>
        /// <param name="width">屏幕宽度。</param>
        /// <param name="height">屏幕高度。</param>
        /// <param name="safeArea">像素坐标安全区。</param>
        public static BlockPortersScreenLayout Calculate(int width, int height, Rect safeArea)
        {
            if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = new Rect(0, 0, width, height);
            float scale = Mathf.Max(.0001f, Mathf.Min(safeArea.width / 600, safeArea.height / 1080));
            var size = new Vector2(600, 1080) * scale;
            return new BlockPortersScreenLayout(new Rect(safeArea.center - size * .5f, size), scale);
        }

        /// <summary>从设计图的左上坐标转换到屏幕像素。</summary>
        /// <param name="topLeftPoint">600×1080 的设计点。</param>
        public Vector2 ToScreen(Vector2 topLeftPoint) => ContentPixels.min + new Vector2(topLeftPoint.x, 1080 - topLeftPoint.y) * Scale;
    }
}
