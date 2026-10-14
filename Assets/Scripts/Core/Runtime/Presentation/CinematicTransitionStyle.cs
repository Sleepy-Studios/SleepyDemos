namespace Core.Runtime.Presentation
{
    /// 七种黑幕图案；持续黑边使用独立节点。
    public enum CinematicTransitionStyle { Fade, Wipe, DoorsHorizontal, DoorsVertical, Iris, Blinds, Pixel }

    /// 擦幕和百叶的闭合方向。
    public enum CinematicWipeDirection { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
}
