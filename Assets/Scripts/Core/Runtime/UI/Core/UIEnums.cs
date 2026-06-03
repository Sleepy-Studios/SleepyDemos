using System;

namespace Core.Runtime
{
    public enum UILayer
    {
        Underground = 0,
        Base = 1,
        Foreground = 2,
        Pop = 3,
        Decorate = 4,
        Tip = 5
    }

    public enum MaskType
    {
        None,
        ShowOnly,
        CloseRaycast
    }

    public enum ViewType
    {
        View,
        ItemView
    }

    [Flags]
    public enum ViewState
    {
        None = 0,
        FirstInit = 1 << 0,
        Loaded = 1 << 1,
        Enabled = 1 << 2,
        Disabled = 1 << 3,
        Destroyed = 1 << 4
    }
}
