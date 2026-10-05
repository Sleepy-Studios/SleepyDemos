using Core.Runtime;

namespace Hotfix
{
    public class UserHandler : HandlerBase<UserAction, UserData>
    {
        /// <summary>
        /// 处理本模块业务命令，按实际服务与规则结果发布状态。
        /// </summary>
        /// <param name="action">当前模块的业务请求。</param>
        protected override void Reduce(UserAction action)
        {
            switch (action)
            {
                case UserRefreshHardwareProfileAction:
                    State.RefreshHardwareProfile();
                    ApplyState();
                    break;
            }
        }
    }
}
