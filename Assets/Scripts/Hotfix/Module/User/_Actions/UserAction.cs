using Core.Runtime;

namespace Hotfix
{
    /// 本机用户状态命令。
    public abstract class UserAction : IAction
    {
    }

    /// 重新采集本机硬件信息。
    public sealed class UserRefreshHardwareProfileAction : UserAction
    {
    }
}
