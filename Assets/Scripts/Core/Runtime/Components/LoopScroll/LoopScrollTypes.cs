using System;
using UnityEngine;

namespace Core.Runtime
{
    public enum LoopListArrangeType
    {
        TopToBottom,
        BottomToTop,
        LeftToRight,
        RightToLeft
    }

    public enum LoopGridArrangeType
    {
        TopLeftToBottomRight,
        BottomLeftToTopRight,
        TopRightToBottomLeft,
        BottomRightToTopLeft
    }

    public enum GridFixedType
    {
        ColumnCountFixed,
        RowCountFixed
    }

    public readonly struct RowColumnPair : IEquatable<RowColumnPair>
    {
        public RowColumnPair(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }

        public bool Equals(RowColumnPair other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is RowColumnPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Column);
        }

        public static bool operator ==(RowColumnPair left, RowColumnPair right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RowColumnPair left, RowColumnPair right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class LoopScrollInitParam
    {
        public int VisibleBuffer { get; set; } = 1;
        public Vector2 DefaultItemSize { get; set; } = new Vector2(100f, 100f);
        public int InitCreateCount { get; set; }
        public bool SnapEnable { get; set; }
        public float SnapFinishThreshold { get; set; } = 0.01f;
        public float SnapVecThreshold { get; set; } = 145f;
    }

    public sealed class LoopGridSettingParam
    {
        public GridFixedType FixedType { get; set; } = GridFixedType.ColumnCountFixed;
        public int FixedRowOrColumnCount { get; set; } = 1;
        public Vector2 ItemSize { get; set; } = new Vector2(100f, 100f);
        public Vector2 ItemPadding { get; set; } = Vector2.zero;
        public RectOffset Padding { get; set; } = new RectOffset();
    }

    public sealed class LoopStaggeredLayoutParam
    {
        public int ColumnOrRowCount { get; set; }
        public float ItemWidthOrHeight { get; set; }
        public float PaddingStart { get; set; }
        public float PaddingEnd { get; set; }
        public float[] CustomColumnOrRowOffsetArray { get; set; }
    }

    public sealed class LoopStaggeredItemIndexData
    {
        public int GroupIndex { get; set; }
        public int IndexInGroup { get; set; }
    }
}
