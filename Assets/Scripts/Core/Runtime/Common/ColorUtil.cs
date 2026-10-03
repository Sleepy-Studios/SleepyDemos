using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Runtime
{
    /// 公共颜色语义与十六进制转换；Demo 专属色板仍归 Demo。
    public static class ColorUtil
    {
        public static class Colors
        {
            public static readonly Color Success = new Color32(60, 167, 54, 255);
            public static readonly Color Notice = new Color32(243, 130, 20, 255);
            public static readonly Color Warning = new Color32(190, 4, 35, 255);
            public static readonly Color TextPrimary = Color.white;
            public static readonly Color TextSecondary = new Color32(180, 180, 190, 255);
            public static readonly Color TipsBackground = new Color32(41, 44, 62, 250);
        }

        private static readonly Dictionary<string, Color> Cache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private const int MaxCachedColors = 256;

        /// <summary>解析 RGB/RGBA 十六进制颜色；不接受颜色名称。</summary>
        /// <param name="hex">可带 #、可大小写混用，支持 3/4/6/8 位。</param>
        /// <param name="color">失败时输出默认 Color。</param>
        /// <returns>空白或非法字符串返回 false，不缓存失败值。</returns>
        public static bool TryParse(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string key = hex.Trim();
            if (key[0] != '#') key = "#" + key;
            int length = key.Length - 1;
            if (length != 3 && length != 4 && length != 6 && length != 8) return false;
            for (int i = 1; i < key.Length; i++) if (!Uri.IsHexDigit(key[i])) return false;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out color)) return true;
                if (!ColorUtility.TryParseHtmlString(key, out color)) return false;
                // 动态内容不应无限扩大常驻缓存。
                if (Cache.Count < MaxCachedColors) Cache.Add(key, color);
                return true;
            }
        }

        /// <summary>读取颜色，解析失败使用调用方备用色。</summary>
        /// <param name="hex">RGB/RGBA 十六进制字符串。</param>
        /// <param name="fallback">null 使用白色；透明色可作为显式备用值。</param>
        public static Color GetColor(string hex, Color? fallback = null)
            => TryParse(hex, out Color color) ? color : fallback ?? Color.white;

        /// <summary>转换为带 # 的大写十六进制字符串。</summary>
        /// <param name="color">Unity 颜色；按 Color32 量化。</param>
        /// <param name="includeAlpha">默认包含透明度。</param>
        public static string ToHex(Color color, bool includeAlpha = true)
            => "#" + (includeAlpha ? ColorUtility.ToHtmlStringRGBA(color) : ColorUtility.ToHtmlStringRGB(color));

        /// <summary>返回替换透明度后的颜色，不修改原值。</summary>
        /// <param name="color">保留 RGB 浮点精度的原颜色。</param>
        /// <param name="alpha">限制在 0 到 1；NaN 视为 0。</param>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = float.IsNaN(alpha) ? 0 : Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>为 TMP 富文本添加颜色标签。</summary>
        /// <param name="text">原内容，保留已有富文本；null 视为空字符串。</param>
        /// <param name="color">含透明度的颜色。</param>
        public static string WrapText(string text, Color color) => $"<color={ToHex(color)}>{text ?? string.Empty}</color>";
    }
}
