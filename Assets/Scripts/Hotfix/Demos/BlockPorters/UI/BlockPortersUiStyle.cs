using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// Demo 方格、文字和世界投影共用的设计规格。
    [CreateAssetMenu(menuName = "SleepyDemos/小小搬豆工/UI 规格")]
    public sealed class BlockPortersUiStyle : ScriptableObject
    {
        public float TileSize = 80;
        public float CornerRadius = 12;
        public float Padding = 8;
        public float ColumnGap = 16;
        public float RowGap = 8;
        public float CountFontSize = 32;
        public float DetailFontSize = 11;
        public float Thickness = 6;
        public float ShadowOffset = 4;
        public float AdvanceDuration = .2f;
        public float WorldPixelsPerUnit = 65;
        public Vector2 BoardCenter = new(300, 304);
        public float TaskRowY = 652;
        public float QueueRowY = 752;
        public Vector2 LeftExtraCenter = new(80, 554);
        public Vector2 RightExtraCenter = new(520, 554);
        public Color PreviewBaseTint = new(.91f, .88f, .84f);
        public Color DarkInk = new(.24f, .20f, .18f);
        public Color LightInk = new(1, .98f, .93f);

        public Vector2 TileDimensions => Vector2.one * TileSize;
        public Vector2 FaceDimensions => Vector2.one * (TileSize - Padding * 2);
        /// <summary>按牌面亮度选择文字，不改变用于辨色的牌面。</summary>
        public Color Ink(Color face) => .2126f * face.r + .7152f * face.g + .0722f * face.b < .42f ? LightInk : DarkInk;
        /// <summary>返回五列居中的设计横坐标。</summary>
        public float ColumnX(int column) => 300 + (column - 2) * (TileSize + ColumnGap);
    }
}
