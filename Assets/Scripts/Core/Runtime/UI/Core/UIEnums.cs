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

    public enum ViewState
    {
        Created,
        Loading,
        LoadedHidden,
        Entering,
        Visible,
        Exiting,
        Destroying,
        Destroyed,
        Faulted
    }
}
