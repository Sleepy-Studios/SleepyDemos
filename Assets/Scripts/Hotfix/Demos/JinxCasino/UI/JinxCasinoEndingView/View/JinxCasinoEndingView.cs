using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [UIBind("JinxCasinoEndingView")]
    public sealed partial class JinxCasinoEndingView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoEndingPresenter_JinxCasinoEndingView.Bind(params1); }
        protected override void OnHide() { JinxCasinoEndingPresenter_JinxCasinoEndingView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoEndingPresenter_JinxCasinoEndingView != null) JinxCasinoEndingPresenter_JinxCasinoEndingView.Unbind(); base.OnDestroy(); }
    }
}
