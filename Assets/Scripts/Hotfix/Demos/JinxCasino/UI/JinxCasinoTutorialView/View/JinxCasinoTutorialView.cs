using Core.Runtime;
using Hotfix.JinxCasino;
namespace Hotfix
{
    [Module("JinxCasino")] [Mvc("JinxCasinoTutorialView")]
    public sealed partial class JinxCasinoTutorialView : View<JinxCasinoController>
    {
        protected override void OnShow() { base.OnShow(); JinxCasinoTutorialPresenter_JinxCasinoTutorialView.Bind(params1); }
        protected override void OnHide() { JinxCasinoTutorialPresenter_JinxCasinoTutorialView.Unbind(); base.OnHide(); }
        protected override void OnDestroy() { if (JinxCasinoTutorialPresenter_JinxCasinoTutorialView != null) JinxCasinoTutorialPresenter_JinxCasinoTutorialView.Unbind(); base.OnDestroy(); }
    }
}
