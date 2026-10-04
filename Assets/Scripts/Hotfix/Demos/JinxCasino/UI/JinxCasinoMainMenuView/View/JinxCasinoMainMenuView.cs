using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [Mvc("JinxCasinoMainMenuView")]
    public sealed partial class JinxCasinoMainMenuView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoMainMenuPresenter_JinxCasinoMainMenuView.Bind(params1); }
        protected override void OnHide() { JinxCasinoMainMenuPresenter_JinxCasinoMainMenuView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoMainMenuPresenter_JinxCasinoMainMenuView != null) JinxCasinoMainMenuPresenter_JinxCasinoMainMenuView.Unbind(); base.OnDestroy(); }
    }
}
