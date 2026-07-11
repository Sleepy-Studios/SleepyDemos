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
        Created = 0,
        Loading = 1 << 0,
        LoadedHidden = 1 << 1,
        Entering = 1 << 2,
        Visible = 1 << 3,
        Exiting = 1 << 4,
        Destroying = 1 << 5,
        Destroyed = 1 << 6,
        Faulted = 1 << 7,

        // 旧 View 生命周期仍使用位运算，任务 4 完成迁移后删除这些兼容成员。
        FirstInit = 1 << 8,
        Loaded = 1 << 9,
        Enabled = 1 << 10,
        Disabled = 1 << 11
    }
}
