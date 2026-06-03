using Core.Runtime;

namespace Hotfix
{
    public class UserHandler : HandlerBase<UserAction, UserData>
    {
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
