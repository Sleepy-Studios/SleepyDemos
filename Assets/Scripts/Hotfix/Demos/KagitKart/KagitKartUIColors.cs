using Core.Runtime;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 当前玩法的源码 UI 色板，资源颜色继续由风格资产维护。
    public static class KagitKartUIColors
    {
        public static readonly Color Ink = ColorUtil.GetColor("#2D2A32");
        public static readonly Color Paper = ColorUtil.GetColor("#FAF4E4");
        public static readonly Color Accent = ColorUtil.GetColor("#D9483B");
        public static readonly Color Blue = ColorUtil.GetColor("#5F7FA3");
        public static readonly Color Mustard = ColorUtil.GetColor("#F2C14E");
        public static readonly Color FirstPlace = ColorUtil.GetColor("#E3A623");
        public static readonly Color SecondPlace = ColorUtil.GetColor("#7D97B8");
        public static readonly Color ThirdPlace = ColorUtil.GetColor("#C8693A");
        public static readonly Color Rising = ColorUtil.GetColor("#6F9A58");
        public static readonly Color NearThreat = ColorUtil.GetColor("#FFD9D2");
        public static readonly Color StatFilled = ColorUtil.GetColor("#D9473B");
        public static readonly Color StatEmpty = ColorUtil.GetColor("#332E33", alphaPercent: 18);
        public static readonly Color ScreenInk = ColorUtil.GetColor("#1C2140");
        public static readonly Color ScreenInkHighlight = ColorUtil.GetColor("#8F9BD0", alphaPercent: 30);
        public static readonly Color ItemHeld = ColorUtil.GetColor("#F2C14E", alphaPercent: 35);
        public static readonly Color ItemEmpty = ColorUtil.GetColor("#2D2A32", alphaPercent: 20);
        public static readonly Color MinimapFallback = ColorUtil.GetColor("#8A8490");
    }
}
