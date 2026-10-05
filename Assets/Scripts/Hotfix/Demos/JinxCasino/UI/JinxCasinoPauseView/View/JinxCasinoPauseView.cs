using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [UIBind("JinxCasinoPauseView")]
    public sealed partial class JinxCasinoPauseView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoPausePresenter_JinxCasinoPauseView.Bind(params1); }
        protected override void OnHide() { JinxCasinoPausePresenter_JinxCasinoPauseView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoPausePresenter_JinxCasinoPauseView != null) JinxCasinoPausePresenter_JinxCasinoPauseView.Unbind(); base.OnDestroy(); }
    }
}
