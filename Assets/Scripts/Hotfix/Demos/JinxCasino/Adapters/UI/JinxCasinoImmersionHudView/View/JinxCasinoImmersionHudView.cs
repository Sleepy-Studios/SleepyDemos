namespace Hotfix
{
    using Core.Runtime;

    [Module("JinxCasino")]
    [Mvc("JinxCasinoImmersionHudView")]
    public partial class JinxCasinoImmersionHudView : View<Hotfix.JinxCasino.Adapters.JinxCasinoController>
    {
        protected override void OnShow()
        {
            base.OnShow();
            SuppressGraphicsEntry();
            JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView.Bind(params1);
        }
        protected override void OnHide() { ReleaseGraphicsEntrySuppression(); JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView?.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { ReleaseGraphicsEntrySuppression(); JinxCasinoImmersionHudPresenter_JinxCasinoImmersionHudView?.Unbind(); base.OnDestroy(); }
    }
}
