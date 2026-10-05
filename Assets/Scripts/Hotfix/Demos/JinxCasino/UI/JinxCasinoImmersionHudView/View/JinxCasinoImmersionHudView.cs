using Hotfix.JinxCasino;
namespace Hotfix
{
    using Core.Runtime;

    [Module("JinxCasino")]
    [UIBind("JinxCasinoImmersionHudView")]
    public partial class JinxCasinoImmersionHudView : View<Hotfix.JinxCasino.JinxCasinoController>
    {
        protected override void OnShow()
        {
            base.OnShow();
            JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView.Bind(params1);
        }
        protected override void OnHide() { JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView?.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView?.Unbind(); base.OnDestroy(); }
    }
}
