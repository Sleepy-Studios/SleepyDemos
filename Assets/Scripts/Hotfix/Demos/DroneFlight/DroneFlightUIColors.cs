using Core.Runtime;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 当前玩法的源码 UI 色板，资源颜色继续由风格资产维护。
    public static class DroneFlightUIColors
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
}
