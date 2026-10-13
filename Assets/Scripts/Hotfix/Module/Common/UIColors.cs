using Core.Runtime;
using UnityEngine;

namespace Hotfix
{
    /// Hub与Demo的固定UI/HUD色板；Prefab外观继续由风格Variant保存。
    public static class UIColors
    {
        #region 公共 UI
        public static class Common
        {
            public static readonly Color LoadingBackground = ColorUtil.GetColor("#0F1214");
            public static readonly Color LoopFocus = ColorUtil.GetColor("#33D9FF");
            public static readonly Color LoopDisabled = ColorUtil.GetColor("#6B7078", alphaPercent: 60);
        }
        #endregion

        #region Hub
        // 首页状态与反馈；见 docs/architecture/ui-art-direction.md。
        public static class Hub
        {
            public static readonly Color Status = ColorUtil.GetColor("#636E6B");
            public static readonly Color EntryFeedback = ColorUtil.GetColor("#B83829");
        }
        #endregion

        #region 无人机
        // 宿主UI/HUD固定色；见 docs/demos/drone_flight/architecture/art-direction.md。
        public static class DroneFlight
        {
            public static readonly Color TelemetryAccent = ColorUtil.GetColor("#F4A23A");
            public static readonly Color EquipmentSecondary = ColorUtil.GetColor("#9BA5AA");
            public static readonly Color VectorOutline = ColorUtil.GetColor("#080A0C");
            public static readonly Color VectorShadow = ColorUtil.GetColor("#000000", alphaPercent: 66.66667f);
            public static readonly Color RotorForce = ColorUtil.GetColor("#00FFFF");
            public static readonly Color TotalThrust = ColorUtil.GetColor("#FFEB04");
            public static readonly Color Gravity = ColorUtil.GetColor("#FF4033");
            public static readonly Color ActualVelocity = ColorUtil.GetColor("#00FF00");
            public static readonly Color DesiredVelocity = ColorUtil.GetColor("#33A6FF");
            public static readonly Color DesiredAcceleration = ColorUtil.GetColor("#FF59FF");
            public static readonly Color GrappleNoSurface = ColorUtil.GetColor("#FF3326");
            public static readonly Color GrappleSearching = ColorUtil.GetColor("#FF8C0D");
            public static readonly Color EquipmentValid = ColorUtil.GetColor("#00FF00");
            public static readonly Color HarpoonAimInvalid = ColorUtil.GetColor("#FF0000");
        }
        #endregion

        #region DLSS 实验室
        // 当前外观均由资产维护，无固定源码色；见 docs/demos/dlss/architecture/art-direction.md。
        #endregion

        #region 夹爆它
        // 宿主UI/HUD固定色；见 docs/demos/wall_squeeze/architecture/art-direction.md。
        public static class WallSqueeze
        {
            public static readonly Color ClockWarning = ColorUtil.GetColor("#D0302A");
            public static readonly Color Ink = ColorUtil.GetColor("#000000");
        }
        #endregion

        #region 纸艺卡丁车
        // 宿主UI/HUD固定色；见 docs/demos/kagit_kart/architecture/art-direction.md。
        public static class KagitKart
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
        #endregion
    }
}
